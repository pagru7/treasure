using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Contracts.Transactions;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Transactions;

public sealed class UpdateTransactionEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<UpdateTransactionRequest>
{
    public override void Configure()
    {
        Put("/api/transactions/{id:guid}");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
    }

    public override async Task HandleAsync(UpdateTransactionRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (request.Id == Guid.Empty || string.IsNullOrWhiteSpace(request.Description))
        {
            if (request.Id == Guid.Empty)
            {
                AddError(x => x.Id, "A valid transaction is required.");
            }

            if (string.IsNullOrWhiteSpace(request.Description))
            {
                AddError(x => x.Description, "Description is required.");
            }

            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var transaction = await db.Transactions
            .Include(x => x.TransactionTags)
            .SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
        if (transaction is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == transaction.AccountId && x.HouseholdId == user.HouseholdId, ct);
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

        var trackedTransactions = await db.Transactions
            .Where(x => x.AccountId == account.Id)
            .ToListAsync(ct);

        var originalSnapshots = trackedTransactions
            .Select(x => new TransactionBalanceSnapshot(x.Id, x.Amount, x.Type, x.TransactionDate, x.CreatedAt))
            .ToList();

        var latestTransactionId = originalSnapshots
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefault();

        var isLatest = latestTransactionId == transaction.Id;

        var normalizedDescription = request.Description.Trim();
        var normalizedCategory = string.IsNullOrWhiteSpace(request.Category) ? transaction.Category : request.Category.Trim();
        var normalizedType = NormalizeType(string.IsNullOrWhiteSpace(request.Type) ? transaction.Type : request.Type);
        var normalizedTransactionDate = request.TransactionDate == default ? transaction.TransactionDate : request.TransactionDate;

        var hasErrors = false;

        if (!isLatest)
        {
            if (request.Amount != transaction.Amount)
            {
                AddError(x => x.Amount, "Amount can only be changed on the latest transaction.");
                hasErrors = true;
            }

            if (normalizedTransactionDate != transaction.TransactionDate)
            {
                AddError(x => x.TransactionDate, "Transaction date can only be changed on the latest transaction.");
                hasErrors = true;
            }

            if (!string.Equals(normalizedType, transaction.Type, StringComparison.OrdinalIgnoreCase))
            {
                AddError(x => x.Type, "Type can only be changed on the latest transaction.");
                hasErrors = true;
            }
        }
        else
        {
            if (request.Amount <= 0m)
            {
                AddError(x => x.Amount, "Amount must be greater than zero.");
                hasErrors = true;
            }
        }

        if (hasErrors)
        {
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        transaction.Description = normalizedDescription;
        transaction.Category = string.IsNullOrWhiteSpace(normalizedCategory) ? "General" : normalizedCategory;
        transaction.Amount = isLatest ? request.Amount : transaction.Amount;
        transaction.Type = isLatest ? normalizedType : transaction.Type;
        transaction.TransactionDate = isLatest ? normalizedTransactionDate : transaction.TransactionDate;
        transaction.UpdatedAt = DateTime.UtcNow;

        var validTagIds = await db.Tags
            .Where(x => x.HouseholdId == user.HouseholdId && request.TagIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(ct);

        db.TransactionTags.RemoveRange(transaction.TransactionTags);
        transaction.TransactionTags.Clear();

        foreach (var tagId in validTagIds)
        {
            transaction.TransactionTags.Add(new TransactionTag
            {
                Transaction = transaction,
                TagId = tagId
            });
        }

        if (isLatest)
        {
            var updatedSnapshots = originalSnapshots
                .Select(x => x.Id == transaction.Id
                    ? x with
                    {
                        Amount = transaction.Amount,
                        Type = transaction.Type,
                        TransactionDate = transaction.TransactionDate
                    }
                    : x)
                .ToList();

            var initialBalance = account.CurrentBalance - originalSnapshots.Sum(x => GetDelta(x.Amount, x.Type));
            var runningBalance = initialBalance;
            var utcNow = DateTime.UtcNow;
            var trackedById = trackedTransactions.ToDictionary(x => x.Id);

            foreach (var snapshot in updatedSnapshots
                .OrderBy(x => x.TransactionDate)
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.Id))
            {
                runningBalance += GetDelta(snapshot.Amount, snapshot.Type);
                var trackedTransaction = trackedById[snapshot.Id];
                trackedTransaction.BalanceAfterTransaction = runningBalance;
                trackedTransaction.UpdatedAt = utcNow;
            }

            account.CurrentBalance = runningBalance;
            account.UpdatedAt = utcNow;
        }

        await db.SaveChangesAsync(ct);

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
        }, StatusCodes.Status200OK, ct);
    }

    private static string NormalizeType(string type) =>
        string.IsNullOrWhiteSpace(type) ? "expense" : type.Trim().ToLowerInvariant();

    private static decimal GetDelta(decimal amount, string type)
    {
        var normalizedType = NormalizeType(type);
        return normalizedType switch
        {
            "expense" => -Math.Abs(amount),
            "income" => Math.Abs(amount),
            "transfer" => Math.Abs(amount),
            "transfer-in" => Math.Abs(amount),
            "transfer-out" => -Math.Abs(amount),
            _ => amount
        };
    }

    private sealed record TransactionBalanceSnapshot(Guid Id, decimal Amount, string Type, DateTime TransactionDate, DateTime CreatedAt);
}
