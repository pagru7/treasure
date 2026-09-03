# Review Package

Base: 0bb45f0435a7cc6968a4439b5b90bf034bd95424
Head: 026e307eb760973cbf0f1aee84eba40b6ecd5067

## Commits

026e307 docs: append Task 1 fix report summary
8ad50dd test: strengthen accounts sharing ui test (verify shared-client behavior, household picker; dispose JsonDocument)\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
2a0620c test: add regression for account sharing ui

## Diff Stat

 .superpowers/sdd/task-1-report.md                  | 32 ++++++++
 .../AccountsSharingUiTests.cs                      | 93 ++++++++++++++++++++++
 2 files changed, 125 insertions(+)

## Full Diff (-U10)

diff --git a/.superpowers/sdd/task-1-report.md b/.superpowers/sdd/task-1-report.md
new file mode 100644
index 0000000..ae1e19f
--- /dev/null
+++ b/.superpowers/sdd/task-1-report.md
@@ -0,0 +1,32 @@
+# Task 1 Report
+
+What I implemented:
+- Added failing integration test: tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
+
+What I tested and test results:
+- Ran the single test; it failed as expected (Accounts page does not yet show sharing UI). See failing test output captured in test run.
+
+TDD evidence:
+- RED: dotnet test ... -> 1 failed
+- GREEN: (not applicable; UI not implemented yet)
+
+Files changed:
+- tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
+
+Self-review findings:
+- Test follows the brief exactly and uses existing helper TreasuryHostFactory.
+- Test is isolated and only adds one file.
+
+Issues or concerns:
+- None
+
+Commit:
+- 2a0620c test: add regression for account sharing ui
+
+--- Fix appended: 2026-09-03T09:34:31+02:00 ---
+
+Status: FAIL (still a red regression; the owner page does not render expected household picker)
+Commit(s): 8ad50dd test: strengthen accounts sharing ui test (verify shared-client behavior, household picker; dispose JsonDocument)
+One-line test summary: Owner should see shared user and household picker; shared user should see the shared account but not owner-only share controls — current UI does not implement the picker, so the test fails.
+Concerns: Assertion for "Household" may be brittle if UI markup changes; it's intentional to keep this a failing regression until UI is implemented.
+Report file path: D:\sources2\treasury\.superpowers\sdd\task-1-report.md
diff --git a/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
new file mode 100644
index 0000000..a5b7207
--- /dev/null
+++ b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
@@ -0,0 +1,93 @@
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
+        body.Should().Contain("Share read-only");
+        body.Should().NotContain(ownerEmail);
+
+        // Stronger UX expectations (keeps test failing until UI implements picker semantics)
+        body.Should().Contain("Household");
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
