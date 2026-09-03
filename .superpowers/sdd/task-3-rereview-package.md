# Review Package

Base: dab7ac4e9b3f810345d1c36a6cd193940a29bc79
Head: fa62627e54da41747d702f6306bdfdf44a40df07

## Commits

fa62627 chore: untrack .superpowers/sdd/task-3-report.md (Task 3 cleanup)
21ccf47 test: cover account sharing UI and permissions
25501b2 test: cover account sharing ui and permissions

## Diff Stat

 tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs     |  2 ++
 .../SharedReadOnlyUiPermissionTests.cs                        | 11 +++++++++++
 2 files changed, 13 insertions(+)

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
index 4998e29..aba03a1 100644
--- a/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
+++ b/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
@@ -59,20 +59,31 @@ public class SharedReadOnlyUiPermissionTests
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
+        // Additional guard: shared users must not be able to update account metadata
+        var putAccount = await sharedClient.PutAsJsonAsync($"/api/accounts/{accountId}", new
+        {
+            Name = "Malicious rename",
+            Currency = "PLN",
+            AccountType = "cash-wallet"
+        });
+
+        // Expect that the mutation is not allowed — either Forbidden or NotFound depending on routing
+        putAccount.IsSuccessStatusCode.Should().BeFalse();
     }
 
     private static async Task RegisterAndSignInAsync(HttpClient client, string email, string password)
     {
         var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
         {
             Email = email,
             Password = password,
             ConfirmPassword = password
         });
