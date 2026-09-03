# Review Package

Base: f667d4a71e9a92476fb40eb36e878669fdad75c6
Head: 0d3a73de0ac9491cfcbc9c31a723d58a939d918d

## Commits

0d3a73d Transactions UI: show historical account names for inactive accounts; block creating transactions on inactive accounts in UI; add integration test for transactions page display\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
2f46306 fix(transactions/transfers): block inactive accounts from new writes; exclude inactive accounts in Transactions UI\n\n- Add inactive-account guard to CreateTransactionEndpoint and CreateTransferEndpoint\n- Update Transactions.razor to list only active accounts and handle no active accounts\n- Add integration tests asserting inactive accounts are rejected for transaction & transfer\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
6e1a0b2 feat: add account lifecycle controls and bank number validation

## Diff Stat

 .superpowers/sdd/phase2-task-1-brief.md            |  149 ++
 .superpowers/sdd/phase2-task-1-report.md           |   42 +
 .superpowers/sdd/phase2-task-1-rereview-package.md | 2181 ++++++++++++++++++++
 .superpowers/sdd/phase2-task-1-review-package.md   | 1979 ++++++++++++++++++
 .../Accounts/AccountLifecycleValidation.cs         |   25 +
 .../Application/Accounts/AccountSharingService.cs  |    4 +
 .../Contracts/Accounts/AccountResponse.cs          |    2 +
 .../Contracts/Accounts/CreateAccountRequest.cs     |    1 +
 .../Accounts/SetAccountActiveStateRequest.cs       |    6 +
 .../Contracts/Accounts/UpdateAccountRequest.cs     |    7 +
 src/Treasury.App/Domain/Account.cs                 |    2 +
 .../Endpoints/Accounts/CreateAccountEndpoint.cs    |   11 +
 .../Endpoints/Accounts/DeleteAccountEndpoint.cs    |   57 +
 .../Endpoints/Accounts/GetAccountsEndpoint.cs      |   12 +-
 .../Accounts/SetAccountActiveStateEndpoint.cs      |   57 +
 .../Endpoints/Accounts/UpdateAccountEndpoint.cs    |   80 +
 .../Transactions/CreateTransactionEndpoint.cs      |    8 +
 .../Endpoints/Transfers/CreateTransferEndpoint.cs  |   17 +
 .../Infrastructure/Data/TreasuryDbContext.cs       |    8 +
 ...903122916_AddAccountLifecycleFields.Designer.cs |  738 +++++++
 .../20260903122916_AddAccountLifecycleFields.cs    |   40 +
 .../Migrations/TreasuryDbContextModelSnapshot.cs   |    9 +
 src/Treasury.App/Pages/Accounts.razor              |   25 +-
 src/Treasury.App/Pages/Accounts.razor.cs           |   75 +-
 src/Treasury.App/Pages/Transactions.razor          |   38 +-
 .../AccountsLifecycleTests.cs                      |  239 +++
 .../AccountsSharingUiTests.cs                      |    9 +
 27 files changed, 5809 insertions(+), 12 deletions(-)

## Full Diff (-U10)

