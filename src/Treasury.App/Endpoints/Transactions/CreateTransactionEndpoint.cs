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

        var normalizedType = TransactionBalanceMath.NormalizeType(request.Type);
        if (normalizedType is "transfer" or "transfer-in" or "transfer-out")
        {
            AddError(x => x.Type, "Use the dedicated Transfers flow to record transfers.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var delta = TransactionBalanceMath.GetDelta(request.Amount, normalizedType);

        var utcNow = DateTime.UtcNow;
        var transaction = new Transaction
        {
            HouseholdId = user.HouseholdId,
            AccountId = account.Id,
            Description = request.Description.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? account.Currency : request.Currency.Trim().ToUpperInvariant(),
            Type = normalizedType,
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
            transaction.Description,
            transaction.Category,
            transaction.Amount,
            transaction.Currency,
            transaction.Type,
            transaction.TransactionDate,
            transaction.BalanceAfterTransaction,
            Tags = validTagIds
        }, StatusCodes.Status201Created, ct);
    }
}
