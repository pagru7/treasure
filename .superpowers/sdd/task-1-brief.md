### Task 1: Write the failing Accounts-page share-panel regression test

**Files:**

- Create: `tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs`

**Interfaces:**

- Consumes:
  - `TreasuryHostFactory`
  - `/auth/register-submit`
  - `/auth/login-submit`
  - `POST /api/accounts`
  - `POST /api/accounts/{id:guid}/share-readonly`
  - `GET /accounts`
- Produces:
  - A regression test proving the Accounts page still does not show the required share panel before implementation.

- [ ] **Step 1: Write the failing test**

```csharp
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
```

- [ ] **Step 2: Run the test to confirm it fails**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsSharingUiTests.Accounts_Page_Shows_Shared_Users_And_Household_Picker"`

Expected: FAIL because the Accounts page does not yet render the share panel or household-user picker.

- [ ] **Step 3: Commit the red test**

```bash
git add tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
git commit -m "test: add regression for account sharing ui"
```

