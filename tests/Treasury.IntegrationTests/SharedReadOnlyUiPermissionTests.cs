using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Treasury.IntegrationTests;

public class SharedReadOnlyUiPermissionTests
{
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

        // Additional guard: shared users must not be able to perform owner-only mutations such as balance correction
        var postBalanceCorrection = await sharedClient.PostAsJsonAsync($"/api/accounts/{accountId}/balance-correction", new
        {
            Amount = 100.00m,
            Reason = "Malicious correction by shared user"
        });

        // Expect that the mutation is forbidden for shared users
        postBalanceCorrection.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
