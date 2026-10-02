using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Application.Transactions;
using Treasury.App.Contracts.Transactions;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Transactions;

public sealed class CreateTransactionEndpoint(
    TreasuryDbContext db,
    UserManager<ApplicationUser> userManager,
    AccountBalanceRecalculationService dbBalanceRecalculationService)
    : Endpoint<CreateTransactionRequest>
{
    public override void Configure()
    {
        Post("/api/transactions");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
    }

    public override async Task HandleAsync(CreateTransactionRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (request.AccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.Description))
        {
            if (request.AccountId == Guid.Empty)
            {
                AddError(x => x.AccountId, "A valid account is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                AddError(x => x.Description, "Description is required.");
            }

            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.AccountId && x.HouseholdId == user.HouseholdId, ct);
        if (account is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (account.OwnerUserId != user.Id)
        {
            await SendForbiddenAsync(ct);
            return;
        }

        // Block creating transactions on inactive accounts
        if (!account.IsActive)
        {
            AddError(x => x.AccountId, "Cannot create transactions on an inactive account.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var category = await ResolveCategoryAsync(user.HouseholdId, request.CategoryId, request.Category, ct);
        if (category is null)
        {
            AddError(x => x.CategoryId, "A valid category is required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        if (!category.IsActive)
        {
            AddError(x => x.CategoryId, "Category is disabled.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var parsedType = request.Type;
        if (parsedType is TransactionType.Transfer or TransactionType.TransferIn or TransactionType.TransferOut)
        {
            AddError(x => x.Type, "Use the dedicated Transfers flow to record transfers.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var delta = TransactionBalanceMath.GetDelta(request.Amount, parsedType);

        var utcNow = DateTime.UtcNow;
        var transaction = new Transaction
        {
            HouseholdId = user.HouseholdId,
            AccountId = account.Id,
            CategoryId = category.Id,
            Description = request.Description.Trim(),
            Category = category.Name,
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? account.Currency : request.Currency.Trim().ToUpperInvariant(),
            Type = parsedType,
            TransactionDate = request.TransactionDate == default ? utcNow : request.TransactionDate,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        var validTagIds = await db.Tags
            .Where(x => x.HouseholdId == user.HouseholdId && request.TagIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(ct);

        foreach (var tagId in validTagIds)
        {
            transaction.TransactionTags.Add(new TransactionTag
            {
                Transaction = transaction,
                TagId = tagId
            });
        }

        async Task PersistAsync()
        {
            db.Transactions.Add(transaction);
            account.CurrentBalance += delta;
            account.UpdatedAt = utcNow;
            await db.SaveChangesAsync(ct);
            await dbBalanceRecalculationService.RecalculateAccountAsync(account.Id, ct);
        }

        if (db.Database.IsRelational())
        {
            await using var transactionScope = await db.Database.BeginTransactionAsync(ct);
            await PersistAsync();
            await transactionScope.CommitAsync(ct);
        }
        else
        {
            await PersistAsync();
        }

        await SendAsync(new
        {
            transaction.Id,
            transaction.AccountId,
            transaction.CategoryId,
            transaction.Description,
            transaction.Category,
            transaction.Amount,
            transaction.Currency,
            Type = TransactionBalanceMath.ToApiType(transaction.Type),
            transaction.TransactionDate,
            transaction.BalanceAfterTransaction,
            Tags = validTagIds
        }, StatusCodes.Status201Created, ct);
    }

    private async Task<Category?> ResolveCategoryAsync(Guid householdId, Guid? categoryId, string? categoryName, CancellationToken ct)
    {
        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            return await db.Categories.SingleOrDefaultAsync(x => x.Id == categoryId.Value && x.HouseholdId == householdId, ct);
        }

        var normalizedName = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        var existing = await db.Categories
            .SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.Name.ToLower() == normalizedName.ToLower(), ct);

        if (existing is not null)
        {
            return existing;
        }

        var created = new Category
        {
            HouseholdId = householdId,
            Name = normalizedName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Categories.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }
}