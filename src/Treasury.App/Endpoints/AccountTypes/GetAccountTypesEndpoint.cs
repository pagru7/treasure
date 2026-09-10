using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.AccountTypes;

public sealed class GetAccountTypesEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/account-types");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var accountTypes = await db.AccountTypes
            .Where(x => x.HouseholdId == user.HouseholdId)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description
            })
            .ToListAsync(ct);

        await SendOkAsync(accountTypes, ct);
    }
}