using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Treasury.IntegrationTests;

public class SharedReadOnlyUiPermissionTests
{
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Share_Readonly_Endpoint_Rejects_Blank_Email(string? email)
    {
        await using var app = new TreasuryHostFactory();

        var ownerClient = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!";

        await RegisterAndSignInAsync(ownerClient, ownerEmail, password);

        var accountResponse = await ownerClient.PostAsJsonAsync("/api/accounts", new
        {
            Name = "Owner account",
            Currency = "PLN",
            AccountType = "cash-wallet"
        });
        accountResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var accountJson = JsonDocument.Parse(await accountResponse.Content.ReadAsStringAsync());
        var accountId = accountJson.RootElement.GetProperty("id").GetGuid();

        var shareResponse = await ownerClient.PostAsJsonAsync($"/api/accounts/{accountId}/share-readonly", new
        {
            Email = email
        });

        shareResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await shareResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Email");
        body.Should().Contain("required");
    }

    [Fact]
    public async Task Shared_User_Cannot_Post_To_Owner_Edit_Endpoints()
    {
        await using var app = new TreasuryHostFactory();

        var ownerClient = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var sharedClient = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        var sharedEmail = $"shared-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!";

        await RegisterAndSignInAsync(ownerClient, ownerEmail, password);
        await RegisterAndSignInAsync(sharedClient, sharedEmail, password);

        var accountResponse = await ownerClient.PostAsJsonAsync("/api/accounts", new
        {
            Name = "Owner account",
            Currency = "PLN",
            AccountType = "cash-wallet"
        });
        accountResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var accountJson = JsonDocument.Parse(await accountResponse.Content.ReadAsStringAsync());
        var accountId = accountJson.RootElement.GetProperty("id").GetGuid();

        var shareResponse = await ownerClient.PostAsJsonAsync($"/api/accounts/{accountId}/share-readonly", new
        {
            Email = sharedEmail
        });
        shareResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var accountsVisibleToShared = await sharedClient.GetAsync("/api/accounts");
        accountsVisibleToShared.StatusCode.Should().Be(HttpStatusCode.OK);
        var accountsBody = await accountsVisibleToShared.Content.ReadAsStringAsync();
        accountsBody.Should().Contain(accountId.ToString());
        accountsBody.Should().Contain("\"isReadOnly\":true");

        var postTransaction = await sharedClient.PostAsJsonAsync("/api/transactions", new
        {
            AccountId = accountId,
            Description = "Attempt by shared user",
            Category = "General",
            Amount = 10m,
            Currency = "PLN",
            Type = "expense",
            TransactionDate = DateTime.UtcNow
        });

        postTransaction.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var ownerTransactionResponse = await ownerClient.PostAsJsonAsync("/api/transactions", new
        {
            AccountId = accountId,
            Description = "Owner transaction",
            Category = "General",
            Amount = 15m,
            Currency = "PLN",
            Type = "expense",
            TransactionDate = DateTime.UtcNow
        });
        ownerTransactionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var transactionJson = JsonDocument.Parse(await ownerTransactionResponse.Content.ReadAsStringAsync());
        var transactionId = transactionJson.RootElement.GetProperty("id").GetGuid();

        var editTransaction = await sharedClient.PutAsJsonAsync($"/api/transactions/{transactionId}", new
        {
            Id = transactionId,
            Description = "Shared user edit attempt"
        });

        editTransaction.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Additional guard: shared users must not be able to perform owner-only mutations such as balance correction
        var postBalanceCorrection = await sharedClient.PostAsJsonAsync($"/api/accounts/{accountId}/balance-correction", new
        {
            Amount = 100.00m,
            Reason = "Malicious correction by shared user"
        });

        // Expect that the mutation is forbidden for shared users
        postBalanceCorrection.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Shared_User_Transactions_Page_Shows_Only_Visible_Accounts_Data()
    {
        await using var app = new TreasuryHostFactory();

        var ownerClient = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var sharedClient = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        var sharedEmail = $"shared-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!";

        await RegisterAndSignInAsync(ownerClient, ownerEmail, password);
        await RegisterAndSignInAsync(sharedClient, sharedEmail, password);

        var sharedAccountId = await CreateAccountAsync(ownerClient, "Shared account");
        var privateAccountId = await CreateAccountAsync(ownerClient, "Private account");

        var shareResponse = await ownerClient.PostAsJsonAsync($"/api/accounts/{sharedAccountId}/share-readonly", new
        {
            Email = sharedEmail
        });
        shareResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sharedTxResponse = await ownerClient.PostAsJsonAsync("/api/transactions", new
        {
            AccountId = sharedAccountId,
            Description = "Visible shared transaction",
            Category = "General",
            Amount = 5m,
            Currency = "PLN",
            Type = "expense",
            TransactionDate = DateTime.UtcNow
        });
        sharedTxResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var privateTxResponse = await ownerClient.PostAsJsonAsync("/api/transactions", new
        {
            AccountId = privateAccountId,
            Description = "Hidden private transaction",
            Category = "General",
            Amount = 7m,
            Currency = "PLN",
            Type = "expense",
            TransactionDate = DateTime.UtcNow
        });
        privateTxResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var pageResponse = await sharedClient.GetAsync("/transactions");
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await pageResponse.Content.ReadAsStringAsync();
        html.Should().Contain("Visible shared transaction");
        html.Should().Contain("Shared account");
        html.Should().NotContain("Hidden private transaction");
        html.Should().NotContain("Private account");
    }

    [Fact]
    public async Task Shared_User_Transfers_Page_Shows_Only_Visible_Transfer_History()
    {
        await using var app = new TreasuryHostFactory();

        var ownerClient = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var sharedClient = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        var sharedEmail = $"shared-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!";

        await RegisterAndSignInAsync(ownerClient, ownerEmail, password);
        await RegisterAndSignInAsync(sharedClient, sharedEmail, password);

        var sharedAccountId = await CreateAccountAsync(ownerClient, "Shared source");
        var ownerVisibleCounterpartyId = await CreateAccountAsync(ownerClient, "Owner counterparty");
        var privateFromId = await CreateAccountAsync(ownerClient, "Private from");
        var privateToId = await CreateAccountAsync(ownerClient, "Private to");

        var shareResponse = await ownerClient.PostAsJsonAsync($"/api/accounts/{sharedAccountId}/share-readonly", new
        {
            Email = sharedEmail
        });
        shareResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var visibleTransferResponse = await ownerClient.PostAsJsonAsync("/api/transfers", new
        {
            FromAccountId = sharedAccountId,
            ToAccountId = ownerVisibleCounterpartyId,
            Amount = 12m,
            Currency = "PLN",
            Description = "Visible transfer for shared user",
            TransferDate = DateTime.UtcNow
        });
        visibleTransferResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var hiddenTransferResponse = await ownerClient.PostAsJsonAsync("/api/transfers", new
        {
            FromAccountId = privateFromId,
            ToAccountId = privateToId,
            Amount = 14m,
            Currency = "PLN",
            Description = "Hidden transfer for shared user",
            TransferDate = DateTime.UtcNow
        });
        hiddenTransferResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var pageResponse = await sharedClient.GetAsync("/transfers");
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await pageResponse.Content.ReadAsStringAsync();
        html.Should().Contain("Visible transfer for shared user");
        html.Should().NotContain("Hidden transfer for shared user");
        html.Should().NotContain("Private from");
        html.Should().NotContain("Private to");
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
        using var accountJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return accountJson.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task RegisterAndSignInAsync(HttpClient client, string email, string password)
    {
        var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
        {
            Email = email,
            Password = password,
            ConfirmPassword = password
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        registerResponse.Headers.Location?.ToString().Should().Be("/");

        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
        {
            Email = email,
            Password = password
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        loginResponse.Headers.Location?.ToString().Should().Be("/");

        var whoAmI = await client.GetAsync("/api/accounts");
        whoAmI.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }
}