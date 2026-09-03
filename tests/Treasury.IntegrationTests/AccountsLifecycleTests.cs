using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Treasury.IntegrationTests;

public class AccountsLifecycleTests
{
    [Fact]
    public async Task Delete_Allows_Account_With_Zero_Balance()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var accountId = await CreateAccountAsync(client, name: "Delete me", bankAccountNumber: "1234567890123456");

        var deleteResponse = await client.DeleteAsync($"/api/accounts/{accountId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var accounts = await GetAccountsAsync(client);
        accounts.RootElement.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == accountId);
    }

    [Fact]
    public async Task Delete_Blocks_Account_With_Transactions_And_NonZero_Balance()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var accountId = await CreateAccountAsync(client, name: "Busy account", bankAccountNumber: "1234567890123456");

        var transactionResponse = await client.PostAsJsonAsync("/api/transactions", new
        {
            AccountId = accountId,
            Description = "Card payment",
            Category = "General",
            Amount = 10m,
            Currency = "PLN",
            Type = "expense",
            TransactionDate = DateTime.UtcNow
        });
        transactionResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var deleteResponse = await client.DeleteAsync($"/api/accounts/{accountId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await deleteResponse.Content.ReadAsStringAsync();
        body.Should().Contain("transaction history and non-zero balance");
    }

    [Fact]
    public async Task Deactivate_Hides_Account_From_Default_List()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var accountId = await CreateAccountAsync(client, name: "Hide me", bankAccountNumber: "1234567890123456");

        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{accountId}/active-state", new
        {
            IsActive = false
        });
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var defaultAccounts = await GetAccountsAsync(client);
        defaultAccounts.RootElement.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == accountId);

        using var includeInactiveAccounts = await GetAccountsAsync(client, "?includeInactive=true");
        var inactiveAccount = includeInactiveAccounts.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == accountId);
        inactiveAccount.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Update_Trims_BankAccountNumber()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var accountId = await CreateAccountAsync(client, name: "Editable account");

        var updateResponse = await client.PutAsJsonAsync($"/api/accounts/{accountId}", new
        {
            Name = "Updated account",
            BankAccountNumber = " 12345678901234567890123456789012 "
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var accounts = await GetAccountsAsync(client, "?includeInactive=true");
        var account = accounts.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == accountId);
        account.GetProperty("name").GetString().Should().Be("Updated account");
        account.GetProperty("bankAccountNumber").GetString().Should().Be("12345678901234567890123456789012");
    }

    [Fact]
    public async Task Creating_Transaction_Blocks_Inactive_Account()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var accountId = await CreateAccountAsync(client, name: "Inactive account");

        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{accountId}/active-state", new { IsActive = false });
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var txResponse = await client.PostAsJsonAsync("/api/transactions", new
        {
            AccountId = accountId,
            Description = "Should be blocked",
            Category = "General",
            Amount = 10m,
            Currency = "PLN",
            Type = "expense",
            TransactionDate = DateTime.UtcNow
        });

        txResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await txResponse.Content.ReadAsStringAsync();
        body.Should().Contain("inactive");
    }

    [Fact]
    public async Task Creating_Transfer_Blocks_Inactive_Source_Account()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var fromId = await CreateAccountAsync(client, name: "From");
        var toId = await CreateAccountAsync(client, name: "To");

        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{fromId}/active-state", new { IsActive = false });
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var transferResponse = await client.PostAsJsonAsync("/api/transfers", new
        {
            FromAccountId = fromId,
            ToAccountId = toId,
            Amount = 5m,
            Currency = "PLN",
            Description = "Blocked transfer",
            TransferDate = DateTime.UtcNow
        });

        transferResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await transferResponse.Content.ReadAsStringAsync();
        body.Should().Contain("inactive");
    }

    [Fact]
    public async Task TransactionsPage_Shows_Inactive_Account_Name_For_Historical_Transactions()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var accountId = await CreateAccountAsync(client, name: "HistoricAccount");

        var transactionResponse = await client.PostAsJsonAsync("/api/transactions", new
        {
            AccountId = accountId,
            Description = "Old payment",
            Category = "General",
            Amount = 12.34m,
            Currency = "PLN",
            Type = "expense",
            TransactionDate = DateTime.UtcNow
        });
        transactionResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{accountId}/active-state", new { IsActive = false });
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Request the transactions page and ensure the historical transaction still shows the account name
        var pageResponse = await client.GetAsync("/transactions");
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await pageResponse.Content.ReadAsStringAsync();

        html.Should().Contain("HistoricAccount");
        html.Should().NotContain("Unknown account");
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

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name, string? bankAccountNumber = null)
    {
        var response = await client.PostAsJsonAsync("/api/accounts", new
        {
            Name = name,
            Currency = "PLN",
            AccountType = "cash-wallet",
            BankAccountNumber = bankAccountNumber
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> GetAccountsAsync(HttpClient client, string query = "")
    {
        var response = await client.GetAsync($"/api/accounts{query}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
