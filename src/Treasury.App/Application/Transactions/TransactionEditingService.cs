using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Contracts.Transactions;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Application.Transactions;

public enum TransactionEditStatus
{
    Success,
    InvalidRequest,
    NotFound,
    Forbidden
}

public sealed record TransactionEditIssue(string Field, string Message);

public sealed record TransactionEditOutcome(
    Guid Id,
    Guid AccountId,
    string Description,
    string Category,
    decimal Amount,
    string Currency,
    string Type,
    DateTime TransactionDate,
    decimal BalanceAfterTransaction,
    IReadOnlyList<Guid> TagIds);

public sealed record TransactionEditResult(
    TransactionEditStatus Status,
    string? Message,
    IReadOnlyList<TransactionEditIssue> Issues,
    TransactionEditOutcome? Outcome)
{
    public bool Succeeded => Status == TransactionEditStatus.Success && Outcome is not null;

    public static TransactionEditResult Success(TransactionEditOutcome outcome) =>
        new(TransactionEditStatus.Success, null, [], outcome);

    public static TransactionEditResult Failure(
        TransactionEditStatus status,
        string message,
        params TransactionEditIssue[] issues) =>
        new(status, message, issues, null);
}

public class TransactionEditingService(
    TreasuryDbContext db,
    AccountBalanceRecalculationService balanceRecalculationService)
{
    public virtual async Task<TransactionEditResult> UpdateAsync(
        ApplicationUser user,
        UpdateTransactionRequest request,
        CancellationToken ct)
    {
        if (request.Id == Guid.Empty)
        {
            return TransactionEditResult.Failure(
                TransactionEditStatus.InvalidRequest,
                "Please correct the highlighted fields.",
                new TransactionEditIssue(nameof(UpdateTransactionRequest.Id), "A valid transaction is required."));
        }

        var transaction = await db.Transactions
            .Include(x => x.TransactionTags)
            .SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
        if (transaction is null)
        {
            return TransactionEditResult.Failure(TransactionEditStatus.NotFound, "Transaction was not found.");
        }

        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == transaction.AccountId && x.HouseholdId == user.HouseholdId, ct);
        if (account is null)
        {
            return TransactionEditResult.Failure(TransactionEditStatus.NotFound, "Account was not found.");
        }

        if (account.OwnerUserId != user.Id)
        {
            return TransactionEditResult.Failure(
                TransactionEditStatus.Forbidden,
                "You do not have permission to edit this transaction.");
        }

        if (await IsTransferLinkedAsync(transaction, ct))
        {
            return TransactionEditResult.Failure(
                TransactionEditStatus.InvalidRequest,
                "Transfer-linked transactions cannot be edited. Edit the transfer instead.",
                new TransactionEditIssue(nameof(UpdateTransactionRequest.Id), "Transfer-linked transactions cannot be edited. Edit the transfer instead."));
        }

        var latestTransactionId = await db.Transactions
            .Where(x => x.AccountId == account.Id)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(ct);

        var isLatest = latestTransactionId == transaction.Id;
        var originalDelta = TransactionBalanceMath.GetDelta(transaction.Amount, transaction.Type);

        var hasDescription = request.Description is not null;
        var hasCategory = request.Category is not null;
        var hasAmount = request.Amount.HasValue;
        var hasType = request.Type is not null;
        var hasTransactionDate = request.TransactionDate.HasValue;
        var hasTagIds = request.TagIds is not null;

        var normalizedDescription = hasDescription ? request.Description!.Trim() : transaction.Description;
        var normalizedCategory = hasCategory ? NormalizeCategory(request.Category!, transaction.Category) : transaction.Category;
        var normalizedAmount = hasAmount ? request.Amount!.Value : transaction.Amount;
        var normalizedType = hasType ? TransactionBalanceMath.NormalizeType(request.Type!, transaction.Type) : transaction.Type;
        var normalizedTransactionDate = hasTransactionDate ? request.TransactionDate!.Value : transaction.TransactionDate;

        var issues = new List<TransactionEditIssue>();

        if (hasDescription && string.IsNullOrWhiteSpace(normalizedDescription))
        {
            issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Description), "Description is required."));
        }

        if (hasAmount && normalizedAmount <= 0m)
        {
            issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Amount), "Amount must be greater than zero."));
        }

        var amountChanged = hasAmount && normalizedAmount != transaction.Amount;
        var typeChanged = hasType && !string.Equals(normalizedType, transaction.Type, StringComparison.OrdinalIgnoreCase);
        var dateChanged = hasTransactionDate && normalizedTransactionDate != transaction.TransactionDate;

        if (!isLatest)
        {
            if (amountChanged)
            {
                issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Amount), "Amount can only be changed on the latest transaction."));
            }

            if (dateChanged)
            {
                issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.TransactionDate), "Transaction date can only be changed on the latest transaction."));
            }

            if (typeChanged)
            {
                issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Type), "Type can only be changed on the latest transaction."));
            }
        }

        if (issues.Count > 0)
        {
            return TransactionEditResult.Failure(
                TransactionEditStatus.InvalidRequest,
                "Please correct the highlighted fields.",
                issues.ToArray());
        }

        transaction.Description = normalizedDescription;
        transaction.Category = normalizedCategory;
        transaction.Amount = normalizedAmount;
        transaction.Type = normalizedType;
        transaction.TransactionDate = normalizedTransactionDate;
        var utcNow = DateTime.UtcNow;
        transaction.UpdatedAt = utcNow;

        var updatedDelta = TransactionBalanceMath.GetDelta(transaction.Amount, transaction.Type);

        if (amountChanged || typeChanged)
        {
            account.CurrentBalance += updatedDelta - originalDelta;
            account.UpdatedAt = utcNow;
        }
        else if (dateChanged)
        {
            account.UpdatedAt = utcNow;
        }

        if (hasTagIds)
        {
            var distinctTagIds = request.TagIds!.Distinct().ToArray();
            var validTagIds = await db.Tags
                .Where(x => x.HouseholdId == user.HouseholdId && distinctTagIds.Contains(x.Id))
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
        }

        async Task PersistAsync()
        {
            await db.SaveChangesAsync(ct);
            await balanceRecalculationService.RecalculateAccountAsync(account.Id, ct);
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

        var tagIds = transaction.TransactionTags
            .Select(x => x.TagId)
            .ToList();

        return TransactionEditResult.Success(new TransactionEditOutcome(
            transaction.Id,
            transaction.AccountId,
            transaction.Description,
            transaction.Category,
            transaction.Amount,
            transaction.Currency,
            transaction.Type,
            transaction.TransactionDate,
            transaction.BalanceAfterTransaction,
            tagIds));
    }

    private async Task<bool> IsTransferLinkedAsync(Transaction transaction, CancellationToken ct) =>
        transaction.Type is "transfer-in" or "transfer-out"
        || await db.Transfers.AnyAsync(
            x => x.OutflowTransactionId == transaction.Id || x.InflowTransactionId == transaction.Id,
            ct);

    private static string NormalizeCategory(string category, string fallback) =>
        string.IsNullOrWhiteSpace(category) ? fallback : category.Trim();
}
