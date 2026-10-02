using Microsoft.EntityFrameworkCore;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Application.Transactions;

//TODO: do we really need this service? It seems like we can just recalculate the balance in the transaction editing service after each transaction edit. If we do need it, we should probably make it internal and not expose it to the API layer.
public sealed class AccountBalanceRecalculationService(TreasuryDbContext db)
{
    public Task RecalculateAccountAsync(Guid accountId, CancellationToken ct) =>
        RecalculateAccountsAsync([accountId], ct);

    public async Task RecalculateAccountsAsync(
        IEnumerable<Guid> accountIds,
        CancellationToken ct)
    {
        foreach (var accountId in accountIds.Distinct())
        {
            var account = await db.Accounts
                .SingleAsync(x => x.Id == accountId, ct);
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