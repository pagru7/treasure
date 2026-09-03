# Review Package

Base: 0bb45f0435a7cc6968a4439b5b90bf034bd95424
Head: 875b9a2fced19ed71f2dbb2171936173a7064bab

## Commits

875b9a2 chore: untrack workflow artifact .superpowers/sdd/task-1-report.md
64db3f2 test: strengthen accounts sharing ui assertions (picker semantics; no manual email entry)
026e307 docs: append Task 1 fix report summary
8ad50dd test: strengthen accounts sharing ui test (verify shared-client behavior, household picker; dispose JsonDocument)\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
2a0620c test: add regression for account sharing ui

## Diff Stat

 .gitignore                                         |  3 +
 .../AccountsSharingUiTests.cs                      | 97 ++++++++++++++++++++++
 2 files changed, 100 insertions(+)

## Full Diff (-U10)

diff --git a/.gitignore b/.gitignore
index e40f379..e43aad8 100644
--- a/.gitignore
+++ b/.gitignore
@@ -1,7 +1,10 @@
 bin/
 obj/
 .vs/
 *.user
 *.suo
 .worktrees/
 worktrees/
+
+# Workflow artifacts produced by superpowers sessions
+.superpowers/sdd/task-1-report.md
diff --git a/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
new file mode 100644
index 0000000..9e99b29
--- /dev/null
+++ b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
@@ -0,0 +1,97 @@
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
+        using var accountJson = JsonDocument.Parse(await accountResponse.Content.ReadAsStringAsync());
+        var accountId = accountJson.RootElement.GetProperty("id").GetGuid();
+
+        var shareResponse = await ownerClient.PostAsJsonAsync($"/api/accounts/{accountId}/share-readonly", new
+        {
+            Email = sharedEmail
+        });
+        shareResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        // Owner's accounts page should show shared-user and (eventually) a household picker in the UI
+        var pageResponse = await ownerClient.GetAsync("/accounts");
+        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        var body = await pageResponse.Content.ReadAsStringAsync();
+        // Core expectations preserved from the brief
+        body.Should().Contain("Shared with:");
+        body.Should().Contain(sharedEmail);
+        // Explicit picker semantics expected in the UI
+        body.Should().Contain("Share with household user");
+        body.Should().Contain("Share read-only");
+        body.Should().NotContain(ownerEmail);
+
+        // No manual email-entry should be present for household sharing
+        body.Should().NotContain("input type=\"email\"");
+        body.Should().NotContain("Enter email");
+        body.Should().NotContain("Invite by email");
+
+        // Verify shared user can see the shared account but does not see owner-only share controls
+        var sharedPageResponse = await sharedClient.GetAsync("/accounts");
+        sharedPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+        var sharedBody = await sharedPageResponse.Content.ReadAsStringAsync();
+        sharedBody.Should().Contain("Shared account");
+        // Shared users should not see the 'Share read-only' control for accounts they only have read access to
+        sharedBody.Should().NotContain("Share read-only");
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
