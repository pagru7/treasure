using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Bills;

public sealed class GetBillsEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/bills");
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

        var bills = await db.Bills
            .Where(x => x.HouseholdId == user.HouseholdId)
            .OrderBy(x => x.DueDay)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Category,
                x.Amount,
                x.Currency,
                x.DueDay,
                x.IsPaid,
                x.Notes
            })
            .ToListAsync(ct);

        await SendOkAsync(bills, ct);
    }
}
