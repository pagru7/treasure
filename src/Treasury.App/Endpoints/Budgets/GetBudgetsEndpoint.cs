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
            .Join(
                db.Categories,
                budget => budget.CategoryId,
                category => category.Id,
                (budget, category) => new
                {
                    budget.Id,
                    budget.CategoryId,
                    CategoryName = category.Name,
                    CategoryIsActive = category.IsActive,
                    budget.MonthlyLimit,
                    budget.Currency,
                    budget.Notes
                })
            .Select(x => new
            {
                x.Id,
                x.CategoryId,
                Name = x.CategoryName,
                x.CategoryIsActive,
                x.MonthlyLimit,
                x.Currency,
                x.Notes
            })
            .ToListAsync(ct);

        await SendOkAsync(budgets, ct);
    }
}