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
    public async Task Create_Backdated_Transaction_Recomputes_Later_Balances()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var accountId = await CreateAccountAsync(client, "Backdated balance");

        var laterTransactionId = await CreateTransactionAsync(client, accountId, "Later income", 100m, "income", DateTime.UtcNow.AddDays(-1));

        var earlierResponse = await client.PostAsJsonAsync("/api/transactions", new
        {
            AccountId = accountId,
            Description = "Earlier expense",
            Category = "General",
            Amount = 25m,
            Currency = "PLN",
            Type = "expense",
            TransactionDate = DateTime.UtcNow.AddDays(-3)
        });

        earlierResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var earlierPayload = JsonDocument.Parse(await earlierResponse.Content.ReadAsStringAsync());
        var earlierTransactionId = earlierPayload.RootElement.GetProperty("id").GetGuid();

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();

        var earlierTransaction = await db.Transactions.SingleAsync(x => x.Id == earlierTransactionId);
        var laterTransaction = await db.Transactions.SingleAsync(x => x.Id == laterTransactionId);
        var account = await db.Accounts.SingleAsync(x => x.Id == accountId);

        earlierTransaction.BalanceAfterTransaction.Should().Be(-25m);
        laterTransaction.BalanceAfterTransaction.Should().Be(75m);
        account.CurrentBalance.Should().Be(75m);
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

    [Fact]
    public async Task Edit_Partial_Request_Leaves_Omitted_Fields_Unchanged()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var accountId = await CreateAccountAsync(client, "Partial update");
        var firstTransactionDate = DateTime.UtcNow.AddDays(-2);

        var firstTransactionId = await CreateTransactionAsync(client, accountId, "First", 10m, "expense", firstTransactionDate);
        _ = await CreateTransactionAsync(client, accountId, "Second", 20m, "income", DateTime.UtcNow.AddDays(-1));

        var response = await client.PutAsJsonAsync($"/api/transactions/{firstTransactionId}", new
        {
            Id = firstTransactionId,
            Description = "First renamed"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();

        var transaction = await db.Transactions.SingleAsync(x => x.Id == firstTransactionId);
        transaction.Description.Should().Be("First renamed");
        transaction.Amount.Should().Be(10m);
        transaction.Type.Should().Be("expense");
        transaction.TransactionDate.Date.Should().Be(firstTransactionDate.Date);
    }

    [Fact]
    public async Task Edit_TransferLinked_Rejects_Changes()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var fromAccountId = await CreateAccountAsync(client, "Transfer from");
        var toAccountId = await CreateAccountAsync(client, "Transfer to");

        var transferResponse = await client.PostAsJsonAsync("/api/transfers", new
        {
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            Amount = 25m,
            Currency = "PLN",
            Description = "Blocked transfer",
            TransferDate = DateTime.UtcNow
        });

        transferResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var transferJson = JsonDocument.Parse(await transferResponse.Content.ReadAsStringAsync());
        var outflowTransactionId = transferJson.RootElement.GetProperty("outflowTransactionId").GetGuid();

        var updateResponse = await client.PutAsJsonAsync($"/api/transactions/{outflowTransactionId}", new
        {
            Id = outflowTransactionId,
            Description = "Should fail"
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await updateResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Transfer-linked transactions cannot be edited");
    }

    [Fact]
    public async Task Create_Transaction_Rejects_Transfer_Type()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var accountId = await CreateAccountAsync(client, "Manual transfer blocked");

        var response = await client.PostAsJsonAsync("/api/transactions", new
        {
            AccountId = accountId,
            Description = "Should fail",
            Category = "General",
            Amount = 10m,
            Currency = "PLN",
            Type = "transfer",
            TransactionDate = DateTime.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("dedicated Transfers flow");
    }

    [Fact]
    public async Task Edit_Transaction_Rejects_Transfer_Type()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var accountId = await CreateAccountAsync(client, "Edit transfer blocked");
        var transactionId = await CreateTransactionAsync(client, accountId, "Regular transaction", 10m, "expense", DateTime.UtcNow);

        var response = await client.PutAsJsonAsync($"/api/transactions/{transactionId}", new
        {
            Id = transactionId,
            Type = "transfer"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("dedicated Transfers flow");
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
