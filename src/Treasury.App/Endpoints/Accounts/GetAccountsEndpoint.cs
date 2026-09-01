using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Contracts.Accounts;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Accounts;

public sealed class GetAccountsEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/accounts");
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

        var accounts = await db.Accounts
            .Where(x =>
                x.HouseholdId == user.HouseholdId
                && (x.OwnerUserId == user.Id
                    || x.OwnerUserId == "seed"
                    || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
            .OrderBy(x => x.Name)
            .Select(x => new AccountResponse
            {
                Id = x.Id,
                Name = x.Name,
                Currency = x.Currency,
                AccountType = x.AccountType,
                CurrentBalance = x.CurrentBalance,
                IsReadOnly = x.OwnerUserId != user.Id
            })
            .ToListAsync(ct);

        await SendOkAsync(accounts, ct);
    }
}
