# Review Package

Base: 0bb45f0435a7cc6968a4439b5b90bf034bd95424
Head: 2a0620c5e8b447bd24d5083686ca144c2c35968f

## Commits

2a0620c test: add regression for account sharing ui

## Diff Stat

 .../AccountsSharingUiTests.cs                      | 80 ++++++++++++++++++++++
 1 file changed, 80 insertions(+)

## Full Diff (-U10)

diff --git a/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
new file mode 100644
index 0000000..39112ba
--- /dev/null
+++ b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
@@ -0,0 +1,80 @@
+using System.Net;
+using System.Net.Http.Json;
+using FluentAssertions;
+using Microsoft.AspNetCore.Mvc.Testing;
+using System.Text.Json;
+
+namespace Treasury.IntegrationTests;
+
+public class AccountsSharingUiTests
+{
+    [Fact]
+    public async Task Accounts_Page_Shows_Shared_Users_And_Household_Picker()
+    {
+        await using var app = new TreasuryHostFactory();
+
+        var ownerClient = app.CreateClient(new WebApplicationFactoryClientOptions
+        {
+            AllowAutoRedirect = false,
+            HandleCookies = true
+        });
+        var sharedClient = app.CreateClient(new WebApplicationFactoryClientOptions
+        {
+            AllowAutoRedirect = false,
+            HandleCookies = true
+        });
+
+        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
+        var sharedEmail = $"shared-{Guid.NewGuid():N}@example.com";
+        const string password = "Password123!";
+
+        await RegisterAndSignInAsync(ownerClient, ownerEmail, password);
+        await RegisterAndSignInAsync(sharedClient, sharedEmail, password);
+
+        var accountResponse = await ownerClient.PostAsJsonAsync("/api/accounts", new
+        {
+            Name = "Shared account",
+            Currency = "PLN",
+            AccountType = "cash-wallet"
+        });
+        accountResponse.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        var accountJson = JsonDocument.Parse(await accountResponse.Content.ReadAsStringAsync());
+        var accountId = accountJson.RootElement.GetProperty("id").GetGuid();
+
+        var shareResponse = await ownerClient.PostAsJsonAsync($"/api/accounts/{accountId}/share-readonly", new
+        {
+            Email = sharedEmail
+        });
+        shareResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        var pageResponse = await ownerClient.GetAsync("/accounts");
+        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        var body = await pageResponse.Content.ReadAsStringAsync();
+        body.Should().Contain("Shared with:");
+        body.Should().Contain(sharedEmail);
+        body.Should().Contain("Share read-only");
+        body.Should().NotContain(ownerEmail);
+    }
+
+    private static async Task RegisterAndSignInAsync(HttpClient client, string email, string password)
+    {
+        var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
+        {
+            Email = email,
+            Password = password,
+            ConfirmPassword = password
+        });
+
+        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
+
+        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
+        {
+            Email = email,
+            Password = password
+        });
+
+        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
+    }
+}
