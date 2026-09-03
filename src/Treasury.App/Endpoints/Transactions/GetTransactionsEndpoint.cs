using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Transactions;

public sealed class GetTransactionsEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/transactions");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var visibleAccountIds = db.Accounts
            .Where(x =>
                x.HouseholdId == user.HouseholdId
                && (x.OwnerUserId == user.Id
                    || x.OwnerUserId == "seed"
                    || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
            .Select(x => x.Id);

        var transactions = await db.Transactions
            .Where(x => x.HouseholdId == user.HouseholdId && visibleAccountIds.Contains(x.AccountId))
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.AccountId,
                x.Description,
                x.Category,
                x.Amount,
                x.Currency,
                x.Type,
                x.TransactionDate,
                x.BalanceAfterTransaction,
                Tags = x.TransactionTags.Select(tt => new
                {
                    tt.Tag.Id,
                    tt.Tag.Name,
                    tt.Tag.Color
                }).ToList()
            })
            .ToListAsync(ct);

        await SendOkAsync(transactions, ct);
    }
}
