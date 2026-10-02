using Microsoft.EntityFrameworkCore;
using Treasury.App.Application.Transactions;
using Treasury.App.Common;
using Treasury.App.Contracts.Transactions;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Application.Transfers;

public class TransferCreationService(
    TreasuryDbContext db,
    AccountBalanceRecalculationService balanceRecalculationService)
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

        Account? fromAccount, toAccount;
        var result = await GetAccounts(user, request, ct);
        if (!result.IsSuccess)
        {
            return result.Error!;
        }
        (fromAccount, toAccount) = result.Value!;

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

        var transferCategory = await ResolveSystemCategoryAsync(user.HouseholdId, "Transfer", ct);

        var outflow = new Transaction
        {
            HouseholdId = user.HouseholdId,
            AccountId = fromAccount.Id,
            CategoryId = transferCategory.Id,
            Description = $"{description} -> {toAccount.Name}",
            Category = transferCategory.Name,
            Amount = request.Amount,
            Currency = currency,
            Type = TransactionType.TransferOut,
            TransactionDate = transferDate,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        var inflow = new Transaction
        {
            HouseholdId = user.HouseholdId,
            AccountId = toAccount.Id,
            CategoryId = transferCategory.Id,
            Description = $"{description} <- {fromAccount.Name}",
            Category = transferCategory.Name,
            Amount = request.Amount,
            Currency = currency,
            Type = TransactionType.TransferIn,
            TransactionDate = transferDate,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        var transferAmount = Math.Abs(request.Amount);
        fromAccount.CurrentBalance -= transferAmount;
        toAccount.CurrentBalance += transferAmount;
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

            await balanceRecalculationService.RecalculateAccountsAsync([fromAccount.Id, toAccount.Id], ct);
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

    private async Task<Result<(Account, Account), TransferCreationResult>> GetAccounts(
        ApplicationUser user,
        CreateTransferRequest request,
        //out Account? fromAccount,
        //out Account? toAccount,
        CancellationToken ct)
    {
        var fromAccount = await db.Accounts
                    .SingleOrDefaultAsync(x => x.Id == request.FromAccountId
                        && x.HouseholdId == user.HouseholdId,
                        ct);
        var toAccount = await db.Accounts
                    .SingleOrDefaultAsync(x => x.Id == request.ToAccountId
                        && x.HouseholdId == user.HouseholdId,
                        ct);
        if (fromAccount is null || toAccount is null)
        {
            return Result<(Account, Account), TransferCreationResult>.Failure(TransferCreationResult.Failure(
                TransferCreationStatus.NotFound,
                "Transfer accounts were not found."));
        }

        if (fromAccount.OwnerUserId != user.Id || toAccount.OwnerUserId != user.Id)
        {
            return Result<(Account, Account), TransferCreationResult>.Failure(TransferCreationResult.Failure(
                TransferCreationStatus.Forbidden,
                "Transfers can only be created for your own accounts."));
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

            return Result<(Account, Account), TransferCreationResult>.Failure(TransferCreationResult.Failure(
                TransferCreationStatus.InvalidRequest,
                "Transfers are allowed only between active accounts.",
                accountIssues.ToArray()));
        }

        return Result<(Account, Account), TransferCreationResult>.Success((fromAccount, toAccount));
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

    private async Task<Category> ResolveSystemCategoryAsync(Guid householdId, string name, CancellationToken ct)
    {
        var existing = await db.Categories
            .SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.Name.ToLower() == name.ToLower(), ct);
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            return existing;
        }

        var created = new Category
        {
            HouseholdId = householdId,
            Name = name,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Categories.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }
}