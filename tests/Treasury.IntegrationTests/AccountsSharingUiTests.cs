using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;

namespace Treasury.IntegrationTests;

public class AccountsSharingUiTests
{
    [Fact]
    public async Task Accounts_Page_Shows_Shared_Users_And_Household_Picker()
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
            Name = "Shared account",
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

        var pageResponse = await ownerClient.GetAsync("/accounts");
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await pageResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Shared with:");
        body.Should().Contain(sharedEmail);
        body.Should().Contain("Share read-only");
        body.Should().NotContain(ownerEmail);
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

        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
        {
            Email = email,
            Password = password
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }
}
