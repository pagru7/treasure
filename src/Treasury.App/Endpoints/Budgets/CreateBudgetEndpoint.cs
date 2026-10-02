using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

        if ((request.CategoryId is null || request.CategoryId == Guid.Empty) && string.IsNullOrWhiteSpace(request.Name))
        {
            AddError("Budget category is required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        if (request.MonthlyLimit <= 0m)
        {
            AddError(x => x.MonthlyLimit, "Monthly limit must be greater than zero.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var category = await ResolveCategoryAsync(user.HouseholdId, request.CategoryId, request.Name, ct);
        if (category is null)
        {
            AddError("Budget category is required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        if (!category.IsActive)
        {
            AddError("Category is disabled.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var existing = await db.BudgetCategories
            .SingleOrDefaultAsync(x => x.HouseholdId == user.HouseholdId && x.CategoryId == category.Id, ct);

        var utcNow = DateTime.UtcNow;
        if (existing is null)
        {
            var budget = new BudgetCategory
            {
                HouseholdId = user.HouseholdId,
                CategoryId = category.Id,
                Name = category.Name,
                MonthlyLimit = request.MonthlyLimit,
                Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
                Notes = request.Notes?.Trim() ?? string.Empty,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            db.BudgetCategories.Add(budget);
            await db.SaveChangesAsync(ct);

            await SendAsync(new
            {
                budget.Id,
                budget.CategoryId,
                budget.Name,
                budget.MonthlyLimit,
                budget.Currency,
                budget.Notes
            }, StatusCodes.Status201Created, ct);
            return;
        }

        existing.Name = category.Name;
        existing.MonthlyLimit = request.MonthlyLimit;
        existing.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant();
        existing.Notes = request.Notes?.Trim() ?? string.Empty;
        existing.UpdatedAt = utcNow;
        await db.SaveChangesAsync(ct);

        await SendOkAsync(new
        {
            existing.Id,
            existing.CategoryId,
            existing.Name,
            existing.MonthlyLimit,
            existing.Currency,
            existing.Notes
        }, ct);
    }

    private async Task<Category?> ResolveCategoryAsync(Guid householdId, Guid? categoryId, string? categoryName, CancellationToken ct)
    {
        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            return await db.Categories.SingleOrDefaultAsync(x => x.Id == categoryId.Value && x.HouseholdId == householdId, ct);
        }

        var normalizedName = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        return await db.Categories
            .SingleOrDefaultAsync(x => x.HouseholdId == householdId && x.Name.ToLower() == normalizedName.ToLower(), ct);
    }
}