using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Treasury.App.Infrastructure.Data;

namespace Treasury.IntegrationTests;

public class TransactionEditingRulesTests
{
    [Fact]
    public async Task Create_Transaction_Stores_BalanceAfterTransaction()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var accountId = await CreateAccountAsync(client, "Balance tracking");

        var response = await client.PostAsJsonAsync("/api/transactions", new
        {
            AccountId = accountId,
            Description = "Salary deposit",
            Category = "Income",
            Amount = 1200m,
            Currency = "PLN",
            Type = "income",
            TransactionDate = DateTime.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var transactionId = payload.RootElement.GetProperty("id").GetGuid();

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();

        var transaction = await db.Transactions.SingleAsync(x => x.Id == transactionId);
        var account = await db.Accounts.SingleAsync(x => x.Id == accountId);

        transaction.BalanceAfterTransaction.Should().Be(account.CurrentBalance);
    }

    [Fact]
    public async Task Edit_NonLatest_Rejects_Amount_Date_Type_Changes()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var accountId = await CreateAccountAsync(client, "Historical edits");

        var firstTransactionId = await CreateTransactionAsync(client, accountId, "First", 10m, "expense", DateTime.UtcNow.AddDays(-2));
        _ = await CreateTransactionAsync(client, accountId, "Second", 20m, "expense", DateTime.UtcNow.AddDays(-1));

        var response = await client.PutAsJsonAsync($"/api/transactions/{firstTransactionId}", new
        {
            Id = firstTransactionId,
            Description = "First updated",
            Category = "Updated",
            Amount = 15m,
            Type = "income",
            TransactionDate = DateTime.UtcNow.AddDays(-3),
            TagIds = Array.Empty<Guid>()
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("latest transaction");
        body.Should().Contain("Amount can only be changed on the latest transaction.");
        body.Should().Contain("Transaction date can only be changed on the latest transaction.");
        body.Should().Contain("Type can only be changed on the latest transaction.");
    }

    [Fact]
    public async Task Edit_Latest_Allows_Amount_And_Recomputes_Balance()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var accountId = await CreateAccountAsync(client, "Latest edit");

        _ = await CreateTransactionAsync(client, accountId, "Earlier", 10m, "expense", DateTime.UtcNow.AddDays(-2));
        var latestTransactionId = await CreateTransactionAsync(client, accountId, "Latest", 5m, "expense", DateTime.UtcNow.AddDays(-1));

        var response = await client.PutAsJsonAsync($"/api/transactions/{latestTransactionId}", new
        {
            Id = latestTransactionId,
            Description = "Latest adjusted",
            Category = "Updated",
            Amount = 8m,
            Type = "income",
            TransactionDate = DateTime.UtcNow.AddDays(-3),
            TagIds = Array.Empty<Guid>()
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();

        var account = await db.Accounts.SingleAsync(x => x.Id == accountId);
        var editedTransaction = await db.Transactions.SingleAsync(x => x.Id == latestTransactionId);
        var earlierTransaction = await db.Transactions.SingleAsync(x => x.Description == "Earlier" && x.AccountId == accountId);

        account.CurrentBalance.Should().Be(-2m);
        editedTransaction.Description.Should().Be("Latest adjusted");
        editedTransaction.BalanceAfterTransaction.Should().Be(8m);
        earlierTransaction.BalanceAfterTransaction.Should().Be(-2m);
    }

    private static HttpClient CreateAuthenticatedClient(TreasuryHostFactory app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task RegisterAndSignInAsync(HttpClient client)
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!";

        var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
        {
            Email = email,
            Password = password,
            ConfirmPassword = password
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
        {
            Email = email,
            Password = password
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/accounts", new
        {
            Name = name,
            Currency = "PLN",
            AccountType = "cash-wallet"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTransactionAsync(
        HttpClient client,
        Guid accountId,
        string description,
        decimal amount,
        string type,
        DateTime transactionDate)
    {
        var response = await client.PostAsJsonAsync("/api/transactions", new
        {
            AccountId = accountId,
            Description = description,
            Category = "General",
            Amount = amount,
            Currency = "PLN",
            Type = type,
            TransactionDate = transactionDate
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }
}
