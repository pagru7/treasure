using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Treasury.App.Contracts.Budgets;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Budgets;

public sealed class CreateBudgetEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : Endpoint<CreateBudgetCategoryRequest>
{
    public override void Configure()
    {
        Post("/api/budgets");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
    }

    public override async Task HandleAsync(CreateBudgetCategoryRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.MonthlyLimit <= 0m)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                AddError(x => x.Name, "Budget category is required.");
            }

            if (request.MonthlyLimit <= 0m)
            {
                AddError(x => x.MonthlyLimit, "Monthly limit must be greater than zero.");
            }

            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var budget = new BudgetCategory
        {
            HouseholdId = user.HouseholdId,
            Name = request.Name.Trim(),
            MonthlyLimit = request.MonthlyLimit,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
            Notes = request.Notes?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.BudgetCategories.Add(budget);
        await db.SaveChangesAsync(ct);

        await SendAsync(new
        {
            budget.Id,
            budget.Name,
            budget.MonthlyLimit,
            budget.Currency,
            budget.Notes
        }, StatusCodes.Status201Created, ct);
    }
}
