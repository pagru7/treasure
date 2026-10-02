using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Contracts.Transactions;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Application.Transactions;

public class TransactionEditingService(
    TreasuryDbContext db,
    AccountBalanceRecalculationService balanceRecalculationService)
{
    public virtual async Task<TransactionEditResult> UpdateAsync(
        ApplicationUser user,
        UpdateTransactionRequest request,
        CancellationToken ct)
    {
        // validate request
        UpdateTransactionRequestValidator validationRules = new();
        var validationResult = validationRules.Validate(request);

        if (!validationResult.IsValid)
        {
            var validationIssues = validationResult.Errors
                .Select(x => new TransactionEditIssue(x.PropertyName, x.ErrorMessage))
                .ToArray();
            return TransactionEditResult.Failure(
                TransactionEditStatus.InvalidRequest,
                "Please correct the highlighted fields.",
                validationIssues);
        }

        var transaction = await db.Transactions
            .Include(x => x.TransactionTags)
            .Include(x => x.Account)
            .SingleOrDefaultAsync(x => x.Id == request.Id
                && x.HouseholdId == user.HouseholdId, ct);

        (bool canContinue, TransactionEditResult? value) = ValidateTransactionAccount(user, transaction);

        if (!canContinue)
            return value!;

        // check if transaction is transfer-linked
        if (await IsTransferLinkedAsync(transaction!, ct))
        {
            return TransactionEditResult.Failure(
                TransactionEditStatus.InvalidRequest,
                "Transfer-linked transactions cannot be edited. Edit the transfer instead.",
                new TransactionEditIssue(nameof(UpdateTransactionRequest.Id), "Transfer-linked transactions cannot be edited. Edit the transfer instead."));
        }

        var latestTransactionId = await db.Transactions
            .Where(x => x.AccountId == transaction!.AccountId)
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(10)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(ct);

        var isLatest = latestTransactionId == transaction!.Id;
        var issues = new List<TransactionEditIssue>();
        var amountChanged = transaction.Amount != request.Amount;

        if (!isLatest)
        {
            if (amountChanged)
                issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Amount), "Amount can only be changed on the latest transaction."));

            if (transaction.TransactionDate != request.TransactionDate)
                issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.TransactionDate), "Transaction date can only be changed on the latest transaction."));

            if (transaction.Type != request.Type)
                issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Type), "Type can only be changed on the latest transaction."));
        }

        if (issues.Count > 0)
        {
            return TransactionEditResult.Failure(
                TransactionEditStatus.InvalidRequest,
                "Please correct the highlighted fields.",
                issues.ToArray());
        }

        var originalDelta = TransactionBalanceMath.GetDelta(transaction.Amount, transaction.Type);

        transaction.Description = request.Description!;
        transaction.CategoryId = request.CategoryId!.Value;
        transaction.Category = request.Category!;
        transaction.Amount = request.Amount!.Value;
        transaction.Type = request.Type!.Value;
        transaction.TransactionDate = request.TransactionDate!.Value;
        transaction.UpdatedAt = DateTime.Now;

        var updatedDelta = TransactionBalanceMath.GetDelta(transaction.Amount, transaction.Type);

        if (amountChanged || transaction.Type != request.Type)
            transaction.Account.CurrentBalance += updatedDelta - originalDelta;

        UpdateTags(request, transaction);

        await using var transactionScope = await db.Database.BeginTransactionAsync(ct);
        db.Transactions.Update(transaction);
        await db.SaveChangesAsync(ct);
        await balanceRecalculationService.RecalculateAccountAsync(transaction.Account.Id, ct);
        await transactionScope.CommitAsync(ct);

        return TransactionEditResult.Success(new TransactionEditOutcome(
            transaction.Id,
            transaction.AccountId,
            transaction.CategoryId,
            transaction.Description,
            transaction.Category,
            transaction.Amount,
            transaction.Currency,
            transaction.Type,
            transaction.TransactionDate,
            transaction.BalanceAfterTransaction,
            transaction.TransactionTags.Select(x => x.TagId).ToList()));
    }

    private static void UpdateTags(UpdateTransactionRequest request, Transaction transaction)
    {
        if (request.TagIds != null && request.TagIds.Count > 0)
        {
            var existingTagIds = transaction.TransactionTags
                .Select(x => x.TagId)
                .ToHashSet();

            var newTagIds = request.TagIds
                .Except(existingTagIds).ToList();

            var tagsToRemove = transaction
                .TransactionTags
                .Where(x => !request.TagIds.Contains(x.TagId))
                .ToList();

            var tagsToAdd = newTagIds.Select(tagId => new TransactionTag
            {
                TransactionId = transaction.Id,
                TagId = tagId
            }).ToList();

            foreach (var tag in tagsToRemove)
            {
                transaction.TransactionTags.Remove(tag);
            }

            foreach (var tag in tagsToAdd)
            {
                transaction.TransactionTags.Add(tag);
            }
        }
    }

    /// <summary>
    /// Validates that the transaction exists and belongs to the user.
    /// </summary>
    /// <param name="user">The user attempting to edit the transaction.</param>
    /// <param name="transaction">The transaction to validate.</param>
    /// <returns>A tuple indicating whether the operation can continue and an optional failure result.</returns>
    private static (bool canContinue, TransactionEditResult? value) ValidateTransactionAccount(ApplicationUser user, Transaction? transaction)
    {
        if (transaction is null)
            return (canContinue: false, value: TransactionEditResult.Failure(TransactionEditStatus.NotFound, "Transaction was not found."));

        if (transaction.Account is null)
            return (canContinue: false, value: TransactionEditResult.Failure(TransactionEditStatus.NotFound, "Account was not found."));

        if (transaction.Account.OwnerUserId != user.Id)
            return (canContinue: false, value: TransactionEditResult.Failure(
                TransactionEditStatus.Forbidden,
                "You do not have permission to edit this transaction."));
        return (canContinue: true, value: null);
    }

    private async Task<bool> IsTransferLinkedAsync(Transaction transaction, CancellationToken ct) =>
        transaction.Type is TransactionType.TransferIn or TransactionType.TransferOut
        || await db.Transactions.AnyAsync(x => x.Id == transaction.Id && (x.Type == TransactionType.TransferIn || x.Type == TransactionType.TransferOut), ct);
}