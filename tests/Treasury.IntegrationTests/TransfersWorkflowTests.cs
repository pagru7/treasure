using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Treasury.App.Infrastructure.Data;

namespace Treasury.IntegrationTests;

public class TransfersWorkflowTests
{
    [Fact]
    public async Task Transfer_Creates_Transfer_And_Two_Linked_Transactions()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var fromAccountId = await CreateAccountAsync(client, "From account");
        var toAccountId = await CreateAccountAsync(client, "To account");

        var response = await client.PostAsJsonAsync("/api/transfers", new
        {
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            Amount = 25.50m,
            Currency = "PLN",
            Description = "Rent split",
            TransferDate = DateTime.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var transferId = payload.RootElement.GetProperty("id").GetGuid();
        var outflowTransactionId = payload.RootElement.GetProperty("outflowTransactionId").GetGuid();
        var inflowTransactionId = payload.RootElement.GetProperty("inflowTransactionId").GetGuid();

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();

        var transfer = await db.Transfers.SingleAsync(x => x.Id == transferId);
        transfer.OutflowTransactionId.Should().Be(outflowTransactionId);
        transfer.InflowTransactionId.Should().Be(inflowTransactionId);
        transfer.FromAccountId.Should().Be(fromAccountId);
        transfer.ToAccountId.Should().Be(toAccountId);

        var outflow = await db.Transactions.SingleAsync(x => x.Id == outflowTransactionId);
        var inflow = await db.Transactions.SingleAsync(x => x.Id == inflowTransactionId);

        outflow.AccountId.Should().Be(fromAccountId);
        outflow.Type.Should().Be("transfer-out");
        inflow.AccountId.Should().Be(toAccountId);
        inflow.Type.Should().Be("transfer-in");

        var fromAccount = await db.Accounts.SingleAsync(x => x.Id == fromAccountId);
        var toAccount = await db.Accounts.SingleAsync(x => x.Id == toAccountId);

        fromAccount.CurrentBalance.Should().Be(-25.50m);
        toAccount.CurrentBalance.Should().Be(25.50m);
    }

    [Fact]
    public async Task Transfer_Rejects_Inactive_Accounts()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var fromAccountId = await CreateAccountAsync(client, "From account");
        var toAccountId = await CreateAccountAsync(client, "To account");

        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{fromAccountId}/active-state", new
        {
            IsActive = false
        });
        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.PostAsJsonAsync("/api/transfers", new
        {
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            Amount = 10m,
            Currency = "PLN",
            Description = "Blocked transfer",
            TransferDate = DateTime.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("inactive");
    }

    [Fact]
    public async Task TransfersPage_Shows_Recent_History()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var fromAccountId = await CreateAccountAsync(client, "History from");
        var toAccountId = await CreateAccountAsync(client, "History to");

        var response = await client.PostAsJsonAsync("/api/transfers", new
        {
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            Amount = 12.34m,
            Currency = "PLN",
            Description = "History transfer",
            TransferDate = DateTime.UtcNow
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var pageResponse = await client.GetAsync("/transfers");
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var html = await pageResponse.Content.ReadAsStringAsync();
        html.Should().Contain("Transfers");
        html.Should().Contain("History transfer");
        html.Should().Contain("History from");
        html.Should().Contain("History to");
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
}
