using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.IntegrationTests;

public class CategoryBudgetLinkingTests
{
    [Fact]
    public async Task Saving_Budget_Twice_For_Same_Category_Updates_Existing_Row()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        var email = await RegisterAndSignInAsync(client);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        var category = new Category
        {
            HouseholdId = user.HouseholdId,
            Name = "Utilities",
            IsActive = true
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var first = await client.PostAsJsonAsync("/api/budgets", new
        {
            CategoryId = category.Id,
            MonthlyLimit = 500m,
            Currency = "PLN",
            Notes = "Initial"
        });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/budgets", new
        {
            CategoryId = category.Id,
            MonthlyLimit = 650m,
            Currency = "PLN",
            Notes = "Updated"
        });
        second.StatusCode.Should().Be(HttpStatusCode.OK);

        var budgets = await db.BudgetCategories
            .Where(x => x.HouseholdId == user.HouseholdId && x.CategoryId == category.Id)
            .ToListAsync();
        budgets.Should().HaveCount(1);
        budgets[0].MonthlyLimit.Should().Be(650m);
        budgets[0].Notes.Should().Be("Updated");
    }

    [Fact]
    public async Task Create_Transaction_With_Inactive_Category_Is_Rejected()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        var email = await RegisterAndSignInAsync(client);
        var accountId = await CreateOwnedAccountAsync(app, email, "Main");

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        var category = new Category
        {
            HouseholdId = user.HouseholdId,
            Name = "Travel",
            IsActive = false
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var response = await client.PostAsJsonAsync("/api/transactions", new
        {
            AccountId = accountId,
            Description = "Trip",
            CategoryId = category.Id,
            Amount = 120m,
            Type = "expense",
            TransactionDate = DateTime.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Dashboard_Budget_Health_Uses_CategoryId_After_Category_Rename()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        var email = await RegisterAndSignInAsync(client);
        var accountId = await CreateOwnedAccountAsync(app, email, "Wallet");

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
            var user = await db.Users.SingleAsync(x => x.Email == email);

            var category = new Category
            {
                HouseholdId = user.HouseholdId,
                Name = "Groceries",
                IsActive = true
            };
            db.Categories.Add(category);
            await db.SaveChangesAsync();

            db.BudgetCategories.Add(new BudgetCategory
            {
                HouseholdId = user.HouseholdId,
                CategoryId = category.Id,
                Name = "Groceries",
                MonthlyLimit = 100m,
                Currency = "PLN",
                Notes = string.Empty
            });
            await db.SaveChangesAsync();

            await client.PostAsJsonAsync("/api/transactions", new
            {
                AccountId = accountId,
                Description = "Big shopping",
                CategoryId = category.Id,
                Amount = 130m,
                Type = "expense",
                TransactionDate = DateTime.UtcNow
            });

            category.Name = "Food";
            category.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var dashboard = await client.GetAsync("/");
        dashboard.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await dashboard.Content.ReadAsStringAsync();

        html.Should().Contain("Budget health");
        html.Should().Contain("Food");
        html.Should().Contain("130,00 PLN / 100,00 PLN");
    }

    [Fact]
    public async Task Create_Transaction_With_Category_Name_And_No_CategoryId_Creates_Category()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        var email = await RegisterAndSignInAsync(client);
        var accountId = await CreateOwnedAccountAsync(app, email, "Main");

        var response = await client.PostAsJsonAsync("/api/transactions", new
        {
            AccountId = accountId,
            Description = "Created with manual category",
            Category = "Emergency",
            Amount = 100m,
            Type = TransactionType.Expense,
            TransactionDate = DateTime.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        var category = await db.Categories.SingleAsync(x => x.HouseholdId == user.HouseholdId && x.Name == "Emergency");
        var transaction = await db.Transactions.SingleAsync(x => x.HouseholdId == user.HouseholdId && x.Description == "Created with manual category");

        category.IsActive.Should().BeTrue();
        transaction.CategoryId.Should().Be(category.Id);
        transaction.Category.Should().Be("Emergency");
    }

    private static HttpClient CreateAuthenticatedClient(TreasuryHostFactory app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<string> RegisterAndSignInAsync(HttpClient client)
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!";
        var householdName = $"household-{Guid.NewGuid():N}";

        var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
        {
            Email = email,
            Password = password,
            ConfirmPassword = password,
            HouseholdNameOrId = householdName
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
        {
            Email = email,
            Password = password
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        return email;
    }

    private static async Task<Guid> CreateOwnedAccountAsync(TreasuryHostFactory app, string email, string name)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);

        var account = new Account
        {
            HouseholdId = user.HouseholdId,
            OwnerUserId = user.Id,
            Name = name,
            Currency = "PLN",
            AccountType = "cash-wallet",
            IsActive = true
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }
}
