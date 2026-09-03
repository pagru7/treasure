using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Contracts.Transactions;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Application.Transfers;

public enum TransferCreationStatus
{
    Success,
    InvalidRequest,
    NotFound,
    Forbidden
}

public sealed record TransferCreationIssue(string Field, string Message);

public sealed record TransferCreationOutcome(
    Guid TransferId,
    Guid OutflowTransactionId,
    Guid InflowTransactionId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string Currency,
    DateTime TransferDate);

public sealed record TransferCreationResult(
    TransferCreationStatus Status,
    string? Message,
    IReadOnlyList<TransferCreationIssue> Issues,
    TransferCreationOutcome? Outcome)
{
    public bool Succeeded => Status == TransferCreationStatus.Success && Outcome is not null;

    public static TransferCreationResult Success(TransferCreationOutcome outcome) =>
        new(TransferCreationStatus.Success, null, [], outcome);

    public static TransferCreationResult Failure(
        TransferCreationStatus status,
        string message,
        params TransferCreationIssue[] issues) =>
        new(status, message, issues, null);
}

public class TransferCreationService(TreasuryDbContext db)
{
    public virtual async Task<TransferCreationResult> CreateAsync(
        ApplicationUser user,
        CreateTransferRequest request,
        CancellationToken ct)
    {
        var issues = ValidateRequest(request);
        if (issues.Count > 0)
        {
            return TransferCreationResult.Failure(
                TransferCreationStatus.InvalidRequest,
                "Please correct the highlighted fields.",
                issues.ToArray());
        }

        var fromAccount = await db.Accounts.SingleOrDefaultAsync(
            x => x.Id == request.FromAccountId && x.HouseholdId == user.HouseholdId,
            ct);
        var toAccount = await db.Accounts.SingleOrDefaultAsync(
            x => x.Id == request.ToAccountId && x.HouseholdId == user.HouseholdId,
            ct);

        if (fromAccount is null || toAccount is null)
        {
            return TransferCreationResult.Failure(
                TransferCreationStatus.NotFound,
                "Transfer accounts were not found.");
        }

        if (fromAccount.OwnerUserId != user.Id || toAccount.OwnerUserId != user.Id)
        {
            return TransferCreationResult.Failure(
                TransferCreationStatus.Forbidden,
                "Transfers can only be created for your own accounts.");
        }

        if (!fromAccount.IsActive || !toAccount.IsActive)
        {
            var accountIssues = new List<TransferCreationIssue>();
            if (!fromAccount.IsActive)
            {
                accountIssues.Add(new TransferCreationIssue("FromAccountId", "Source account is inactive."));
            }

            if (!toAccount.IsActive)
            {
                accountIssues.Add(new TransferCreationIssue("ToAccountId", "Destination account is inactive."));
            }

            return TransferCreationResult.Failure(
                TransferCreationStatus.InvalidRequest,
                "Transfers are allowed only between active accounts.",
                accountIssues.ToArray());
        }

        var transferDate = request.TransferDate == default ? DateTime.UtcNow : request.TransferDate;
        var currency = string.IsNullOrWhiteSpace(request.Currency)
            ? fromAccount.Currency
            : request.Currency.Trim().ToUpperInvariant();
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? "Account transfer"
            : request.Description.Trim();
        var utcNow = DateTime.UtcNow;

        var transfer = new Transfer
        {
            HouseholdId = user.HouseholdId,
            FromAccountId = fromAccount.Id,
            ToAccountId = toAccount.Id,
            Amount = request.Amount,
            Currency = currency,
            Description = description,
            TransferDate = transferDate,
            CreatedAt = utcNow
        };

        var outflow = new Transaction
        {
            HouseholdId = user.HouseholdId,
            AccountId = fromAccount.Id,
            Description = $"{description} -> {toAccount.Name}",
            Category = "Transfer",
            Amount = request.Amount,
            Currency = currency,
            Type = "transfer-out",
            TransactionDate = transferDate,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        var inflow = new Transaction
        {
            HouseholdId = user.HouseholdId,
            AccountId = toAccount.Id,
            Description = $"{description} <- {fromAccount.Name}",
            Category = "Transfer",
            Amount = request.Amount,
            Currency = currency,
            Type = "transfer-in",
            TransactionDate = transferDate,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        fromAccount.CurrentBalance -= Math.Abs(request.Amount);
        toAccount.CurrentBalance += Math.Abs(request.Amount);
        fromAccount.UpdatedAt = utcNow;
        toAccount.UpdatedAt = utcNow;

        async Task PersistAsync()
        {
            db.Transfers.Add(transfer);
            db.Transactions.Add(outflow);
            db.Transactions.Add(inflow);
            await db.SaveChangesAsync(ct);

            transfer.OutflowTransactionId = outflow.Id;
            transfer.InflowTransactionId = inflow.Id;
            await db.SaveChangesAsync(ct);
        }

        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await PersistAsync();
            await transaction.CommitAsync(ct);
        }
        else
        {
            await PersistAsync();
        }

        return TransferCreationResult.Success(new TransferCreationOutcome(
            transfer.Id,
            outflow.Id,
            inflow.Id,
            transfer.FromAccountId,
            transfer.ToAccountId,
            transfer.Amount,
            transfer.Currency,
            transfer.TransferDate));
    }

    private static List<TransferCreationIssue> ValidateRequest(CreateTransferRequest request)
    {
        var issues = new List<TransferCreationIssue>();

        if (request.FromAccountId == Guid.Empty || request.FromAccountId == request.ToAccountId)
        {
            issues.Add(new TransferCreationIssue("FromAccountId", "A valid source account is required."));
        }

        if (request.ToAccountId == Guid.Empty || request.FromAccountId == request.ToAccountId)
        {
            issues.Add(new TransferCreationIssue("ToAccountId", "A valid destination account is required."));
        }

        if (request.Amount <= 0m)
        {
            issues.Add(new TransferCreationIssue("Amount", "Transfer amount must be greater than zero."));
        }

        return issues;
    }
}
