using Microsoft.EntityFrameworkCore;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Application.Transactions;

public sealed class AccountBalanceRecalculationService(TreasuryDbContext db)
{
    public Task RecalculateAccountAsync(Guid accountId, CancellationToken ct) =>
        RecalculateAccountsAsync([accountId], ct);

    public async Task RecalculateAccountsAsync(IEnumerable<Guid> accountIds, CancellationToken ct)
    {
        foreach (var accountId in accountIds.Distinct())
        {
            var account = await db.Accounts.SingleAsync(x => x.Id == accountId, ct);
            var transactions = await db.Transactions
                .Where(x => x.AccountId == accountId)
                .OrderBy(x => x.TransactionDate)
                .ThenBy(x => x.CreatedAt)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);

            var openingBalance = account.CurrentBalance - transactions.Sum(x => TransactionBalanceMath.GetDelta(x.Amount, x.Type));
            var runningBalance = openingBalance;
            var utcNow = DateTime.UtcNow;
            var accountChanged = false;

            foreach (var transaction in transactions)
            {
                runningBalance += TransactionBalanceMath.GetDelta(transaction.Amount, transaction.Type);
                if (transaction.BalanceAfterTransaction != runningBalance)
                {
                    transaction.BalanceAfterTransaction = runningBalance;
                    accountChanged = true;
                }
            }

            if (account.CurrentBalance != runningBalance)
            {
                account.CurrentBalance = runningBalance;
                accountChanged = true;
            }

            if (accountChanged)
            {
                account.UpdatedAt = utcNow;
            }

            await db.SaveChangesAsync(ct);
        }
    }

    public async Task RecalculateAllAccountsAsync(CancellationToken ct)
    {
        var accountIds = await db.Accounts
            .OrderBy(x => x.Name)
            .Select(x => x.Id)
            .ToListAsync(ct);

        await RecalculateAccountsAsync(accountIds, ct);
    }
}

public static class TransactionBalanceMath
{
    public static string NormalizeType(string? type, string fallback = "expense") =>
        string.IsNullOrWhiteSpace(type) ? fallback : type.Trim().ToLowerInvariant();

    public static decimal GetDelta(decimal amount, string type)
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
}