diff --git a/.superpowers/sdd/phase2-task-1-brief.md b/.superpowers/sdd/phase2-task-1-brief.md
new file mode 100644
index 0000000..7141508
--- /dev/null
+++ b/.superpowers/sdd/phase2-task-1-brief.md
@@ -0,0 +1,149 @@
+### Task 1: Implement accounts lifecycle and bank account number support
+
+**Files:**
+- Create: `src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs`
+- Create: `src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs`
+- Create: `src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs`
+- Create: `src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs`
+- Create: `src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs`
+- Modify: `src/Treasury.App/Domain/Account.cs`
+- Modify: `src/Treasury.App/Pages/Accounts.razor`
+- Modify: `src/Treasury.App/Pages/Accounts.razor.cs`
+- Modify: `src/Treasury.App/Infrastructure/Migrations/*` (new migration)
+- Test: `tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs`
+
+**Interfaces:**
+- Consumes:
+  - `AccountSharingService.GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct)`
+  - `Policies.OwnerOnly`
+- Produces:
+  - `PUT /api/accounts/{id:guid}`
+  - `PUT /api/accounts/{id:guid}/active-state`
+  - `DELETE /api/accounts/{id:guid}`
+  - Account fields: `bool IsActive`, `string? BankAccountNumber`
+
+- [ ] **Step 1: Write failing lifecycle tests**
+
+```csharp
+// tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
+public class AccountsLifecycleTests
+{
+    [Fact]
+    public async Task Delete_Allows_Account_With_Zero_Balance() { /* create owner + account, call DELETE, expect 204 */ }
+
+    [Fact]
+    public async Task Delete_Blocks_Account_With_Transactions_And_NonZero_Balance() { /* expect validation error */ }
+
+    [Fact]
+    public async Task Deactivate_Hides_Account_From_Default_List() { /* set inactive, GET /api/accounts, expect missing by default */ }
+}
+```
+
+- [ ] **Step 2: Run tests to verify failure**
+
+Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests"`
+Expected: FAIL with missing endpoints/fields/behavior.
+
+- [ ] **Step 3: Add account fields and contracts**
+
+```csharp
+// src/Treasury.App/Domain/Account.cs
+public bool IsActive { get; set; } = true;
+public string? BankAccountNumber { get; set; }
+```
+
+```csharp
+// src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs
+namespace Treasury.App.Contracts.Accounts;
+public sealed class UpdateAccountRequest
+{
+    public string Name { get; set; } = string.Empty;
+    public string? BankAccountNumber { get; set; }
+}
+```
+
+```csharp
+// src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs
+namespace Treasury.App.Contracts.Accounts;
+public sealed class SetAccountActiveStateRequest
+{
+    public bool IsActive { get; set; }
+}
+```
+
+- [ ] **Step 4: Implement account lifecycle endpoints**
+
+```csharp
+// src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs
+public sealed class UpdateAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+    : Endpoint<UpdateAccountRequest>
+{
+    public override void Configure()
+    {
+        Put("/api/accounts/{id:guid}");
+        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
+    }
+
+    public override async Task HandleAsync(UpdateAccountRequest req, CancellationToken ct)
+    {
+        if (!Route<Guid>("id", out var id))
+        {
+            await SendNotFoundAsync(ct);
+            return;
+        }
+        // load user + account, owner check, validate name and bank number (digits 16-34), save, return 200
+    }
+}
+```
+
+```csharp
+// src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs
+public override async Task HandleAsync(CancellationToken ct)
+{
+    // owner-only account lookup
+    // if has transactions and CurrentBalance != 0 => AddError("Cannot remove account with transaction history and non-zero balance. Deactivate it instead.")
+    // else delete and return 204
+}
+```
+
+```csharp
+// src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs
+public override async Task HandleAsync(SetAccountActiveStateRequest req, CancellationToken ct)
+{
+    // owner-only account lookup, set IsActive, UpdatedAt, save, return 200
+}
+```
+
+- [ ] **Step 5: Wire Accounts page controls**
+
+```razor
+<MudSwitch T="bool" Label="Show inactive" @bind-Value="_showInactive" />
+<MudTextField Label="Bank account number (optional)" @bind-Value="_newAccount.BankAccountNumber" />
+<MudButton OnClick="() => ToggleActiveStateAsync(account)">@((account.IsActive) ? "Deactivate" : "Reactivate")</MudButton>
+<MudButton Color="Color.Error" OnClick="() => DeleteAccountAsync(account)">Remove</MudButton>
+```
+
+```csharp
+// Accounts.razor.cs
+private bool _showInactive;
+private async Task LoadAccountsAsync()
+{
+    var accounts = await AccountSharingService.GetVisibleAccountsAsync(_currentUser!, CancellationToken.None);
+    if (!_showInactive) accounts = accounts.Where(x => x.IsActive).ToList();
+    // map to card VM including IsActive + BankAccountNumber
+}
+```
+
+- [ ] **Step 6: Add migration and run tests**
+
+Run: `dotnet ef migrations add AddAccountLifecycleFields --project src\Treasury.App\Treasury.App.csproj`
+Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests"`
+Expected: PASS.
+
+- [ ] **Step 7: Commit**
+
+```bash
+git add src/Treasury.App/Domain/Account.cs src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs src/Treasury.App/Pages/Accounts.razor src/Treasury.App/Pages/Accounts.razor.cs src/Treasury.App/Infrastructure/Migrations tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
+git commit -m "feat: add account lifecycle controls and bank account number"
+```
+
diff --git a/.superpowers/sdd/phase2-task-1-report.md b/.superpowers/sdd/phase2-task-1-report.md
new file mode 100644
index 0000000..2e6e246
--- /dev/null
+++ b/.superpowers/sdd/phase2-task-1-report.md
@@ -0,0 +1,42 @@
+# Phase 2 Task 1 Report
+
+## What changed
+- Added `IsActive` and `BankAccountNumber` to the account domain model.
+- Added lifecycle DTOs and endpoints for updating account details, toggling active state, and deleting accounts.
+- Updated `/api/accounts` to hide inactive accounts by default and support `?includeInactive=true`.
+- Added bank account number validation: optional, trimmed, digits-only, length 16..34.
+- Updated the Accounts page to support showing inactive accounts, displaying bank account numbers, and owner actions to deactivate/reactivate or remove accounts.
+- Added an EF Core migration for the new account columns.
+- Added focused lifecycle integration tests.
+
+## Tests run
+- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests"`
+  - Result: passed, 4/4 tests.
+- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsEndpointTests|FullyQualifiedName~AccountsSharingUiTests|FullyQualifiedName~SharedReadOnlyUiPermissionTests"`
+  - Result: passed, 8/8 tests.
+
+## Files changed
+- `src/Treasury.App/Domain/Account.cs`
+- `src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs`
+- `src/Treasury.App/Contracts/Accounts/AccountResponse.cs`
+- `src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs`
+- `src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs`
+- `src/Treasury.App/Application/Accounts/AccountLifecycleValidation.cs`
+- `src/Treasury.App/Application/Accounts/AccountSharingService.cs`
+- `src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs`
+- `src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs`
+- `src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs`
+- `src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs`
+- `src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs`
+- `src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs`
+- `src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.cs`
+- `src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.Designer.cs`
+- `src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs`
+- `src/Treasury.App/Pages/Accounts.razor`
+- `src/Treasury.App/Pages/Accounts.razor.cs`
+- `tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs`
+- `tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs`
+
+## Concerns
+- The Accounts page still uses server-side data access for owner actions, while the API endpoints provide the same lifecycle behavior for external callers.
+- No dedicated inline edit UI was added for account name changes; the update endpoint is in place for API use and future UI work.
diff --git a/.superpowers/sdd/phase2-task-1-rereview-package.md b/.superpowers/sdd/phase2-task-1-rereview-package.md
new file mode 100644
index 0000000..6817046
--- /dev/null
+++ b/.superpowers/sdd/phase2-task-1-rereview-package.md
@@ -0,0 +1,2181 @@
+# Review Package
+
+Base: f667d4a71e9a92476fb40eb36e878669fdad75c6
+Head: 2f4630667007b867a6f32dabab876803bc8cab80
+
+## Commits
+
+2f46306 fix(transactions/transfers): block inactive accounts from new writes; exclude inactive accounts in Transactions UI\n\n- Add inactive-account guard to CreateTransactionEndpoint and CreateTransferEndpoint\n- Update Transactions.razor to list only active accounts and handle no active accounts\n- Add integration tests asserting inactive accounts are rejected for transaction & transfer\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
+6e1a0b2 feat: add account lifecycle controls and bank number validation
+
+## Diff Stat
+
+ .superpowers/sdd/phase2-task-1-report.md           |  42 ++
+ .../Accounts/AccountLifecycleValidation.cs         |  25 +
+ .../Application/Accounts/AccountSharingService.cs  |   4 +
+ .../Contracts/Accounts/AccountResponse.cs          |   2 +
+ .../Contracts/Accounts/CreateAccountRequest.cs     |   1 +
+ .../Accounts/SetAccountActiveStateRequest.cs       |   6 +
+ .../Contracts/Accounts/UpdateAccountRequest.cs     |   7 +
+ src/Treasury.App/Domain/Account.cs                 |   2 +
+ .../Endpoints/Accounts/CreateAccountEndpoint.cs    |  11 +
+ .../Endpoints/Accounts/DeleteAccountEndpoint.cs    |  57 ++
+ .../Endpoints/Accounts/GetAccountsEndpoint.cs      |  12 +-
+ .../Accounts/SetAccountActiveStateEndpoint.cs      |  57 ++
+ .../Endpoints/Accounts/UpdateAccountEndpoint.cs    |  80 +++
+ .../Transactions/CreateTransactionEndpoint.cs      |   8 +
+ .../Endpoints/Transfers/CreateTransferEndpoint.cs  |  17 +
+ .../Infrastructure/Data/TreasuryDbContext.cs       |   8 +
+ ...903122916_AddAccountLifecycleFields.Designer.cs | 738 +++++++++++++++++++++
+ .../20260903122916_AddAccountLifecycleFields.cs    |  40 ++
+ .../Migrations/TreasuryDbContextModelSnapshot.cs   |   9 +
+ src/Treasury.App/Pages/Accounts.razor              |  25 +-
+ src/Treasury.App/Pages/Accounts.razor.cs           |  75 ++-
+ src/Treasury.App/Pages/Transactions.razor          |  21 +-
+ .../AccountsLifecycleTests.cs                      | 206 ++++++
+ .../AccountsSharingUiTests.cs                      |   9 +
+ 24 files changed, 1451 insertions(+), 11 deletions(-)
+
+## Full Diff (-U10)
+
+diff --git a/.superpowers/sdd/phase2-task-1-report.md b/.superpowers/sdd/phase2-task-1-report.md
+new file mode 100644
+index 0000000..2e6e246
+--- /dev/null
++++ b/.superpowers/sdd/phase2-task-1-report.md
+@@ -0,0 +1,42 @@
++# Phase 2 Task 1 Report
++
++## What changed
++- Added `IsActive` and `BankAccountNumber` to the account domain model.
++- Added lifecycle DTOs and endpoints for updating account details, toggling active state, and deleting accounts.
++- Updated `/api/accounts` to hide inactive accounts by default and support `?includeInactive=true`.
++- Added bank account number validation: optional, trimmed, digits-only, length 16..34.
++- Updated the Accounts page to support showing inactive accounts, displaying bank account numbers, and owner actions to deactivate/reactivate or remove accounts.
++- Added an EF Core migration for the new account columns.
++- Added focused lifecycle integration tests.
++
++## Tests run
++- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests"`
++  - Result: passed, 4/4 tests.
++- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsEndpointTests|FullyQualifiedName~AccountsSharingUiTests|FullyQualifiedName~SharedReadOnlyUiPermissionTests"`
++  - Result: passed, 8/8 tests.
++
++## Files changed
++- `src/Treasury.App/Domain/Account.cs`
++- `src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs`
++- `src/Treasury.App/Contracts/Accounts/AccountResponse.cs`
++- `src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs`
++- `src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs`
++- `src/Treasury.App/Application/Accounts/AccountLifecycleValidation.cs`
++- `src/Treasury.App/Application/Accounts/AccountSharingService.cs`
++- `src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs`
++- `src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs`
++- `src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs`
++- `src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs`
++- `src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs`
++- `src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs`
++- `src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.cs`
++- `src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.Designer.cs`
++- `src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs`
++- `src/Treasury.App/Pages/Accounts.razor`
++- `src/Treasury.App/Pages/Accounts.razor.cs`
++- `tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs`
++- `tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs`
++
++## Concerns
++- The Accounts page still uses server-side data access for owner actions, while the API endpoints provide the same lifecycle behavior for external callers.
++- No dedicated inline edit UI was added for account name changes; the update endpoint is in place for API use and future UI work.
+diff --git a/src/Treasury.App/Application/Accounts/AccountLifecycleValidation.cs b/src/Treasury.App/Application/Accounts/AccountLifecycleValidation.cs
+new file mode 100644
+index 0000000..9a85ee2
+--- /dev/null
++++ b/src/Treasury.App/Application/Accounts/AccountLifecycleValidation.cs
+@@ -0,0 +1,25 @@
++namespace Treasury.App.Application.Accounts;
++
++public static class AccountLifecycleValidation
++{
++    public static bool TryNormalizeOptionalBankAccountNumber(string? bankAccountNumber, out string? normalized, out string? errorMessage)
++    {
++        normalized = null;
++        errorMessage = null;
++
++        if (string.IsNullOrWhiteSpace(bankAccountNumber))
++        {
++            return true;
++        }
++
++        normalized = bankAccountNumber.Trim();
++        if (normalized.Length is < 16 or > 34 || normalized.Any(ch => !char.IsDigit(ch)))
++        {
++            errorMessage = "Bank account number must contain only digits and be 16 to 34 characters long.";
++            normalized = null;
++            return false;
++        }
++
++        return true;
++    }
++}
+diff --git a/src/Treasury.App/Application/Accounts/AccountSharingService.cs b/src/Treasury.App/Application/Accounts/AccountSharingService.cs
+index 4f81e0a..44e7872 100644
+--- a/src/Treasury.App/Application/Accounts/AccountSharingService.cs
++++ b/src/Treasury.App/Application/Accounts/AccountSharingService.cs
+@@ -6,23 +6,27 @@ using Treasury.App.Infrastructure.Data;
+ namespace Treasury.App.Application.Accounts;
+ 
+ public sealed record HouseholdUserChoice(string Email, string DisplayName);
+ public sealed record AccountViewerChoice(string ViewerUserId, string Email, string DisplayName);
+ public sealed record AccountViewerAssignment(Guid AccountId, string ViewerUserId, string Email, string DisplayName);
+ public sealed record ShareReadOnlyResult(bool Succeeded, string? ErrorMessage);
+ 
+ public class AccountSharingService(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+ {
+     public virtual Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct) =>
++        GetVisibleAccountsAsync(user, includeInactive: false, ct);
++
++    public virtual Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, bool includeInactive, CancellationToken ct) =>
+         db.Accounts
+             .Where(x =>
+                 x.HouseholdId == user.HouseholdId
++                && (includeInactive || x.IsActive)
+                 && (x.OwnerUserId == user.Id
+                     || x.OwnerUserId == "seed"
+                     || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
+             .OrderBy(x => x.Name)
+             .ToListAsync(ct);
+ 
+     public virtual Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
+         db.Users
+             .Where(x => x.HouseholdId == user.HouseholdId && x.Id != user.Id)
+             .OrderBy(x => x.Email ?? x.UserName ?? x.Id)
+diff --git a/src/Treasury.App/Contracts/Accounts/AccountResponse.cs b/src/Treasury.App/Contracts/Accounts/AccountResponse.cs
+index ad202f7..2fd0ea4 100644
+--- a/src/Treasury.App/Contracts/Accounts/AccountResponse.cs
++++ b/src/Treasury.App/Contracts/Accounts/AccountResponse.cs
+@@ -1,11 +1,13 @@
+ namespace Treasury.App.Contracts.Accounts;
+ 
+ public sealed class AccountResponse
+ {
+     public Guid Id { get; set; }
+     public string Name { get; set; } = string.Empty;
+     public string Currency { get; set; } = string.Empty;
+     public string AccountType { get; set; } = string.Empty;
++    public bool IsActive { get; set; }
++    public string? BankAccountNumber { get; set; }
+     public decimal CurrentBalance { get; set; }
+     public bool IsReadOnly { get; set; }
+ }
+diff --git a/src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs b/src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs
+index c8659fa..e26a149 100644
+--- a/src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs
++++ b/src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs
+@@ -1,8 +1,9 @@
+ namespace Treasury.App.Contracts.Accounts;
+ 
+ public sealed class CreateAccountRequest
+ {
+     public string Name { get; set; } = string.Empty;
+     public string Currency { get; set; } = "PLN";
+     public string AccountType { get; set; } = "cash-wallet";
++    public string? BankAccountNumber { get; set; }
+ }
+diff --git a/src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs b/src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs
+new file mode 100644
+index 0000000..77d1ee7
+--- /dev/null
++++ b/src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs
+@@ -0,0 +1,6 @@
++namespace Treasury.App.Contracts.Accounts;
++
++public sealed class SetAccountActiveStateRequest
++{
++    public bool IsActive { get; set; }
++}
+diff --git a/src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs b/src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs
+new file mode 100644
+index 0000000..791d7d7
+--- /dev/null
++++ b/src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs
+@@ -0,0 +1,7 @@
++namespace Treasury.App.Contracts.Accounts;
++
++public sealed class UpdateAccountRequest
++{
++    public string Name { get; set; } = string.Empty;
++    public string? BankAccountNumber { get; set; }
++}
+diff --git a/src/Treasury.App/Domain/Account.cs b/src/Treasury.App/Domain/Account.cs
+index 83dde01..6e9a7d4 100644
+--- a/src/Treasury.App/Domain/Account.cs
++++ b/src/Treasury.App/Domain/Account.cs
+@@ -1,16 +1,18 @@
+ namespace Treasury.App.Domain;
+ 
+ public class Account
+ {
+     public Guid Id { get; set; } = Guid.NewGuid();
+     public Guid HouseholdId { get; set; }
+     public string OwnerUserId { get; set; } = string.Empty;
+     public string Name { get; set; } = string.Empty;
+     public string Currency { get; set; } = "PLN";
+     public string AccountType { get; set; } = "cash-wallet";
++    public bool IsActive { get; set; } = true;
++    public string? BankAccountNumber { get; set; }
+     public decimal CurrentBalance { get; set; }
+     public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
+     public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
+ 
+     public ICollection<VisibilityRule> VisibilityRules { get; set; } = new List<VisibilityRule>();
+ }
+diff --git a/src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs
+index 543cf2e..16e2d62 100644
+--- a/src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs
++++ b/src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs
+@@ -1,13 +1,14 @@
+ using FastEndpoints;
+ using Microsoft.AspNetCore.Identity;
+ using Microsoft.EntityFrameworkCore;
++using Treasury.App.Application.Accounts;
+ using Treasury.App.Contracts.Accounts;
+ using Treasury.App.Domain;
+ using Treasury.App.Infrastructure.Data;
+ 
+ namespace Treasury.App.Endpoints.Accounts;
+ 
+ public sealed class CreateAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+     : Endpoint<CreateAccountRequest>
+ {
+     public override void Configure()
+@@ -34,36 +35,46 @@ public sealed class CreateAccountEndpoint(TreasuryDbContext db, UserManager<Appl
+ 
+         var accountType = string.IsNullOrWhiteSpace(request.AccountType) ? "cash-wallet" : request.AccountType.Trim().ToLowerInvariant();
+         var isKnownType = await db.AccountTypes.AnyAsync(x => x.HouseholdId == user.HouseholdId && x.Name == accountType, ct);
+         if (!isKnownType)
+         {
+             AddError(x => x.AccountType, "Unknown account type for this household.");
+             await SendErrorsAsync(cancellation: ct);
+             return;
+         }
+ 
++        if (!AccountLifecycleValidation.TryNormalizeOptionalBankAccountNumber(request.BankAccountNumber, out var bankAccountNumber, out var bankAccountNumberError))
++        {
++            AddError(x => x.BankAccountNumber, bankAccountNumberError!);
++            await SendErrorsAsync(cancellation: ct);
++            return;
++        }
++
+         var account = new Account
+         {
+             HouseholdId = user.HouseholdId,
+             OwnerUserId = user.Id,
+             Name = request.Name.Trim(),
+             Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
+             AccountType = accountType,
++            BankAccountNumber = bankAccountNumber,
+             CurrentBalance = 0m
+         };
+ 
+         db.Accounts.Add(account);
+         await db.SaveChangesAsync(ct);
+ 
+         var response = new AccountResponse
+         {
+             Id = account.Id,
+             Name = account.Name,
+             Currency = account.Currency,
+             AccountType = account.AccountType,
++            IsActive = account.IsActive,
++            BankAccountNumber = account.BankAccountNumber,
+             CurrentBalance = account.CurrentBalance,
+             IsReadOnly = false
+         };
+ 
+         await SendAsync(response, StatusCodes.Status201Created, ct);
+     }
+ }
+diff --git a/src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs
+new file mode 100644
+index 0000000..277cf58
+--- /dev/null
++++ b/src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs
+@@ -0,0 +1,57 @@
++using FastEndpoints;
++using Microsoft.AspNetCore.Identity;
++using Microsoft.EntityFrameworkCore;
++using Treasury.App.Domain;
++using Treasury.App.Infrastructure.Data;
++
++namespace Treasury.App.Endpoints.Accounts;
++
++public sealed class DeleteAccountRouteRequest
++{
++    public Guid Id { get; set; }
++}
++
++public sealed class DeleteAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
++    : Endpoint<DeleteAccountRouteRequest>
++{
++    public override void Configure()
++    {
++        Delete("/api/accounts/{id:guid}");
++        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
++    }
++
++    public override async Task HandleAsync(DeleteAccountRouteRequest request, CancellationToken ct)
++    {
++        var user = await userManager.GetUserAsync(User);
++        if (user is null)
++        {
++            await SendUnauthorizedAsync(ct);
++            return;
++        }
++
++        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
++        if (account is null)
++        {
++            await SendNotFoundAsync(ct);
++            return;
++        }
++
++        if (account.OwnerUserId != user.Id)
++        {
++            await SendForbiddenAsync(ct);
++            return;
++        }
++
++        var hasTransactions = await db.Transactions.AnyAsync(x => x.AccountId == account.Id, ct);
++        if (hasTransactions && account.CurrentBalance != 0m)
++        {
++            AddError("Cannot remove account with transaction history and non-zero balance. Deactivate it instead.");
++            await SendErrorsAsync(cancellation: ct);
++            return;
++        }
++
++        db.Accounts.Remove(account);
++        await db.SaveChangesAsync(ct);
++        await SendNoContentAsync(ct);
++    }
++}
+diff --git a/src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs
+index 46eb0e1..f773fa5 100644
+--- a/src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs
++++ b/src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs
+@@ -1,47 +1,55 @@
+ using FastEndpoints;
+ using Microsoft.AspNetCore.Identity;
+ using Microsoft.EntityFrameworkCore;
+ using Treasury.App.Contracts.Accounts;
+ using Treasury.App.Domain;
+ using Treasury.App.Infrastructure.Data;
+ 
+ namespace Treasury.App.Endpoints.Accounts;
+ 
+-public sealed class GetAccountsEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : EndpointWithoutRequest
++public sealed class GetAccountsRequest
++{
++    public bool IncludeInactive { get; set; }
++}
++
++public sealed class GetAccountsEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : Endpoint<GetAccountsRequest>
+ {
+     public override void Configure()
+     {
+         Get("/api/accounts");
+         Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
+     }
+ 
+-    public override async Task HandleAsync(CancellationToken ct)
++    public override async Task HandleAsync(GetAccountsRequest request, CancellationToken ct)
+     {
+         var user = await userManager.GetUserAsync(User);
+         if (user is null)
+         {
+             await SendUnauthorizedAsync(ct);
+             return;
+         }
+ 
+         var accounts = await db.Accounts
+             .Where(x =>
+                 x.HouseholdId == user.HouseholdId
++                && (request.IncludeInactive || x.IsActive)
+                 && (x.OwnerUserId == user.Id
+                     || x.OwnerUserId == "seed"
+                     || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
+             .OrderBy(x => x.Name)
+             .Select(x => new AccountResponse
+             {
+                 Id = x.Id,
+                 Name = x.Name,
+                 Currency = x.Currency,
+                 AccountType = x.AccountType,
++                IsActive = x.IsActive,
++                BankAccountNumber = x.BankAccountNumber,
+                 CurrentBalance = x.CurrentBalance,
+                 IsReadOnly = x.OwnerUserId != user.Id
+             })
+             .ToListAsync(ct);
+ 
+         await SendOkAsync(accounts, ct);
+     }
+ }
+diff --git a/src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs
+new file mode 100644
+index 0000000..1ec69d0
+--- /dev/null
++++ b/src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs
+@@ -0,0 +1,57 @@
++using FastEndpoints;
++using Microsoft.AspNetCore.Identity;
++using Microsoft.EntityFrameworkCore;
++using Treasury.App.Contracts.Accounts;
++using Treasury.App.Domain;
++using Treasury.App.Infrastructure.Data;
++
++namespace Treasury.App.Endpoints.Accounts;
++
++public sealed class SetAccountActiveStateRouteRequest
++{
++    public Guid Id { get; set; }
++    public bool IsActive { get; set; }
++}
++
++public sealed class SetAccountActiveStateEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
++    : Endpoint<SetAccountActiveStateRouteRequest>
++{
++    public override void Configure()
++    {
++        Put("/api/accounts/{id:guid}/active-state");
++        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
++    }
++
++    public override async Task HandleAsync(SetAccountActiveStateRouteRequest request, CancellationToken ct)
++    {
++        var user = await userManager.GetUserAsync(User);
++        if (user is null)
++        {
++            await SendUnauthorizedAsync(ct);
++            return;
++        }
++
++        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
++        if (account is null)
++        {
++            await SendNotFoundAsync(ct);
++            return;
++        }
++
++        if (account.OwnerUserId != user.Id)
++        {
++            await SendForbiddenAsync(ct);
++            return;
++        }
++
++        account.IsActive = request.IsActive;
++        account.UpdatedAt = DateTime.UtcNow;
++        await db.SaveChangesAsync(ct);
++
++        await SendOkAsync(new
++        {
++            account.Id,
++            account.IsActive
++        }, ct);
++    }
++}
+diff --git a/src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs
+new file mode 100644
+index 0000000..c6175d0
+--- /dev/null
++++ b/src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs
+@@ -0,0 +1,80 @@
++using FastEndpoints;
++using Microsoft.AspNetCore.Identity;
++using Microsoft.EntityFrameworkCore;
++using Treasury.App.Application.Accounts;
++using Treasury.App.Domain;
++using Treasury.App.Infrastructure.Data;
++
++namespace Treasury.App.Endpoints.Accounts;
++
++public sealed class UpdateAccountRouteRequest
++{
++    public Guid Id { get; set; }
++    public string Name { get; set; } = string.Empty;
++    public string? BankAccountNumber { get; set; }
++}
++
++public sealed class UpdateAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
++    : Endpoint<UpdateAccountRouteRequest>
++{
++    public override void Configure()
++    {
++        Put("/api/accounts/{id:guid}");
++        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
++    }
++
++    public override async Task HandleAsync(UpdateAccountRouteRequest request, CancellationToken ct)
++    {
++        var user = await userManager.GetUserAsync(User);
++        if (user is null)
++        {
++            await SendUnauthorizedAsync(ct);
++            return;
++        }
++
++        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
++        if (account is null)
++        {
++            await SendNotFoundAsync(ct);
++            return;
++        }
++
++        if (account.OwnerUserId != user.Id)
++        {
++            await SendForbiddenAsync(ct);
++            return;
++        }
++
++        var hasErrors = false;
++        if (string.IsNullOrWhiteSpace(request.Name))
++        {
++            AddError(x => x.Name, "Account name is required.");
++            hasErrors = true;
++        }
++
++        if (!AccountLifecycleValidation.TryNormalizeOptionalBankAccountNumber(request.BankAccountNumber, out var bankAccountNumber, out var bankAccountNumberError))
++        {
++            AddError(x => x.BankAccountNumber, bankAccountNumberError!);
++            hasErrors = true;
++        }
++
++        if (hasErrors)
++        {
++            await SendErrorsAsync(cancellation: ct);
++            return;
++        }
++
++        account.Name = request.Name.Trim();
++        account.BankAccountNumber = bankAccountNumber;
++        account.UpdatedAt = DateTime.UtcNow;
++
++        await db.SaveChangesAsync(ct);
++
++        await SendOkAsync(new
++        {
++            account.Id,
++            account.Name,
++            account.BankAccountNumber
++        }, ct);
++    }
++}
+diff --git a/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs b/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs
+index 961f8f9..12cdc21 100644
+--- a/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs
++++ b/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs
+@@ -47,20 +47,28 @@ public sealed class CreateTransactionEndpoint(TreasuryDbContext db, UserManager<
+             await SendNotFoundAsync(ct);
+             return;
+         }
+ 
+         if (account.OwnerUserId != user.Id)
+         {
+             await SendForbiddenAsync(ct);
+             return;
+         }
+ 
++        // Block creating transactions on inactive accounts
++        if (!account.IsActive)
++        {
++            AddError(x => x.AccountId, "Cannot create transactions on an inactive account.");
++            await SendErrorsAsync(cancellation: ct);
++            return;
++        }
++
+         var normalizedType = (request.Type ?? "expense").Trim().ToLowerInvariant();
+         var delta = request.Amount;
+         if (normalizedType == "expense")
+         {
+             delta = -Math.Abs(request.Amount);
+         }
+         else if (normalizedType == "income")
+         {
+             delta = Math.Abs(request.Amount);
+         }
+diff --git a/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs b/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs
+index b33048a..dca5eff 100644
+--- a/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs
++++ b/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs
+@@ -53,20 +53,37 @@ public sealed class CreateTransferEndpoint(TreasuryDbContext db, UserManager<App
+             await SendNotFoundAsync(ct);
+             return;
+         }
+ 
+         if (fromAccount.OwnerUserId != user.Id || toAccount.OwnerUserId != user.Id)
+         {
+             await SendForbiddenAsync(ct);
+             return;
+         }
+ 
++        // Block transfers involving inactive accounts
++        if (!fromAccount.IsActive || !toAccount.IsActive)
++        {
++            if (!fromAccount.IsActive)
++            {
++                AddError(x => x.FromAccountId, "Source account is inactive.");
++            }
++
++            if (!toAccount.IsActive)
++            {
++                AddError(x => x.ToAccountId, "Destination account is inactive.");
++            }
++
++            await SendErrorsAsync(cancellation: ct);
++            return;
++        }
++
+         var transferDate = request.TransferDate == default ? DateTime.UtcNow : request.TransferDate;
+         var currency = string.IsNullOrWhiteSpace(request.Currency) ? fromAccount.Currency : request.Currency.Trim().ToUpperInvariant();
+         var description = string.IsNullOrWhiteSpace(request.Description) ? "Account transfer" : request.Description.Trim();
+ 
+         var transfer = new Transfer
+         {
+             HouseholdId = user.HouseholdId,
+             FromAccountId = fromAccount.Id,
+             ToAccountId = toAccount.Id,
+             Amount = request.Amount,
+diff --git a/src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs b/src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs
+index 36de716..20a1ec0 100644
+--- a/src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs
++++ b/src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs
+@@ -46,20 +46,28 @@ public class TreasuryDbContext : IdentityDbContext<ApplicationUser>
+ 
+         modelBuilder.Entity<TransactionTag>()
+             .HasOne(x => x.Transaction)
+             .WithMany(x => x.TransactionTags)
+             .HasForeignKey(x => x.TransactionId);
+ 
+         modelBuilder.Entity<TransactionTag>()
+             .HasOne(x => x.Tag)
+             .WithMany(x => x.TransactionTags)
+             .HasForeignKey(x => x.TagId);
++
++        modelBuilder.Entity<Account>()
++            .Property(x => x.IsActive)
++            .HasDefaultValue(true);
++
++        modelBuilder.Entity<Account>()
++            .Property(x => x.BankAccountNumber)
++            .HasMaxLength(34);
+     }
+ 
+     public DbSet<Household> Households => Set<Household>();
+     public DbSet<AccountTypeDefinition> AccountTypes => Set<AccountTypeDefinition>();
+     public DbSet<Account> Accounts => Set<Account>();
+     public DbSet<VisibilityRule> VisibilityRules => Set<VisibilityRule>();
+     public DbSet<Transfer> Transfers => Set<Transfer>();
+     public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();
+     public DbSet<AssetValuation> AssetValuations => Set<AssetValuation>();
+     public DbSet<Transaction> Transactions => Set<Transaction>();
+diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.Designer.cs b/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.Designer.cs
+new file mode 100644
+index 0000000..5d8ec06
+--- /dev/null
++++ b/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.Designer.cs
+@@ -0,0 +1,738 @@
++﻿// <auto-generated />
++using System;
++using Microsoft.EntityFrameworkCore;
++using Microsoft.EntityFrameworkCore.Infrastructure;
++using Microsoft.EntityFrameworkCore.Migrations;
++using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
++using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
++using Treasury.App.Infrastructure.Data;
++
++#nullable disable
++
++namespace Treasury.App.Infrastructure.Migrations
++{
++    [DbContext(typeof(TreasuryDbContext))]
++    [Migration("20260903122916_AddAccountLifecycleFields")]
++    partial class AddAccountLifecycleFields
++    {
++        /// <inheritdoc />
++        protected override void BuildTargetModel(ModelBuilder modelBuilder)
++        {
++#pragma warning disable 612, 618
++            modelBuilder
++                .HasAnnotation("ProductVersion", "9.0.0")
++                .HasAnnotation("Relational:MaxIdentifierLength", 63);
++
++            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
++                {
++                    b.Property<string>("Id")
++                        .HasColumnType("text");
++
++                    b.Property<string>("ConcurrencyStamp")
++                        .IsConcurrencyToken()
++                        .HasColumnType("text");
++
++                    b.Property<string>("Name")
++                        .HasMaxLength(256)
++                        .HasColumnType("character varying(256)");
++
++                    b.Property<string>("NormalizedName")
++                        .HasMaxLength(256)
++                        .HasColumnType("character varying(256)");
++
++                    b.HasKey("Id");
++
++                    b.HasIndex("NormalizedName")
++                        .IsUnique()
++                        .HasDatabaseName("RoleNameIndex");
++
++                    b.ToTable("AspNetRoles", (string)null);
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
++                {
++                    b.Property<int>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("integer");
++
++                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));
++
++                    b.Property<string>("ClaimType")
++                        .HasColumnType("text");
++
++                    b.Property<string>("ClaimValue")
++                        .HasColumnType("text");
++
++                    b.Property<string>("RoleId")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.HasKey("Id");
++
++                    b.HasIndex("RoleId");
++
++                    b.ToTable("AspNetRoleClaims", (string)null);
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
++                {
++                    b.Property<int>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("integer");
++
++                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));
++
++                    b.Property<string>("ClaimType")
++                        .HasColumnType("text");
++
++                    b.Property<string>("ClaimValue")
++                        .HasColumnType("text");
++
++                    b.Property<string>("UserId")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.HasKey("Id");
++
++                    b.HasIndex("UserId");
++
++                    b.ToTable("AspNetUserClaims", (string)null);
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
++                {
++                    b.Property<string>("LoginProvider")
++                        .HasColumnType("text");
++
++                    b.Property<string>("ProviderKey")
++                        .HasColumnType("text");
++
++                    b.Property<string>("ProviderDisplayName")
++                        .HasColumnType("text");
++
++                    b.Property<string>("UserId")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.HasKey("LoginProvider", "ProviderKey");
++
++                    b.HasIndex("UserId");
++
++                    b.ToTable("AspNetUserLogins", (string)null);
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
++                {
++                    b.Property<string>("UserId")
++                        .HasColumnType("text");
++
++                    b.Property<string>("RoleId")
++                        .HasColumnType("text");
++
++                    b.HasKey("UserId", "RoleId");
++
++                    b.HasIndex("RoleId");
++
++                    b.ToTable("AspNetUserRoles", (string)null);
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
++                {
++                    b.Property<string>("UserId")
++                        .HasColumnType("text");
++
++                    b.Property<string>("LoginProvider")
++                        .HasColumnType("text");
++
++                    b.Property<string>("Name")
++                        .HasColumnType("text");
++
++                    b.Property<string>("Value")
++                        .HasColumnType("text");
++
++                    b.HasKey("UserId", "LoginProvider", "Name");
++
++                    b.ToTable("AspNetUserTokens", (string)null);
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Account", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<string>("AccountType")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<string>("BankAccountNumber")
++                        .HasMaxLength(34)
++                        .HasColumnType("character varying(34)");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Currency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<decimal>("CurrentBalance")
++                        .HasColumnType("numeric");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<bool>("IsActive")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("boolean")
++                        .HasDefaultValue(true);
++
++                    b.Property<string>("Name")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<string>("OwnerUserId")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("Accounts");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.AccountTypeDefinition", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Description")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<string>("Name")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("AccountTypes");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.ApplicationUser", b =>
++                {
++                    b.Property<string>("Id")
++                        .HasColumnType("text");
++
++                    b.Property<int>("AccessFailedCount")
++                        .HasColumnType("integer");
++
++                    b.Property<string>("ConcurrencyStamp")
++                        .IsConcurrencyToken()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Email")
++                        .HasMaxLength(256)
++                        .HasColumnType("character varying(256)");
++
++                    b.Property<bool>("EmailConfirmed")
++                        .HasColumnType("boolean");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<bool>("LockoutEnabled")
++                        .HasColumnType("boolean");
++
++                    b.Property<DateTimeOffset?>("LockoutEnd")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("NormalizedEmail")
++                        .HasMaxLength(256)
++                        .HasColumnType("character varying(256)");
++
++                    b.Property<string>("NormalizedUserName")
++                        .HasMaxLength(256)
++                        .HasColumnType("character varying(256)");
++
++                    b.Property<string>("PasswordHash")
++                        .HasColumnType("text");
++
++                    b.Property<string>("PhoneNumber")
++                        .HasColumnType("text");
++
++                    b.Property<bool>("PhoneNumberConfirmed")
++                        .HasColumnType("boolean");
++
++                    b.Property<string>("SecurityStamp")
++                        .HasColumnType("text");
++
++                    b.Property<bool>("TwoFactorEnabled")
++                        .HasColumnType("boolean");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("UserName")
++                        .HasMaxLength(256)
++                        .HasColumnType("character varying(256)");
++
++                    b.HasKey("Id");
++
++                    b.HasIndex("NormalizedEmail")
++                        .HasDatabaseName("EmailIndex");
++
++                    b.HasIndex("NormalizedUserName")
++                        .IsUnique()
++                        .HasDatabaseName("UserNameIndex");
++
++                    b.ToTable("AspNetUsers", (string)null);
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.AssetValuation", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<string>("AssetName")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Currency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<decimal>("CurrentTotalValue")
++                        .HasColumnType("numeric");
++
++                    b.Property<decimal>("CurrentUnitValue")
++                        .HasColumnType("numeric");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<int>("Kind")
++                        .HasColumnType("integer");
++
++                    b.Property<decimal>("Purity")
++                        .HasColumnType("numeric");
++
++                    b.Property<decimal>("Quantity")
++                        .HasColumnType("numeric");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<DateTime>("ValuationDate")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<decimal>("Weight")
++                        .HasColumnType("numeric");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("AssetValuations");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Bill", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<decimal>("Amount")
++                        .HasColumnType("numeric");
++
++                    b.Property<string>("Category")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Currency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<int>("DueDay")
++                        .HasColumnType("integer");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<bool>("IsPaid")
++                        .HasColumnType("boolean");
++
++                    b.Property<string>("Name")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<string>("Notes")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("Bills");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.BudgetCategory", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Currency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<decimal>("MonthlyLimit")
++                        .HasColumnType("numeric");
++
++                    b.Property<string>("Name")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<string>("Notes")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("BudgetCategories");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.CurrencyRate", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<DateTime>("EffectiveAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("FromCurrency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<decimal>("Rate")
++                        .HasColumnType("numeric");
++
++                    b.Property<string>("ToCurrency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.HasIndex("HouseholdId", "FromCurrency", "ToCurrency")
++                        .IsUnique();
++
++                    b.ToTable("CurrencyRates");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Household", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Name")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("Households");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Tag", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<string>("Color")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<string>("Name")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("Tags");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Transaction", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<Guid>("AccountId")
++                        .HasColumnType("uuid");
++
++                    b.Property<decimal>("Amount")
++                        .HasColumnType("numeric");
++
++                    b.Property<string>("Category")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Currency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<string>("Description")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<DateTime>("TransactionDate")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Type")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("Transactions");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.TransactionTag", b =>
++                {
++                    b.Property<Guid>("TransactionId")
++                        .HasColumnType("uuid");
++
++                    b.Property<Guid>("TagId")
++                        .HasColumnType("uuid");
++
++                    b.HasKey("TransactionId", "TagId");
++
++                    b.HasIndex("TagId");
++
++                    b.ToTable("TransactionTags");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Transfer", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<decimal>("Amount")
++                        .HasColumnType("numeric");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Currency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<string>("Description")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<Guid>("FromAccountId")
++                        .HasColumnType("uuid");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<Guid>("ToAccountId")
++                        .HasColumnType("uuid");
++
++                    b.Property<DateTime>("TransferDate")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("Transfers");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.VisibilityRule", b =>
++                {
++                    b.Property<Guid>("AccountId")
++                        .HasColumnType("uuid");
++
++                    b.Property<string>("ViewerUserId")
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<bool>("IsReadOnly")
++                        .HasColumnType("boolean");
++
++                    b.HasKey("AccountId", "ViewerUserId");
++
++                    b.ToTable("VisibilityRules");
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
++                {
++                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
++                        .WithMany()
++                        .HasForeignKey("RoleId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
++                {
++                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
++                        .WithMany()
++                        .HasForeignKey("UserId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
++                {
++                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
++                        .WithMany()
++                        .HasForeignKey("UserId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
++                {
++                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
++                        .WithMany()
++                        .HasForeignKey("RoleId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++
++                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
++                        .WithMany()
++                        .HasForeignKey("UserId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
++                {
++                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
++                        .WithMany()
++                        .HasForeignKey("UserId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.TransactionTag", b =>
++                {
++                    b.HasOne("Treasury.App.Domain.Tag", "Tag")
++                        .WithMany("TransactionTags")
++                        .HasForeignKey("TagId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++
++                    b.HasOne("Treasury.App.Domain.Transaction", "Transaction")
++                        .WithMany("TransactionTags")
++                        .HasForeignKey("TransactionId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++
++                    b.Navigation("Tag");
++
++                    b.Navigation("Transaction");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.VisibilityRule", b =>
++                {
++                    b.HasOne("Treasury.App.Domain.Account", "Account")
++                        .WithMany("VisibilityRules")
++                        .HasForeignKey("AccountId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++
++                    b.Navigation("Account");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Account", b =>
++                {
++                    b.Navigation("VisibilityRules");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Tag", b =>
++                {
++                    b.Navigation("TransactionTags");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Transaction", b =>
++                {
++                    b.Navigation("TransactionTags");
++                });
++#pragma warning restore 612, 618
++        }
++    }
++}
+diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.cs b/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.cs
+new file mode 100644
+index 0000000..98fe170
+--- /dev/null
++++ b/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.cs
+@@ -0,0 +1,40 @@
++﻿using Microsoft.EntityFrameworkCore.Migrations;
++
++#nullable disable
++
++namespace Treasury.App.Infrastructure.Migrations
++{
++    /// <inheritdoc />
++    public partial class AddAccountLifecycleFields : Migration
++    {
++        /// <inheritdoc />
++        protected override void Up(MigrationBuilder migrationBuilder)
++        {
++            migrationBuilder.AddColumn<string>(
++                name: "BankAccountNumber",
++                table: "Accounts",
++                type: "character varying(34)",
++                maxLength: 34,
++                nullable: true);
++
++            migrationBuilder.AddColumn<bool>(
++                name: "IsActive",
++                table: "Accounts",
++                type: "boolean",
++                nullable: false,
++                defaultValue: true);
++        }
++
++        /// <inheritdoc />
++        protected override void Down(MigrationBuilder migrationBuilder)
++        {
++            migrationBuilder.DropColumn(
++                name: "BankAccountNumber",
++                table: "Accounts");
++
++            migrationBuilder.DropColumn(
++                name: "IsActive",
++                table: "Accounts");
++        }
++    }
++}
+diff --git a/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs b/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
+index 2037f1f..a9b083a 100644
+--- a/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
++++ b/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
+@@ -157,33 +157,42 @@ namespace Treasury.App.Infrastructure.Migrations
+             modelBuilder.Entity("Treasury.App.Domain.Account", b =>
+                 {
+                     b.Property<Guid>("Id")
+                         .ValueGeneratedOnAdd()
+                         .HasColumnType("uuid");
+ 
+                     b.Property<string>("AccountType")
+                         .IsRequired()
+                         .HasColumnType("text");
+ 
++                    b.Property<string>("BankAccountNumber")
++                        .HasMaxLength(34)
++                        .HasColumnType("character varying(34)");
++
+                     b.Property<DateTime>("CreatedAt")
+                         .HasColumnType("timestamp with time zone");
+ 
+                     b.Property<string>("Currency")
+                         .IsRequired()
+                         .HasColumnType("text");
+ 
+                     b.Property<decimal>("CurrentBalance")
+                         .HasColumnType("numeric");
+ 
+                     b.Property<Guid>("HouseholdId")
+                         .HasColumnType("uuid");
+ 
++                    b.Property<bool>("IsActive")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("boolean")
++                        .HasDefaultValue(true);
++
+                     b.Property<string>("Name")
+                         .IsRequired()
+                         .HasColumnType("text");
+ 
+                     b.Property<string>("OwnerUserId")
+                         .IsRequired()
+                         .HasColumnType("text");
+ 
+                     b.Property<DateTime>("UpdatedAt")
+                         .HasColumnType("timestamp with time zone");
+diff --git a/src/Treasury.App/Pages/Accounts.razor b/src/Treasury.App/Pages/Accounts.razor
+index f6e1603..5cf5477 100644
+--- a/src/Treasury.App/Pages/Accounts.razor
++++ b/src/Treasury.App/Pages/Accounts.razor
+@@ -1,37 +1,43 @@
+ @page "/accounts"
+ @attribute [Authorize]
+ 
+ <MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="pa-4 pa-sm-6">
+     <MudStack Spacing="3">
+         <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
+             <MudText Typo="Typo.h4">Accounts</MudText>
+-            <MudButton Variant="Variant.Filled" Color="Color.Tertiary" StartIcon="@Icons.Material.Filled.Add" OnClick="ToggleCreateForm">Add account</MudButton>
++            <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
++                <MudSwitch T="bool" Label="Show inactive" checked="_showInactive" checkedChanged="OnShowInactiveChanged" />
++                <MudButton Variant="Variant.Filled" Color="Color.Tertiary" StartIcon="@Icons.Material.Filled.Add" OnClick="ToggleCreateForm">Add account</MudButton>
++            </MudStack>
+         </MudStack>
+ 
+         @if (!string.IsNullOrWhiteSpace(_accountsLoadError))
+         {
+             <MudAlert Severity="Severity.Error">@_accountsLoadError</MudAlert>
+         }
+ 
+         @if (!string.IsNullOrWhiteSpace(_householdUsersLoadError))
+         {
+             <MudAlert Severity="Severity.Error">@_householdUsersLoadError</MudAlert>
+         }
+ 
+         @if (_showCreateForm)
+         {
+             <MudPaper Class="pa-4 rounded-xl" Elevation="2">
+                 <MudGrid>
+                     <MudItem xs="12" md="4">
+                         <MudTextField Label="Account name" @bind-Value="_newAccount.Name" Required="true" />
+                     </MudItem>
++                    <MudItem xs="12" md="4">
++                        <MudTextField Label="Bank account number (optional)" @bind-Value="_newAccount.BankAccountNumber" />
++                    </MudItem>
+                     <MudItem xs="12" md="3">
+                         <MudSelect T="string" Label="Currency" @bind-Value="_newAccount.Currency">
+                             <MudSelectItem Value="@("PLN")">PLN</MudSelectItem>
+                             <MudSelectItem Value="@("EUR")">EUR</MudSelectItem>
+                             <MudSelectItem Value="@("USD")">USD</MudSelectItem>
+                             <MudSelectItem Value="@("GBP")">GBP</MudSelectItem>
+                         </MudSelect>
+                     </MudItem>
+                     <MudItem xs="12" md="3">
+                         <MudSelect T="string" Label="Type" @bind-Value="_newAccount.AccountType">
+@@ -59,25 +65,42 @@
+         }
+         else
+         {
+             <MudGrid>
+                 @foreach (var account in _accounts)
+                 {
+                     <MudItem xs="12" md="6" lg="4">
+                         <MudPaper Class="pa-4 rounded-xl" Elevation="2">
+                             <MudText Typo="Typo.h6">@account.Name</MudText>
+                             <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mt-1">@account.AccountType</MudText>
++                            @if (!string.IsNullOrWhiteSpace(account.BankAccountNumber))
++                            {
++                                <MudText Typo="Typo.body2" Color="Color.Secondary">Bank account: @account.BankAccountNumber</MudText>
++                            }
++                            @if (!account.IsActive)
++                            {
++                                <MudChip Class="mt-2" T="string" Color="Color.Warning" Variant="Variant.Outlined">Inactive</MudChip>
++                            }
+                             <MudDivider Class="my-3" />
+                             <MudText Typo="Typo.h5">@account.CurrentBalance.ToString("N2") @account.Currency</MudText>
+ 
+                             @if (account.IsOwner)
+                             {
++                                <MudStack Row="true" Class="mt-4" Spacing="2" Wrap="Wrap.Wrap">
++                                    <MudButton Variant="Variant.Outlined" OnClick="() => ToggleActiveStateAsync(account)">
++                                        @(account.IsActive ? "Deactivate" : "Reactivate")
++                                    </MudButton>
++                                    <MudButton Color="Color.Error" Variant="Variant.Outlined" OnClick="() => DeleteAccountAsync(account)">
++                                        Remove
++                                    </MudButton>
++                                </MudStack>
++
+                                 <MudDivider Class="my-3" />
+                                 <MudText Typo="Typo.subtitle2">Shared with:</MudText>
+                                 @if (account.SharedWithLoadFailed)
+                                 {
+                                     <MudText Typo="Typo.body2" Color="Color.Secondary">Shared viewer details unavailable.</MudText>
+                                 }
+                                 else if (account.SharedWith.Count == 0)
+                                 {
+                                     <MudText Typo="Typo.body2" Color="Color.Secondary">Nobody yet.</MudText>
+                                 }
+diff --git a/src/Treasury.App/Pages/Accounts.razor.cs b/src/Treasury.App/Pages/Accounts.razor.cs
+index 7094576..b10bef4 100644
+--- a/src/Treasury.App/Pages/Accounts.razor.cs
++++ b/src/Treasury.App/Pages/Accounts.razor.cs
+@@ -16,20 +16,21 @@ public partial class Accounts
+     [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
+     [Inject] public UserManager<ApplicationUser> UserManager { get; set; } = default!;
+     [Inject] public AccountSharingService AccountSharingService { get; set; } = default!;
+ 
+     private ApplicationUser? _currentUser;
+     private readonly List<AccountCardVm> _accounts = new();
+     private List<HouseholdUserChoice> _householdUsers = new();
+     private string? _accountsLoadError;
+     private string? _householdUsersLoadError;
+     private bool _showCreateForm;
++    private bool _showInactive;
+     private readonly NewAccountForm _newAccount = new();
+ 
+     protected override async Task OnInitializedAsync()
+     {
+         _accountsLoadError = null;
+         _householdUsersLoadError = null;
+ 
+         var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
+         _currentUser = await UserManager.GetUserAsync(authState.User);
+         if (_currentUser is null)
+@@ -48,21 +49,23 @@ public partial class Accounts
+         if (_currentUser is null)
+         {
+             _accounts.Clear();
+             _accountsLoadError = null;
+             return;
+         }
+ 
+         try
+         {
+             _accountsLoadError = null;
+-            var accounts = await AccountSharingService.GetVisibleAccountsAsync(_currentUser, CancellationToken.None);
++            var accounts = _showInactive
++                ? await AccountSharingService.GetVisibleAccountsAsync(_currentUser, includeInactive: true, CancellationToken.None)
++                : await AccountSharingService.GetVisibleAccountsAsync(_currentUser, CancellationToken.None);
+             Dictionary<Guid, List<AccountViewerChoice>> viewersByAccountId;
+             var sharedViewerLoadFailed = false;
+ 
+             try
+             {
+                 var sharedViewers = await AccountSharingService.GetSharedViewersAsync(
+                     _currentUser,
+                     accounts.Select(account => account.Id).ToArray(),
+                     CancellationToken.None);
+ 
+@@ -81,20 +84,22 @@ public partial class Accounts
+             }
+ 
+             _accounts.Clear();
+             _accounts.AddRange(accounts.Select(account => new AccountCardVm
+             {
+                 Id = account.Id,
+                 Name = account.Name,
+                 Currency = account.Currency,
+                 AccountType = account.AccountType,
+                 CurrentBalance = account.CurrentBalance,
++                IsActive = account.IsActive,
++                BankAccountNumber = account.BankAccountNumber,
+                 IsOwner = account.OwnerUserId == _currentUser.Id,
+                 SharedWith = viewersByAccountId.TryGetValue(account.Id, out var viewers) ? viewers : new List<AccountViewerChoice>(),
+                 SharedWithLoadFailed = sharedViewerLoadFailed
+             }));
+         }
+         catch (Exception ex)
+         {
+             _accounts.Clear();
+             _accountsLoadError = "Unable to load accounts.";
+             Snackbar.Add($"Unable to load accounts: {ex.Message}", Severity.Error);
+@@ -125,41 +130,105 @@ public partial class Accounts
+ 
+     private void ToggleCreateForm()
+     {
+         _showCreateForm = !_showCreateForm;
+         if (!_showCreateForm)
+         {
+             ResetCreateForm();
+         }
+     }
+ 
++    private async Task OnShowInactiveChanged(bool value)
++    {
++        _showInactive = value;
++        await LoadAccountsAsync();
++    }
++
++    private async Task ToggleActiveStateAsync(AccountCardVm account)
++    {
++        if (_currentUser is null || !account.IsOwner)
++        {
++            Snackbar.Add("Only the account owner can change account status.", Severity.Warning);
++            return;
++        }
++
++        var entity = await DbContext.Accounts.SingleOrDefaultAsync(x => x.Id == account.Id && x.HouseholdId == _currentUser.HouseholdId, CancellationToken.None);
++        if (entity is null)
++        {
++            Snackbar.Add("Account not found.", Severity.Error);
++            return;
++        }
++
++        entity.IsActive = !entity.IsActive;
++        entity.UpdatedAt = DateTime.UtcNow;
++        await DbContext.SaveChangesAsync();
++
++        Snackbar.Add(entity.IsActive ? "Account reactivated." : "Account deactivated.", Severity.Success);
++        await LoadAccountsAsync();
++    }
++
++    private async Task DeleteAccountAsync(AccountCardVm account)
++    {
++        if (_currentUser is null || !account.IsOwner)
++        {
++            Snackbar.Add("Only the account owner can remove an account.", Severity.Warning);
++            return;
++        }
++
++        var entity = await DbContext.Accounts.SingleOrDefaultAsync(x => x.Id == account.Id && x.HouseholdId == _currentUser.HouseholdId, CancellationToken.None);
++        if (entity is null)
++        {
++            Snackbar.Add("Account not found.", Severity.Error);
++            return;
++        }
++
++        var hasTransactions = await DbContext.Transactions.AnyAsync(x => x.AccountId == entity.Id, CancellationToken.None);
++        if (hasTransactions && entity.CurrentBalance != 0m)
++        {
++            Snackbar.Add("Cannot remove account with transaction history and non-zero balance. Deactivate it instead.", Severity.Error);
++            return;
++        }
++
++        DbContext.Accounts.Remove(entity);
++        await DbContext.SaveChangesAsync();
++        Snackbar.Add("Account removed.", Severity.Success);
++        await LoadAccountsAsync();
++    }
++
+     private async Task CreateAccountAsync()
+     {
+         if (_currentUser is null)
+         {
+             Snackbar.Add("Sign in first.", Severity.Warning);
+             return;
+         }
+ 
+         if (string.IsNullOrWhiteSpace(_newAccount.Name))
+         {
+             Snackbar.Add("Account name is required.", Severity.Warning);
+             return;
+         }
+ 
++        if (!AccountLifecycleValidation.TryNormalizeOptionalBankAccountNumber(_newAccount.BankAccountNumber, out var bankAccountNumber, out var bankAccountNumberError))
++        {
++            Snackbar.Add(bankAccountNumberError!, Severity.Warning);
++            return;
++        }
++
+         var account = new Treasury.App.Domain.Account
+         {
+             HouseholdId = _currentUser.HouseholdId,
+             OwnerUserId = _currentUser.Id,
+             Name = _newAccount.Name.Trim(),
+             Currency = string.IsNullOrWhiteSpace(_newAccount.Currency) ? "PLN" : _newAccount.Currency.Trim().ToUpperInvariant(),
+             AccountType = string.IsNullOrWhiteSpace(_newAccount.AccountType) ? "cash-wallet" : _newAccount.AccountType.Trim(),
++            BankAccountNumber = bankAccountNumber,
+             CurrentBalance = _newAccount.InitialBalance,
+             CreatedAt = DateTime.UtcNow,
+             UpdatedAt = DateTime.UtcNow
+         };
+ 
+         DbContext.Accounts.Add(account);
+         await DbContext.SaveChangesAsync();
+ 
+         _showCreateForm = false;
+         ResetCreateForm();
+@@ -192,33 +261,37 @@ public partial class Accounts
+         Snackbar.Add("Account shared read-only.", Severity.Success);
+         await LoadAccountsAsync();
+     }
+ 
+     private void ResetCreateForm()
+     {
+         _newAccount.Name = string.Empty;
+         _newAccount.Currency = "PLN";
+         _newAccount.AccountType = "cash-wallet";
+         _newAccount.InitialBalance = 0m;
++        _newAccount.BankAccountNumber = string.Empty;
+     }
+ 
+     private sealed class NewAccountForm
+     {
+         public string Name { get; set; } = string.Empty;
+         public string Currency { get; set; } = "PLN";
+         public string AccountType { get; set; } = "cash-wallet";
+         public decimal InitialBalance { get; set; }
++        public string? BankAccountNumber { get; set; }
+     }
+ 
+     private sealed class AccountCardVm
+     {
+         public Guid Id { get; set; }
+         public string Name { get; set; } = string.Empty;
+         public string Currency { get; set; } = string.Empty;
+         public string AccountType { get; set; } = string.Empty;
+         public decimal CurrentBalance { get; set; }
++        public bool IsActive { get; set; }
+         public bool IsOwner { get; set; }
++        public string? BankAccountNumber { get; set; }
+         public string SelectedShareEmail { get; set; } = string.Empty;
+         public List<AccountViewerChoice> SharedWith { get; set; } = new();
+         public bool SharedWithLoadFailed { get; set; }
+     }
+ }
+diff --git a/src/Treasury.App/Pages/Transactions.razor b/src/Treasury.App/Pages/Transactions.razor
+index dea65c2..c4ca3d8 100644
+--- a/src/Treasury.App/Pages/Transactions.razor
++++ b/src/Treasury.App/Pages/Transactions.razor
+@@ -3,26 +3,33 @@
+ @inject TreasuryDbContext DbContext
+ @inject ISnackbar Snackbar
+ 
+ <MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="pa-4 pa-sm-6">
+     <MudStack Spacing="3">
+         <MudText Typo="Typo.h4">Transactions</MudText>
+ 
+         <MudPaper Class="pa-4 rounded-xl" Elevation="2">
+             <MudGrid>
+                 <MudItem xs="12" md="3">
+-                    <MudSelect T="string" Label="Account" @bind-Value="_newTransaction.AccountId" Required="true">
+-                        @foreach (var account in _accounts)
+-                        {
+-                            <MudSelectItem Value="@account.Id.ToString()">@account.Name (@account.Currency)</MudSelectItem>
+-                        }
+-                    </MudSelect>
++                    @if (_accounts.Count == 0)
++                    {
++                        <MudText Typo="Typo.body2" Class="mt-2">No active accounts. Create or reactivate an account in the Accounts page.</MudText>
++                    }
++                    else
++                    {
++                        <MudSelect T="string" Label="Account" @bind-Value="_newTransaction.AccountId" Required="true">
++                            @foreach (var account in _accounts)
++                            {
++                                <MudSelectItem Value="@account.Id.ToString()">@account.Name (@account.Currency)</MudSelectItem>
++                            }
++                        </MudSelect>
++                    }
+                 </MudItem>
+                 <MudItem xs="12" md="3">
+                     <MudTextField Label="Description" @bind-Value="_newTransaction.Description" Required="true" />
+                 </MudItem>
+                 <MudItem xs="12" md="2">
+                     <MudTextField Label="Category" @bind-Value="_newTransaction.Category" />
+                 </MudItem>
+                 <MudItem xs="12" md="2">
+                     <MudNumericField Label="Amount" @bind-Value="_newTransaction.Amount" Required="true" Min="0.01m" />
+                 </MudItem>
+@@ -117,21 +124,21 @@
+     private readonly NewTransactionForm _newTransaction = new();
+ 
+     protected override async Task OnInitializedAsync()
+     {
+         await LoadAsync();
+     }
+ 
+     private async Task LoadAsync()
+     {
+         _accounts.Clear();
+-        _accounts.AddRange(await DbContext.Accounts.OrderBy(x => x.Name).ToListAsync());
++        _accounts.AddRange(await DbContext.Accounts.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync());
+ 
+         _tags.Clear();
+         _tags.AddRange(await DbContext.Tags.OrderBy(x => x.Name).ToListAsync());
+ 
+         _transactions.Clear();
+         _transactions.AddRange(await DbContext.Transactions.OrderByDescending(x => x.TransactionDate).ToListAsync());
+ 
+         _transactionTags.Clear();
+         var tagLinks = await DbContext.TransactionTags
+             .Include(x => x.Tag)
+diff --git a/tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs b/tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
+new file mode 100644
+index 0000000..4297217
+--- /dev/null
++++ b/tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
+@@ -0,0 +1,206 @@
++using System.Net;
++using System.Net.Http.Json;
++using System.Text.Json;
++using FluentAssertions;
++using Microsoft.AspNetCore.Mvc.Testing;
++
++namespace Treasury.IntegrationTests;
++
++public class AccountsLifecycleTests
++{
++    [Fact]
++    public async Task Delete_Allows_Account_With_Zero_Balance()
++    {
++        await using var app = new TreasuryHostFactory();
++        var client = CreateAuthenticatedClient(app);
++        await RegisterAndSignInAsync(client);
++
++        var accountId = await CreateAccountAsync(client, name: "Delete me", bankAccountNumber: "1234567890123456");
++
++        var deleteResponse = await client.DeleteAsync($"/api/accounts/{accountId}");
++        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
++
++        using var accounts = await GetAccountsAsync(client);
++        accounts.RootElement.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == accountId);
++    }
++
++    [Fact]
++    public async Task Delete_Blocks_Account_With_Transactions_And_NonZero_Balance()
++    {
++        await using var app = new TreasuryHostFactory();
++        var client = CreateAuthenticatedClient(app);
++        await RegisterAndSignInAsync(client);
++
++        var accountId = await CreateAccountAsync(client, name: "Busy account", bankAccountNumber: "1234567890123456");
++
++        var transactionResponse = await client.PostAsJsonAsync("/api/transactions", new
++        {
++            AccountId = accountId,
++            Description = "Card payment",
++            Category = "General",
++            Amount = 10m,
++            Currency = "PLN",
++            Type = "expense",
++            TransactionDate = DateTime.UtcNow
++        });
++        transactionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
++
++        var deleteResponse = await client.DeleteAsync($"/api/accounts/{accountId}");
++        deleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
++
++        var body = await deleteResponse.Content.ReadAsStringAsync();
++        body.Should().Contain("transaction history and non-zero balance");
++    }
++
++    [Fact]
++    public async Task Deactivate_Hides_Account_From_Default_List()
++    {
++        await using var app = new TreasuryHostFactory();
++        var client = CreateAuthenticatedClient(app);
++        await RegisterAndSignInAsync(client);
++
++        var accountId = await CreateAccountAsync(client, name: "Hide me", bankAccountNumber: "1234567890123456");
++
++        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{accountId}/active-state", new
++        {
++            IsActive = false
++        });
++        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
++
++        using var defaultAccounts = await GetAccountsAsync(client);
++        defaultAccounts.RootElement.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == accountId);
++
++        using var includeInactiveAccounts = await GetAccountsAsync(client, "?includeInactive=true");
++        var inactiveAccount = includeInactiveAccounts.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == accountId);
++        inactiveAccount.GetProperty("isActive").GetBoolean().Should().BeFalse();
++    }
++
++    [Fact]
++    public async Task Update_Trims_BankAccountNumber()
++    {
++        await using var app = new TreasuryHostFactory();
++        var client = CreateAuthenticatedClient(app);
++        await RegisterAndSignInAsync(client);
++
++        var accountId = await CreateAccountAsync(client, name: "Editable account");
++
++        var updateResponse = await client.PutAsJsonAsync($"/api/accounts/{accountId}", new
++        {
++            Name = "Updated account",
++            BankAccountNumber = " 12345678901234567890123456789012 "
++        });
++        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
++
++        using var accounts = await GetAccountsAsync(client, "?includeInactive=true");
++        var account = accounts.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == accountId);
++        account.GetProperty("name").GetString().Should().Be("Updated account");
++        account.GetProperty("bankAccountNumber").GetString().Should().Be("12345678901234567890123456789012");
++    }
++
++    [Fact]
++    public async Task Creating_Transaction_Blocks_Inactive_Account()
++    {
++        await using var app = new TreasuryHostFactory();
++        var client = CreateAuthenticatedClient(app);
++        await RegisterAndSignInAsync(client);
++
++        var accountId = await CreateAccountAsync(client, name: "Inactive account");
++
++        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{accountId}/active-state", new { IsActive = false });
++        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
++
++        var txResponse = await client.PostAsJsonAsync("/api/transactions", new
++        {
++            AccountId = accountId,
++            Description = "Should be blocked",
++            Category = "General",
++            Amount = 10m,
++            Currency = "PLN",
++            Type = "expense",
++            TransactionDate = DateTime.UtcNow
++        });
++
++        txResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
++        var body = await txResponse.Content.ReadAsStringAsync();
++        body.Should().Contain("inactive");
++    }
++
++    [Fact]
++    public async Task Creating_Transfer_Blocks_Inactive_Source_Account()
++    {
++        await using var app = new TreasuryHostFactory();
++        var client = CreateAuthenticatedClient(app);
++        await RegisterAndSignInAsync(client);
++
++        var fromId = await CreateAccountAsync(client, name: "From");
++        var toId = await CreateAccountAsync(client, name: "To");
++
++        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{fromId}/active-state", new { IsActive = false });
++        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
++
++        var transferResponse = await client.PostAsJsonAsync("/api/transfers", new
++        {
++            FromAccountId = fromId,
++            ToAccountId = toId,
++            Amount = 5m,
++            Currency = "PLN",
++            Description = "Blocked transfer",
++            TransferDate = DateTime.UtcNow
++        });
++
++        transferResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
++        var body = await transferResponse.Content.ReadAsStringAsync();
++        body.Should().Contain("inactive");
++    }
++
++    private static HttpClient CreateAuthenticatedClient(TreasuryHostFactory app) =>
++        app.CreateClient(new WebApplicationFactoryClientOptions
++        {
++            AllowAutoRedirect = false,
++            HandleCookies = true
++        });
++
++    private static async Task RegisterAndSignInAsync(HttpClient client)
++    {
++        var email = $"owner-{Guid.NewGuid():N}@example.com";
++        const string password = "Password123!";
++
++        var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
++        {
++            Email = email,
++            Password = password,
++            ConfirmPassword = password
++        });
++        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
++
++        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
++        {
++            Email = email,
++            Password = password
++        });
++        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
++    }
++
++    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name, string? bankAccountNumber = null)
++    {
++        var response = await client.PostAsJsonAsync("/api/accounts", new
++        {
++            Name = name,
++            Currency = "PLN",
++            AccountType = "cash-wallet",
++            BankAccountNumber = bankAccountNumber
++        });
++
++        response.StatusCode.Should().Be(HttpStatusCode.Created);
++
++        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
++        return json.RootElement.GetProperty("id").GetGuid();
++    }
++
++    private static async Task<JsonDocument> GetAccountsAsync(HttpClient client, string query = "")
++    {
++        var response = await client.GetAsync($"/api/accounts{query}");
++        response.StatusCode.Should().Be(HttpStatusCode.OK);
++        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
++    }
++}
+diff --git a/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
+index c5c30f5..25edc5d 100644
+--- a/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
++++ b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
+@@ -195,20 +195,23 @@ public class AccountsSharingUiTests
+                     Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
+                     HouseholdId = user.HouseholdId,
+                     OwnerUserId = user.Id,
+                     Name = "Loaded account",
+                     Currency = "PLN",
+                     AccountType = "cash-wallet",
+                     CurrentBalance = 12.34m
+                 }
+             });
+ 
++        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, bool includeInactive, CancellationToken ct) =>
++            GetVisibleAccountsAsync(user, ct);
++
+         public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
+             throw new InvalidOperationException("household users query failed");
+ 
+         public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct) =>
+             Task.FromResult(new List<AccountViewerAssignment>
+             {
+                 new(
+                     Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
+                     user.Id,
+                     "viewer@example.com",
+@@ -226,20 +229,23 @@ public class AccountsSharingUiTests
+                     Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
+                     HouseholdId = user.HouseholdId,
+                     OwnerUserId = user.Id,
+                     Name = "Loaded account",
+                     Currency = "PLN",
+                     AccountType = "cash-wallet",
+                     CurrentBalance = 12.34m
+                 }
+             });
+ 
++        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, bool includeInactive, CancellationToken ct) =>
++            GetVisibleAccountsAsync(user, ct);
++
+         public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
+             Task.FromResult(new List<HouseholdUserChoice>
+             {
+                 new("viewer@example.com", "Viewer")
+             });
+ 
+         public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct) =>
+             throw new InvalidOperationException("shared viewers query failed");
+     }
+ 
+@@ -266,20 +272,23 @@ public class AccountsSharingUiTests
+                     Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
+                     HouseholdId = user.HouseholdId,
+                     OwnerUserId = user.Id,
+                     Name = "Account B",
+                     Currency = "PLN",
+                     AccountType = "cash-wallet",
+                     CurrentBalance = 2m
+                 }
+             });
+ 
++        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, bool includeInactive, CancellationToken ct) =>
++            GetVisibleAccountsAsync(user, ct);
++
+         public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
+             Task.FromResult(new List<HouseholdUserChoice>
+             {
+                 new("viewer-one@example.com", "Viewer One"),
+                 new("viewer-two@example.com", "Viewer Two")
+             });
+ 
+         public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
+         {
+             SharedViewerBatchCalls++;
diff --git a/.superpowers/sdd/phase2-task-1-review-package.md b/.superpowers/sdd/phase2-task-1-review-package.md
new file mode 100644
index 0000000..65d757a
--- /dev/null
+++ b/.superpowers/sdd/phase2-task-1-review-package.md
@@ -0,0 +1,1979 @@
+# Review Package
+
+Base: f667d4a71e9a92476fb40eb36e878669fdad75c6
+Head: 6e1a0b28f00fa0d4b3d2b88806da43c8602f5cb9
+
+## Commits
+
+6e1a0b2 feat: add account lifecycle controls and bank number validation
+
+## Diff Stat
+
+ .superpowers/sdd/phase2-task-1-report.md           |  42 ++
+ .../Accounts/AccountLifecycleValidation.cs         |  25 +
+ .../Application/Accounts/AccountSharingService.cs  |   4 +
+ .../Contracts/Accounts/AccountResponse.cs          |   2 +
+ .../Contracts/Accounts/CreateAccountRequest.cs     |   1 +
+ .../Accounts/SetAccountActiveStateRequest.cs       |   6 +
+ .../Contracts/Accounts/UpdateAccountRequest.cs     |   7 +
+ src/Treasury.App/Domain/Account.cs                 |   2 +
+ .../Endpoints/Accounts/CreateAccountEndpoint.cs    |  11 +
+ .../Endpoints/Accounts/DeleteAccountEndpoint.cs    |  57 ++
+ .../Endpoints/Accounts/GetAccountsEndpoint.cs      |  12 +-
+ .../Accounts/SetAccountActiveStateEndpoint.cs      |  57 ++
+ .../Endpoints/Accounts/UpdateAccountEndpoint.cs    |  80 +++
+ .../Infrastructure/Data/TreasuryDbContext.cs       |   8 +
+ ...903122916_AddAccountLifecycleFields.Designer.cs | 738 +++++++++++++++++++++
+ .../20260903122916_AddAccountLifecycleFields.cs    |  40 ++
+ .../Migrations/TreasuryDbContextModelSnapshot.cs   |   9 +
+ src/Treasury.App/Pages/Accounts.razor              |  25 +-
+ src/Treasury.App/Pages/Accounts.razor.cs           |  75 ++-
+ .../AccountsLifecycleTests.cs                      | 150 +++++
+ .../AccountsSharingUiTests.cs                      |   9 +
+ 21 files changed, 1356 insertions(+), 4 deletions(-)
+
+## Full Diff (-U10)
+
+diff --git a/.superpowers/sdd/phase2-task-1-report.md b/.superpowers/sdd/phase2-task-1-report.md
+new file mode 100644
+index 0000000..2e6e246
+--- /dev/null
++++ b/.superpowers/sdd/phase2-task-1-report.md
+@@ -0,0 +1,42 @@
++# Phase 2 Task 1 Report
++
++## What changed
++- Added `IsActive` and `BankAccountNumber` to the account domain model.
++- Added lifecycle DTOs and endpoints for updating account details, toggling active state, and deleting accounts.
++- Updated `/api/accounts` to hide inactive accounts by default and support `?includeInactive=true`.
++- Added bank account number validation: optional, trimmed, digits-only, length 16..34.
++- Updated the Accounts page to support showing inactive accounts, displaying bank account numbers, and owner actions to deactivate/reactivate or remove accounts.
++- Added an EF Core migration for the new account columns.
++- Added focused lifecycle integration tests.
++
++## Tests run
++- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests"`
++  - Result: passed, 4/4 tests.
++- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsEndpointTests|FullyQualifiedName~AccountsSharingUiTests|FullyQualifiedName~SharedReadOnlyUiPermissionTests"`
++  - Result: passed, 8/8 tests.
++
++## Files changed
++- `src/Treasury.App/Domain/Account.cs`
++- `src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs`
++- `src/Treasury.App/Contracts/Accounts/AccountResponse.cs`
++- `src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs`
++- `src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs`
++- `src/Treasury.App/Application/Accounts/AccountLifecycleValidation.cs`
++- `src/Treasury.App/Application/Accounts/AccountSharingService.cs`
++- `src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs`
++- `src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs`
++- `src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs`
++- `src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs`
++- `src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs`
++- `src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs`
++- `src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.cs`
++- `src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.Designer.cs`
++- `src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs`
++- `src/Treasury.App/Pages/Accounts.razor`
++- `src/Treasury.App/Pages/Accounts.razor.cs`
++- `tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs`
++- `tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs`
++
++## Concerns
++- The Accounts page still uses server-side data access for owner actions, while the API endpoints provide the same lifecycle behavior for external callers.
++- No dedicated inline edit UI was added for account name changes; the update endpoint is in place for API use and future UI work.
+diff --git a/src/Treasury.App/Application/Accounts/AccountLifecycleValidation.cs b/src/Treasury.App/Application/Accounts/AccountLifecycleValidation.cs
+new file mode 100644
+index 0000000..9a85ee2
+--- /dev/null
++++ b/src/Treasury.App/Application/Accounts/AccountLifecycleValidation.cs
+@@ -0,0 +1,25 @@
++namespace Treasury.App.Application.Accounts;
++
++public static class AccountLifecycleValidation
++{
++    public static bool TryNormalizeOptionalBankAccountNumber(string? bankAccountNumber, out string? normalized, out string? errorMessage)
++    {
++        normalized = null;
++        errorMessage = null;
++
++        if (string.IsNullOrWhiteSpace(bankAccountNumber))
++        {
++            return true;
++        }
++
++        normalized = bankAccountNumber.Trim();
++        if (normalized.Length is < 16 or > 34 || normalized.Any(ch => !char.IsDigit(ch)))
++        {
++            errorMessage = "Bank account number must contain only digits and be 16 to 34 characters long.";
++            normalized = null;
++            return false;
++        }
++
++        return true;
++    }
++}
+diff --git a/src/Treasury.App/Application/Accounts/AccountSharingService.cs b/src/Treasury.App/Application/Accounts/AccountSharingService.cs
+index 4f81e0a..44e7872 100644
+--- a/src/Treasury.App/Application/Accounts/AccountSharingService.cs
++++ b/src/Treasury.App/Application/Accounts/AccountSharingService.cs
+@@ -6,23 +6,27 @@ using Treasury.App.Infrastructure.Data;
+ namespace Treasury.App.Application.Accounts;
+ 
+ public sealed record HouseholdUserChoice(string Email, string DisplayName);
+ public sealed record AccountViewerChoice(string ViewerUserId, string Email, string DisplayName);
+ public sealed record AccountViewerAssignment(Guid AccountId, string ViewerUserId, string Email, string DisplayName);
+ public sealed record ShareReadOnlyResult(bool Succeeded, string? ErrorMessage);
+ 
+ public class AccountSharingService(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+ {
+     public virtual Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct) =>
++        GetVisibleAccountsAsync(user, includeInactive: false, ct);
++
++    public virtual Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, bool includeInactive, CancellationToken ct) =>
+         db.Accounts
+             .Where(x =>
+                 x.HouseholdId == user.HouseholdId
++                && (includeInactive || x.IsActive)
+                 && (x.OwnerUserId == user.Id
+                     || x.OwnerUserId == "seed"
+                     || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
+             .OrderBy(x => x.Name)
+             .ToListAsync(ct);
+ 
+     public virtual Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
+         db.Users
+             .Where(x => x.HouseholdId == user.HouseholdId && x.Id != user.Id)
+             .OrderBy(x => x.Email ?? x.UserName ?? x.Id)
+diff --git a/src/Treasury.App/Contracts/Accounts/AccountResponse.cs b/src/Treasury.App/Contracts/Accounts/AccountResponse.cs
+index ad202f7..2fd0ea4 100644
+--- a/src/Treasury.App/Contracts/Accounts/AccountResponse.cs
++++ b/src/Treasury.App/Contracts/Accounts/AccountResponse.cs
+@@ -1,11 +1,13 @@
+ namespace Treasury.App.Contracts.Accounts;
+ 
+ public sealed class AccountResponse
+ {
+     public Guid Id { get; set; }
+     public string Name { get; set; } = string.Empty;
+     public string Currency { get; set; } = string.Empty;
+     public string AccountType { get; set; } = string.Empty;
++    public bool IsActive { get; set; }
++    public string? BankAccountNumber { get; set; }
+     public decimal CurrentBalance { get; set; }
+     public bool IsReadOnly { get; set; }
+ }
+diff --git a/src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs b/src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs
+index c8659fa..e26a149 100644
+--- a/src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs
++++ b/src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs
+@@ -1,8 +1,9 @@
+ namespace Treasury.App.Contracts.Accounts;
+ 
+ public sealed class CreateAccountRequest
+ {
+     public string Name { get; set; } = string.Empty;
+     public string Currency { get; set; } = "PLN";
+     public string AccountType { get; set; } = "cash-wallet";
++    public string? BankAccountNumber { get; set; }
+ }
+diff --git a/src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs b/src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs
+new file mode 100644
+index 0000000..77d1ee7
+--- /dev/null
++++ b/src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs
+@@ -0,0 +1,6 @@
++namespace Treasury.App.Contracts.Accounts;
++
++public sealed class SetAccountActiveStateRequest
++{
++    public bool IsActive { get; set; }
++}
+diff --git a/src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs b/src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs
+new file mode 100644
+index 0000000..791d7d7
+--- /dev/null
++++ b/src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs
+@@ -0,0 +1,7 @@
++namespace Treasury.App.Contracts.Accounts;
++
++public sealed class UpdateAccountRequest
++{
++    public string Name { get; set; } = string.Empty;
++    public string? BankAccountNumber { get; set; }
++}
+diff --git a/src/Treasury.App/Domain/Account.cs b/src/Treasury.App/Domain/Account.cs
+index 83dde01..6e9a7d4 100644
+--- a/src/Treasury.App/Domain/Account.cs
++++ b/src/Treasury.App/Domain/Account.cs
+@@ -1,16 +1,18 @@
+ namespace Treasury.App.Domain;
+ 
+ public class Account
+ {
+     public Guid Id { get; set; } = Guid.NewGuid();
+     public Guid HouseholdId { get; set; }
+     public string OwnerUserId { get; set; } = string.Empty;
+     public string Name { get; set; } = string.Empty;
+     public string Currency { get; set; } = "PLN";
+     public string AccountType { get; set; } = "cash-wallet";
++    public bool IsActive { get; set; } = true;
++    public string? BankAccountNumber { get; set; }
+     public decimal CurrentBalance { get; set; }
+     public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
+     public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
+ 
+     public ICollection<VisibilityRule> VisibilityRules { get; set; } = new List<VisibilityRule>();
+ }
+diff --git a/src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs
+index 543cf2e..16e2d62 100644
+--- a/src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs
++++ b/src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs
+@@ -1,13 +1,14 @@
+ using FastEndpoints;
+ using Microsoft.AspNetCore.Identity;
+ using Microsoft.EntityFrameworkCore;
++using Treasury.App.Application.Accounts;
+ using Treasury.App.Contracts.Accounts;
+ using Treasury.App.Domain;
+ using Treasury.App.Infrastructure.Data;
+ 
+ namespace Treasury.App.Endpoints.Accounts;
+ 
+ public sealed class CreateAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+     : Endpoint<CreateAccountRequest>
+ {
+     public override void Configure()
+@@ -34,36 +35,46 @@ public sealed class CreateAccountEndpoint(TreasuryDbContext db, UserManager<Appl
+ 
+         var accountType = string.IsNullOrWhiteSpace(request.AccountType) ? "cash-wallet" : request.AccountType.Trim().ToLowerInvariant();
+         var isKnownType = await db.AccountTypes.AnyAsync(x => x.HouseholdId == user.HouseholdId && x.Name == accountType, ct);
+         if (!isKnownType)
+         {
+             AddError(x => x.AccountType, "Unknown account type for this household.");
+             await SendErrorsAsync(cancellation: ct);
+             return;
+         }
+ 
++        if (!AccountLifecycleValidation.TryNormalizeOptionalBankAccountNumber(request.BankAccountNumber, out var bankAccountNumber, out var bankAccountNumberError))
++        {
++            AddError(x => x.BankAccountNumber, bankAccountNumberError!);
++            await SendErrorsAsync(cancellation: ct);
++            return;
++        }
++
+         var account = new Account
+         {
+             HouseholdId = user.HouseholdId,
+             OwnerUserId = user.Id,
+             Name = request.Name.Trim(),
+             Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
+             AccountType = accountType,
++            BankAccountNumber = bankAccountNumber,
+             CurrentBalance = 0m
+         };
+ 
+         db.Accounts.Add(account);
+         await db.SaveChangesAsync(ct);
+ 
+         var response = new AccountResponse
+         {
+             Id = account.Id,
+             Name = account.Name,
+             Currency = account.Currency,
+             AccountType = account.AccountType,
++            IsActive = account.IsActive,
++            BankAccountNumber = account.BankAccountNumber,
+             CurrentBalance = account.CurrentBalance,
+             IsReadOnly = false
+         };
+ 
+         await SendAsync(response, StatusCodes.Status201Created, ct);
+     }
+ }
+diff --git a/src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs
+new file mode 100644
+index 0000000..277cf58
+--- /dev/null
++++ b/src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs
+@@ -0,0 +1,57 @@
++using FastEndpoints;
++using Microsoft.AspNetCore.Identity;
++using Microsoft.EntityFrameworkCore;
++using Treasury.App.Domain;
++using Treasury.App.Infrastructure.Data;
++
++namespace Treasury.App.Endpoints.Accounts;
++
++public sealed class DeleteAccountRouteRequest
++{
++    public Guid Id { get; set; }
++}
++
++public sealed class DeleteAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
++    : Endpoint<DeleteAccountRouteRequest>
++{
++    public override void Configure()
++    {
++        Delete("/api/accounts/{id:guid}");
++        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
++    }
++
++    public override async Task HandleAsync(DeleteAccountRouteRequest request, CancellationToken ct)
++    {
++        var user = await userManager.GetUserAsync(User);
++        if (user is null)
++        {
++            await SendUnauthorizedAsync(ct);
++            return;
++        }
++
++        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
++        if (account is null)
++        {
++            await SendNotFoundAsync(ct);
++            return;
++        }
++
++        if (account.OwnerUserId != user.Id)
++        {
++            await SendForbiddenAsync(ct);
++            return;
++        }
++
++        var hasTransactions = await db.Transactions.AnyAsync(x => x.AccountId == account.Id, ct);
++        if (hasTransactions && account.CurrentBalance != 0m)
++        {
++            AddError("Cannot remove account with transaction history and non-zero balance. Deactivate it instead.");
++            await SendErrorsAsync(cancellation: ct);
++            return;
++        }
++
++        db.Accounts.Remove(account);
++        await db.SaveChangesAsync(ct);
++        await SendNoContentAsync(ct);
++    }
++}
+diff --git a/src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs
+index 46eb0e1..f773fa5 100644
+--- a/src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs
++++ b/src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs
+@@ -1,47 +1,55 @@
+ using FastEndpoints;
+ using Microsoft.AspNetCore.Identity;
+ using Microsoft.EntityFrameworkCore;
+ using Treasury.App.Contracts.Accounts;
+ using Treasury.App.Domain;
+ using Treasury.App.Infrastructure.Data;
+ 
+ namespace Treasury.App.Endpoints.Accounts;
+ 
+-public sealed class GetAccountsEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : EndpointWithoutRequest
++public sealed class GetAccountsRequest
++{
++    public bool IncludeInactive { get; set; }
++}
++
++public sealed class GetAccountsEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : Endpoint<GetAccountsRequest>
+ {
+     public override void Configure()
+     {
+         Get("/api/accounts");
+         Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
+     }
+ 
+-    public override async Task HandleAsync(CancellationToken ct)
++    public override async Task HandleAsync(GetAccountsRequest request, CancellationToken ct)
+     {
+         var user = await userManager.GetUserAsync(User);
+         if (user is null)
+         {
+             await SendUnauthorizedAsync(ct);
+             return;
+         }
+ 
+         var accounts = await db.Accounts
+             .Where(x =>
+                 x.HouseholdId == user.HouseholdId
++                && (request.IncludeInactive || x.IsActive)
+                 && (x.OwnerUserId == user.Id
+                     || x.OwnerUserId == "seed"
+                     || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
+             .OrderBy(x => x.Name)
+             .Select(x => new AccountResponse
+             {
+                 Id = x.Id,
+                 Name = x.Name,
+                 Currency = x.Currency,
+                 AccountType = x.AccountType,
++                IsActive = x.IsActive,
++                BankAccountNumber = x.BankAccountNumber,
+                 CurrentBalance = x.CurrentBalance,
+                 IsReadOnly = x.OwnerUserId != user.Id
+             })
+             .ToListAsync(ct);
+ 
+         await SendOkAsync(accounts, ct);
+     }
+ }
+diff --git a/src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs
+new file mode 100644
+index 0000000..1ec69d0
+--- /dev/null
++++ b/src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs
+@@ -0,0 +1,57 @@
++using FastEndpoints;
++using Microsoft.AspNetCore.Identity;
++using Microsoft.EntityFrameworkCore;
++using Treasury.App.Contracts.Accounts;
++using Treasury.App.Domain;
++using Treasury.App.Infrastructure.Data;
++
++namespace Treasury.App.Endpoints.Accounts;
++
++public sealed class SetAccountActiveStateRouteRequest
++{
++    public Guid Id { get; set; }
++    public bool IsActive { get; set; }
++}
++
++public sealed class SetAccountActiveStateEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
++    : Endpoint<SetAccountActiveStateRouteRequest>
++{
++    public override void Configure()
++    {
++        Put("/api/accounts/{id:guid}/active-state");
++        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
++    }
++
++    public override async Task HandleAsync(SetAccountActiveStateRouteRequest request, CancellationToken ct)
++    {
++        var user = await userManager.GetUserAsync(User);
++        if (user is null)
++        {
++            await SendUnauthorizedAsync(ct);
++            return;
++        }
++
++        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
++        if (account is null)
++        {
++            await SendNotFoundAsync(ct);
++            return;
++        }
++
++        if (account.OwnerUserId != user.Id)
++        {
++            await SendForbiddenAsync(ct);
++            return;
++        }
++
++        account.IsActive = request.IsActive;
++        account.UpdatedAt = DateTime.UtcNow;
++        await db.SaveChangesAsync(ct);
++
++        await SendOkAsync(new
++        {
++            account.Id,
++            account.IsActive
++        }, ct);
++    }
++}
+diff --git a/src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs
+new file mode 100644
+index 0000000..c6175d0
+--- /dev/null
++++ b/src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs
+@@ -0,0 +1,80 @@
++using FastEndpoints;
++using Microsoft.AspNetCore.Identity;
++using Microsoft.EntityFrameworkCore;
++using Treasury.App.Application.Accounts;
++using Treasury.App.Domain;
++using Treasury.App.Infrastructure.Data;
++
++namespace Treasury.App.Endpoints.Accounts;
++
++public sealed class UpdateAccountRouteRequest
++{
++    public Guid Id { get; set; }
++    public string Name { get; set; } = string.Empty;
++    public string? BankAccountNumber { get; set; }
++}
++
++public sealed class UpdateAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
++    : Endpoint<UpdateAccountRouteRequest>
++{
++    public override void Configure()
++    {
++        Put("/api/accounts/{id:guid}");
++        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
++    }
++
++    public override async Task HandleAsync(UpdateAccountRouteRequest request, CancellationToken ct)
++    {
++        var user = await userManager.GetUserAsync(User);
++        if (user is null)
++        {
++            await SendUnauthorizedAsync(ct);
++            return;
++        }
++
++        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
++        if (account is null)
++        {
++            await SendNotFoundAsync(ct);
++            return;
++        }
++
++        if (account.OwnerUserId != user.Id)
++        {
++            await SendForbiddenAsync(ct);
++            return;
++        }
++
++        var hasErrors = false;
++        if (string.IsNullOrWhiteSpace(request.Name))
++        {
++            AddError(x => x.Name, "Account name is required.");
++            hasErrors = true;
++        }
++
++        if (!AccountLifecycleValidation.TryNormalizeOptionalBankAccountNumber(request.BankAccountNumber, out var bankAccountNumber, out var bankAccountNumberError))
++        {
++            AddError(x => x.BankAccountNumber, bankAccountNumberError!);
++            hasErrors = true;
++        }
++
++        if (hasErrors)
++        {
++            await SendErrorsAsync(cancellation: ct);
++            return;
++        }
++
++        account.Name = request.Name.Trim();
++        account.BankAccountNumber = bankAccountNumber;
++        account.UpdatedAt = DateTime.UtcNow;
++
++        await db.SaveChangesAsync(ct);
++
++        await SendOkAsync(new
++        {
++            account.Id,
++            account.Name,
++            account.BankAccountNumber
++        }, ct);
++    }
++}
+diff --git a/src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs b/src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs
+index 36de716..20a1ec0 100644
+--- a/src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs
++++ b/src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs
+@@ -46,20 +46,28 @@ public class TreasuryDbContext : IdentityDbContext<ApplicationUser>
+ 
+         modelBuilder.Entity<TransactionTag>()
+             .HasOne(x => x.Transaction)
+             .WithMany(x => x.TransactionTags)
+             .HasForeignKey(x => x.TransactionId);
+ 
+         modelBuilder.Entity<TransactionTag>()
+             .HasOne(x => x.Tag)
+             .WithMany(x => x.TransactionTags)
+             .HasForeignKey(x => x.TagId);
++
++        modelBuilder.Entity<Account>()
++            .Property(x => x.IsActive)
++            .HasDefaultValue(true);
++
++        modelBuilder.Entity<Account>()
++            .Property(x => x.BankAccountNumber)
++            .HasMaxLength(34);
+     }
+ 
+     public DbSet<Household> Households => Set<Household>();
+     public DbSet<AccountTypeDefinition> AccountTypes => Set<AccountTypeDefinition>();
+     public DbSet<Account> Accounts => Set<Account>();
+     public DbSet<VisibilityRule> VisibilityRules => Set<VisibilityRule>();
+     public DbSet<Transfer> Transfers => Set<Transfer>();
+     public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();
+     public DbSet<AssetValuation> AssetValuations => Set<AssetValuation>();
+     public DbSet<Transaction> Transactions => Set<Transaction>();
+diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.Designer.cs b/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.Designer.cs
+new file mode 100644
+index 0000000..5d8ec06
+--- /dev/null
++++ b/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.Designer.cs
+@@ -0,0 +1,738 @@
++﻿// <auto-generated />
++using System;
++using Microsoft.EntityFrameworkCore;
++using Microsoft.EntityFrameworkCore.Infrastructure;
++using Microsoft.EntityFrameworkCore.Migrations;
++using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
++using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
++using Treasury.App.Infrastructure.Data;
++
++#nullable disable
++
++namespace Treasury.App.Infrastructure.Migrations
++{
++    [DbContext(typeof(TreasuryDbContext))]
++    [Migration("20260903122916_AddAccountLifecycleFields")]
++    partial class AddAccountLifecycleFields
++    {
++        /// <inheritdoc />
++        protected override void BuildTargetModel(ModelBuilder modelBuilder)
++        {
++#pragma warning disable 612, 618
++            modelBuilder
++                .HasAnnotation("ProductVersion", "9.0.0")
++                .HasAnnotation("Relational:MaxIdentifierLength", 63);
++
++            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
++                {
++                    b.Property<string>("Id")
++                        .HasColumnType("text");
++
++                    b.Property<string>("ConcurrencyStamp")
++                        .IsConcurrencyToken()
++                        .HasColumnType("text");
++
++                    b.Property<string>("Name")
++                        .HasMaxLength(256)
++                        .HasColumnType("character varying(256)");
++
++                    b.Property<string>("NormalizedName")
++                        .HasMaxLength(256)
++                        .HasColumnType("character varying(256)");
++
++                    b.HasKey("Id");
++
++                    b.HasIndex("NormalizedName")
++                        .IsUnique()
++                        .HasDatabaseName("RoleNameIndex");
++
++                    b.ToTable("AspNetRoles", (string)null);
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
++                {
++                    b.Property<int>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("integer");
++
++                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));
++
++                    b.Property<string>("ClaimType")
++                        .HasColumnType("text");
++
++                    b.Property<string>("ClaimValue")
++                        .HasColumnType("text");
++
++                    b.Property<string>("RoleId")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.HasKey("Id");
++
++                    b.HasIndex("RoleId");
++
++                    b.ToTable("AspNetRoleClaims", (string)null);
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
++                {
++                    b.Property<int>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("integer");
++
++                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));
++
++                    b.Property<string>("ClaimType")
++                        .HasColumnType("text");
++
++                    b.Property<string>("ClaimValue")
++                        .HasColumnType("text");
++
++                    b.Property<string>("UserId")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.HasKey("Id");
++
++                    b.HasIndex("UserId");
++
++                    b.ToTable("AspNetUserClaims", (string)null);
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
++                {
++                    b.Property<string>("LoginProvider")
++                        .HasColumnType("text");
++
++                    b.Property<string>("ProviderKey")
++                        .HasColumnType("text");
++
++                    b.Property<string>("ProviderDisplayName")
++                        .HasColumnType("text");
++
++                    b.Property<string>("UserId")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.HasKey("LoginProvider", "ProviderKey");
++
++                    b.HasIndex("UserId");
++
++                    b.ToTable("AspNetUserLogins", (string)null);
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
++                {
++                    b.Property<string>("UserId")
++                        .HasColumnType("text");
++
++                    b.Property<string>("RoleId")
++                        .HasColumnType("text");
++
++                    b.HasKey("UserId", "RoleId");
++
++                    b.HasIndex("RoleId");
++
++                    b.ToTable("AspNetUserRoles", (string)null);
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
++                {
++                    b.Property<string>("UserId")
++                        .HasColumnType("text");
++
++                    b.Property<string>("LoginProvider")
++                        .HasColumnType("text");
++
++                    b.Property<string>("Name")
++                        .HasColumnType("text");
++
++                    b.Property<string>("Value")
++                        .HasColumnType("text");
++
++                    b.HasKey("UserId", "LoginProvider", "Name");
++
++                    b.ToTable("AspNetUserTokens", (string)null);
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Account", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<string>("AccountType")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<string>("BankAccountNumber")
++                        .HasMaxLength(34)
++                        .HasColumnType("character varying(34)");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Currency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<decimal>("CurrentBalance")
++                        .HasColumnType("numeric");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<bool>("IsActive")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("boolean")
++                        .HasDefaultValue(true);
++
++                    b.Property<string>("Name")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<string>("OwnerUserId")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("Accounts");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.AccountTypeDefinition", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Description")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<string>("Name")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("AccountTypes");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.ApplicationUser", b =>
++                {
++                    b.Property<string>("Id")
++                        .HasColumnType("text");
++
++                    b.Property<int>("AccessFailedCount")
++                        .HasColumnType("integer");
++
++                    b.Property<string>("ConcurrencyStamp")
++                        .IsConcurrencyToken()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Email")
++                        .HasMaxLength(256)
++                        .HasColumnType("character varying(256)");
++
++                    b.Property<bool>("EmailConfirmed")
++                        .HasColumnType("boolean");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<bool>("LockoutEnabled")
++                        .HasColumnType("boolean");
++
++                    b.Property<DateTimeOffset?>("LockoutEnd")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("NormalizedEmail")
++                        .HasMaxLength(256)
++                        .HasColumnType("character varying(256)");
++
++                    b.Property<string>("NormalizedUserName")
++                        .HasMaxLength(256)
++                        .HasColumnType("character varying(256)");
++
++                    b.Property<string>("PasswordHash")
++                        .HasColumnType("text");
++
++                    b.Property<string>("PhoneNumber")
++                        .HasColumnType("text");
++
++                    b.Property<bool>("PhoneNumberConfirmed")
++                        .HasColumnType("boolean");
++
++                    b.Property<string>("SecurityStamp")
++                        .HasColumnType("text");
++
++                    b.Property<bool>("TwoFactorEnabled")
++                        .HasColumnType("boolean");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("UserName")
++                        .HasMaxLength(256)
++                        .HasColumnType("character varying(256)");
++
++                    b.HasKey("Id");
++
++                    b.HasIndex("NormalizedEmail")
++                        .HasDatabaseName("EmailIndex");
++
++                    b.HasIndex("NormalizedUserName")
++                        .IsUnique()
++                        .HasDatabaseName("UserNameIndex");
++
++                    b.ToTable("AspNetUsers", (string)null);
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.AssetValuation", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<string>("AssetName")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Currency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<decimal>("CurrentTotalValue")
++                        .HasColumnType("numeric");
++
++                    b.Property<decimal>("CurrentUnitValue")
++                        .HasColumnType("numeric");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<int>("Kind")
++                        .HasColumnType("integer");
++
++                    b.Property<decimal>("Purity")
++                        .HasColumnType("numeric");
++
++                    b.Property<decimal>("Quantity")
++                        .HasColumnType("numeric");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<DateTime>("ValuationDate")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<decimal>("Weight")
++                        .HasColumnType("numeric");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("AssetValuations");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Bill", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<decimal>("Amount")
++                        .HasColumnType("numeric");
++
++                    b.Property<string>("Category")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Currency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<int>("DueDay")
++                        .HasColumnType("integer");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<bool>("IsPaid")
++                        .HasColumnType("boolean");
++
++                    b.Property<string>("Name")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<string>("Notes")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("Bills");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.BudgetCategory", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Currency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<decimal>("MonthlyLimit")
++                        .HasColumnType("numeric");
++
++                    b.Property<string>("Name")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<string>("Notes")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("BudgetCategories");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.CurrencyRate", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<DateTime>("EffectiveAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("FromCurrency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<decimal>("Rate")
++                        .HasColumnType("numeric");
++
++                    b.Property<string>("ToCurrency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.HasIndex("HouseholdId", "FromCurrency", "ToCurrency")
++                        .IsUnique();
++
++                    b.ToTable("CurrencyRates");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Household", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Name")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("Households");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Tag", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<string>("Color")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<string>("Name")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("Tags");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Transaction", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<Guid>("AccountId")
++                        .HasColumnType("uuid");
++
++                    b.Property<decimal>("Amount")
++                        .HasColumnType("numeric");
++
++                    b.Property<string>("Category")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Currency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<string>("Description")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<DateTime>("TransactionDate")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Type")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("UpdatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("Transactions");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.TransactionTag", b =>
++                {
++                    b.Property<Guid>("TransactionId")
++                        .HasColumnType("uuid");
++
++                    b.Property<Guid>("TagId")
++                        .HasColumnType("uuid");
++
++                    b.HasKey("TransactionId", "TagId");
++
++                    b.HasIndex("TagId");
++
++                    b.ToTable("TransactionTags");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Transfer", b =>
++                {
++                    b.Property<Guid>("Id")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("uuid");
++
++                    b.Property<decimal>("Amount")
++                        .HasColumnType("numeric");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<string>("Currency")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<string>("Description")
++                        .IsRequired()
++                        .HasColumnType("text");
++
++                    b.Property<Guid>("FromAccountId")
++                        .HasColumnType("uuid");
++
++                    b.Property<Guid>("HouseholdId")
++                        .HasColumnType("uuid");
++
++                    b.Property<Guid>("ToAccountId")
++                        .HasColumnType("uuid");
++
++                    b.Property<DateTime>("TransferDate")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.HasKey("Id");
++
++                    b.ToTable("Transfers");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.VisibilityRule", b =>
++                {
++                    b.Property<Guid>("AccountId")
++                        .HasColumnType("uuid");
++
++                    b.Property<string>("ViewerUserId")
++                        .HasColumnType("text");
++
++                    b.Property<DateTime>("CreatedAt")
++                        .HasColumnType("timestamp with time zone");
++
++                    b.Property<bool>("IsReadOnly")
++                        .HasColumnType("boolean");
++
++                    b.HasKey("AccountId", "ViewerUserId");
++
++                    b.ToTable("VisibilityRules");
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
++                {
++                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
++                        .WithMany()
++                        .HasForeignKey("RoleId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
++                {
++                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
++                        .WithMany()
++                        .HasForeignKey("UserId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
++                {
++                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
++                        .WithMany()
++                        .HasForeignKey("UserId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
++                {
++                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
++                        .WithMany()
++                        .HasForeignKey("RoleId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++
++                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
++                        .WithMany()
++                        .HasForeignKey("UserId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++                });
++
++            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
++                {
++                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
++                        .WithMany()
++                        .HasForeignKey("UserId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.TransactionTag", b =>
++                {
++                    b.HasOne("Treasury.App.Domain.Tag", "Tag")
++                        .WithMany("TransactionTags")
++                        .HasForeignKey("TagId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++
++                    b.HasOne("Treasury.App.Domain.Transaction", "Transaction")
++                        .WithMany("TransactionTags")
++                        .HasForeignKey("TransactionId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++
++                    b.Navigation("Tag");
++
++                    b.Navigation("Transaction");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.VisibilityRule", b =>
++                {
++                    b.HasOne("Treasury.App.Domain.Account", "Account")
++                        .WithMany("VisibilityRules")
++                        .HasForeignKey("AccountId")
++                        .OnDelete(DeleteBehavior.Cascade)
++                        .IsRequired();
++
++                    b.Navigation("Account");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Account", b =>
++                {
++                    b.Navigation("VisibilityRules");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Tag", b =>
++                {
++                    b.Navigation("TransactionTags");
++                });
++
++            modelBuilder.Entity("Treasury.App.Domain.Transaction", b =>
++                {
++                    b.Navigation("TransactionTags");
++                });
++#pragma warning restore 612, 618
++        }
++    }
++}
+diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.cs b/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.cs
+new file mode 100644
+index 0000000..98fe170
+--- /dev/null
++++ b/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.cs
+@@ -0,0 +1,40 @@
++﻿using Microsoft.EntityFrameworkCore.Migrations;
++
++#nullable disable
++
++namespace Treasury.App.Infrastructure.Migrations
++{
++    /// <inheritdoc />
++    public partial class AddAccountLifecycleFields : Migration
++    {
++        /// <inheritdoc />
++        protected override void Up(MigrationBuilder migrationBuilder)
++        {
++            migrationBuilder.AddColumn<string>(
++                name: "BankAccountNumber",
++                table: "Accounts",
++                type: "character varying(34)",
++                maxLength: 34,
++                nullable: true);
++
++            migrationBuilder.AddColumn<bool>(
++                name: "IsActive",
++                table: "Accounts",
++                type: "boolean",
++                nullable: false,
++                defaultValue: true);
++        }
++
++        /// <inheritdoc />
++        protected override void Down(MigrationBuilder migrationBuilder)
++        {
++            migrationBuilder.DropColumn(
++                name: "BankAccountNumber",
++                table: "Accounts");
++
++            migrationBuilder.DropColumn(
++                name: "IsActive",
++                table: "Accounts");
++        }
++    }
++}
+diff --git a/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs b/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
+index 2037f1f..a9b083a 100644
+--- a/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
++++ b/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
+@@ -157,33 +157,42 @@ namespace Treasury.App.Infrastructure.Migrations
+             modelBuilder.Entity("Treasury.App.Domain.Account", b =>
+                 {
+                     b.Property<Guid>("Id")
+                         .ValueGeneratedOnAdd()
+                         .HasColumnType("uuid");
+ 
+                     b.Property<string>("AccountType")
+                         .IsRequired()
+                         .HasColumnType("text");
+ 
++                    b.Property<string>("BankAccountNumber")
++                        .HasMaxLength(34)
++                        .HasColumnType("character varying(34)");
++
+                     b.Property<DateTime>("CreatedAt")
+                         .HasColumnType("timestamp with time zone");
+ 
+                     b.Property<string>("Currency")
+                         .IsRequired()
+                         .HasColumnType("text");
+ 
+                     b.Property<decimal>("CurrentBalance")
+                         .HasColumnType("numeric");
+ 
+                     b.Property<Guid>("HouseholdId")
+                         .HasColumnType("uuid");
+ 
++                    b.Property<bool>("IsActive")
++                        .ValueGeneratedOnAdd()
++                        .HasColumnType("boolean")
++                        .HasDefaultValue(true);
++
+                     b.Property<string>("Name")
+                         .IsRequired()
+                         .HasColumnType("text");
+ 
+                     b.Property<string>("OwnerUserId")
+                         .IsRequired()
+                         .HasColumnType("text");
+ 
+                     b.Property<DateTime>("UpdatedAt")
+                         .HasColumnType("timestamp with time zone");
+diff --git a/src/Treasury.App/Pages/Accounts.razor b/src/Treasury.App/Pages/Accounts.razor
+index f6e1603..5cf5477 100644
+--- a/src/Treasury.App/Pages/Accounts.razor
++++ b/src/Treasury.App/Pages/Accounts.razor
+@@ -1,37 +1,43 @@
+ @page "/accounts"
+ @attribute [Authorize]
+ 
+ <MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="pa-4 pa-sm-6">
+     <MudStack Spacing="3">
+         <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
+             <MudText Typo="Typo.h4">Accounts</MudText>
+-            <MudButton Variant="Variant.Filled" Color="Color.Tertiary" StartIcon="@Icons.Material.Filled.Add" OnClick="ToggleCreateForm">Add account</MudButton>
++            <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
++                <MudSwitch T="bool" Label="Show inactive" checked="_showInactive" checkedChanged="OnShowInactiveChanged" />
++                <MudButton Variant="Variant.Filled" Color="Color.Tertiary" StartIcon="@Icons.Material.Filled.Add" OnClick="ToggleCreateForm">Add account</MudButton>
++            </MudStack>
+         </MudStack>
+ 
+         @if (!string.IsNullOrWhiteSpace(_accountsLoadError))
+         {
+             <MudAlert Severity="Severity.Error">@_accountsLoadError</MudAlert>
+         }
+ 
+         @if (!string.IsNullOrWhiteSpace(_householdUsersLoadError))
+         {
+             <MudAlert Severity="Severity.Error">@_householdUsersLoadError</MudAlert>
+         }
+ 
+         @if (_showCreateForm)
+         {
+             <MudPaper Class="pa-4 rounded-xl" Elevation="2">
+                 <MudGrid>
+                     <MudItem xs="12" md="4">
+                         <MudTextField Label="Account name" @bind-Value="_newAccount.Name" Required="true" />
+                     </MudItem>
++                    <MudItem xs="12" md="4">
++                        <MudTextField Label="Bank account number (optional)" @bind-Value="_newAccount.BankAccountNumber" />
++                    </MudItem>
+                     <MudItem xs="12" md="3">
+                         <MudSelect T="string" Label="Currency" @bind-Value="_newAccount.Currency">
+                             <MudSelectItem Value="@("PLN")">PLN</MudSelectItem>
+                             <MudSelectItem Value="@("EUR")">EUR</MudSelectItem>
+                             <MudSelectItem Value="@("USD")">USD</MudSelectItem>
+                             <MudSelectItem Value="@("GBP")">GBP</MudSelectItem>
+                         </MudSelect>
+                     </MudItem>
+                     <MudItem xs="12" md="3">
+                         <MudSelect T="string" Label="Type" @bind-Value="_newAccount.AccountType">
+@@ -59,25 +65,42 @@
+         }
+         else
+         {
+             <MudGrid>
+                 @foreach (var account in _accounts)
+                 {
+                     <MudItem xs="12" md="6" lg="4">
+                         <MudPaper Class="pa-4 rounded-xl" Elevation="2">
+                             <MudText Typo="Typo.h6">@account.Name</MudText>
+                             <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mt-1">@account.AccountType</MudText>
++                            @if (!string.IsNullOrWhiteSpace(account.BankAccountNumber))
++                            {
++                                <MudText Typo="Typo.body2" Color="Color.Secondary">Bank account: @account.BankAccountNumber</MudText>
++                            }
++                            @if (!account.IsActive)
++                            {
++                                <MudChip Class="mt-2" T="string" Color="Color.Warning" Variant="Variant.Outlined">Inactive</MudChip>
++                            }
+                             <MudDivider Class="my-3" />
+                             <MudText Typo="Typo.h5">@account.CurrentBalance.ToString("N2") @account.Currency</MudText>
+ 
+                             @if (account.IsOwner)
+                             {
++                                <MudStack Row="true" Class="mt-4" Spacing="2" Wrap="Wrap.Wrap">
++                                    <MudButton Variant="Variant.Outlined" OnClick="() => ToggleActiveStateAsync(account)">
++                                        @(account.IsActive ? "Deactivate" : "Reactivate")
++                                    </MudButton>
++                                    <MudButton Color="Color.Error" Variant="Variant.Outlined" OnClick="() => DeleteAccountAsync(account)">
++                                        Remove
++                                    </MudButton>
++                                </MudStack>
++
+                                 <MudDivider Class="my-3" />
+                                 <MudText Typo="Typo.subtitle2">Shared with:</MudText>
+                                 @if (account.SharedWithLoadFailed)
+                                 {
+                                     <MudText Typo="Typo.body2" Color="Color.Secondary">Shared viewer details unavailable.</MudText>
+                                 }
+                                 else if (account.SharedWith.Count == 0)
+                                 {
+                                     <MudText Typo="Typo.body2" Color="Color.Secondary">Nobody yet.</MudText>
+                                 }
+diff --git a/src/Treasury.App/Pages/Accounts.razor.cs b/src/Treasury.App/Pages/Accounts.razor.cs
+index 7094576..b10bef4 100644
+--- a/src/Treasury.App/Pages/Accounts.razor.cs
++++ b/src/Treasury.App/Pages/Accounts.razor.cs
+@@ -16,20 +16,21 @@ public partial class Accounts
+     [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
+     [Inject] public UserManager<ApplicationUser> UserManager { get; set; } = default!;
+     [Inject] public AccountSharingService AccountSharingService { get; set; } = default!;
+ 
+     private ApplicationUser? _currentUser;
+     private readonly List<AccountCardVm> _accounts = new();
+     private List<HouseholdUserChoice> _householdUsers = new();
+     private string? _accountsLoadError;
+     private string? _householdUsersLoadError;
+     private bool _showCreateForm;
++    private bool _showInactive;
+     private readonly NewAccountForm _newAccount = new();
+ 
+     protected override async Task OnInitializedAsync()
+     {
+         _accountsLoadError = null;
+         _householdUsersLoadError = null;
+ 
+         var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
+         _currentUser = await UserManager.GetUserAsync(authState.User);
+         if (_currentUser is null)
+@@ -48,21 +49,23 @@ public partial class Accounts
+         if (_currentUser is null)
+         {
+             _accounts.Clear();
+             _accountsLoadError = null;
+             return;
+         }
+ 
+         try
+         {
+             _accountsLoadError = null;
+-            var accounts = await AccountSharingService.GetVisibleAccountsAsync(_currentUser, CancellationToken.None);
++            var accounts = _showInactive
++                ? await AccountSharingService.GetVisibleAccountsAsync(_currentUser, includeInactive: true, CancellationToken.None)
++                : await AccountSharingService.GetVisibleAccountsAsync(_currentUser, CancellationToken.None);
+             Dictionary<Guid, List<AccountViewerChoice>> viewersByAccountId;
+             var sharedViewerLoadFailed = false;
+ 
+             try
+             {
+                 var sharedViewers = await AccountSharingService.GetSharedViewersAsync(
+                     _currentUser,
+                     accounts.Select(account => account.Id).ToArray(),
+                     CancellationToken.None);
+ 
+@@ -81,20 +84,22 @@ public partial class Accounts
+             }
+ 
+             _accounts.Clear();
+             _accounts.AddRange(accounts.Select(account => new AccountCardVm
+             {
+                 Id = account.Id,
+                 Name = account.Name,
+                 Currency = account.Currency,
+                 AccountType = account.AccountType,
+                 CurrentBalance = account.CurrentBalance,
++                IsActive = account.IsActive,
++                BankAccountNumber = account.BankAccountNumber,
+                 IsOwner = account.OwnerUserId == _currentUser.Id,
+                 SharedWith = viewersByAccountId.TryGetValue(account.Id, out var viewers) ? viewers : new List<AccountViewerChoice>(),
+                 SharedWithLoadFailed = sharedViewerLoadFailed
+             }));
+         }
+         catch (Exception ex)
+         {
+             _accounts.Clear();
+             _accountsLoadError = "Unable to load accounts.";
+             Snackbar.Add($"Unable to load accounts: {ex.Message}", Severity.Error);
+@@ -125,41 +130,105 @@ public partial class Accounts
+ 
+     private void ToggleCreateForm()
+     {
+         _showCreateForm = !_showCreateForm;
+         if (!_showCreateForm)
+         {
+             ResetCreateForm();
+         }
+     }
+ 
++    private async Task OnShowInactiveChanged(bool value)
++    {
++        _showInactive = value;
++        await LoadAccountsAsync();
++    }
++
++    private async Task ToggleActiveStateAsync(AccountCardVm account)
++    {
++        if (_currentUser is null || !account.IsOwner)
++        {
++            Snackbar.Add("Only the account owner can change account status.", Severity.Warning);
++            return;
++        }
++
++        var entity = await DbContext.Accounts.SingleOrDefaultAsync(x => x.Id == account.Id && x.HouseholdId == _currentUser.HouseholdId, CancellationToken.None);
++        if (entity is null)
++        {
++            Snackbar.Add("Account not found.", Severity.Error);
++            return;
++        }
++
++        entity.IsActive = !entity.IsActive;
++        entity.UpdatedAt = DateTime.UtcNow;
++        await DbContext.SaveChangesAsync();
++
++        Snackbar.Add(entity.IsActive ? "Account reactivated." : "Account deactivated.", Severity.Success);
++        await LoadAccountsAsync();
++    }
++
++    private async Task DeleteAccountAsync(AccountCardVm account)
++    {
++        if (_currentUser is null || !account.IsOwner)
++        {
++            Snackbar.Add("Only the account owner can remove an account.", Severity.Warning);
++            return;
++        }
++
++        var entity = await DbContext.Accounts.SingleOrDefaultAsync(x => x.Id == account.Id && x.HouseholdId == _currentUser.HouseholdId, CancellationToken.None);
++        if (entity is null)
++        {
++            Snackbar.Add("Account not found.", Severity.Error);
++            return;
++        }
++
++        var hasTransactions = await DbContext.Transactions.AnyAsync(x => x.AccountId == entity.Id, CancellationToken.None);
++        if (hasTransactions && entity.CurrentBalance != 0m)
++        {
++            Snackbar.Add("Cannot remove account with transaction history and non-zero balance. Deactivate it instead.", Severity.Error);
++            return;
++        }
++
++        DbContext.Accounts.Remove(entity);
++        await DbContext.SaveChangesAsync();
++        Snackbar.Add("Account removed.", Severity.Success);
++        await LoadAccountsAsync();
++    }
++
+     private async Task CreateAccountAsync()
+     {
+         if (_currentUser is null)
+         {
+             Snackbar.Add("Sign in first.", Severity.Warning);
+             return;
+         }
+ 
+         if (string.IsNullOrWhiteSpace(_newAccount.Name))
+         {
+             Snackbar.Add("Account name is required.", Severity.Warning);
+             return;
+         }
+ 
++        if (!AccountLifecycleValidation.TryNormalizeOptionalBankAccountNumber(_newAccount.BankAccountNumber, out var bankAccountNumber, out var bankAccountNumberError))
++        {
++            Snackbar.Add(bankAccountNumberError!, Severity.Warning);
++            return;
++        }
++
+         var account = new Treasury.App.Domain.Account
+         {
+             HouseholdId = _currentUser.HouseholdId,
+             OwnerUserId = _currentUser.Id,
+             Name = _newAccount.Name.Trim(),
+             Currency = string.IsNullOrWhiteSpace(_newAccount.Currency) ? "PLN" : _newAccount.Currency.Trim().ToUpperInvariant(),
+             AccountType = string.IsNullOrWhiteSpace(_newAccount.AccountType) ? "cash-wallet" : _newAccount.AccountType.Trim(),
++            BankAccountNumber = bankAccountNumber,
+             CurrentBalance = _newAccount.InitialBalance,
+             CreatedAt = DateTime.UtcNow,
+             UpdatedAt = DateTime.UtcNow
+         };
+ 
+         DbContext.Accounts.Add(account);
+         await DbContext.SaveChangesAsync();
+ 
+         _showCreateForm = false;
+         ResetCreateForm();
+@@ -192,33 +261,37 @@ public partial class Accounts
+         Snackbar.Add("Account shared read-only.", Severity.Success);
+         await LoadAccountsAsync();
+     }
+ 
+     private void ResetCreateForm()
+     {
+         _newAccount.Name = string.Empty;
+         _newAccount.Currency = "PLN";
+         _newAccount.AccountType = "cash-wallet";
+         _newAccount.InitialBalance = 0m;
++        _newAccount.BankAccountNumber = string.Empty;
+     }
+ 
+     private sealed class NewAccountForm
+     {
+         public string Name { get; set; } = string.Empty;
+         public string Currency { get; set; } = "PLN";
+         public string AccountType { get; set; } = "cash-wallet";
+         public decimal InitialBalance { get; set; }
++        public string? BankAccountNumber { get; set; }
+     }
+ 
+     private sealed class AccountCardVm
+     {
+         public Guid Id { get; set; }
+         public string Name { get; set; } = string.Empty;
+         public string Currency { get; set; } = string.Empty;
+         public string AccountType { get; set; } = string.Empty;
+         public decimal CurrentBalance { get; set; }
++        public bool IsActive { get; set; }
+         public bool IsOwner { get; set; }
++        public string? BankAccountNumber { get; set; }
+         public string SelectedShareEmail { get; set; } = string.Empty;
+         public List<AccountViewerChoice> SharedWith { get; set; } = new();
+         public bool SharedWithLoadFailed { get; set; }
+     }
+ }
+diff --git a/tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs b/tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
+new file mode 100644
+index 0000000..7893241
+--- /dev/null
++++ b/tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
+@@ -0,0 +1,150 @@
++using System.Net;
++using System.Net.Http.Json;
++using System.Text.Json;
++using FluentAssertions;
++using Microsoft.AspNetCore.Mvc.Testing;
++
++namespace Treasury.IntegrationTests;
++
++public class AccountsLifecycleTests
++{
++    [Fact]
++    public async Task Delete_Allows_Account_With_Zero_Balance()
++    {
++        await using var app = new TreasuryHostFactory();
++        var client = CreateAuthenticatedClient(app);
++        await RegisterAndSignInAsync(client);
++
++        var accountId = await CreateAccountAsync(client, name: "Delete me", bankAccountNumber: "1234567890123456");
++
++        var deleteResponse = await client.DeleteAsync($"/api/accounts/{accountId}");
++        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
++
++        using var accounts = await GetAccountsAsync(client);
++        accounts.RootElement.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == accountId);
++    }
++
++    [Fact]
++    public async Task Delete_Blocks_Account_With_Transactions_And_NonZero_Balance()
++    {
++        await using var app = new TreasuryHostFactory();
++        var client = CreateAuthenticatedClient(app);
++        await RegisterAndSignInAsync(client);
++
++        var accountId = await CreateAccountAsync(client, name: "Busy account", bankAccountNumber: "1234567890123456");
++
++        var transactionResponse = await client.PostAsJsonAsync("/api/transactions", new
++        {
++            AccountId = accountId,
++            Description = "Card payment",
++            Category = "General",
++            Amount = 10m,
++            Currency = "PLN",
++            Type = "expense",
++            TransactionDate = DateTime.UtcNow
++        });
++        transactionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
++
++        var deleteResponse = await client.DeleteAsync($"/api/accounts/{accountId}");
++        deleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
++
++        var body = await deleteResponse.Content.ReadAsStringAsync();
++        body.Should().Contain("transaction history and non-zero balance");
++    }
++
++    [Fact]
++    public async Task Deactivate_Hides_Account_From_Default_List()
++    {
++        await using var app = new TreasuryHostFactory();
++        var client = CreateAuthenticatedClient(app);
++        await RegisterAndSignInAsync(client);
++
++        var accountId = await CreateAccountAsync(client, name: "Hide me", bankAccountNumber: "1234567890123456");
++
++        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{accountId}/active-state", new
++        {
++            IsActive = false
++        });
++        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
++
++        using var defaultAccounts = await GetAccountsAsync(client);
++        defaultAccounts.RootElement.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == accountId);
++
++        using var includeInactiveAccounts = await GetAccountsAsync(client, "?includeInactive=true");
++        var inactiveAccount = includeInactiveAccounts.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == accountId);
++        inactiveAccount.GetProperty("isActive").GetBoolean().Should().BeFalse();
++    }
++
++    [Fact]
++    public async Task Update_Trims_BankAccountNumber()
++    {
++        await using var app = new TreasuryHostFactory();
++        var client = CreateAuthenticatedClient(app);
++        await RegisterAndSignInAsync(client);
++
++        var accountId = await CreateAccountAsync(client, name: "Editable account");
++
++        var updateResponse = await client.PutAsJsonAsync($"/api/accounts/{accountId}", new
++        {
++            Name = "Updated account",
++            BankAccountNumber = " 12345678901234567890123456789012 "
++        });
++        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
++
++        using var accounts = await GetAccountsAsync(client, "?includeInactive=true");
++        var account = accounts.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == accountId);
++        account.GetProperty("name").GetString().Should().Be("Updated account");
++        account.GetProperty("bankAccountNumber").GetString().Should().Be("12345678901234567890123456789012");
++    }
++
++    private static HttpClient CreateAuthenticatedClient(TreasuryHostFactory app) =>
++        app.CreateClient(new WebApplicationFactoryClientOptions
++        {
++            AllowAutoRedirect = false,
++            HandleCookies = true
++        });
++
++    private static async Task RegisterAndSignInAsync(HttpClient client)
++    {
++        var email = $"owner-{Guid.NewGuid():N}@example.com";
++        const string password = "Password123!";
++
++        var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
++        {
++            Email = email,
++            Password = password,
++            ConfirmPassword = password
++        });
++        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
++
++        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
++        {
++            Email = email,
++            Password = password
++        });
++        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
++    }
++
++    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name, string? bankAccountNumber = null)
++    {
++        var response = await client.PostAsJsonAsync("/api/accounts", new
++        {
++            Name = name,
++            Currency = "PLN",
++            AccountType = "cash-wallet",
++            BankAccountNumber = bankAccountNumber
++        });
++
++        response.StatusCode.Should().Be(HttpStatusCode.Created);
++
++        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
++        return json.RootElement.GetProperty("id").GetGuid();
++    }
++
++    private static async Task<JsonDocument> GetAccountsAsync(HttpClient client, string query = "")
++    {
++        var response = await client.GetAsync($"/api/accounts{query}");
++        response.StatusCode.Should().Be(HttpStatusCode.OK);
++        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
++    }
++}
+diff --git a/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
+index c5c30f5..25edc5d 100644
+--- a/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
++++ b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
+@@ -195,20 +195,23 @@ public class AccountsSharingUiTests
+                     Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
+                     HouseholdId = user.HouseholdId,
+                     OwnerUserId = user.Id,
+                     Name = "Loaded account",
+                     Currency = "PLN",
+                     AccountType = "cash-wallet",
+                     CurrentBalance = 12.34m
+                 }
+             });
+ 
++        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, bool includeInactive, CancellationToken ct) =>
++            GetVisibleAccountsAsync(user, ct);
++
+         public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
+             throw new InvalidOperationException("household users query failed");
+ 
+         public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct) =>
+             Task.FromResult(new List<AccountViewerAssignment>
+             {
+                 new(
+                     Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
+                     user.Id,
+                     "viewer@example.com",
+@@ -226,20 +229,23 @@ public class AccountsSharingUiTests
+                     Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
+                     HouseholdId = user.HouseholdId,
+                     OwnerUserId = user.Id,
+                     Name = "Loaded account",
+                     Currency = "PLN",
+                     AccountType = "cash-wallet",
+                     CurrentBalance = 12.34m
+                 }
+             });
+ 
++        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, bool includeInactive, CancellationToken ct) =>
++            GetVisibleAccountsAsync(user, ct);
++
+         public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
+             Task.FromResult(new List<HouseholdUserChoice>
+             {
+                 new("viewer@example.com", "Viewer")
+             });
+ 
+         public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct) =>
+             throw new InvalidOperationException("shared viewers query failed");
+     }
+ 
+@@ -266,20 +272,23 @@ public class AccountsSharingUiTests
+                     Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
+                     HouseholdId = user.HouseholdId,
+                     OwnerUserId = user.Id,
+                     Name = "Account B",
+                     Currency = "PLN",
+                     AccountType = "cash-wallet",
+                     CurrentBalance = 2m
+                 }
+             });
+ 
++        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, bool includeInactive, CancellationToken ct) =>
++            GetVisibleAccountsAsync(user, ct);
++
+         public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
+             Task.FromResult(new List<HouseholdUserChoice>
+             {
+                 new("viewer-one@example.com", "Viewer One"),
+                 new("viewer-two@example.com", "Viewer Two")
+             });
+ 
+         public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
+         {
+             SharedViewerBatchCalls++;
diff --git a/src/Treasury.App/Application/Accounts/AccountLifecycleValidation.cs b/src/Treasury.App/Application/Accounts/AccountLifecycleValidation.cs
new file mode 100644
index 0000000..9a85ee2
--- /dev/null
+++ b/src/Treasury.App/Application/Accounts/AccountLifecycleValidation.cs
@@ -0,0 +1,25 @@
+namespace Treasury.App.Application.Accounts;
+
+public static class AccountLifecycleValidation
+{
+    public static bool TryNormalizeOptionalBankAccountNumber(string? bankAccountNumber, out string? normalized, out string? errorMessage)
+    {
+        normalized = null;
+        errorMessage = null;
+
+        if (string.IsNullOrWhiteSpace(bankAccountNumber))
+        {
+            return true;
+        }
+
+        normalized = bankAccountNumber.Trim();
+        if (normalized.Length is < 16 or > 34 || normalized.Any(ch => !char.IsDigit(ch)))
+        {
+            errorMessage = "Bank account number must contain only digits and be 16 to 34 characters long.";
+            normalized = null;
+            return false;
+        }
+
+        return true;
+    }
+}
diff --git a/src/Treasury.App/Application/Accounts/AccountSharingService.cs b/src/Treasury.App/Application/Accounts/AccountSharingService.cs
index 4f81e0a..44e7872 100644
--- a/src/Treasury.App/Application/Accounts/AccountSharingService.cs
+++ b/src/Treasury.App/Application/Accounts/AccountSharingService.cs
@@ -6,23 +6,27 @@ using Treasury.App.Infrastructure.Data;
 namespace Treasury.App.Application.Accounts;
 
 public sealed record HouseholdUserChoice(string Email, string DisplayName);
 public sealed record AccountViewerChoice(string ViewerUserId, string Email, string DisplayName);
 public sealed record AccountViewerAssignment(Guid AccountId, string ViewerUserId, string Email, string DisplayName);
 public sealed record ShareReadOnlyResult(bool Succeeded, string? ErrorMessage);
 
 public class AccountSharingService(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
 {
     public virtual Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct) =>
+        GetVisibleAccountsAsync(user, includeInactive: false, ct);
+
+    public virtual Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, bool includeInactive, CancellationToken ct) =>
         db.Accounts
             .Where(x =>
                 x.HouseholdId == user.HouseholdId
+                && (includeInactive || x.IsActive)
                 && (x.OwnerUserId == user.Id
                     || x.OwnerUserId == "seed"
                     || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
             .OrderBy(x => x.Name)
             .ToListAsync(ct);
 
     public virtual Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
         db.Users
             .Where(x => x.HouseholdId == user.HouseholdId && x.Id != user.Id)
             .OrderBy(x => x.Email ?? x.UserName ?? x.Id)
diff --git a/src/Treasury.App/Contracts/Accounts/AccountResponse.cs b/src/Treasury.App/Contracts/Accounts/AccountResponse.cs
index ad202f7..2fd0ea4 100644
--- a/src/Treasury.App/Contracts/Accounts/AccountResponse.cs
+++ b/src/Treasury.App/Contracts/Accounts/AccountResponse.cs
@@ -1,11 +1,13 @@
 namespace Treasury.App.Contracts.Accounts;
 
 public sealed class AccountResponse
 {
     public Guid Id { get; set; }
     public string Name { get; set; } = string.Empty;
     public string Currency { get; set; } = string.Empty;
     public string AccountType { get; set; } = string.Empty;
+    public bool IsActive { get; set; }
+    public string? BankAccountNumber { get; set; }
     public decimal CurrentBalance { get; set; }
     public bool IsReadOnly { get; set; }
 }
diff --git a/src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs b/src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs
index c8659fa..e26a149 100644
--- a/src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs
+++ b/src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs
@@ -1,8 +1,9 @@
 namespace Treasury.App.Contracts.Accounts;
 
 public sealed class CreateAccountRequest
 {
     public string Name { get; set; } = string.Empty;
     public string Currency { get; set; } = "PLN";
     public string AccountType { get; set; } = "cash-wallet";
+    public string? BankAccountNumber { get; set; }
 }
diff --git a/src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs b/src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs
new file mode 100644
index 0000000..77d1ee7
--- /dev/null
+++ b/src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs
@@ -0,0 +1,6 @@
+namespace Treasury.App.Contracts.Accounts;
+
+public sealed class SetAccountActiveStateRequest
+{
+    public bool IsActive { get; set; }
+}
diff --git a/src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs b/src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs
new file mode 100644
index 0000000..791d7d7
--- /dev/null
+++ b/src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs
@@ -0,0 +1,7 @@
+namespace Treasury.App.Contracts.Accounts;
+
+public sealed class UpdateAccountRequest
+{
+    public string Name { get; set; } = string.Empty;
+    public string? BankAccountNumber { get; set; }
+}
diff --git a/src/Treasury.App/Domain/Account.cs b/src/Treasury.App/Domain/Account.cs
index 83dde01..6e9a7d4 100644
--- a/src/Treasury.App/Domain/Account.cs
+++ b/src/Treasury.App/Domain/Account.cs
@@ -1,16 +1,18 @@
 namespace Treasury.App.Domain;
 
 public class Account
 {
     public Guid Id { get; set; } = Guid.NewGuid();
     public Guid HouseholdId { get; set; }
     public string OwnerUserId { get; set; } = string.Empty;
     public string Name { get; set; } = string.Empty;
     public string Currency { get; set; } = "PLN";
     public string AccountType { get; set; } = "cash-wallet";
+    public bool IsActive { get; set; } = true;
+    public string? BankAccountNumber { get; set; }
     public decimal CurrentBalance { get; set; }
     public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
     public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
 
     public ICollection<VisibilityRule> VisibilityRules { get; set; } = new List<VisibilityRule>();
 }
diff --git a/src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs
index 543cf2e..16e2d62 100644
--- a/src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs
@@ -1,13 +1,14 @@
 using FastEndpoints;
 using Microsoft.AspNetCore.Identity;
 using Microsoft.EntityFrameworkCore;
+using Treasury.App.Application.Accounts;
 using Treasury.App.Contracts.Accounts;
 using Treasury.App.Domain;
 using Treasury.App.Infrastructure.Data;
 
 namespace Treasury.App.Endpoints.Accounts;
 
 public sealed class CreateAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
     : Endpoint<CreateAccountRequest>
 {
     public override void Configure()
@@ -34,36 +35,46 @@ public sealed class CreateAccountEndpoint(TreasuryDbContext db, UserManager<Appl
 
         var accountType = string.IsNullOrWhiteSpace(request.AccountType) ? "cash-wallet" : request.AccountType.Trim().ToLowerInvariant();
         var isKnownType = await db.AccountTypes.AnyAsync(x => x.HouseholdId == user.HouseholdId && x.Name == accountType, ct);
         if (!isKnownType)
         {
             AddError(x => x.AccountType, "Unknown account type for this household.");
             await SendErrorsAsync(cancellation: ct);
             return;
         }
 
+        if (!AccountLifecycleValidation.TryNormalizeOptionalBankAccountNumber(request.BankAccountNumber, out var bankAccountNumber, out var bankAccountNumberError))
+        {
+            AddError(x => x.BankAccountNumber, bankAccountNumberError!);
+            await SendErrorsAsync(cancellation: ct);
+            return;
+        }
+
         var account = new Account
         {
             HouseholdId = user.HouseholdId,
             OwnerUserId = user.Id,
             Name = request.Name.Trim(),
             Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
             AccountType = accountType,
+            BankAccountNumber = bankAccountNumber,
             CurrentBalance = 0m
         };
 
         db.Accounts.Add(account);
         await db.SaveChangesAsync(ct);
 
         var response = new AccountResponse
         {
             Id = account.Id,
             Name = account.Name,
             Currency = account.Currency,
             AccountType = account.AccountType,
+            IsActive = account.IsActive,
+            BankAccountNumber = account.BankAccountNumber,
             CurrentBalance = account.CurrentBalance,
             IsReadOnly = false
         };
 
         await SendAsync(response, StatusCodes.Status201Created, ct);
     }
 }
diff --git a/src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs
new file mode 100644
index 0000000..277cf58
--- /dev/null
+++ b/src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs
@@ -0,0 +1,57 @@
+using FastEndpoints;
+using Microsoft.AspNetCore.Identity;
+using Microsoft.EntityFrameworkCore;
+using Treasury.App.Domain;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.App.Endpoints.Accounts;
+
+public sealed class DeleteAccountRouteRequest
+{
+    public Guid Id { get; set; }
+}
+
+public sealed class DeleteAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+    : Endpoint<DeleteAccountRouteRequest>
+{
+    public override void Configure()
+    {
+        Delete("/api/accounts/{id:guid}");
+        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
+    }
+
+    public override async Task HandleAsync(DeleteAccountRouteRequest request, CancellationToken ct)
+    {
+        var user = await userManager.GetUserAsync(User);
+        if (user is null)
+        {
+            await SendUnauthorizedAsync(ct);
+            return;
+        }
+
+        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
+        if (account is null)
+        {
+            await SendNotFoundAsync(ct);
+            return;
+        }
+
+        if (account.OwnerUserId != user.Id)
+        {
+            await SendForbiddenAsync(ct);
+            return;
+        }
+
+        var hasTransactions = await db.Transactions.AnyAsync(x => x.AccountId == account.Id, ct);
+        if (hasTransactions && account.CurrentBalance != 0m)
+        {
+            AddError("Cannot remove account with transaction history and non-zero balance. Deactivate it instead.");
+            await SendErrorsAsync(cancellation: ct);
+            return;
+        }
+
+        db.Accounts.Remove(account);
+        await db.SaveChangesAsync(ct);
+        await SendNoContentAsync(ct);
+    }
+}
diff --git a/src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs
index 46eb0e1..f773fa5 100644
--- a/src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs
@@ -1,47 +1,55 @@
 using FastEndpoints;
 using Microsoft.AspNetCore.Identity;
 using Microsoft.EntityFrameworkCore;
 using Treasury.App.Contracts.Accounts;
 using Treasury.App.Domain;
 using Treasury.App.Infrastructure.Data;
 
 namespace Treasury.App.Endpoints.Accounts;
 
-public sealed class GetAccountsEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : EndpointWithoutRequest
+public sealed class GetAccountsRequest
+{
+    public bool IncludeInactive { get; set; }
+}
+
+public sealed class GetAccountsEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : Endpoint<GetAccountsRequest>
 {
     public override void Configure()
     {
         Get("/api/accounts");
         Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
     }
 
-    public override async Task HandleAsync(CancellationToken ct)
+    public override async Task HandleAsync(GetAccountsRequest request, CancellationToken ct)
     {
         var user = await userManager.GetUserAsync(User);
         if (user is null)
         {
             await SendUnauthorizedAsync(ct);
             return;
         }
 
         var accounts = await db.Accounts
             .Where(x =>
                 x.HouseholdId == user.HouseholdId
+                && (request.IncludeInactive || x.IsActive)
                 && (x.OwnerUserId == user.Id
                     || x.OwnerUserId == "seed"
                     || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
             .OrderBy(x => x.Name)
             .Select(x => new AccountResponse
             {
                 Id = x.Id,
                 Name = x.Name,
                 Currency = x.Currency,
                 AccountType = x.AccountType,
+                IsActive = x.IsActive,
+                BankAccountNumber = x.BankAccountNumber,
                 CurrentBalance = x.CurrentBalance,
                 IsReadOnly = x.OwnerUserId != user.Id
             })
             .ToListAsync(ct);
 
         await SendOkAsync(accounts, ct);
     }
 }
diff --git a/src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs
new file mode 100644
index 0000000..1ec69d0
--- /dev/null
+++ b/src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs
@@ -0,0 +1,57 @@
+using FastEndpoints;
+using Microsoft.AspNetCore.Identity;
+using Microsoft.EntityFrameworkCore;
+using Treasury.App.Contracts.Accounts;
+using Treasury.App.Domain;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.App.Endpoints.Accounts;
+
+public sealed class SetAccountActiveStateRouteRequest
+{
+    public Guid Id { get; set; }
+    public bool IsActive { get; set; }
+}
+
+public sealed class SetAccountActiveStateEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+    : Endpoint<SetAccountActiveStateRouteRequest>
+{
+    public override void Configure()
+    {
+        Put("/api/accounts/{id:guid}/active-state");
+        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
+    }
+
+    public override async Task HandleAsync(SetAccountActiveStateRouteRequest request, CancellationToken ct)
+    {
+        var user = await userManager.GetUserAsync(User);
+        if (user is null)
+        {
+            await SendUnauthorizedAsync(ct);
+            return;
+        }
+
+        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
+        if (account is null)
+        {
+            await SendNotFoundAsync(ct);
+            return;
+        }
+
+        if (account.OwnerUserId != user.Id)
+        {
+            await SendForbiddenAsync(ct);
+            return;
+        }
+
+        account.IsActive = request.IsActive;
+        account.UpdatedAt = DateTime.UtcNow;
+        await db.SaveChangesAsync(ct);
+
+        await SendOkAsync(new
+        {
+            account.Id,
+            account.IsActive
+        }, ct);
+    }
+}
diff --git a/src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs
new file mode 100644
index 0000000..c6175d0
--- /dev/null
+++ b/src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs
@@ -0,0 +1,80 @@
+using FastEndpoints;
+using Microsoft.AspNetCore.Identity;
+using Microsoft.EntityFrameworkCore;
+using Treasury.App.Application.Accounts;
+using Treasury.App.Domain;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.App.Endpoints.Accounts;
+
+public sealed class UpdateAccountRouteRequest
+{
+    public Guid Id { get; set; }
+    public string Name { get; set; } = string.Empty;
+    public string? BankAccountNumber { get; set; }
+}
+
+public sealed class UpdateAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+    : Endpoint<UpdateAccountRouteRequest>
+{
+    public override void Configure()
+    {
+        Put("/api/accounts/{id:guid}");
+        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
+    }
+
+    public override async Task HandleAsync(UpdateAccountRouteRequest request, CancellationToken ct)
+    {
+        var user = await userManager.GetUserAsync(User);
+        if (user is null)
+        {
+            await SendUnauthorizedAsync(ct);
+            return;
+        }
+
+        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
+        if (account is null)
+        {
+            await SendNotFoundAsync(ct);
+            return;
+        }
+
+        if (account.OwnerUserId != user.Id)
+        {
+            await SendForbiddenAsync(ct);
+            return;
+        }
+
+        var hasErrors = false;
+        if (string.IsNullOrWhiteSpace(request.Name))
+        {
+            AddError(x => x.Name, "Account name is required.");
+            hasErrors = true;
+        }
+
+        if (!AccountLifecycleValidation.TryNormalizeOptionalBankAccountNumber(request.BankAccountNumber, out var bankAccountNumber, out var bankAccountNumberError))
+        {
+            AddError(x => x.BankAccountNumber, bankAccountNumberError!);
+            hasErrors = true;
+        }
+
+        if (hasErrors)
+        {
+            await SendErrorsAsync(cancellation: ct);
+            return;
+        }
+
+        account.Name = request.Name.Trim();
+        account.BankAccountNumber = bankAccountNumber;
+        account.UpdatedAt = DateTime.UtcNow;
+
+        await db.SaveChangesAsync(ct);
+
+        await SendOkAsync(new
+        {
+            account.Id,
+            account.Name,
+            account.BankAccountNumber
+        }, ct);
+    }
+}
diff --git a/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs b/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs
index 961f8f9..12cdc21 100644
--- a/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs
@@ -47,20 +47,28 @@ public sealed class CreateTransactionEndpoint(TreasuryDbContext db, UserManager<
             await SendNotFoundAsync(ct);
             return;
         }
 
         if (account.OwnerUserId != user.Id)
         {
             await SendForbiddenAsync(ct);
             return;
         }
 
+        // Block creating transactions on inactive accounts
+        if (!account.IsActive)
+        {
+            AddError(x => x.AccountId, "Cannot create transactions on an inactive account.");
+            await SendErrorsAsync(cancellation: ct);
+            return;
+        }
+
         var normalizedType = (request.Type ?? "expense").Trim().ToLowerInvariant();
         var delta = request.Amount;
         if (normalizedType == "expense")
         {
             delta = -Math.Abs(request.Amount);
         }
         else if (normalizedType == "income")
         {
             delta = Math.Abs(request.Amount);
         }
diff --git a/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs b/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs
index b33048a..dca5eff 100644
--- a/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs
@@ -53,20 +53,37 @@ public sealed class CreateTransferEndpoint(TreasuryDbContext db, UserManager<App
             await SendNotFoundAsync(ct);
             return;
         }
 
         if (fromAccount.OwnerUserId != user.Id || toAccount.OwnerUserId != user.Id)
         {
             await SendForbiddenAsync(ct);
             return;
         }
 
+        // Block transfers involving inactive accounts
+        if (!fromAccount.IsActive || !toAccount.IsActive)
+        {
+            if (!fromAccount.IsActive)
+            {
+                AddError(x => x.FromAccountId, "Source account is inactive.");
+            }
+
+            if (!toAccount.IsActive)
+            {
+                AddError(x => x.ToAccountId, "Destination account is inactive.");
+            }
+
+            await SendErrorsAsync(cancellation: ct);
+            return;
+        }
+
         var transferDate = request.TransferDate == default ? DateTime.UtcNow : request.TransferDate;
         var currency = string.IsNullOrWhiteSpace(request.Currency) ? fromAccount.Currency : request.Currency.Trim().ToUpperInvariant();
         var description = string.IsNullOrWhiteSpace(request.Description) ? "Account transfer" : request.Description.Trim();
 
         var transfer = new Transfer
         {
             HouseholdId = user.HouseholdId,
             FromAccountId = fromAccount.Id,
             ToAccountId = toAccount.Id,
             Amount = request.Amount,
diff --git a/src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs b/src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs
index 36de716..20a1ec0 100644
--- a/src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs
+++ b/src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs
@@ -46,20 +46,28 @@ public class TreasuryDbContext : IdentityDbContext<ApplicationUser>
 
         modelBuilder.Entity<TransactionTag>()
             .HasOne(x => x.Transaction)
             .WithMany(x => x.TransactionTags)
             .HasForeignKey(x => x.TransactionId);
 
         modelBuilder.Entity<TransactionTag>()
             .HasOne(x => x.Tag)
             .WithMany(x => x.TransactionTags)
             .HasForeignKey(x => x.TagId);
+
+        modelBuilder.Entity<Account>()
+            .Property(x => x.IsActive)
+            .HasDefaultValue(true);
+
+        modelBuilder.Entity<Account>()
+            .Property(x => x.BankAccountNumber)
+            .HasMaxLength(34);
     }
 
     public DbSet<Household> Households => Set<Household>();
     public DbSet<AccountTypeDefinition> AccountTypes => Set<AccountTypeDefinition>();
     public DbSet<Account> Accounts => Set<Account>();
     public DbSet<VisibilityRule> VisibilityRules => Set<VisibilityRule>();
     public DbSet<Transfer> Transfers => Set<Transfer>();
     public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();
     public DbSet<AssetValuation> AssetValuations => Set<AssetValuation>();
     public DbSet<Transaction> Transactions => Set<Transaction>();
diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.Designer.cs b/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.Designer.cs
new file mode 100644
index 0000000..5d8ec06
--- /dev/null
+++ b/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.Designer.cs
@@ -0,0 +1,738 @@
+﻿// <auto-generated />
+using System;
+using Microsoft.EntityFrameworkCore;
+using Microsoft.EntityFrameworkCore.Infrastructure;
+using Microsoft.EntityFrameworkCore.Migrations;
+using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
+using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
+using Treasury.App.Infrastructure.Data;
+
+#nullable disable
+
+namespace Treasury.App.Infrastructure.Migrations
+{
+    [DbContext(typeof(TreasuryDbContext))]
+    [Migration("20260903122916_AddAccountLifecycleFields")]
+    partial class AddAccountLifecycleFields
+    {
+        /// <inheritdoc />
+        protected override void BuildTargetModel(ModelBuilder modelBuilder)
+        {
+#pragma warning disable 612, 618
+            modelBuilder
+                .HasAnnotation("ProductVersion", "9.0.0")
+                .HasAnnotation("Relational:MaxIdentifierLength", 63);
+
+            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
+                {
+                    b.Property<string>("Id")
+                        .HasColumnType("text");
+
+                    b.Property<string>("ConcurrencyStamp")
+                        .IsConcurrencyToken()
+                        .HasColumnType("text");
+
+                    b.Property<string>("Name")
+                        .HasMaxLength(256)
+                        .HasColumnType("character varying(256)");
+
+                    b.Property<string>("NormalizedName")
+                        .HasMaxLength(256)
+                        .HasColumnType("character varying(256)");
+
+                    b.HasKey("Id");
+
+                    b.HasIndex("NormalizedName")
+                        .IsUnique()
+                        .HasDatabaseName("RoleNameIndex");
+
+                    b.ToTable("AspNetRoles", (string)null);
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
+                {
+                    b.Property<int>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("integer");
+
+                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));
+
+                    b.Property<string>("ClaimType")
+                        .HasColumnType("text");
+
+                    b.Property<string>("ClaimValue")
+                        .HasColumnType("text");
+
+                    b.Property<string>("RoleId")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.HasKey("Id");
+
+                    b.HasIndex("RoleId");
+
+                    b.ToTable("AspNetRoleClaims", (string)null);
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
+                {
+                    b.Property<int>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("integer");
+
+                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));
+
+                    b.Property<string>("ClaimType")
+                        .HasColumnType("text");
+
+                    b.Property<string>("ClaimValue")
+                        .HasColumnType("text");
+
+                    b.Property<string>("UserId")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.HasKey("Id");
+
+                    b.HasIndex("UserId");
+
+                    b.ToTable("AspNetUserClaims", (string)null);
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
+                {
+                    b.Property<string>("LoginProvider")
+                        .HasColumnType("text");
+
+                    b.Property<string>("ProviderKey")
+                        .HasColumnType("text");
+
+                    b.Property<string>("ProviderDisplayName")
+                        .HasColumnType("text");
+
+                    b.Property<string>("UserId")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.HasKey("LoginProvider", "ProviderKey");
+
+                    b.HasIndex("UserId");
+
+                    b.ToTable("AspNetUserLogins", (string)null);
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
+                {
+                    b.Property<string>("UserId")
+                        .HasColumnType("text");
+
+                    b.Property<string>("RoleId")
+                        .HasColumnType("text");
+
+                    b.HasKey("UserId", "RoleId");
+
+                    b.HasIndex("RoleId");
+
+                    b.ToTable("AspNetUserRoles", (string)null);
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
+                {
+                    b.Property<string>("UserId")
+                        .HasColumnType("text");
+
+                    b.Property<string>("LoginProvider")
+                        .HasColumnType("text");
+
+                    b.Property<string>("Name")
+                        .HasColumnType("text");
+
+                    b.Property<string>("Value")
+                        .HasColumnType("text");
+
+                    b.HasKey("UserId", "LoginProvider", "Name");
+
+                    b.ToTable("AspNetUserTokens", (string)null);
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Account", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<string>("AccountType")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<string>("BankAccountNumber")
+                        .HasMaxLength(34)
+                        .HasColumnType("character varying(34)");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Currency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<decimal>("CurrentBalance")
+                        .HasColumnType("numeric");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<bool>("IsActive")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("boolean")
+                        .HasDefaultValue(true);
+
+                    b.Property<string>("Name")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<string>("OwnerUserId")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("Accounts");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.AccountTypeDefinition", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Description")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<string>("Name")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("AccountTypes");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.ApplicationUser", b =>
+                {
+                    b.Property<string>("Id")
+                        .HasColumnType("text");
+
+                    b.Property<int>("AccessFailedCount")
+                        .HasColumnType("integer");
+
+                    b.Property<string>("ConcurrencyStamp")
+                        .IsConcurrencyToken()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Email")
+                        .HasMaxLength(256)
+                        .HasColumnType("character varying(256)");
+
+                    b.Property<bool>("EmailConfirmed")
+                        .HasColumnType("boolean");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<bool>("LockoutEnabled")
+                        .HasColumnType("boolean");
+
+                    b.Property<DateTimeOffset?>("LockoutEnd")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("NormalizedEmail")
+                        .HasMaxLength(256)
+                        .HasColumnType("character varying(256)");
+
+                    b.Property<string>("NormalizedUserName")
+                        .HasMaxLength(256)
+                        .HasColumnType("character varying(256)");
+
+                    b.Property<string>("PasswordHash")
+                        .HasColumnType("text");
+
+                    b.Property<string>("PhoneNumber")
+                        .HasColumnType("text");
+
+                    b.Property<bool>("PhoneNumberConfirmed")
+                        .HasColumnType("boolean");
+
+                    b.Property<string>("SecurityStamp")
+                        .HasColumnType("text");
+
+                    b.Property<bool>("TwoFactorEnabled")
+                        .HasColumnType("boolean");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("UserName")
+                        .HasMaxLength(256)
+                        .HasColumnType("character varying(256)");
+
+                    b.HasKey("Id");
+
+                    b.HasIndex("NormalizedEmail")
+                        .HasDatabaseName("EmailIndex");
+
+                    b.HasIndex("NormalizedUserName")
+                        .IsUnique()
+                        .HasDatabaseName("UserNameIndex");
+
+                    b.ToTable("AspNetUsers", (string)null);
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.AssetValuation", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<string>("AssetName")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Currency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<decimal>("CurrentTotalValue")
+                        .HasColumnType("numeric");
+
+                    b.Property<decimal>("CurrentUnitValue")
+                        .HasColumnType("numeric");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<int>("Kind")
+                        .HasColumnType("integer");
+
+                    b.Property<decimal>("Purity")
+                        .HasColumnType("numeric");
+
+                    b.Property<decimal>("Quantity")
+                        .HasColumnType("numeric");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<DateTime>("ValuationDate")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<decimal>("Weight")
+                        .HasColumnType("numeric");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("AssetValuations");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Bill", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<decimal>("Amount")
+                        .HasColumnType("numeric");
+
+                    b.Property<string>("Category")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Currency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<int>("DueDay")
+                        .HasColumnType("integer");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<bool>("IsPaid")
+                        .HasColumnType("boolean");
+
+                    b.Property<string>("Name")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<string>("Notes")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("Bills");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.BudgetCategory", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Currency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<decimal>("MonthlyLimit")
+                        .HasColumnType("numeric");
+
+                    b.Property<string>("Name")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<string>("Notes")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("BudgetCategories");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.CurrencyRate", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<DateTime>("EffectiveAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("FromCurrency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<decimal>("Rate")
+                        .HasColumnType("numeric");
+
+                    b.Property<string>("ToCurrency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.HasIndex("HouseholdId", "FromCurrency", "ToCurrency")
+                        .IsUnique();
+
+                    b.ToTable("CurrencyRates");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Household", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Name")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("Households");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Tag", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<string>("Color")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<string>("Name")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("Tags");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Transaction", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid>("AccountId")
+                        .HasColumnType("uuid");
+
+                    b.Property<decimal>("Amount")
+                        .HasColumnType("numeric");
+
+                    b.Property<string>("Category")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Currency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<string>("Description")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<DateTime>("TransactionDate")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Type")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("Transactions");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.TransactionTag", b =>
+                {
+                    b.Property<Guid>("TransactionId")
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid>("TagId")
+                        .HasColumnType("uuid");
+
+                    b.HasKey("TransactionId", "TagId");
+
+                    b.HasIndex("TagId");
+
+                    b.ToTable("TransactionTags");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Transfer", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<decimal>("Amount")
+                        .HasColumnType("numeric");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Currency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<string>("Description")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<Guid>("FromAccountId")
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid>("ToAccountId")
+                        .HasColumnType("uuid");
+
+                    b.Property<DateTime>("TransferDate")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("Transfers");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.VisibilityRule", b =>
+                {
+                    b.Property<Guid>("AccountId")
+                        .HasColumnType("uuid");
+
+                    b.Property<string>("ViewerUserId")
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<bool>("IsReadOnly")
+                        .HasColumnType("boolean");
+
+                    b.HasKey("AccountId", "ViewerUserId");
+
+                    b.ToTable("VisibilityRules");
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
+                {
+                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
+                        .WithMany()
+                        .HasForeignKey("RoleId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
+                {
+                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
+                        .WithMany()
+                        .HasForeignKey("UserId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
+                {
+                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
+                        .WithMany()
+                        .HasForeignKey("UserId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
+                {
+                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
+                        .WithMany()
+                        .HasForeignKey("RoleId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+
+                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
+                        .WithMany()
+                        .HasForeignKey("UserId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
+                {
+                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
+                        .WithMany()
+                        .HasForeignKey("UserId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.TransactionTag", b =>
+                {
+                    b.HasOne("Treasury.App.Domain.Tag", "Tag")
+                        .WithMany("TransactionTags")
+                        .HasForeignKey("TagId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+
+                    b.HasOne("Treasury.App.Domain.Transaction", "Transaction")
+                        .WithMany("TransactionTags")
+                        .HasForeignKey("TransactionId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+
+                    b.Navigation("Tag");
+
+                    b.Navigation("Transaction");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.VisibilityRule", b =>
+                {
+                    b.HasOne("Treasury.App.Domain.Account", "Account")
+                        .WithMany("VisibilityRules")
+                        .HasForeignKey("AccountId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+
+                    b.Navigation("Account");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Account", b =>
+                {
+                    b.Navigation("VisibilityRules");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Tag", b =>
+                {
+                    b.Navigation("TransactionTags");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Transaction", b =>
+                {
+                    b.Navigation("TransactionTags");
+                });
+#pragma warning restore 612, 618
+        }
+    }
+}
diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.cs b/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.cs
new file mode 100644
index 0000000..98fe170
--- /dev/null
+++ b/src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.cs
@@ -0,0 +1,40 @@
+﻿using Microsoft.EntityFrameworkCore.Migrations;
+
+#nullable disable
+
+namespace Treasury.App.Infrastructure.Migrations
+{
+    /// <inheritdoc />
+    public partial class AddAccountLifecycleFields : Migration
+    {
+        /// <inheritdoc />
+        protected override void Up(MigrationBuilder migrationBuilder)
+        {
+            migrationBuilder.AddColumn<string>(
+                name: "BankAccountNumber",
+                table: "Accounts",
+                type: "character varying(34)",
+                maxLength: 34,
+                nullable: true);
+
+            migrationBuilder.AddColumn<bool>(
+                name: "IsActive",
+                table: "Accounts",
+                type: "boolean",
+                nullable: false,
+                defaultValue: true);
+        }
+
+        /// <inheritdoc />
+        protected override void Down(MigrationBuilder migrationBuilder)
+        {
+            migrationBuilder.DropColumn(
+                name: "BankAccountNumber",
+                table: "Accounts");
+
+            migrationBuilder.DropColumn(
+                name: "IsActive",
+                table: "Accounts");
+        }
+    }
+}
diff --git a/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs b/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
index 2037f1f..a9b083a 100644
--- a/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
+++ b/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
@@ -157,33 +157,42 @@ namespace Treasury.App.Infrastructure.Migrations
             modelBuilder.Entity("Treasury.App.Domain.Account", b =>
                 {
                     b.Property<Guid>("Id")
                         .ValueGeneratedOnAdd()
                         .HasColumnType("uuid");
 
                     b.Property<string>("AccountType")
                         .IsRequired()
                         .HasColumnType("text");
 
+                    b.Property<string>("BankAccountNumber")
+                        .HasMaxLength(34)
+                        .HasColumnType("character varying(34)");
+
                     b.Property<DateTime>("CreatedAt")
                         .HasColumnType("timestamp with time zone");
 
                     b.Property<string>("Currency")
                         .IsRequired()
                         .HasColumnType("text");
 
                     b.Property<decimal>("CurrentBalance")
                         .HasColumnType("numeric");
 
                     b.Property<Guid>("HouseholdId")
                         .HasColumnType("uuid");
 
+                    b.Property<bool>("IsActive")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("boolean")
+                        .HasDefaultValue(true);
+
                     b.Property<string>("Name")
                         .IsRequired()
                         .HasColumnType("text");
 
                     b.Property<string>("OwnerUserId")
                         .IsRequired()
                         .HasColumnType("text");
 
                     b.Property<DateTime>("UpdatedAt")
                         .HasColumnType("timestamp with time zone");
diff --git a/src/Treasury.App/Pages/Accounts.razor b/src/Treasury.App/Pages/Accounts.razor
index f6e1603..5cf5477 100644
--- a/src/Treasury.App/Pages/Accounts.razor
+++ b/src/Treasury.App/Pages/Accounts.razor
@@ -1,37 +1,43 @@
 @page "/accounts"
 @attribute [Authorize]
 
 <MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="pa-4 pa-sm-6">
     <MudStack Spacing="3">
         <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
             <MudText Typo="Typo.h4">Accounts</MudText>
-            <MudButton Variant="Variant.Filled" Color="Color.Tertiary" StartIcon="@Icons.Material.Filled.Add" OnClick="ToggleCreateForm">Add account</MudButton>
+            <MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">
+                <MudSwitch T="bool" Label="Show inactive" checked="_showInactive" checkedChanged="OnShowInactiveChanged" />
+                <MudButton Variant="Variant.Filled" Color="Color.Tertiary" StartIcon="@Icons.Material.Filled.Add" OnClick="ToggleCreateForm">Add account</MudButton>
+            </MudStack>
         </MudStack>
 
         @if (!string.IsNullOrWhiteSpace(_accountsLoadError))
         {
             <MudAlert Severity="Severity.Error">@_accountsLoadError</MudAlert>
         }
 
         @if (!string.IsNullOrWhiteSpace(_householdUsersLoadError))
         {
             <MudAlert Severity="Severity.Error">@_householdUsersLoadError</MudAlert>
         }
 
         @if (_showCreateForm)
         {
             <MudPaper Class="pa-4 rounded-xl" Elevation="2">
                 <MudGrid>
                     <MudItem xs="12" md="4">
                         <MudTextField Label="Account name" @bind-Value="_newAccount.Name" Required="true" />
                     </MudItem>
+                    <MudItem xs="12" md="4">
+                        <MudTextField Label="Bank account number (optional)" @bind-Value="_newAccount.BankAccountNumber" />
+                    </MudItem>
                     <MudItem xs="12" md="3">
                         <MudSelect T="string" Label="Currency" @bind-Value="_newAccount.Currency">
                             <MudSelectItem Value="@("PLN")">PLN</MudSelectItem>
                             <MudSelectItem Value="@("EUR")">EUR</MudSelectItem>
                             <MudSelectItem Value="@("USD")">USD</MudSelectItem>
                             <MudSelectItem Value="@("GBP")">GBP</MudSelectItem>
                         </MudSelect>
                     </MudItem>
                     <MudItem xs="12" md="3">
                         <MudSelect T="string" Label="Type" @bind-Value="_newAccount.AccountType">
@@ -59,25 +65,42 @@
         }
         else
         {
             <MudGrid>
                 @foreach (var account in _accounts)
                 {
                     <MudItem xs="12" md="6" lg="4">
                         <MudPaper Class="pa-4 rounded-xl" Elevation="2">
                             <MudText Typo="Typo.h6">@account.Name</MudText>
                             <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mt-1">@account.AccountType</MudText>
+                            @if (!string.IsNullOrWhiteSpace(account.BankAccountNumber))
+                            {
+                                <MudText Typo="Typo.body2" Color="Color.Secondary">Bank account: @account.BankAccountNumber</MudText>
+                            }
+                            @if (!account.IsActive)
+                            {
+                                <MudChip Class="mt-2" T="string" Color="Color.Warning" Variant="Variant.Outlined">Inactive</MudChip>
+                            }
                             <MudDivider Class="my-3" />
                             <MudText Typo="Typo.h5">@account.CurrentBalance.ToString("N2") @account.Currency</MudText>
 
                             @if (account.IsOwner)
                             {
+                                <MudStack Row="true" Class="mt-4" Spacing="2" Wrap="Wrap.Wrap">
+                                    <MudButton Variant="Variant.Outlined" OnClick="() => ToggleActiveStateAsync(account)">
+                                        @(account.IsActive ? "Deactivate" : "Reactivate")
+                                    </MudButton>
+                                    <MudButton Color="Color.Error" Variant="Variant.Outlined" OnClick="() => DeleteAccountAsync(account)">
+                                        Remove
+                                    </MudButton>
+                                </MudStack>
+
                                 <MudDivider Class="my-3" />
                                 <MudText Typo="Typo.subtitle2">Shared with:</MudText>
                                 @if (account.SharedWithLoadFailed)
                                 {
                                     <MudText Typo="Typo.body2" Color="Color.Secondary">Shared viewer details unavailable.</MudText>
                                 }
                                 else if (account.SharedWith.Count == 0)
                                 {
                                     <MudText Typo="Typo.body2" Color="Color.Secondary">Nobody yet.</MudText>
                                 }
diff --git a/src/Treasury.App/Pages/Accounts.razor.cs b/src/Treasury.App/Pages/Accounts.razor.cs
index 7094576..b10bef4 100644
--- a/src/Treasury.App/Pages/Accounts.razor.cs
+++ b/src/Treasury.App/Pages/Accounts.razor.cs
@@ -16,20 +16,21 @@ public partial class Accounts
     [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
     [Inject] public UserManager<ApplicationUser> UserManager { get; set; } = default!;
     [Inject] public AccountSharingService AccountSharingService { get; set; } = default!;
 
     private ApplicationUser? _currentUser;
     private readonly List<AccountCardVm> _accounts = new();
     private List<HouseholdUserChoice> _householdUsers = new();
     private string? _accountsLoadError;
     private string? _householdUsersLoadError;
     private bool _showCreateForm;
+    private bool _showInactive;
     private readonly NewAccountForm _newAccount = new();
 
     protected override async Task OnInitializedAsync()
     {
         _accountsLoadError = null;
         _householdUsersLoadError = null;
 
         var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
         _currentUser = await UserManager.GetUserAsync(authState.User);
         if (_currentUser is null)
@@ -48,21 +49,23 @@ public partial class Accounts
         if (_currentUser is null)
         {
             _accounts.Clear();
             _accountsLoadError = null;
             return;
         }
 
         try
         {
             _accountsLoadError = null;
-            var accounts = await AccountSharingService.GetVisibleAccountsAsync(_currentUser, CancellationToken.None);
+            var accounts = _showInactive
+                ? await AccountSharingService.GetVisibleAccountsAsync(_currentUser, includeInactive: true, CancellationToken.None)
+                : await AccountSharingService.GetVisibleAccountsAsync(_currentUser, CancellationToken.None);
             Dictionary<Guid, List<AccountViewerChoice>> viewersByAccountId;
             var sharedViewerLoadFailed = false;
 
             try
             {
                 var sharedViewers = await AccountSharingService.GetSharedViewersAsync(
                     _currentUser,
                     accounts.Select(account => account.Id).ToArray(),
                     CancellationToken.None);
 
@@ -81,20 +84,22 @@ public partial class Accounts
             }
 
             _accounts.Clear();
             _accounts.AddRange(accounts.Select(account => new AccountCardVm
             {
                 Id = account.Id,
                 Name = account.Name,
                 Currency = account.Currency,
                 AccountType = account.AccountType,
                 CurrentBalance = account.CurrentBalance,
+                IsActive = account.IsActive,
+                BankAccountNumber = account.BankAccountNumber,
                 IsOwner = account.OwnerUserId == _currentUser.Id,
                 SharedWith = viewersByAccountId.TryGetValue(account.Id, out var viewers) ? viewers : new List<AccountViewerChoice>(),
                 SharedWithLoadFailed = sharedViewerLoadFailed
             }));
         }
         catch (Exception ex)
         {
             _accounts.Clear();
             _accountsLoadError = "Unable to load accounts.";
             Snackbar.Add($"Unable to load accounts: {ex.Message}", Severity.Error);
@@ -125,41 +130,105 @@ public partial class Accounts
 
     private void ToggleCreateForm()
     {
         _showCreateForm = !_showCreateForm;
         if (!_showCreateForm)
         {
             ResetCreateForm();
         }
     }
 
+    private async Task OnShowInactiveChanged(bool value)
+    {
+        _showInactive = value;
+        await LoadAccountsAsync();
+    }
+
+    private async Task ToggleActiveStateAsync(AccountCardVm account)
+    {
+        if (_currentUser is null || !account.IsOwner)
+        {
+            Snackbar.Add("Only the account owner can change account status.", Severity.Warning);
+            return;
+        }
+
+        var entity = await DbContext.Accounts.SingleOrDefaultAsync(x => x.Id == account.Id && x.HouseholdId == _currentUser.HouseholdId, CancellationToken.None);
+        if (entity is null)
+        {
+            Snackbar.Add("Account not found.", Severity.Error);
+            return;
+        }
+
+        entity.IsActive = !entity.IsActive;
+        entity.UpdatedAt = DateTime.UtcNow;
+        await DbContext.SaveChangesAsync();
+
+        Snackbar.Add(entity.IsActive ? "Account reactivated." : "Account deactivated.", Severity.Success);
+        await LoadAccountsAsync();
+    }
+
+    private async Task DeleteAccountAsync(AccountCardVm account)
+    {
+        if (_currentUser is null || !account.IsOwner)
+        {
+            Snackbar.Add("Only the account owner can remove an account.", Severity.Warning);
+            return;
+        }
+
+        var entity = await DbContext.Accounts.SingleOrDefaultAsync(x => x.Id == account.Id && x.HouseholdId == _currentUser.HouseholdId, CancellationToken.None);
+        if (entity is null)
+        {
+            Snackbar.Add("Account not found.", Severity.Error);
+            return;
+        }
+
+        var hasTransactions = await DbContext.Transactions.AnyAsync(x => x.AccountId == entity.Id, CancellationToken.None);
+        if (hasTransactions && entity.CurrentBalance != 0m)
+        {
+            Snackbar.Add("Cannot remove account with transaction history and non-zero balance. Deactivate it instead.", Severity.Error);
+            return;
+        }
+
+        DbContext.Accounts.Remove(entity);
+        await DbContext.SaveChangesAsync();
+        Snackbar.Add("Account removed.", Severity.Success);
+        await LoadAccountsAsync();
+    }
+
     private async Task CreateAccountAsync()
     {
         if (_currentUser is null)
         {
             Snackbar.Add("Sign in first.", Severity.Warning);
             return;
         }
 
         if (string.IsNullOrWhiteSpace(_newAccount.Name))
         {
             Snackbar.Add("Account name is required.", Severity.Warning);
             return;
         }
 
+        if (!AccountLifecycleValidation.TryNormalizeOptionalBankAccountNumber(_newAccount.BankAccountNumber, out var bankAccountNumber, out var bankAccountNumberError))
+        {
+            Snackbar.Add(bankAccountNumberError!, Severity.Warning);
+            return;
+        }
+
         var account = new Treasury.App.Domain.Account
         {
             HouseholdId = _currentUser.HouseholdId,
             OwnerUserId = _currentUser.Id,
             Name = _newAccount.Name.Trim(),
             Currency = string.IsNullOrWhiteSpace(_newAccount.Currency) ? "PLN" : _newAccount.Currency.Trim().ToUpperInvariant(),
             AccountType = string.IsNullOrWhiteSpace(_newAccount.AccountType) ? "cash-wallet" : _newAccount.AccountType.Trim(),
+            BankAccountNumber = bankAccountNumber,
             CurrentBalance = _newAccount.InitialBalance,
             CreatedAt = DateTime.UtcNow,
             UpdatedAt = DateTime.UtcNow
         };
 
         DbContext.Accounts.Add(account);
         await DbContext.SaveChangesAsync();
 
         _showCreateForm = false;
         ResetCreateForm();
@@ -192,33 +261,37 @@ public partial class Accounts
         Snackbar.Add("Account shared read-only.", Severity.Success);
         await LoadAccountsAsync();
     }
 
     private void ResetCreateForm()
     {
         _newAccount.Name = string.Empty;
         _newAccount.Currency = "PLN";
         _newAccount.AccountType = "cash-wallet";
         _newAccount.InitialBalance = 0m;
+        _newAccount.BankAccountNumber = string.Empty;
     }
 
     private sealed class NewAccountForm
     {
         public string Name { get; set; } = string.Empty;
         public string Currency { get; set; } = "PLN";
         public string AccountType { get; set; } = "cash-wallet";
         public decimal InitialBalance { get; set; }
+        public string? BankAccountNumber { get; set; }
     }
 
     private sealed class AccountCardVm
     {
         public Guid Id { get; set; }
         public string Name { get; set; } = string.Empty;
         public string Currency { get; set; } = string.Empty;
         public string AccountType { get; set; } = string.Empty;
         public decimal CurrentBalance { get; set; }
+        public bool IsActive { get; set; }
         public bool IsOwner { get; set; }
+        public string? BankAccountNumber { get; set; }
         public string SelectedShareEmail { get; set; } = string.Empty;
         public List<AccountViewerChoice> SharedWith { get; set; } = new();
         public bool SharedWithLoadFailed { get; set; }
     }
 }
diff --git a/src/Treasury.App/Pages/Transactions.razor b/src/Treasury.App/Pages/Transactions.razor
index dea65c2..626e971 100644
--- a/src/Treasury.App/Pages/Transactions.razor
+++ b/src/Treasury.App/Pages/Transactions.razor
@@ -3,26 +3,33 @@
 @inject TreasuryDbContext DbContext
 @inject ISnackbar Snackbar
 
 <MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="pa-4 pa-sm-6">
     <MudStack Spacing="3">
         <MudText Typo="Typo.h4">Transactions</MudText>
 
         <MudPaper Class="pa-4 rounded-xl" Elevation="2">
             <MudGrid>
                 <MudItem xs="12" md="3">
-                    <MudSelect T="string" Label="Account" @bind-Value="_newTransaction.AccountId" Required="true">
-                        @foreach (var account in _accounts)
-                        {
-                            <MudSelectItem Value="@account.Id.ToString()">@account.Name (@account.Currency)</MudSelectItem>
-                        }
-                    </MudSelect>
+                    @if (_accounts.Count == 0)
+                    {
+                        <MudText Typo="Typo.body2" Class="mt-2">No active accounts. Create or reactivate an account in the Accounts page.</MudText>
+                    }
+                    else
+                    {
+                        <MudSelect T="string" Label="Account" @bind-Value="_newTransaction.AccountId" Required="true">
+                            @foreach (var account in _accounts)
+                            {
+                                <MudSelectItem Value="@account.Id.ToString()">@account.Name (@account.Currency)</MudSelectItem>
+                            }
+                        </MudSelect>
+                    }
                 </MudItem>
                 <MudItem xs="12" md="3">
                     <MudTextField Label="Description" @bind-Value="_newTransaction.Description" Required="true" />
                 </MudItem>
                 <MudItem xs="12" md="2">
                     <MudTextField Label="Category" @bind-Value="_newTransaction.Category" />
                 </MudItem>
                 <MudItem xs="12" md="2">
                     <MudNumericField Label="Amount" @bind-Value="_newTransaction.Amount" Required="true" Min="0.01m" />
                 </MudItem>
@@ -102,36 +109,43 @@
                         <MudTd DataLabel="Date">@context.TransactionDate.ToLocalTime().ToString("yyyy-MM-dd")</MudTd>
                     </RowTemplate>
                 </MudTable>
             </MudPaper>
         }
     </MudStack>
 </MudContainer>
 
 @code {
     private readonly List<Treasury.App.Domain.Transaction> _transactions = new();
+    // active accounts used for the picker
     private readonly List<Treasury.App.Domain.Account> _accounts = new();
+    // full visible accounts (includes inactive) used for mapping historical transactions to names
+    private readonly List<Treasury.App.Domain.Account> _visibleAccounts = new();
     private readonly List<Treasury.App.Domain.Tag> _tags = new();
     private readonly Dictionary<Guid, List<Treasury.App.Domain.Tag>> _transactionTags = new();
     private readonly HashSet<Guid> _selectedTagIds = new();
 
     private readonly NewTransactionForm _newTransaction = new();
 
     protected override async Task OnInitializedAsync()
     {
         await LoadAsync();
     }
 
     private async Task LoadAsync()
     {
         _accounts.Clear();
-        _accounts.AddRange(await DbContext.Accounts.OrderBy(x => x.Name).ToListAsync());
+        _accounts.AddRange(await DbContext.Accounts.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync());
+
+        // Load full visible account list (includes inactive) so historical transactions still show account names
+        _visibleAccounts.Clear();
+        _visibleAccounts.AddRange(await DbContext.Accounts.OrderBy(x => x.Name).ToListAsync());
 
         _tags.Clear();
         _tags.AddRange(await DbContext.Tags.OrderBy(x => x.Name).ToListAsync());
 
         _transactions.Clear();
         _transactions.AddRange(await DbContext.Transactions.OrderByDescending(x => x.TransactionDate).ToListAsync());
 
         _transactionTags.Clear();
         var tagLinks = await DbContext.TransactionTags
             .Include(x => x.Tag)
@@ -180,20 +194,27 @@
             return;
         }
 
         var account = await DbContext.Accounts.SingleOrDefaultAsync(x => x.Id == accountId);
         if (account is null)
         {
             Snackbar.Add("Selected account could not be found.", Severity.Error);
             return;
         }
 
+        // Prevent creating transactions against inactive accounts via the UI save path
+        if (!account.IsActive)
+        {
+            Snackbar.Add("Selected account is inactive. Reactivate it before adding transactions.", Severity.Warning);
+            return;
+        }
+
         var normalizedType = (_newTransaction.Type ?? "expense").Trim().ToLowerInvariant();
         var delta = _newTransaction.Amount;
         if (normalizedType == "expense")
         {
             delta = -Math.Abs(_newTransaction.Amount);
         }
         else if (normalizedType == "income")
         {
             delta = Math.Abs(_newTransaction.Amount);
         }
@@ -243,21 +264,22 @@
         _newTransaction.Type = "expense";
         _newTransaction.TransactionDate = DateTime.UtcNow;
         _selectedTagIds.Clear();
 
         Snackbar.Add("Transaction saved.", Severity.Success);
         await LoadAsync();
     }
 
     private string GetAccountName(Guid accountId)
     {
-        return _accounts.FirstOrDefault(x => x.Id == accountId)?.Name ?? "Unknown account";
+        // Use the full visible accounts collection so historical transactions from inactive accounts still show a name
+        return _visibleAccounts.FirstOrDefault(x => x.Id == accountId)?.Name ?? "Unknown account";
     }
 
     private sealed class NewTransactionForm
     {
         public string AccountId { get; set; } = string.Empty;
         public string Description { get; set; } = string.Empty;
         public string Category { get; set; } = "General";
         public decimal Amount { get; set; }
         public string Type { get; set; } = "expense";
         public DateTime? TransactionDate { get; set; } = DateTime.UtcNow;
diff --git a/tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs b/tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
new file mode 100644
index 0000000..060dc5d
--- /dev/null
+++ b/tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
@@ -0,0 +1,239 @@
+using System.Net;
+using System.Net.Http.Json;
+using System.Text.Json;
+using FluentAssertions;
+using Microsoft.AspNetCore.Mvc.Testing;
+
+namespace Treasury.IntegrationTests;
+
+public class AccountsLifecycleTests
+{
+    [Fact]
+    public async Task Delete_Allows_Account_With_Zero_Balance()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, name: "Delete me", bankAccountNumber: "1234567890123456");
+
+        var deleteResponse = await client.DeleteAsync($"/api/accounts/{accountId}");
+        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
+
+        using var accounts = await GetAccountsAsync(client);
+        accounts.RootElement.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == accountId);
+    }
+
+    [Fact]
+    public async Task Delete_Blocks_Account_With_Transactions_And_NonZero_Balance()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, name: "Busy account", bankAccountNumber: "1234567890123456");
+
+        var transactionResponse = await client.PostAsJsonAsync("/api/transactions", new
+        {
+            AccountId = accountId,
+            Description = "Card payment",
+            Category = "General",
+            Amount = 10m,
+            Currency = "PLN",
+            Type = "expense",
+            TransactionDate = DateTime.UtcNow
+        });
+        transactionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        var deleteResponse = await client.DeleteAsync($"/api/accounts/{accountId}");
+        deleteResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
+
+        var body = await deleteResponse.Content.ReadAsStringAsync();
+        body.Should().Contain("transaction history and non-zero balance");
+    }
+
+    [Fact]
+    public async Task Deactivate_Hides_Account_From_Default_List()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, name: "Hide me", bankAccountNumber: "1234567890123456");
+
+        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{accountId}/active-state", new
+        {
+            IsActive = false
+        });
+        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        using var defaultAccounts = await GetAccountsAsync(client);
+        defaultAccounts.RootElement.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == accountId);
+
+        using var includeInactiveAccounts = await GetAccountsAsync(client, "?includeInactive=true");
+        var inactiveAccount = includeInactiveAccounts.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == accountId);
+        inactiveAccount.GetProperty("isActive").GetBoolean().Should().BeFalse();
+    }
+
+    [Fact]
+    public async Task Update_Trims_BankAccountNumber()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, name: "Editable account");
+
+        var updateResponse = await client.PutAsJsonAsync($"/api/accounts/{accountId}", new
+        {
+            Name = "Updated account",
+            BankAccountNumber = " 12345678901234567890123456789012 "
+        });
+        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        using var accounts = await GetAccountsAsync(client, "?includeInactive=true");
+        var account = accounts.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == accountId);
+        account.GetProperty("name").GetString().Should().Be("Updated account");
+        account.GetProperty("bankAccountNumber").GetString().Should().Be("12345678901234567890123456789012");
+    }
+
+    [Fact]
+    public async Task Creating_Transaction_Blocks_Inactive_Account()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, name: "Inactive account");
+
+        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{accountId}/active-state", new { IsActive = false });
+        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        var txResponse = await client.PostAsJsonAsync("/api/transactions", new
+        {
+            AccountId = accountId,
+            Description = "Should be blocked",
+            Category = "General",
+            Amount = 10m,
+            Currency = "PLN",
+            Type = "expense",
+            TransactionDate = DateTime.UtcNow
+        });
+
+        txResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
+        var body = await txResponse.Content.ReadAsStringAsync();
+        body.Should().Contain("inactive");
+    }
+
+    [Fact]
+    public async Task Creating_Transfer_Blocks_Inactive_Source_Account()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var fromId = await CreateAccountAsync(client, name: "From");
+        var toId = await CreateAccountAsync(client, name: "To");
+
+        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{fromId}/active-state", new { IsActive = false });
+        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        var transferResponse = await client.PostAsJsonAsync("/api/transfers", new
+        {
+            FromAccountId = fromId,
+            ToAccountId = toId,
+            Amount = 5m,
+            Currency = "PLN",
+            Description = "Blocked transfer",
+            TransferDate = DateTime.UtcNow
+        });
+
+        transferResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
+        var body = await transferResponse.Content.ReadAsStringAsync();
+        body.Should().Contain("inactive");
+    }
+
+    [Fact]
+    public async Task TransactionsPage_Shows_Inactive_Account_Name_For_Historical_Transactions()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, name: "HistoricAccount");
+
+        var transactionResponse = await client.PostAsJsonAsync("/api/transactions", new
+        {
+            AccountId = accountId,
+            Description = "Old payment",
+            Category = "General",
+            Amount = 12.34m,
+            Currency = "PLN",
+            Type = "expense",
+            TransactionDate = DateTime.UtcNow
+        });
+        transactionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{accountId}/active-state", new { IsActive = false });
+        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        // Request the transactions page and ensure the historical transaction still shows the account name
+        var pageResponse = await client.GetAsync("/transactions");
+        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+        var html = await pageResponse.Content.ReadAsStringAsync();
+
+        html.Should().Contain("HistoricAccount");
+        html.Should().NotContain("Unknown account");
+    }
+
+    private static HttpClient CreateAuthenticatedClient(TreasuryHostFactory app) =>
+        app.CreateClient(new WebApplicationFactoryClientOptions
+        {
+            AllowAutoRedirect = false,
+            HandleCookies = true
+        });
+
+    private static async Task RegisterAndSignInAsync(HttpClient client)
+    {
+        var email = $"owner-{Guid.NewGuid():N}@example.com";
+        const string password = "Password123!";
+
+        var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
+        {
+            Email = email,
+            Password = password,
+            ConfirmPassword = password
+        });
+        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
+
+        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
+        {
+            Email = email,
+            Password = password
+        });
+        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
+    }
+
+    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name, string? bankAccountNumber = null)
+    {
+        var response = await client.PostAsJsonAsync("/api/accounts", new
+        {
+            Name = name,
+            Currency = "PLN",
+            AccountType = "cash-wallet",
+            BankAccountNumber = bankAccountNumber
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
+        return json.RootElement.GetProperty("id").GetGuid();
+    }
+
+    private static async Task<JsonDocument> GetAccountsAsync(HttpClient client, string query = "")
+    {
+        var response = await client.GetAsync($"/api/accounts{query}");
+        response.StatusCode.Should().Be(HttpStatusCode.OK);
+        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
+    }
+}
diff --git a/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
index c5c30f5..25edc5d 100644
--- a/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
+++ b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
@@ -195,20 +195,23 @@ public class AccountsSharingUiTests
                     Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                     HouseholdId = user.HouseholdId,
                     OwnerUserId = user.Id,
                     Name = "Loaded account",
                     Currency = "PLN",
                     AccountType = "cash-wallet",
                     CurrentBalance = 12.34m
                 }
             });
 
+        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, bool includeInactive, CancellationToken ct) =>
+            GetVisibleAccountsAsync(user, ct);
+
         public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
             throw new InvalidOperationException("household users query failed");
 
         public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct) =>
             Task.FromResult(new List<AccountViewerAssignment>
             {
                 new(
                     Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                     user.Id,
                     "viewer@example.com",
@@ -226,20 +229,23 @@ public class AccountsSharingUiTests
                     Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                     HouseholdId = user.HouseholdId,
                     OwnerUserId = user.Id,
                     Name = "Loaded account",
                     Currency = "PLN",
                     AccountType = "cash-wallet",
                     CurrentBalance = 12.34m
                 }
             });
 
+        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, bool includeInactive, CancellationToken ct) =>
+            GetVisibleAccountsAsync(user, ct);
+
         public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
             Task.FromResult(new List<HouseholdUserChoice>
             {
                 new("viewer@example.com", "Viewer")
             });
 
         public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct) =>
             throw new InvalidOperationException("shared viewers query failed");
     }
 
@@ -266,20 +272,23 @@ public class AccountsSharingUiTests
                     Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                     HouseholdId = user.HouseholdId,
                     OwnerUserId = user.Id,
                     Name = "Account B",
                     Currency = "PLN",
                     AccountType = "cash-wallet",
                     CurrentBalance = 2m
                 }
             });
 
+        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, bool includeInactive, CancellationToken ct) =>
+            GetVisibleAccountsAsync(user, ct);
+
         public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
             Task.FromResult(new List<HouseholdUserChoice>
             {
                 new("viewer-one@example.com", "Viewer One"),
                 new("viewer-two@example.com", "Viewer Two")
             });
 
         public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
         {
             SharedViewerBatchCalls++;
