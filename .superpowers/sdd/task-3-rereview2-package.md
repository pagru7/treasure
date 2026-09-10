# Review Package

Base: dab7ac4e9b3f810345d1c36a6cd193940a29bc79
Head: 42eb509a56b7406ece91741ef2713beea64fe616

## Commits

42eb509 Fix Task 3: assert owner-only balance-correction endpoint is forbidden for shared users\n\nReplace ineffective PUT /api/accounts/{id} check with POST /api/accounts/{id}/balance-correction assertion
fa62627 chore: untrack .superpowers/sdd/task-3-report.md (Task 3 cleanup)
21ccf47 test: cover account sharing UI and permissions
25501b2 test: cover account sharing ui and permissions

## Diff Stat

 tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs      |  2 ++
 .../SharedReadOnlyUiPermissionTests.cs                         | 10 ++++++++++
 2 files changed, 12 insertions(+)

## Full Diff (-U10)

diff --git a/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
index 26196f5..5455833 100644
--- a/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
+++ b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
@@ -58,20 +58,22 @@ public class AccountsSharingUiTests
         pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
 
         var body = await pageResponse.Content.ReadAsStringAsync();
         // Core expectations preserved from the brief
         body.Should().Contain("Shared with:");
         body.Should().Contain(sharedEmail);
         // Explicit picker semantics expected in the UI
         body.Should().Contain("Share with household user");
         body.Should().Contain("Share read-only");
         body.Should().NotContain(ownerEmail);
+        // Explicit picker exclusion check — the owner's email should not appear as a selectable household option
+        body.Should().NotContain($"<option value=\"{ownerEmail}\"");
 
         // No manual email-entry should be present for household sharing
         body.Should().NotContain("input type=\"email\"");
         body.Should().NotContain("Enter email");
         body.Should().NotContain("Invite by email");
 
         // Verify shared user can see the shared account but does not see owner-only share controls
         var sharedPageResponse = await sharedClient.GetAsync("/accounts");
         sharedPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
         var sharedBody = await sharedPageResponse.Content.ReadAsStringAsync();
diff --git a/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs b/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
index 4998e29..c0f28bf 100644
--- a/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
+++ b/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
@@ -59,20 +59,30 @@ public class SharedReadOnlyUiPermissionTests
             AccountId = accountId,
             Description = "Attempt by shared user",
             Category = "General",
             Amount = 10m,
             Currency = "PLN",
             Type = "expense",
             TransactionDate = DateTime.UtcNow
         });
 
         postTransaction.StatusCode.Should().Be(HttpStatusCode.Forbidden);
+
+        // Additional guard: shared users must not be able to perform owner-only mutations such as balance correction
+        var postBalanceCorrection = await sharedClient.PostAsJsonAsync($"/api/accounts/{accountId}/balance-correction", new
+        {
+            Amount = 100.00m,
+            Reason = "Malicious correction by shared user"
+        });
+
+        // Expect that the mutation is forbidden for shared users
+        postBalanceCorrection.StatusCode.Should().Be(HttpStatusCode.Forbidden);
     }
 
     private static async Task RegisterAndSignInAsync(HttpClient client, string email, string password)
     {
         var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
         {
             Email = email,
             Password = password,
             ConfirmPassword = password
         });
