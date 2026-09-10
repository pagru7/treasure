using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Budgets;

public sealed class GetBudgetsEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/budgets");
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

        var budgets = await db.BudgetCategories
            .Where(x => x.HouseholdId == user.HouseholdId)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.MonthlyLimit,
                x.Currency,
                x.Notes
            })
            .ToListAsync(ct);

        await SendOkAsync(budgets, ct);
    }
}