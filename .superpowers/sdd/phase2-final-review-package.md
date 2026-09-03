# Final Review Package

Base: f667d4a71e9a92476fb40eb36e878669fdad75c6
Head: d4a4027e15c6aea807559e31466cbafae0156002

## Commits

d4a4027 docs: clarify transaction editing rule in README
ed7599f test/docs: finalize phase2 lifecycle transfer transaction coverage
74328d0 Fix running balance recomputation
97c373f fix: enforce transaction edit rules
ae09ae7 feat: add transaction balance tracking and edit rules
e5746be fix: share canonical transfer workflow
d97d260 feat: add linked transfers workflow and transfers page
0d3a73d Transactions UI: show historical account names for inactive accounts; block creating transactions on inactive accounts in UI; add integration test for transactions page display\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
2f46306 fix(transactions/transfers): block inactive accounts from new writes; exclude inactive accounts in Transactions UI\n\n- Add inactive-account guard to CreateTransactionEndpoint and CreateTransferEndpoint\n- Update Transactions.razor to list only active accounts and handle no active accounts\n- Add integration tests asserting inactive accounts are rejected for transaction & transfer\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
6e1a0b2 feat: add account lifecycle controls and bank number validation

## Diff Stat

 .superpowers/sdd/phase2-task-1-brief.md            |  149 ++
 .superpowers/sdd/phase2-task-1-report.md           |   42 +
 .superpowers/sdd/phase2-task-1-rereview-package.md | 2181 ++++++++++++++++++++
 .superpowers/sdd/phase2-task-1-review-package.md   | 1979 ++++++++++++++++++
 .superpowers/sdd/phase2-task-2-report.md           |   31 +
 .superpowers/sdd/phase2-task-3-report.md           |   52 +
 .superpowers/sdd/phase2-task-4-report.md           |    8 +
 README.md                                          |    8 +
 .../Accounts/AccountLifecycleValidation.cs         |   25 +
 .../Application/Accounts/AccountSharingService.cs  |    4 +
 .../AccountBalanceRecalculationService.cs          |   82 +
 .../Transactions/TransactionEditingService.cs      |  244 +++
 .../Transfers/TransferCreationService.cs           |  218 ++
 .../Components/Layout/MainLayout.razor             |    2 +
 .../Contracts/Accounts/AccountResponse.cs          |    2 +
 .../Contracts/Accounts/CreateAccountRequest.cs     |    1 +
 .../Accounts/SetAccountActiveStateRequest.cs       |    6 +
 .../Contracts/Accounts/UpdateAccountRequest.cs     |    7 +
 .../Transactions/UpdateTransactionRequest.cs       |   12 +
 src/Treasury.App/Domain/Account.cs                 |    2 +
 src/Treasury.App/Domain/Transaction.cs             |    1 +
 src/Treasury.App/Domain/Transfer.cs                |    2 +
 .../Accounts/BalanceCorrectionEndpoint.cs          |   33 +-
 .../Endpoints/Accounts/CreateAccountEndpoint.cs    |   11 +
 .../Endpoints/Accounts/DeleteAccountEndpoint.cs    |   57 +
 .../Accounts/GetAccountTransactionsEndpoint.cs     |    5 +-
 .../Endpoints/Accounts/GetAccountsEndpoint.cs      |   12 +-
 .../Accounts/SetAccountActiveStateEndpoint.cs      |   57 +
 .../Endpoints/Accounts/UpdateAccountEndpoint.cs    |   80 +
 .../Transactions/CreateTransactionEndpoint.cs      |   55 +-
 .../Transactions/GetTransactionsEndpoint.cs        |    3 +
 .../Transactions/UpdateTransactionEndpoint.cs      |   75 +
 .../Endpoints/Transfers/CreateTransferEndpoint.cs  |  121 +-
 .../Infrastructure/Data/Seed/InitialSeed.cs        |    3 +
 .../Infrastructure/Data/TreasuryDbContext.cs       |    8 +
 ...903122916_AddAccountLifecycleFields.Designer.cs |  738 +++++++
 .../20260903122916_AddAccountLifecycleFields.cs    |   40 +
 ...dTransactionBalanceAfterTransaction.Designer.cs |  747 +++++++
 ...134028_AddTransactionBalanceAfterTransaction.cs |   29 +
 ...3153000_AddTransferTransactionLinks.Designer.cs |  744 +++++++
 .../20260903153000_AddTransferTransactionLinks.cs  |   38 +
 .../Migrations/TreasuryDbContextModelSnapshot.cs   |   18 +
 src/Treasury.App/Pages/Accounts.razor              |   25 +-
 src/Treasury.App/Pages/Accounts.razor.cs           |   75 +-
 src/Treasury.App/Pages/Transactions.razor          |  357 +++-
 src/Treasury.App/Pages/Transfers.razor             |   92 +
 src/Treasury.App/Pages/Transfers.razor.cs          |  199 ++
 src/Treasury.App/Program.cs                        |   10 +
 src/Treasury.App/_Imports.razor                    |    2 +
 .../AccountsLifecycleTests.cs                      |  280 +++
 .../AccountsSharingUiTests.cs                      |    9 +
 .../SharedReadOnlyUiPermissionTests.cs             |   22 +
 .../TransactionEditingRulesTests.cs                |  288 +++
 .../TransfersWorkflowTests.cs                      |  239 +++
 54 files changed, 9391 insertions(+), 139 deletions(-)

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
diff --git a/.superpowers/sdd/phase2-task-2-report.md b/.superpowers/sdd/phase2-task-2-report.md
new file mode 100644
index 0000000..1d9eda0
--- /dev/null
+++ b/.superpowers/sdd/phase2-task-2-report.md
@@ -0,0 +1,31 @@
+# Phase 2 Task 2 Report
+
+## What changed
+- Added `OutflowTransactionId` and `InflowTransactionId` to the transfer domain model.
+- Updated `POST /api/transfers` to persist the transfer and both linked transactions atomically, then write back the transaction IDs.
+- Added a dedicated `/transfers` page with a creation form and recent transfer history only, matching the approved option C scope.
+- Added transfers navigation entries in the top bar and drawer.
+- Added an EF Core migration for the new transfer link columns and updated the model snapshot.
+- Added `TransfersWorkflowTests` integration coverage for successful linked transfers, inactive-account rejection, and transfers page rendering.
+
+## Validation
+- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransfersWorkflowTests"`
+  - Result: passed, 3/3 tests.
+- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests"`
+  - Result: passed, 7/7 tests.
+
+## Notes
+- The page uses direct server-side database access for the form/history workflow, while the API endpoint remains the canonical write path for transfer creation behavior and validation.
+- No balance summary widgets were added, per the scope clarification.
+
+## Commit
+- 4a56c75
+
+## Follow-up fixes
+- Extracted a shared `TransferCreationService` so the transfers page and `POST /api/transfers` use the same canonical write path.
+- Changed transfer link columns to nullable so pre-existing rows are not backfilled with `Guid.Empty` placeholders.
+- Kept atomic persistence, active-account checks, and paired transfer transaction creation intact.
+
+## Follow-up validation
+- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransfersWorkflowTests"` — passed, 3/3 tests.
+- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests"` — passed, 7/7 tests.
diff --git a/.superpowers/sdd/phase2-task-3-report.md b/.superpowers/sdd/phase2-task-3-report.md
new file mode 100644
index 0000000..33da5cc
--- /dev/null
+++ b/.superpowers/sdd/phase2-task-3-report.md
@@ -0,0 +1,52 @@
+# Phase 2 Task 3 Report
+
+## Summary
+
+Implemented transaction running-balance tracking and edit-rule enforcement.
+
+### Data/model
+- Added `Transaction.BalanceAfterTransaction`.
+- Generated EF migration `20260903134028_AddTransactionBalanceAfterTransaction`.
+- Updated the model snapshot and seeded historical transactions with running-balance values.
+
+### Write paths
+- `CreateTransactionEndpoint` now stores `BalanceAfterTransaction` on new transactions.
+- `TransferCreationService` now stores running balances for transfer outflow/inflow transactions.
+- Added `PUT /api/transactions/{id:guid}` via `UpdateTransactionEndpoint`.
+
+### Edit rules
+- Non-latest transactions can only change description, category, and tags.
+- Latest transactions can change amount, date, and type, and the account balance plus running balances are recomputed.
+- Transactions page now shows an edit affordance and disables amount/date/type editing for historical rows.
+
+### API/read updates
+- Transaction list endpoints now return `BalanceAfterTransaction`.
+- Transaction ordering is now deterministic with `TransactionDate`, `CreatedAt`, and `Id`.
+
+## Tests
+
+Passed:
+- `dotnet test tests\\Treasury.IntegrationTests\\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransactionEditingRulesTests"`
+- `dotnet test tests\\Treasury.IntegrationTests\\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests|FullyQualifiedName~TransfersWorkflowTests|FullyQualifiedName~SharedReadOnlyUiPermissionTests"`
+- `dotnet test tests\\Treasury.IntegrationTests\\Treasury.IntegrationTests.csproj -c Debug`
+
+## Notes
+
+- The migration adds `BalanceAfterTransaction` with a default value of `0m` for existing rows.
+- Latest-transaction edits recompute the full account running balance chain so date changes remain consistent.
+
+## Resolution update
+
+- Routed transaction edits through a shared `TransactionEditingService` so the UI and `PUT /api/transactions/{id:guid}` enforce the same permission checks.
+- Switched `UpdateTransactionRequest` to nullable/optional fields so omitted values are preserved instead of treated as edits.
+- Blocked edits for transfer-linked transactions and added coverage for shared-readonly permission checks, partial updates, and transfer-linked rejection.
+
+## Task 3 rereview follow-up
+
+- Added a shared `AccountBalanceRecalculationService` that recomputes one account's running balance chain in `TransactionDate`, `CreatedAt`, `Id` order and updates both `BalanceAfterTransaction` and `Account.CurrentBalance`.
+- Hooked that recomputation into transaction create, transfer create, transaction update, balance correction, and the transactions page create path.
+- Chose a startup backfill routine after seed/migration to repair any existing `BalanceAfterTransaction = 0` rows while preserving each account's opening balance baseline.
+- Added regression coverage for backdated transaction inserts, backdated transfer inserts, and balance-correction running-balance updates.
+- Validation rerun:
+  - `dotnet test tests\\Treasury.IntegrationTests\\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransactionEditingRulesTests|FullyQualifiedName~TransfersWorkflowTests|FullyQualifiedName~AccountsLifecycleTests"`
+  - `dotnet test tests\\Treasury.IntegrationTests\\Treasury.IntegrationTests.csproj -c Debug`
diff --git a/.superpowers/sdd/phase2-task-4-report.md b/.superpowers/sdd/phase2-task-4-report.md
new file mode 100644
index 0000000..14792ac
--- /dev/null
+++ b/.superpowers/sdd/phase2-task-4-report.md
@@ -0,0 +1,8 @@
+Status: Completed
+Commits: ed7599f - test/docs: finalize phase2 lifecycle transfer transaction coverage
+Tests: Integration: 37 passed, Domain: 2 passed (all tests green)
+Concerns: None notable — README doc additions only; no test failures. Verify UI route /transfers is deployed in runtime for manual verification.
+Report path: .superpowers/sdd/phase2-task-4-report.md
+
+Notes:
+- README wording corrected to precisely state that older transactions may only have description, category, and tags edited; amount, date, and type are restricted to the latest transaction on an account.
diff --git a/README.md b/README.md
index f5ff5ac..9b810fa 100644
--- a/README.md
+++ b/README.md
@@ -47,10 +47,18 @@ API equivalents:
 3. For coins: fill asset name, quantity, unit value, then save.
 
 API equivalents:
 - `POST /api/valuations/bullion`
 - `POST /api/valuations/coin`
 
 ## Notes
 
 - In Docker production mode, PostgreSQL is used and migrations are applied automatically on startup.
 - DataProtection keys are persisted in a Docker volume to keep auth/antiforgery tokens stable across restarts.
+
+## Behavior and UI notes
+
+- Inactive accounts are hidden by default and blocked from creating new transfers or transactions.
+- Transfers page (UI): `/transfers` — use this page to create and view transfer records between accounts.
+- Transaction editing rule: only the latest transaction on an account may change amount, date, or type; older transactions may only have description, category, and tags edited.
+
+Operator guidance above reflects Phase 2 lifecycle and is enforced by acceptance/domain rules and integration tests.
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
diff --git a/src/Treasury.App/Application/Transactions/AccountBalanceRecalculationService.cs b/src/Treasury.App/Application/Transactions/AccountBalanceRecalculationService.cs
new file mode 100644
index 0000000..f5a4522
--- /dev/null
+++ b/src/Treasury.App/Application/Transactions/AccountBalanceRecalculationService.cs
@@ -0,0 +1,82 @@
+using Microsoft.EntityFrameworkCore;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.App.Application.Transactions;
+
+public sealed class AccountBalanceRecalculationService(TreasuryDbContext db)
+{
+    public Task RecalculateAccountAsync(Guid accountId, CancellationToken ct) =>
+        RecalculateAccountsAsync([accountId], ct);
+
+    public async Task RecalculateAccountsAsync(IEnumerable<Guid> accountIds, CancellationToken ct)
+    {
+        foreach (var accountId in accountIds.Distinct())
+        {
+            var account = await db.Accounts.SingleAsync(x => x.Id == accountId, ct);
+            var transactions = await db.Transactions
+                .Where(x => x.AccountId == accountId)
+                .OrderBy(x => x.TransactionDate)
+                .ThenBy(x => x.CreatedAt)
+                .ThenBy(x => x.Id)
+                .ToListAsync(ct);
+
+            var openingBalance = account.CurrentBalance - transactions.Sum(x => TransactionBalanceMath.GetDelta(x.Amount, x.Type));
+            var runningBalance = openingBalance;
+            var utcNow = DateTime.UtcNow;
+            var accountChanged = false;
+
+            foreach (var transaction in transactions)
+            {
+                runningBalance += TransactionBalanceMath.GetDelta(transaction.Amount, transaction.Type);
+                if (transaction.BalanceAfterTransaction != runningBalance)
+                {
+                    transaction.BalanceAfterTransaction = runningBalance;
+                    accountChanged = true;
+                }
+            }
+
+            if (account.CurrentBalance != runningBalance)
+            {
+                account.CurrentBalance = runningBalance;
+                accountChanged = true;
+            }
+
+            if (accountChanged)
+            {
+                account.UpdatedAt = utcNow;
+            }
+
+            await db.SaveChangesAsync(ct);
+        }
+    }
+
+    public async Task RecalculateAllAccountsAsync(CancellationToken ct)
+    {
+        var accountIds = await db.Accounts
+            .OrderBy(x => x.Name)
+            .Select(x => x.Id)
+            .ToListAsync(ct);
+
+        await RecalculateAccountsAsync(accountIds, ct);
+    }
+}
+
+public static class TransactionBalanceMath
+{
+    public static string NormalizeType(string? type, string fallback = "expense") =>
+        string.IsNullOrWhiteSpace(type) ? fallback : type.Trim().ToLowerInvariant();
+
+    public static decimal GetDelta(decimal amount, string type)
+    {
+        var normalizedType = NormalizeType(type);
+        return normalizedType switch
+        {
+            "expense" => -Math.Abs(amount),
+            "income" => Math.Abs(amount),
+            "transfer" => Math.Abs(amount),
+            "transfer-in" => Math.Abs(amount),
+            "transfer-out" => -Math.Abs(amount),
+            _ => amount
+        };
+    }
+}
diff --git a/src/Treasury.App/Application/Transactions/TransactionEditingService.cs b/src/Treasury.App/Application/Transactions/TransactionEditingService.cs
new file mode 100644
index 0000000..78e15e7
--- /dev/null
+++ b/src/Treasury.App/Application/Transactions/TransactionEditingService.cs
@@ -0,0 +1,244 @@
+using Microsoft.AspNetCore.Identity;
+using Microsoft.EntityFrameworkCore;
+using Treasury.App.Contracts.Transactions;
+using Treasury.App.Domain;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.App.Application.Transactions;
+
+public enum TransactionEditStatus
+{
+    Success,
+    InvalidRequest,
+    NotFound,
+    Forbidden
+}
+
+public sealed record TransactionEditIssue(string Field, string Message);
+
+public sealed record TransactionEditOutcome(
+    Guid Id,
+    Guid AccountId,
+    string Description,
+    string Category,
+    decimal Amount,
+    string Currency,
+    string Type,
+    DateTime TransactionDate,
+    decimal BalanceAfterTransaction,
+    IReadOnlyList<Guid> TagIds);
+
+public sealed record TransactionEditResult(
+    TransactionEditStatus Status,
+    string? Message,
+    IReadOnlyList<TransactionEditIssue> Issues,
+    TransactionEditOutcome? Outcome)
+{
+    public bool Succeeded => Status == TransactionEditStatus.Success && Outcome is not null;
+
+    public static TransactionEditResult Success(TransactionEditOutcome outcome) =>
+        new(TransactionEditStatus.Success, null, [], outcome);
+
+    public static TransactionEditResult Failure(
+        TransactionEditStatus status,
+        string message,
+        params TransactionEditIssue[] issues) =>
+        new(status, message, issues, null);
+}
+
+public class TransactionEditingService(
+    TreasuryDbContext db,
+    AccountBalanceRecalculationService balanceRecalculationService)
+{
+    public virtual async Task<TransactionEditResult> UpdateAsync(
+        ApplicationUser user,
+        UpdateTransactionRequest request,
+        CancellationToken ct)
+    {
+        if (request.Id == Guid.Empty)
+        {
+            return TransactionEditResult.Failure(
+                TransactionEditStatus.InvalidRequest,
+                "Please correct the highlighted fields.",
+                new TransactionEditIssue(nameof(UpdateTransactionRequest.Id), "A valid transaction is required."));
+        }
+
+        var transaction = await db.Transactions
+            .Include(x => x.TransactionTags)
+            .SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
+        if (transaction is null)
+        {
+            return TransactionEditResult.Failure(TransactionEditStatus.NotFound, "Transaction was not found.");
+        }
+
+        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == transaction.AccountId && x.HouseholdId == user.HouseholdId, ct);
+        if (account is null)
+        {
+            return TransactionEditResult.Failure(TransactionEditStatus.NotFound, "Account was not found.");
+        }
+
+        if (account.OwnerUserId != user.Id)
+        {
+            return TransactionEditResult.Failure(
+                TransactionEditStatus.Forbidden,
+                "You do not have permission to edit this transaction.");
+        }
+
+        if (await IsTransferLinkedAsync(transaction, ct))
+        {
+            return TransactionEditResult.Failure(
+                TransactionEditStatus.InvalidRequest,
+                "Transfer-linked transactions cannot be edited. Edit the transfer instead.",
+                new TransactionEditIssue(nameof(UpdateTransactionRequest.Id), "Transfer-linked transactions cannot be edited. Edit the transfer instead."));
+        }
+
+        var latestTransactionId = await db.Transactions
+            .Where(x => x.AccountId == account.Id)
+            .OrderByDescending(x => x.TransactionDate)
+            .ThenByDescending(x => x.CreatedAt)
+            .ThenByDescending(x => x.Id)
+            .Select(x => x.Id)
+            .FirstOrDefaultAsync(ct);
+
+        var isLatest = latestTransactionId == transaction.Id;
+        var originalDelta = TransactionBalanceMath.GetDelta(transaction.Amount, transaction.Type);
+
+        var hasDescription = request.Description is not null;
+        var hasCategory = request.Category is not null;
+        var hasAmount = request.Amount.HasValue;
+        var hasType = request.Type is not null;
+        var hasTransactionDate = request.TransactionDate.HasValue;
+        var hasTagIds = request.TagIds is not null;
+
+        var normalizedDescription = hasDescription ? request.Description!.Trim() : transaction.Description;
+        var normalizedCategory = hasCategory ? NormalizeCategory(request.Category!, transaction.Category) : transaction.Category;
+        var normalizedAmount = hasAmount ? request.Amount!.Value : transaction.Amount;
+        var normalizedType = hasType ? TransactionBalanceMath.NormalizeType(request.Type!, transaction.Type) : transaction.Type;
+        var normalizedTransactionDate = hasTransactionDate ? request.TransactionDate!.Value : transaction.TransactionDate;
+
+        var issues = new List<TransactionEditIssue>();
+
+        if (hasDescription && string.IsNullOrWhiteSpace(normalizedDescription))
+        {
+            issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Description), "Description is required."));
+        }
+
+        if (hasAmount && normalizedAmount <= 0m)
+        {
+            issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Amount), "Amount must be greater than zero."));
+        }
+
+        var amountChanged = hasAmount && normalizedAmount != transaction.Amount;
+        var typeChanged = hasType && !string.Equals(normalizedType, transaction.Type, StringComparison.OrdinalIgnoreCase);
+        var dateChanged = hasTransactionDate && normalizedTransactionDate != transaction.TransactionDate;
+
+        if (!isLatest)
+        {
+            if (amountChanged)
+            {
+                issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Amount), "Amount can only be changed on the latest transaction."));
+            }
+
+            if (dateChanged)
+            {
+                issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.TransactionDate), "Transaction date can only be changed on the latest transaction."));
+            }
+
+            if (typeChanged)
+            {
+                issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Type), "Type can only be changed on the latest transaction."));
+            }
+        }
+
+        if (issues.Count > 0)
+        {
+            return TransactionEditResult.Failure(
+                TransactionEditStatus.InvalidRequest,
+                "Please correct the highlighted fields.",
+                issues.ToArray());
+        }
+
+        transaction.Description = normalizedDescription;
+        transaction.Category = normalizedCategory;
+        transaction.Amount = normalizedAmount;
+        transaction.Type = normalizedType;
+        transaction.TransactionDate = normalizedTransactionDate;
+        var utcNow = DateTime.UtcNow;
+        transaction.UpdatedAt = utcNow;
+
+        var updatedDelta = TransactionBalanceMath.GetDelta(transaction.Amount, transaction.Type);
+
+        if (amountChanged || typeChanged)
+        {
+            account.CurrentBalance += updatedDelta - originalDelta;
+            account.UpdatedAt = utcNow;
+        }
+        else if (dateChanged)
+        {
+            account.UpdatedAt = utcNow;
+        }
+
+        if (hasTagIds)
+        {
+            var distinctTagIds = request.TagIds!.Distinct().ToArray();
+            var validTagIds = await db.Tags
+                .Where(x => x.HouseholdId == user.HouseholdId && distinctTagIds.Contains(x.Id))
+                .Select(x => x.Id)
+                .ToListAsync(ct);
+
+            db.TransactionTags.RemoveRange(transaction.TransactionTags);
+            transaction.TransactionTags.Clear();
+
+            foreach (var tagId in validTagIds)
+            {
+                transaction.TransactionTags.Add(new TransactionTag
+                {
+                    Transaction = transaction,
+                    TagId = tagId
+                });
+            }
+        }
+
+        async Task PersistAsync()
+        {
+            await db.SaveChangesAsync(ct);
+            await balanceRecalculationService.RecalculateAccountAsync(account.Id, ct);
+        }
+
+        if (db.Database.IsRelational())
+        {
+            await using var transactionScope = await db.Database.BeginTransactionAsync(ct);
+            await PersistAsync();
+            await transactionScope.CommitAsync(ct);
+        }
+        else
+        {
+            await PersistAsync();
+        }
+
+        var tagIds = transaction.TransactionTags
+            .Select(x => x.TagId)
+            .ToList();
+
+        return TransactionEditResult.Success(new TransactionEditOutcome(
+            transaction.Id,
+            transaction.AccountId,
+            transaction.Description,
+            transaction.Category,
+            transaction.Amount,
+            transaction.Currency,
+            transaction.Type,
+            transaction.TransactionDate,
+            transaction.BalanceAfterTransaction,
+            tagIds));
+    }
+
+    private async Task<bool> IsTransferLinkedAsync(Transaction transaction, CancellationToken ct) =>
+        transaction.Type is "transfer-in" or "transfer-out"
+        || await db.Transfers.AnyAsync(
+            x => x.OutflowTransactionId == transaction.Id || x.InflowTransactionId == transaction.Id,
+            ct);
+
+    private static string NormalizeCategory(string category, string fallback) =>
+        string.IsNullOrWhiteSpace(category) ? fallback : category.Trim();
+}
diff --git a/src/Treasury.App/Application/Transfers/TransferCreationService.cs b/src/Treasury.App/Application/Transfers/TransferCreationService.cs
new file mode 100644
index 0000000..48e5617
--- /dev/null
+++ b/src/Treasury.App/Application/Transfers/TransferCreationService.cs
@@ -0,0 +1,218 @@
+using Microsoft.AspNetCore.Identity;
+using Microsoft.EntityFrameworkCore;
+using Treasury.App.Application.Transactions;
+using Treasury.App.Contracts.Transactions;
+using Treasury.App.Domain;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.App.Application.Transfers;
+
+public enum TransferCreationStatus
+{
+    Success,
+    InvalidRequest,
+    NotFound,
+    Forbidden
+}
+
+public sealed record TransferCreationIssue(string Field, string Message);
+
+public sealed record TransferCreationOutcome(
+    Guid TransferId,
+    Guid OutflowTransactionId,
+    Guid InflowTransactionId,
+    Guid FromAccountId,
+    Guid ToAccountId,
+    decimal Amount,
+    string Currency,
+    DateTime TransferDate);
+
+public sealed record TransferCreationResult(
+    TransferCreationStatus Status,
+    string? Message,
+    IReadOnlyList<TransferCreationIssue> Issues,
+    TransferCreationOutcome? Outcome)
+{
+    public bool Succeeded => Status == TransferCreationStatus.Success && Outcome is not null;
+
+    public static TransferCreationResult Success(TransferCreationOutcome outcome) =>
+        new(TransferCreationStatus.Success, null, [], outcome);
+
+    public static TransferCreationResult Failure(
+        TransferCreationStatus status,
+        string message,
+        params TransferCreationIssue[] issues) =>
+        new(status, message, issues, null);
+}
+
+public class TransferCreationService(
+    TreasuryDbContext db,
+    AccountBalanceRecalculationService balanceRecalculationService)
+{
+    public virtual async Task<TransferCreationResult> CreateAsync(
+        ApplicationUser user,
+        CreateTransferRequest request,
+        CancellationToken ct)
+    {
+        var issues = ValidateRequest(request);
+        if (issues.Count > 0)
+        {
+            return TransferCreationResult.Failure(
+                TransferCreationStatus.InvalidRequest,
+                "Please correct the highlighted fields.",
+                issues.ToArray());
+        }
+
+        var fromAccount = await db.Accounts.SingleOrDefaultAsync(
+            x => x.Id == request.FromAccountId && x.HouseholdId == user.HouseholdId,
+            ct);
+        var toAccount = await db.Accounts.SingleOrDefaultAsync(
+            x => x.Id == request.ToAccountId && x.HouseholdId == user.HouseholdId,
+            ct);
+
+        if (fromAccount is null || toAccount is null)
+        {
+            return TransferCreationResult.Failure(
+                TransferCreationStatus.NotFound,
+                "Transfer accounts were not found.");
+        }
+
+        if (fromAccount.OwnerUserId != user.Id || toAccount.OwnerUserId != user.Id)
+        {
+            return TransferCreationResult.Failure(
+                TransferCreationStatus.Forbidden,
+                "Transfers can only be created for your own accounts.");
+        }
+
+        if (!fromAccount.IsActive || !toAccount.IsActive)
+        {
+            var accountIssues = new List<TransferCreationIssue>();
+            if (!fromAccount.IsActive)
+            {
+                accountIssues.Add(new TransferCreationIssue("FromAccountId", "Source account is inactive."));
+            }
+
+            if (!toAccount.IsActive)
+            {
+                accountIssues.Add(new TransferCreationIssue("ToAccountId", "Destination account is inactive."));
+            }
+
+            return TransferCreationResult.Failure(
+                TransferCreationStatus.InvalidRequest,
+                "Transfers are allowed only between active accounts.",
+                accountIssues.ToArray());
+        }
+
+        var transferDate = request.TransferDate == default ? DateTime.UtcNow : request.TransferDate;
+        var currency = string.IsNullOrWhiteSpace(request.Currency)
+            ? fromAccount.Currency
+            : request.Currency.Trim().ToUpperInvariant();
+        var description = string.IsNullOrWhiteSpace(request.Description)
+            ? "Account transfer"
+            : request.Description.Trim();
+        var utcNow = DateTime.UtcNow;
+
+        var transfer = new Transfer
+        {
+            HouseholdId = user.HouseholdId,
+            FromAccountId = fromAccount.Id,
+            ToAccountId = toAccount.Id,
+            Amount = request.Amount,
+            Currency = currency,
+            Description = description,
+            TransferDate = transferDate,
+            CreatedAt = utcNow
+        };
+
+        var outflow = new Transaction
+        {
+            HouseholdId = user.HouseholdId,
+            AccountId = fromAccount.Id,
+            Description = $"{description} -> {toAccount.Name}",
+            Category = "Transfer",
+            Amount = request.Amount,
+            Currency = currency,
+            Type = "transfer-out",
+            TransactionDate = transferDate,
+            CreatedAt = utcNow,
+            UpdatedAt = utcNow
+        };
+
+        var inflow = new Transaction
+        {
+            HouseholdId = user.HouseholdId,
+            AccountId = toAccount.Id,
+            Description = $"{description} <- {fromAccount.Name}",
+            Category = "Transfer",
+            Amount = request.Amount,
+            Currency = currency,
+            Type = "transfer-in",
+            TransactionDate = transferDate,
+            CreatedAt = utcNow,
+            UpdatedAt = utcNow
+        };
+
+        var transferAmount = Math.Abs(request.Amount);
+        fromAccount.CurrentBalance -= transferAmount;
+        toAccount.CurrentBalance += transferAmount;
+        fromAccount.UpdatedAt = utcNow;
+        toAccount.UpdatedAt = utcNow;
+
+        async Task PersistAsync()
+        {
+            db.Transfers.Add(transfer);
+            db.Transactions.Add(outflow);
+            db.Transactions.Add(inflow);
+            await db.SaveChangesAsync(ct);
+
+            transfer.OutflowTransactionId = outflow.Id;
+            transfer.InflowTransactionId = inflow.Id;
+            await db.SaveChangesAsync(ct);
+
+            await balanceRecalculationService.RecalculateAccountsAsync([fromAccount.Id, toAccount.Id], ct);
+        }
+
+        if (db.Database.IsRelational())
+        {
+            await using var transaction = await db.Database.BeginTransactionAsync(ct);
+            await PersistAsync();
+            await transaction.CommitAsync(ct);
+        }
+        else
+        {
+            await PersistAsync();
+        }
+
+        return TransferCreationResult.Success(new TransferCreationOutcome(
+            transfer.Id,
+            outflow.Id,
+            inflow.Id,
+            transfer.FromAccountId,
+            transfer.ToAccountId,
+            transfer.Amount,
+            transfer.Currency,
+            transfer.TransferDate));
+    }
+
+    private static List<TransferCreationIssue> ValidateRequest(CreateTransferRequest request)
+    {
+        var issues = new List<TransferCreationIssue>();
+
+        if (request.FromAccountId == Guid.Empty || request.FromAccountId == request.ToAccountId)
+        {
+            issues.Add(new TransferCreationIssue("FromAccountId", "A valid source account is required."));
+        }
+
+        if (request.ToAccountId == Guid.Empty || request.FromAccountId == request.ToAccountId)
+        {
+            issues.Add(new TransferCreationIssue("ToAccountId", "A valid destination account is required."));
+        }
+
+        if (request.Amount <= 0m)
+        {
+            issues.Add(new TransferCreationIssue("Amount", "Transfer amount must be greater than zero."));
+        }
+
+        return issues;
+    }
+}
diff --git a/src/Treasury.App/Components/Layout/MainLayout.razor b/src/Treasury.App/Components/Layout/MainLayout.razor
index aff3575..a7b43c1 100644
--- a/src/Treasury.App/Components/Layout/MainLayout.razor
+++ b/src/Treasury.App/Components/Layout/MainLayout.razor
@@ -9,20 +9,21 @@
     <MudAppBar Elevation="1" Dense="true">
         <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit" OnClick="ToggleDrawer" />
         <MudText Typo="Typo.h6">Treasury</MudText>
         <MudSpacer />
 
         <AuthorizeView>
             <Authorized>
                 <MudButton Variant="Variant.Text" Href="/">Dashboard</MudButton>
                 <MudButton Variant="Variant.Text" Href="/accounts">Accounts</MudButton>
                 <MudButton Variant="Variant.Text" Href="/transactions">Transactions</MudButton>
+                <MudButton Variant="Variant.Text" Href="/transfers">Transfers</MudButton>
                 <MudButton Variant="Variant.Text" Href="/budgets">Budgets</MudButton>
                 <MudButton Variant="Variant.Text" Href="/bills">Bills</MudButton>
                 <MudButton Variant="Variant.Text" Href="/tags">Tags</MudButton>
                 <MudButton Variant="Variant.Text" Href="/valuations">Valuations</MudButton>
                 <MudButton Variant="Variant.Text" Href="/shared">Shared</MudButton>
                 <MudButton Variant="Variant.Text" Href="/rates">Rates</MudButton>
                 <form method="post" action="/auth/logout-submit" style="display:inline; margin-left: 0.5rem;">
                     <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Text" Color="Color.Inherit">Logout</MudButton>
                 </form>
             </Authorized>
@@ -33,20 +34,21 @@
         </AuthorizeView>
     </MudAppBar>
 
     <MudDrawer @bind-Open="_drawerOpen" Variant="DrawerVariant.Temporary" ClipMode="DrawerClipMode.Always">
         <MudNavMenu>
             <AuthorizeView>
                 <Authorized>
                     <MudNavLink Href="/">Dashboard</MudNavLink>
                     <MudNavLink Href="/accounts">Accounts</MudNavLink>
                     <MudNavLink Href="/transactions">Transactions</MudNavLink>
+                    <MudNavLink Href="/transfers">Transfers</MudNavLink>
                     <MudNavLink Href="/budgets">Budgets</MudNavLink>
                     <MudNavLink Href="/bills">Bills</MudNavLink>
                     <MudNavLink Href="/tags">Tags</MudNavLink>
                     <MudNavLink Href="/valuations">Valuations</MudNavLink>
                     <MudNavLink Href="/shared">Shared</MudNavLink>
                     <MudNavLink Href="/rates">Rates</MudNavLink>
                     <form method="post" action="/auth/logout-submit">
                         <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Text" FullWidth="true">Logout</MudButton>
                     </form>
                 </Authorized>
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
diff --git a/src/Treasury.App/Contracts/Transactions/UpdateTransactionRequest.cs b/src/Treasury.App/Contracts/Transactions/UpdateTransactionRequest.cs
new file mode 100644
index 0000000..3731079
--- /dev/null
+++ b/src/Treasury.App/Contracts/Transactions/UpdateTransactionRequest.cs
@@ -0,0 +1,12 @@
+namespace Treasury.App.Contracts.Transactions;
+
+public sealed class UpdateTransactionRequest
+{
+    public Guid Id { get; set; }
+    public string? Description { get; set; }
+    public string? Category { get; set; }
+    public decimal? Amount { get; set; }
+    public string? Type { get; set; }
+    public DateTime? TransactionDate { get; set; }
+    public List<Guid>? TagIds { get; set; }
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
diff --git a/src/Treasury.App/Domain/Transaction.cs b/src/Treasury.App/Domain/Transaction.cs
index ab920e5..0673a4c 100644
--- a/src/Treasury.App/Domain/Transaction.cs
+++ b/src/Treasury.App/Domain/Transaction.cs
@@ -4,15 +4,16 @@ public class Transaction
 {
     public Guid Id { get; set; } = Guid.NewGuid();
     public Guid HouseholdId { get; set; }
     public Guid AccountId { get; set; }
     public string Description { get; set; } = string.Empty;
     public string Category { get; set; } = "General";
     public decimal Amount { get; set; }
     public string Currency { get; set; } = "PLN";
     public string Type { get; set; } = "expense";
     public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
+    public decimal BalanceAfterTransaction { get; set; }
     public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
     public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
 
     public ICollection<TransactionTag> TransactionTags { get; set; } = new List<TransactionTag>();
 }
diff --git a/src/Treasury.App/Domain/Transfer.cs b/src/Treasury.App/Domain/Transfer.cs
index f49a6cd..f277adf 100644
--- a/src/Treasury.App/Domain/Transfer.cs
+++ b/src/Treasury.App/Domain/Transfer.cs
@@ -1,14 +1,16 @@
 namespace Treasury.App.Domain;
 
 public sealed class Transfer
 {
     public Guid Id { get; set; } = Guid.NewGuid();
     public Guid HouseholdId { get; set; }
     public Guid FromAccountId { get; set; }
     public Guid ToAccountId { get; set; }
+    public Guid? OutflowTransactionId { get; set; }
+    public Guid? InflowTransactionId { get; set; }
     public decimal Amount { get; set; }
     public string Currency { get; set; } = "PLN";
     public string Description { get; set; } = string.Empty;
     public DateTime TransferDate { get; set; } = DateTime.UtcNow;
     public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 }
diff --git a/src/Treasury.App/Endpoints/Accounts/BalanceCorrectionEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/BalanceCorrectionEndpoint.cs
index 5f7689b..e9be6ec 100644
--- a/src/Treasury.App/Endpoints/Accounts/BalanceCorrectionEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Accounts/BalanceCorrectionEndpoint.cs
@@ -1,26 +1,30 @@
 using FastEndpoints;
 using Microsoft.AspNetCore.Identity;
 using Microsoft.EntityFrameworkCore;
+using Treasury.App.Application.Transactions;
 using Treasury.App.Domain;
 using Treasury.App.Infrastructure.Data;
 
 namespace Treasury.App.Endpoints.Accounts;
 
 public sealed class BalanceCorrectionRouteRequest
 {
     public Guid Id { get; set; }
     public decimal NewBalance { get; set; }
     public string Description { get; set; } = "Balance correction";
 }
 
-public sealed class BalanceCorrectionEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+public sealed class BalanceCorrectionEndpoint(
+    TreasuryDbContext db,
+    UserManager<ApplicationUser> userManager,
+    AccountBalanceRecalculationService balanceRecalculationService)
     : Endpoint<BalanceCorrectionRouteRequest>
 {
     public override void Configure()
     {
         Post("/api/accounts/{id:guid}/balance-correction");
         Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
     }
 
     public override async Task HandleAsync(BalanceCorrectionRouteRequest request, CancellationToken ct)
     {
@@ -37,37 +41,54 @@ public sealed class BalanceCorrectionEndpoint(TreasuryDbContext db, UserManager<
             await SendNotFoundAsync(ct);
             return;
         }
 
         if (account.OwnerUserId != user.Id)
         {
             await SendForbiddenAsync(ct);
             return;
         }
 
+        var utcNow = DateTime.UtcNow;
         var delta = request.NewBalance - account.CurrentBalance;
+
         account.CurrentBalance = request.NewBalance;
-        account.UpdatedAt = DateTime.UtcNow;
+        account.UpdatedAt = utcNow;
 
         db.Transactions.Add(new Transaction
         {
             HouseholdId = user.HouseholdId,
             AccountId = account.Id,
             Description = string.IsNullOrWhiteSpace(request.Description) ? "Balance correction" : request.Description.Trim(),
             Category = "Correction",
             Amount = delta,
             Currency = account.Currency,
             Type = "balance-correction",
-            TransactionDate = DateTime.UtcNow,
-            CreatedAt = DateTime.UtcNow,
-            UpdatedAt = DateTime.UtcNow
+            TransactionDate = utcNow,
+            CreatedAt = utcNow,
+            UpdatedAt = utcNow
         });
 
-        await db.SaveChangesAsync(ct);
+        async Task PersistAsync()
+        {
+            await db.SaveChangesAsync(ct);
+            await balanceRecalculationService.RecalculateAccountAsync(account.Id, ct);
+        }
+
+        if (db.Database.IsRelational())
+        {
+            await using var transactionScope = await db.Database.BeginTransactionAsync(ct);
+            await PersistAsync();
+            await transactionScope.CommitAsync(ct);
+        }
+        else
+        {
+            await PersistAsync();
+        }
 
         await SendOkAsync(new
         {
             account.Id,
             account.CurrentBalance
         }, ct);
     }
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
diff --git a/src/Treasury.App/Endpoints/Accounts/GetAccountTransactionsEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/GetAccountTransactionsEndpoint.cs
index a67b401..eb2997d 100644
--- a/src/Treasury.App/Endpoints/Accounts/GetAccountTransactionsEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Accounts/GetAccountTransactionsEndpoint.cs
@@ -35,26 +35,29 @@ public sealed class GetAccountTransactionsEndpoint(TreasuryDbContext db, UserMan
             && (x.OwnerUserId == user.Id || x.OwnerUserId == "seed" || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)), ct);
         if (!canAccess)
         {
             await SendNotFoundAsync(ct);
             return;
         }
 
         var transactions = await db.Transactions
             .Where(x => x.HouseholdId == user.HouseholdId && x.AccountId == request.Id)
             .OrderByDescending(x => x.TransactionDate)
+            .ThenByDescending(x => x.CreatedAt)
+            .ThenByDescending(x => x.Id)
             .Select(x => new
             {
                 x.Id,
                 x.AccountId,
                 x.Description,
                 x.Category,
                 x.Amount,
                 x.Currency,
                 x.Type,
-                x.TransactionDate
+                x.TransactionDate,
+                x.BalanceAfterTransaction
             })
             .ToListAsync(ct);
 
         await SendOkAsync(transactions, ct);
     }
 }
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
index 961f8f9..793e122 100644
--- a/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs
@@ -1,20 +1,24 @@
 using FastEndpoints;
 using Microsoft.AspNetCore.Identity;
 using Microsoft.EntityFrameworkCore;
+using Treasury.App.Application.Transactions;
 using Treasury.App.Contracts.Transactions;
 using Treasury.App.Domain;
 using Treasury.App.Infrastructure.Data;
 
 namespace Treasury.App.Endpoints.Transactions;
 
-public sealed class CreateTransactionEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+public sealed class CreateTransactionEndpoint(
+    TreasuryDbContext db,
+    UserManager<ApplicationUser> userManager,
+    AccountBalanceRecalculationService dbBalanceRecalculationService)
     : Endpoint<CreateTransactionRequest>
 {
     public override void Configure()
     {
         Post("/api/transactions");
         Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
     }
 
     public override async Task HandleAsync(CreateTransactionRequest request, CancellationToken ct)
     {
@@ -47,70 +51,85 @@ public sealed class CreateTransactionEndpoint(TreasuryDbContext db, UserManager<
             await SendNotFoundAsync(ct);
             return;
         }
 
         if (account.OwnerUserId != user.Id)
         {
             await SendForbiddenAsync(ct);
             return;
         }
 
-        var normalizedType = (request.Type ?? "expense").Trim().ToLowerInvariant();
-        var delta = request.Amount;
-        if (normalizedType == "expense")
+        // Block creating transactions on inactive accounts
+        if (!account.IsActive)
         {
-            delta = -Math.Abs(request.Amount);
-        }
-        else if (normalizedType == "income")
-        {
-            delta = Math.Abs(request.Amount);
-        }
-        else if (normalizedType == "transfer")
-        {
-            delta = request.Amount;
+            AddError(x => x.AccountId, "Cannot create transactions on an inactive account.");
+            await SendErrorsAsync(cancellation: ct);
+            return;
         }
 
+        var normalizedType = TransactionBalanceMath.NormalizeType(request.Type);
+        var delta = TransactionBalanceMath.GetDelta(request.Amount, normalizedType);
+
+        var utcNow = DateTime.UtcNow;
         var transaction = new Transaction
         {
             HouseholdId = user.HouseholdId,
             AccountId = account.Id,
             Description = request.Description.Trim(),
             Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
             Amount = request.Amount,
             Currency = string.IsNullOrWhiteSpace(request.Currency) ? account.Currency : request.Currency.Trim().ToUpperInvariant(),
             Type = normalizedType,
-            TransactionDate = request.TransactionDate == default ? DateTime.UtcNow : request.TransactionDate
+            TransactionDate = request.TransactionDate == default ? utcNow : request.TransactionDate,
+            CreatedAt = utcNow,
+            UpdatedAt = utcNow
         };
 
         var validTagIds = await db.Tags
             .Where(x => x.HouseholdId == user.HouseholdId && request.TagIds.Contains(x.Id))
             .Select(x => x.Id)
             .ToListAsync(ct);
 
         foreach (var tagId in validTagIds)
         {
             transaction.TransactionTags.Add(new TransactionTag
             {
                 Transaction = transaction,
                 TagId = tagId
             });
         }
 
-        db.Transactions.Add(transaction);
-        account.CurrentBalance += delta;
-        account.UpdatedAt = DateTime.UtcNow;
-        await db.SaveChangesAsync(ct);
+        async Task PersistAsync()
+        {
+            db.Transactions.Add(transaction);
+            account.CurrentBalance += delta;
+            account.UpdatedAt = utcNow;
+            await db.SaveChangesAsync(ct);
+            await dbBalanceRecalculationService.RecalculateAccountAsync(account.Id, ct);
+        }
+
+        if (db.Database.IsRelational())
+        {
+            await using var transactionScope = await db.Database.BeginTransactionAsync(ct);
+            await PersistAsync();
+            await transactionScope.CommitAsync(ct);
+        }
+        else
+        {
+            await PersistAsync();
+        }
 
         await SendAsync(new
         {
             transaction.Id,
             transaction.AccountId,
             transaction.Description,
             transaction.Category,
             transaction.Amount,
             transaction.Currency,
             transaction.Type,
             transaction.TransactionDate,
+            transaction.BalanceAfterTransaction,
             Tags = validTagIds
         }, StatusCodes.Status201Created, ct);
     }
 }
diff --git a/src/Treasury.App/Endpoints/Transactions/GetTransactionsEndpoint.cs b/src/Treasury.App/Endpoints/Transactions/GetTransactionsEndpoint.cs
index e31b52e..205dd76 100644
--- a/src/Treasury.App/Endpoints/Transactions/GetTransactionsEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Transactions/GetTransactionsEndpoint.cs
@@ -27,30 +27,33 @@ public sealed class GetTransactionsEndpoint(TreasuryDbContext db, UserManager<Ap
             .Where(x =>
                 x.HouseholdId == user.HouseholdId
                 && (x.OwnerUserId == user.Id
                     || x.OwnerUserId == "seed"
                     || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
             .Select(x => x.Id);
 
         var transactions = await db.Transactions
             .Where(x => x.HouseholdId == user.HouseholdId && visibleAccountIds.Contains(x.AccountId))
             .OrderByDescending(x => x.TransactionDate)
+            .ThenByDescending(x => x.CreatedAt)
+            .ThenByDescending(x => x.Id)
             .Select(x => new
             {
                 x.Id,
                 x.AccountId,
                 x.Description,
                 x.Category,
                 x.Amount,
                 x.Currency,
                 x.Type,
                 x.TransactionDate,
+                x.BalanceAfterTransaction,
                 Tags = x.TransactionTags.Select(tt => new
                 {
                     tt.Tag.Id,
                     tt.Tag.Name,
                     tt.Tag.Color
                 }).ToList()
             })
             .ToListAsync(ct);
 
         await SendOkAsync(transactions, ct);
diff --git a/src/Treasury.App/Endpoints/Transactions/UpdateTransactionEndpoint.cs b/src/Treasury.App/Endpoints/Transactions/UpdateTransactionEndpoint.cs
new file mode 100644
index 0000000..57e7f4d
--- /dev/null
+++ b/src/Treasury.App/Endpoints/Transactions/UpdateTransactionEndpoint.cs
@@ -0,0 +1,75 @@
+using FastEndpoints;
+using Microsoft.AspNetCore.Identity;
+using Treasury.App.Application.Transactions;
+using Treasury.App.Contracts.Transactions;
+using Treasury.App.Domain;
+
+namespace Treasury.App.Endpoints.Transactions;
+
+public sealed class UpdateTransactionEndpoint(
+    TransactionEditingService transactionEditingService,
+    UserManager<ApplicationUser> userManager)
+    : Endpoint<UpdateTransactionRequest>
+{
+    public override void Configure()
+    {
+        Put("/api/transactions/{id:guid}");
+        Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
+    }
+
+    public override async Task HandleAsync(UpdateTransactionRequest request, CancellationToken ct)
+    {
+        var user = await userManager.GetUserAsync(User);
+        if (user is null)
+        {
+            await SendUnauthorizedAsync(ct);
+            return;
+        }
+
+        var result = await transactionEditingService.UpdateAsync(user, request, ct);
+        if (!result.Succeeded)
+        {
+            if (result.Status == TransactionEditStatus.NotFound)
+            {
+                await SendNotFoundAsync(ct);
+                return;
+            }
+
+            if (result.Status == TransactionEditStatus.Forbidden)
+            {
+                await SendForbiddenAsync(ct);
+                return;
+            }
+
+            if (result.Issues.Count > 0)
+            {
+                foreach (var issue in result.Issues)
+                {
+                    AddError(issue.Message);
+                }
+            }
+            else if (!string.IsNullOrWhiteSpace(result.Message))
+            {
+                AddError(result.Message);
+            }
+
+            await SendErrorsAsync(cancellation: ct);
+            return;
+        }
+
+        var outcome = result.Outcome!;
+        await SendAsync(new
+        {
+            outcome.Id,
+            outcome.AccountId,
+            outcome.Description,
+            outcome.Category,
+            outcome.Amount,
+            outcome.Currency,
+            outcome.Type,
+            outcome.TransactionDate,
+            outcome.BalanceAfterTransaction,
+            Tags = outcome.TagIds
+        }, StatusCodes.Status200OK, ct);
+    }
+}
diff --git a/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs b/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs
index b33048a..dcc3d3d 100644
--- a/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs
@@ -1,127 +1,80 @@
 using FastEndpoints;
 using Microsoft.AspNetCore.Identity;
-using Microsoft.EntityFrameworkCore;
 using Treasury.App.Contracts.Transactions;
+using Treasury.App.Application.Transfers;
 using Treasury.App.Domain;
-using Treasury.App.Infrastructure.Data;
 
 namespace Treasury.App.Endpoints.Transfers;
 
-public sealed class CreateTransferEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+public sealed class CreateTransferEndpoint(
+    TransferCreationService transferCreationService,
+    UserManager<ApplicationUser> userManager)
     : Endpoint<CreateTransferRequest>
 {
     public override void Configure()
     {
         Post("/api/transfers");
         Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
     }
 
     public override async Task HandleAsync(CreateTransferRequest request, CancellationToken ct)
     {
         var user = await userManager.GetUserAsync(User);
         if (user is null)
         {
             await SendUnauthorizedAsync(ct);
             return;
         }
 
-        if (request.FromAccountId == Guid.Empty || request.ToAccountId == Guid.Empty || request.FromAccountId == request.ToAccountId || request.Amount <= 0m)
+        var result = await transferCreationService.CreateAsync(user, request, ct);
+        if (!result.Succeeded)
         {
-            if (request.FromAccountId == Guid.Empty || request.FromAccountId == request.ToAccountId)
+            if (result.Status == TransferCreationStatus.NotFound)
             {
-                AddError(x => x.FromAccountId, "A valid source account is required.");
+                await SendNotFoundAsync(ct);
+                return;
             }
 
-            if (request.ToAccountId == Guid.Empty || request.FromAccountId == request.ToAccountId)
+            if (result.Status == TransferCreationStatus.Forbidden)
             {
-                AddError(x => x.ToAccountId, "A valid destination account is required.");
+                await SendForbiddenAsync(ct);
+                return;
             }
 
-            if (request.Amount <= 0m)
+            foreach (var issue in result.Issues)
             {
-                AddError(x => x.Amount, "Transfer amount must be greater than zero.");
+                switch (issue.Field)
+                {
+                    case nameof(CreateTransferRequest.FromAccountId):
+                        AddError(x => x.FromAccountId, issue.Message);
+                        break;
+                    case nameof(CreateTransferRequest.ToAccountId):
+                        AddError(x => x.ToAccountId, issue.Message);
+                        break;
+                    case nameof(CreateTransferRequest.Amount):
+                        AddError(x => x.Amount, issue.Message);
+                        break;
+                    default:
+                        AddError(issue.Message);
+                        break;
+                }
             }
 
             await SendErrorsAsync(cancellation: ct);
             return;
         }
 
-        var fromAccount = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.FromAccountId && x.HouseholdId == user.HouseholdId, ct);
-        var toAccount = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.ToAccountId && x.HouseholdId == user.HouseholdId, ct);
-        if (fromAccount is null || toAccount is null)
-        {
-            await SendNotFoundAsync(ct);
-            return;
-        }
-
-        if (fromAccount.OwnerUserId != user.Id || toAccount.OwnerUserId != user.Id)
-        {
-            await SendForbiddenAsync(ct);
-            return;
-        }
-
-        var transferDate = request.TransferDate == default ? DateTime.UtcNow : request.TransferDate;
-        var currency = string.IsNullOrWhiteSpace(request.Currency) ? fromAccount.Currency : request.Currency.Trim().ToUpperInvariant();
-        var description = string.IsNullOrWhiteSpace(request.Description) ? "Account transfer" : request.Description.Trim();
-
-        var transfer = new Transfer
-        {
-            HouseholdId = user.HouseholdId,
-            FromAccountId = fromAccount.Id,
-            ToAccountId = toAccount.Id,
-            Amount = request.Amount,
-            Currency = currency,
-            Description = description,
-            TransferDate = transferDate,
-            CreatedAt = DateTime.UtcNow
-        };
-
-        var outflow = new Transaction
-        {
-            HouseholdId = user.HouseholdId,
-            AccountId = fromAccount.Id,
-            Description = $"{description} -> {toAccount.Name}",
-            Category = "Transfer",
-            Amount = request.Amount,
-            Currency = currency,
-            Type = "transfer-out",
-            TransactionDate = transferDate,
-            CreatedAt = DateTime.UtcNow,
-            UpdatedAt = DateTime.UtcNow
-        };
-
-        var inflow = new Transaction
-        {
-            HouseholdId = user.HouseholdId,
-            AccountId = toAccount.Id,
-            Description = $"{description} <- {fromAccount.Name}",
-            Category = "Transfer",
-            Amount = request.Amount,
-            Currency = currency,
-            Type = "transfer-in",
-            TransactionDate = transferDate,
-            CreatedAt = DateTime.UtcNow,
-            UpdatedAt = DateTime.UtcNow
-        };
-
-        fromAccount.CurrentBalance -= Math.Abs(request.Amount);
-        toAccount.CurrentBalance += Math.Abs(request.Amount);
-        fromAccount.UpdatedAt = DateTime.UtcNow;
-        toAccount.UpdatedAt = DateTime.UtcNow;
-
-        db.Transfers.Add(transfer);
-        db.Transactions.Add(outflow);
-        db.Transactions.Add(inflow);
-        await db.SaveChangesAsync(ct);
-
+        var outcome = result.Outcome!;
         await SendAsync(new
         {
-            transfer.Id,
-            transfer.FromAccountId,
-            transfer.ToAccountId,
-            transfer.Amount,
-            transfer.Currency,
-            transfer.TransferDate
+            Id = outcome.TransferId,
+            outcome.FromAccountId,
+            outcome.ToAccountId,
+            OutflowTransactionId = outcome.OutflowTransactionId,
+            InflowTransactionId = outcome.InflowTransactionId,
+            outcome.Amount,
+            outcome.Currency,
+            outcome.TransferDate
         }, StatusCodes.Status201Created, ct);
     }
 }
diff --git a/src/Treasury.App/Infrastructure/Data/Seed/InitialSeed.cs b/src/Treasury.App/Infrastructure/Data/Seed/InitialSeed.cs
index 1e48e5d..6150421 100644
--- a/src/Treasury.App/Infrastructure/Data/Seed/InitialSeed.cs
+++ b/src/Treasury.App/Infrastructure/Data/Seed/InitialSeed.cs
@@ -107,46 +107,49 @@ public static class InitialSeed
                     new Transaction
                     {
                         HouseholdId = householdId,
                         AccountId = mainWallet,
                         Description = "Salary deposit",
                         Category = "Income",
                         Amount = 4200.00m,
                         Currency = "PLN",
                         Type = "income",
                         TransactionDate = DateTime.UtcNow.AddDays(-2),
+                        BalanceAfterTransaction = 13320.90m,
                         CreatedAt = DateTime.UtcNow,
                         UpdatedAt = DateTime.UtcNow
                     },
                     new Transaction
                     {
                         HouseholdId = householdId,
                         AccountId = mainWallet,
                         Description = "Groceries",
                         Category = "Groceries",
                         Amount = 480.32m,
                         Currency = "PLN",
                         Type = "expense",
                         TransactionDate = DateTime.UtcNow.AddDays(-1),
+                        BalanceAfterTransaction = 12840.58m,
                         CreatedAt = DateTime.UtcNow,
                         UpdatedAt = DateTime.UtcNow
                     },
                     new Transaction
                     {
                         HouseholdId = householdId,
                         AccountId = mainWallet,
                         Description = "Home savings transfer",
                         Category = "Savings",
                         Amount = 1000.00m,
                         Currency = "PLN",
                         Type = "transfer",
                         TransactionDate = DateTime.UtcNow.AddDays(-3),
+                        BalanceAfterTransaction = 9120.90m,
                         CreatedAt = DateTime.UtcNow,
                         UpdatedAt = DateTime.UtcNow
                     });
             }
         }
 
         if (!await db.CurrencyRates.AnyAsync(cancellationToken))
         {
             var householdId = Guid.Parse("11111111-1111-1111-1111-111111111111");
             db.CurrencyRates.AddRange(
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
diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903134028_AddTransactionBalanceAfterTransaction.Designer.cs b/src/Treasury.App/Infrastructure/Migrations/20260903134028_AddTransactionBalanceAfterTransaction.Designer.cs
new file mode 100644
index 0000000..b7a4ea8
--- /dev/null
+++ b/src/Treasury.App/Infrastructure/Migrations/20260903134028_AddTransactionBalanceAfterTransaction.Designer.cs
@@ -0,0 +1,747 @@
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
+    [Migration("20260903134028_AddTransactionBalanceAfterTransaction")]
+    partial class AddTransactionBalanceAfterTransaction
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
+                    b.Property<decimal>("BalanceAfterTransaction")
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
+                    b.Property<Guid?>("InflowTransactionId")
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid?>("OutflowTransactionId")
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
diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903134028_AddTransactionBalanceAfterTransaction.cs b/src/Treasury.App/Infrastructure/Migrations/20260903134028_AddTransactionBalanceAfterTransaction.cs
new file mode 100644
index 0000000..227c7c1
--- /dev/null
+++ b/src/Treasury.App/Infrastructure/Migrations/20260903134028_AddTransactionBalanceAfterTransaction.cs
@@ -0,0 +1,29 @@
+﻿using Microsoft.EntityFrameworkCore.Migrations;
+
+#nullable disable
+
+namespace Treasury.App.Infrastructure.Migrations
+{
+    /// <inheritdoc />
+    public partial class AddTransactionBalanceAfterTransaction : Migration
+    {
+        /// <inheritdoc />
+        protected override void Up(MigrationBuilder migrationBuilder)
+        {
+            migrationBuilder.AddColumn<decimal>(
+                name: "BalanceAfterTransaction",
+                table: "Transactions",
+                type: "numeric",
+                nullable: false,
+                defaultValue: 0m);
+        }
+
+        /// <inheritdoc />
+        protected override void Down(MigrationBuilder migrationBuilder)
+        {
+            migrationBuilder.DropColumn(
+                name: "BalanceAfterTransaction",
+                table: "Transactions");
+        }
+    }
+}
diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903153000_AddTransferTransactionLinks.Designer.cs b/src/Treasury.App/Infrastructure/Migrations/20260903153000_AddTransferTransactionLinks.Designer.cs
new file mode 100644
index 0000000..6acd1b6
--- /dev/null
+++ b/src/Treasury.App/Infrastructure/Migrations/20260903153000_AddTransferTransactionLinks.Designer.cs
@@ -0,0 +1,744 @@
+// <auto-generated />
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
+    [Migration("20260903153000_AddTransferTransactionLinks")]
+    partial class AddTransferTransactionLinks
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
+                        modelBuilder.Entity("Treasury.App.Domain.Transfer", b =>
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
+                    b.Property<Guid?>("InflowTransactionId")
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid?>("OutflowTransactionId")
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
diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903153000_AddTransferTransactionLinks.cs b/src/Treasury.App/Infrastructure/Migrations/20260903153000_AddTransferTransactionLinks.cs
new file mode 100644
index 0000000..d28c674
--- /dev/null
+++ b/src/Treasury.App/Infrastructure/Migrations/20260903153000_AddTransferTransactionLinks.cs
@@ -0,0 +1,38 @@
+﻿using Microsoft.EntityFrameworkCore.Migrations;
+
+#nullable disable
+
+namespace Treasury.App.Infrastructure.Migrations
+{
+    /// <inheritdoc />
+    public partial class AddTransferTransactionLinks : Migration
+    {
+        /// <inheritdoc />
+        protected override void Up(MigrationBuilder migrationBuilder)
+        {
+            migrationBuilder.AddColumn<Guid>(
+                name: "InflowTransactionId",
+                table: "Transfers",
+                type: "uuid",
+                nullable: true);
+
+            migrationBuilder.AddColumn<Guid>(
+                name: "OutflowTransactionId",
+                table: "Transfers",
+                type: "uuid",
+                nullable: true);
+        }
+
+        /// <inheritdoc />
+        protected override void Down(MigrationBuilder migrationBuilder)
+        {
+            migrationBuilder.DropColumn(
+                name: "InflowTransactionId",
+                table: "Transfers");
+
+            migrationBuilder.DropColumn(
+                name: "OutflowTransactionId",
+                table: "Transfers");
+        }
+    }
+}
diff --git a/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs b/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
index 2037f1f..9340be2 100644
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
@@ -514,20 +523,23 @@ namespace Treasury.App.Infrastructure.Migrations
                     b.Property<Guid>("Id")
                         .ValueGeneratedOnAdd()
                         .HasColumnType("uuid");
 
                     b.Property<Guid>("AccountId")
                         .HasColumnType("uuid");
 
                     b.Property<decimal>("Amount")
                         .HasColumnType("numeric");
 
+                    b.Property<decimal>("BalanceAfterTransaction")
+                        .HasColumnType("numeric");
+
                     b.Property<string>("Category")
                         .IsRequired()
                         .HasColumnType("text");
 
                     b.Property<DateTime>("CreatedAt")
                         .HasColumnType("timestamp with time zone");
 
                     b.Property<string>("Currency")
                         .IsRequired()
                         .HasColumnType("text");
@@ -588,20 +600,26 @@ namespace Treasury.App.Infrastructure.Migrations
                     b.Property<string>("Description")
                         .IsRequired()
                         .HasColumnType("text");
 
                     b.Property<Guid>("FromAccountId")
                         .HasColumnType("uuid");
 
                     b.Property<Guid>("HouseholdId")
                         .HasColumnType("uuid");
 
+                    b.Property<Guid?>("InflowTransactionId")
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid?>("OutflowTransactionId")
+                        .HasColumnType("uuid");
+
                     b.Property<Guid>("ToAccountId")
                         .HasColumnType("uuid");
 
                     b.Property<DateTime>("TransferDate")
                         .HasColumnType("timestamp with time zone");
 
                     b.HasKey("Id");
 
                     b.ToTable("Transfers");
                 });
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
index dea65c2..e45d3c2 100644
--- a/src/Treasury.App/Pages/Transactions.razor
+++ b/src/Treasury.App/Pages/Transactions.razor
@@ -1,28 +1,39 @@
 @page "/transactions"
 @attribute [Authorize]
 @inject TreasuryDbContext DbContext
+@inject AuthenticationStateProvider AuthenticationStateProvider
+@inject UserManager<ApplicationUser> UserManager
+@inject TransactionEditingService TransactionEditingService
+@inject AccountBalanceRecalculationService BalanceRecalculationService
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
@@ -56,36 +67,111 @@
                                      Label="@tag.Name" />
                     }
                 </MudStack>
             }
 
             <MudStack Row="true" Class="mt-4" Justify="Justify.FlexEnd">
                 <MudButton Variant="Variant.Filled" Color="Color.Tertiary" OnClick="CreateTransactionAsync">Add transaction</MudButton>
             </MudStack>
         </MudPaper>
 
+        @if (_editingTransaction is not null)
+        {
+            <MudPaper Class="pa-4 rounded-xl" Elevation="2">
+                <MudStack Spacing="3">
+                    <MudStack Spacing="1">
+                        <MudText Typo="Typo.subtitle1">Edit transaction</MudText>
+                        <MudText Typo="Typo.body2">
+                            @(_editingTransaction.IsLatest
+                                ? "This is the latest transaction for the account, so amount, date, and type can be edited."
+                                : "This is not the latest transaction, so only description, category, and tags can be edited.")
+                        </MudText>
+                    </MudStack>
+
+                    <MudGrid>
+                        <MudItem xs="12" md="4">
+                            <MudTextField Label="Description" @bind-Value="_editingTransaction.Description" Required="true" />
+                        </MudItem>
+                        <MudItem xs="12" md="3">
+                            <MudTextField Label="Category" @bind-Value="_editingTransaction.Category" />
+                        </MudItem>
+                        <MudItem xs="12" md="2">
+                            <MudNumericField Label="Amount"
+                                             @bind-Value="_editingTransaction.Amount"
+                                             Required="true"
+                                             Min="0.01m"
+                                             Disabled="@(!_editingTransaction.IsLatest)" />
+                        </MudItem>
+                        <MudItem xs="12" md="2">
+                            <MudSelect T="string"
+                                       Label="Type"
+                                       @bind-Value="_editingTransaction.Type"
+                                       Disabled="@(!_editingTransaction.IsLatest)">
+                                <MudSelectItem Value="@("income")">Income</MudSelectItem>
+                                <MudSelectItem Value="@("expense")">Expense</MudSelectItem>
+                                <MudSelectItem Value="@("transfer")">Transfer</MudSelectItem>
+                                <MudSelectItem Value="@("transfer-out")">Transfer out</MudSelectItem>
+                                <MudSelectItem Value="@("transfer-in")">Transfer in</MudSelectItem>
+                            </MudSelect>
+                        </MudItem>
+                        <MudItem xs="12" md="1">
+                            <MudDatePicker Label="Date"
+                                           @bind-Date="_editingTransaction.TransactionDate"
+                                           Disabled="@(!_editingTransaction.IsLatest)" />
+                        </MudItem>
+                    </MudGrid>
+
+                    <MudDivider />
+
+                    <MudText Typo="Typo.subtitle2">Tags</MudText>
+                    @if (_tags.Count == 0)
+                    {
+                        <MudText Typo="Typo.body2" Class="mt-2">No tags yet. Add one from the Tags page.</MudText>
+                    }
+                    else
+                    {
+                        <MudStack Row="true" Class="mt-2" Wrap="Wrap.Wrap">
+                            @foreach (var tag in _tags)
+                            {
+                                <MudCheckBox T="bool"
+                                             Value="@_editingSelectedTagIds.Contains(tag.Id)"
+                                             ValueChanged="@(value => ToggleEditingTag(tag.Id, value))"
+                                             Label="@tag.Name" />
+                            }
+                        </MudStack>
+                    }
+
+                    <MudStack Row="true" Justify="Justify.FlexEnd" Spacing="2">
+                        <MudButton Variant="Variant.Outlined" OnClick="CancelEditAsync">Cancel</MudButton>
+                        <MudButton Variant="Variant.Filled" Color="Color.Tertiary" OnClick="SaveTransactionEditAsync">Save changes</MudButton>
+                    </MudStack>
+                </MudStack>
+            </MudPaper>
+        }
+
         @if (_transactions.Count == 0)
         {
             <MudAlert Severity="Severity.Info">No transactions yet. Add your first household transaction to start tracking cash flow.</MudAlert>
         }
         else
         {
             <MudPaper Class="pa-4 rounded-xl" Elevation="2">
                 <MudTable T="Treasury.App.Domain.Transaction" Items="_transactions" Hover="true" Dense="true">
                     <HeaderContent>
                         <MudTh>Description</MudTh>
                         <MudTh>Category</MudTh>
                         <MudTh>Tags</MudTh>
                         <MudTh>Account</MudTh>
                         <MudTh>Type</MudTh>
                         <MudTh>Amount</MudTh>
                         <MudTh>Date</MudTh>
+                        <MudTh>Actions</MudTh>
                     </HeaderContent>
                     <RowTemplate>
                         <MudTd DataLabel="Description">@context.Description</MudTd>
                         <MudTd DataLabel="Category">@context.Category</MudTd>
                         <MudTd DataLabel="Tags">
                             @if (_transactionTags.TryGetValue(context.Id, out var tags) && tags.Count > 0)
                             {
                                 foreach (var tag in tags)
                                 {
                                     <MudChip T="string" Size="Size.Small" Style="@($"background:{tag.Color}; color:white;")">@tag.Name</MudChip>
@@ -93,173 +179,392 @@
                             }
                             else
                             {
                                 <MudText Typo="Typo.caption">No tags</MudText>
                             }
                         </MudTd>
                         <MudTd DataLabel="Account">@GetAccountName(context.AccountId)</MudTd>
                         <MudTd DataLabel="Type">@context.Type</MudTd>
                         <MudTd DataLabel="Amount">@context.Amount.ToString("N2") @context.Currency</MudTd>
                         <MudTd DataLabel="Date">@context.TransactionDate.ToLocalTime().ToString("yyyy-MM-dd")</MudTd>
+                        <MudTd DataLabel="Actions">
+                            <MudStack Spacing="0">
+                                <MudButton Size="Size.Small"
+                                           Variant="Variant.Outlined"
+                                           Disabled="@(!CanEditTransaction(context))"
+                                           OnClick="@(() => BeginEdit(context))">
+                                    Edit
+                                </MudButton>
+                                @if (IsTransferLinked(context))
+                                {
+                                    <MudText Typo="Typo.caption" Color="Color.Warning">Transfer-linked</MudText>
+                                }
+                            </MudStack>
+                        </MudTd>
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
+    private readonly HashSet<Guid> _transferLinkedTransactionIds = new();
     private readonly HashSet<Guid> _selectedTagIds = new();
+    private readonly HashSet<Guid> _editingSelectedTagIds = new();
 
     private readonly NewTransactionForm _newTransaction = new();
+    private EditingTransactionForm? _editingTransaction;
+    private ApplicationUser? _currentUser;
 
     protected override async Task OnInitializedAsync()
     {
+        _currentUser = await GetCurrentUserAsync();
         await LoadAsync();
     }
 
     private async Task LoadAsync()
     {
         _accounts.Clear();
-        _accounts.AddRange(await DbContext.Accounts.OrderBy(x => x.Name).ToListAsync());
+        if (_currentUser is not null)
+        {
+            _accounts.AddRange(await DbContext.Accounts
+                .Where(x => x.IsActive && x.OwnerUserId == _currentUser.Id)
+                .OrderBy(x => x.Name)
+                .ToListAsync());
+        }
+
+        // Load full visible account list (includes inactive) so historical transactions still show account names
+        _visibleAccounts.Clear();
+        _visibleAccounts.AddRange(await DbContext.Accounts.OrderBy(x => x.Name).ToListAsync());
 
         _tags.Clear();
         _tags.AddRange(await DbContext.Tags.OrderBy(x => x.Name).ToListAsync());
 
+        _transferLinkedTransactionIds.Clear();
+        var transferLinks = await DbContext.Transfers
+            .Select(x => new { x.OutflowTransactionId, x.InflowTransactionId })
+            .ToListAsync();
+        foreach (var link in transferLinks)
+        {
+            if (link.OutflowTransactionId.HasValue)
+            {
+                _transferLinkedTransactionIds.Add(link.OutflowTransactionId.Value);
+            }
+
+            if (link.InflowTransactionId.HasValue)
+            {
+                _transferLinkedTransactionIds.Add(link.InflowTransactionId.Value);
+            }
+        }
+
         _transactions.Clear();
-        _transactions.AddRange(await DbContext.Transactions.OrderByDescending(x => x.TransactionDate).ToListAsync());
+        _transactions.AddRange(await DbContext.Transactions
+            .OrderByDescending(x => x.TransactionDate)
+            .ThenByDescending(x => x.CreatedAt)
+            .ThenByDescending(x => x.Id)
+            .ToListAsync());
 
         _transactionTags.Clear();
         var tagLinks = await DbContext.TransactionTags
             .Include(x => x.Tag)
             .Where(x => _transactions.Select(t => t.Id).Contains(x.TransactionId))
             .ToListAsync();
 
         foreach (var group in tagLinks.GroupBy(x => x.TransactionId))
         {
             _transactionTags[group.Key] = group.Select(x => x.Tag).ToList();
         }
 
         if (_accounts.Count > 0 && string.IsNullOrWhiteSpace(_newTransaction.AccountId))
         {
             _newTransaction.AccountId = _accounts[0].Id.ToString();
         }
     }
 
+    private async Task<ApplicationUser?> GetCurrentUserAsync()
+    {
+        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
+        return await UserManager.GetUserAsync(authState.User);
+    }
+
     private void ToggleTag(Guid tagId, bool isSelected)
     {
         if (isSelected)
         {
             _selectedTagIds.Add(tagId);
             return;
         }
 
         _selectedTagIds.Remove(tagId);
     }
 
     private async Task CreateTransactionAsync()
     {
         if (_accounts.Count == 0)
         {
             Snackbar.Add("Create an account before adding transactions.", Severity.Warning);
             return;
         }
 
+        var currentUser = _currentUser ?? await GetCurrentUserAsync();
+        if (currentUser is null)
+        {
+            Snackbar.Add("You need to sign in again before adding transactions.", Severity.Error);
+            return;
+        }
+
         if (string.IsNullOrWhiteSpace(_newTransaction.Description) || string.IsNullOrWhiteSpace(_newTransaction.AccountId) || _newTransaction.Amount <= 0m)
         {
             Snackbar.Add("Description, account and a positive amount are required.", Severity.Warning);
             return;
         }
 
         if (!Guid.TryParse(_newTransaction.AccountId, out var accountId))
         {
             Snackbar.Add("Please select a valid account.", Severity.Warning);
             return;
         }
 
         var account = await DbContext.Accounts.SingleOrDefaultAsync(x => x.Id == accountId);
         if (account is null)
         {
             Snackbar.Add("Selected account could not be found.", Severity.Error);
             return;
         }
 
-        var normalizedType = (_newTransaction.Type ?? "expense").Trim().ToLowerInvariant();
-        var delta = _newTransaction.Amount;
-        if (normalizedType == "expense")
-        {
-            delta = -Math.Abs(_newTransaction.Amount);
-        }
-        else if (normalizedType == "income")
+        // Prevent creating transactions against inactive accounts via the UI save path
+        if (!account.IsActive)
         {
-            delta = Math.Abs(_newTransaction.Amount);
+            Snackbar.Add("Selected account is inactive. Reactivate it before adding transactions.", Severity.Warning);
+            return;
         }
-        else if (normalizedType == "transfer")
+
+        if (account.OwnerUserId != currentUser.Id)
         {
-            delta = _newTransaction.Amount;
+            Snackbar.Add("You can only add transactions to your own accounts.", Severity.Warning);
+            return;
         }
 
+        var normalizedType = TransactionBalanceMath.NormalizeType(_newTransaction.Type);
+        var delta = TransactionBalanceMath.GetDelta(_newTransaction.Amount, normalizedType);
+        var utcNow = DateTime.UtcNow;
+
         var transaction = new Treasury.App.Domain.Transaction
         {
             HouseholdId = account.HouseholdId,
             AccountId = account.Id,
             Description = _newTransaction.Description.Trim(),
             Category = string.IsNullOrWhiteSpace(_newTransaction.Category) ? "General" : _newTransaction.Category.Trim(),
             Amount = _newTransaction.Amount,
             Currency = account.Currency,
             Type = normalizedType,
-            TransactionDate = _newTransaction.TransactionDate ?? DateTime.UtcNow,
-            CreatedAt = DateTime.UtcNow,
-            UpdatedAt = DateTime.UtcNow
+            TransactionDate = _newTransaction.TransactionDate ?? utcNow,
+            CreatedAt = utcNow,
+            UpdatedAt = utcNow
         };
 
         foreach (var tagId in _selectedTagIds)
         {
             var tag = _tags.FirstOrDefault(x => x.Id == tagId);
             if (tag is null)
             {
                 continue;
             }
 
             transaction.TransactionTags.Add(new Treasury.App.Domain.TransactionTag
             {
                 Transaction = transaction,
                 TagId = tag.Id,
                 Tag = tag
             });
         }
 
-        DbContext.Transactions.Add(transaction);
-        account.CurrentBalance += delta;
-        account.UpdatedAt = DateTime.UtcNow;
-        await DbContext.SaveChangesAsync();
+        async Task PersistAsync()
+        {
+            DbContext.Transactions.Add(transaction);
+            account.CurrentBalance += delta;
+            account.UpdatedAt = utcNow;
+            await DbContext.SaveChangesAsync();
+            await BalanceRecalculationService.RecalculateAccountAsync(account.Id, CancellationToken.None);
+        }
+
+        if (DbContext.Database.IsRelational())
+        {
+            await using var transactionScope = await DbContext.Database.BeginTransactionAsync();
+            await PersistAsync();
+            await transactionScope.CommitAsync();
+        }
+        else
+        {
+            await PersistAsync();
+        }
 
         _newTransaction.Description = string.Empty;
         _newTransaction.Category = "General";
         _newTransaction.Amount = 0m;
         _newTransaction.Type = "expense";
         _newTransaction.TransactionDate = DateTime.UtcNow;
         _selectedTagIds.Clear();
 
         Snackbar.Add("Transaction saved.", Severity.Success);
         await LoadAsync();
     }
 
+    private void BeginEdit(Treasury.App.Domain.Transaction transaction)
+    {
+        if (!CanEditTransaction(transaction))
+        {
+            Snackbar.Add("Transfer-linked transactions and transactions from shared accounts cannot be edited here.", Severity.Warning);
+            return;
+        }
+
+        _editingTransaction = new EditingTransactionForm
+        {
+            Id = transaction.Id,
+            AccountId = transaction.AccountId,
+            Description = transaction.Description,
+            Category = transaction.Category,
+            Amount = transaction.Amount,
+            Type = transaction.Type,
+            TransactionDate = transaction.TransactionDate,
+            IsLatest = IsLatestTransaction(transaction)
+        };
+
+        _editingSelectedTagIds.Clear();
+        if (_transactionTags.TryGetValue(transaction.Id, out var tags))
+        {
+            foreach (var tag in tags)
+            {
+                _editingSelectedTagIds.Add(tag.Id);
+            }
+        }
+    }
+
+    private Task CancelEditAsync()
+    {
+        _editingTransaction = null;
+        _editingSelectedTagIds.Clear();
+        return Task.CompletedTask;
+    }
+
+    private void ToggleEditingTag(Guid tagId, bool isSelected)
+    {
+        if (isSelected)
+        {
+            _editingSelectedTagIds.Add(tagId);
+            return;
+        }
+
+        _editingSelectedTagIds.Remove(tagId);
+    }
+
+    private async Task SaveTransactionEditAsync()
+    {
+        if (_editingTransaction is null)
+        {
+            return;
+        }
+
+        var currentUser = _currentUser ?? await GetCurrentUserAsync();
+        if (currentUser is null)
+        {
+            Snackbar.Add("You need to sign in again before saving changes.", Severity.Error);
+            return;
+        }
+
+        var request = new UpdateTransactionRequest
+        {
+            Id = _editingTransaction.Id,
+            Description = _editingTransaction.Description,
+            Category = _editingTransaction.Category,
+            Amount = _editingTransaction.IsLatest ? _editingTransaction.Amount : null,
+            Type = _editingTransaction.IsLatest ? _editingTransaction.Type : null,
+            TransactionDate = _editingTransaction.IsLatest ? _editingTransaction.TransactionDate : null,
+            TagIds = _editingSelectedTagIds.ToList()
+        };
+
+        var result = await TransactionEditingService.UpdateAsync(currentUser, request, CancellationToken.None);
+        if (!result.Succeeded)
+        {
+            var message = result.Issues.Count > 0
+                ? string.Join(" ", result.Issues.Select(x => x.Message).Distinct())
+                : result.Message ?? "Unable to update transaction.";
+            var severity = result.Status == TransactionEditStatus.Forbidden || result.Status == TransactionEditStatus.NotFound
+                ? Severity.Error
+                : Severity.Warning;
+            Snackbar.Add(message, severity);
+            return;
+        }
+
+        Snackbar.Add("Transaction updated.", Severity.Success);
+        await CancelEditAsync();
+        await LoadAsync();
+    }
+
     private string GetAccountName(Guid accountId)
     {
-        return _accounts.FirstOrDefault(x => x.Id == accountId)?.Name ?? "Unknown account";
+        // Use the full visible accounts collection so historical transactions from inactive accounts still show a name
+        return _visibleAccounts.FirstOrDefault(x => x.Id == accountId)?.Name ?? "Unknown account";
+    }
+
+    private bool CanEditTransaction(Treasury.App.Domain.Transaction transaction)
+    {
+        if (_currentUser is null)
+        {
+            return false;
+        }
+
+        if (IsTransferLinked(transaction))
+        {
+            return false;
+        }
+
+        var account = _visibleAccounts.FirstOrDefault(x => x.Id == transaction.AccountId);
+        return account is not null && account.OwnerUserId == _currentUser.Id;
+    }
+
+    private bool IsTransferLinked(Treasury.App.Domain.Transaction transaction) =>
+        _transferLinkedTransactionIds.Contains(transaction.Id)
+        || transaction.Type is "transfer-in" or "transfer-out";
+
+    private bool IsLatestTransaction(Treasury.App.Domain.Transaction transaction)
+    {
+        var latestTransaction = _transactions
+            .Where(x => x.AccountId == transaction.AccountId)
+            .OrderByDescending(x => x.TransactionDate)
+            .ThenByDescending(x => x.CreatedAt)
+            .ThenByDescending(x => x.Id)
+            .FirstOrDefault();
+
+        return latestTransaction?.Id == transaction.Id;
     }
 
     private sealed class NewTransactionForm
     {
         public string AccountId { get; set; } = string.Empty;
         public string Description { get; set; } = string.Empty;
         public string Category { get; set; } = "General";
         public decimal Amount { get; set; }
         public string Type { get; set; } = "expense";
         public DateTime? TransactionDate { get; set; } = DateTime.UtcNow;
     }
+
+    private sealed class EditingTransactionForm
+    {
+        public Guid Id { get; set; }
+        public Guid AccountId { get; set; }
+        public string Description { get; set; } = string.Empty;
+        public string Category { get; set; } = "General";
+        public decimal Amount { get; set; }
+        public string Type { get; set; } = "expense";
+        public DateTime? TransactionDate { get; set; } = DateTime.UtcNow;
+        public bool IsLatest { get; set; }
+    }
 }
diff --git a/src/Treasury.App/Pages/Transfers.razor b/src/Treasury.App/Pages/Transfers.razor
new file mode 100644
index 0000000..f80df8b
--- /dev/null
+++ b/src/Treasury.App/Pages/Transfers.razor
@@ -0,0 +1,92 @@
+@page "/transfers"
+@attribute [Authorize]
+
+<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="pa-4 pa-sm-6">
+    <MudStack Spacing="3">
+        <MudText Typo="Typo.h4">Transfers</MudText>
+
+        @if (!string.IsNullOrWhiteSpace(_loadError))
+        {
+            <MudAlert Severity="Severity.Error">@_loadError</MudAlert>
+        }
+
+        <MudPaper Class="pa-4 rounded-xl" Elevation="2">
+            <MudGrid>
+                <MudItem xs="12" md="3">
+                    @if (_accounts.Count == 0)
+                    {
+                        <MudText Typo="Typo.body2" Class="mt-2">No active accounts available. Create or reactivate one of your accounts first.</MudText>
+                    }
+                    else
+                    {
+                        <MudSelect T="string" Label="From account" @bind-Value="_newTransfer.FromAccountId">
+                            @foreach (var account in _accounts)
+                            {
+                                <MudSelectItem Value="@account.Id.ToString()">@account.Name (@account.Currency)</MudSelectItem>
+                            }
+                        </MudSelect>
+                    }
+                </MudItem>
+                <MudItem xs="12" md="3">
+                    @if (_accounts.Count == 0)
+                    {
+                        <MudText Typo="Typo.body2" Class="mt-2">Transferring between active accounts is only available to account owners.</MudText>
+                    }
+                    else
+                    {
+                        <MudSelect T="string" Label="To account" @bind-Value="_newTransfer.ToAccountId">
+                            @foreach (var account in _accounts)
+                            {
+                                <MudSelectItem Value="@account.Id.ToString()">@account.Name (@account.Currency)</MudSelectItem>
+                            }
+                        </MudSelect>
+                    }
+                </MudItem>
+                <MudItem xs="12" md="2">
+                    <MudNumericField Label="Amount" @bind-Value="_newTransfer.Amount" Min="0.01m" />
+                </MudItem>
+                <MudItem xs="12" md="2">
+                    <MudDatePicker Label="Date" @bind-Date="_newTransfer.TransferDate" />
+                </MudItem>
+                <MudItem xs="12" md="2">
+                    <MudTextField Label="Description" @bind-Value="_newTransfer.Description" />
+                </MudItem>
+            </MudGrid>
+
+            <MudStack Row="true" Class="mt-4" Justify="Justify.FlexEnd">
+                <MudButton Variant="Variant.Filled" Color="Color.Tertiary" OnClick="CreateTransferAsync">Save transfer</MudButton>
+            </MudStack>
+        </MudPaper>
+
+        <MudPaper Class="pa-4 rounded-xl" Elevation="2">
+            <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center" Class="mb-3">
+                <MudText Typo="Typo.h6">Recent history</MudText>
+                <MudText Typo="Typo.body2" Color="Color.Secondary">Latest 10 transfers in your household</MudText>
+            </MudStack>
+
+            @if (_transfers.Count == 0)
+            {
+                <MudAlert Severity="Severity.Info">No transfers yet. Create your first transfer to see it listed here.</MudAlert>
+            }
+            else
+            {
+                <MudTable T="TransferHistoryRow" Items="_transfers" Hover="true" Dense="true">
+                    <HeaderContent>
+                        <MudTh>Date</MudTh>
+                        <MudTh>From</MudTh>
+                        <MudTh>To</MudTh>
+                        <MudTh>Description</MudTh>
+                        <MudTh>Amount</MudTh>
+                    </HeaderContent>
+                    <RowTemplate>
+                        <MudTd DataLabel="Date">@context.TransferDate.ToLocalTime().ToString("yyyy-MM-dd")</MudTd>
+                        <MudTd DataLabel="From">@context.FromAccountName</MudTd>
+                        <MudTd DataLabel="To">@context.ToAccountName</MudTd>
+                        <MudTd DataLabel="Description">@context.Description</MudTd>
+                        <MudTd DataLabel="Amount">@context.Amount.ToString("N2") @context.Currency</MudTd>
+                    </RowTemplate>
+                </MudTable>
+            }
+        </MudPaper>
+    </MudStack>
+</MudContainer>
diff --git a/src/Treasury.App/Pages/Transfers.razor.cs b/src/Treasury.App/Pages/Transfers.razor.cs
new file mode 100644
index 0000000..eefa68c
--- /dev/null
+++ b/src/Treasury.App/Pages/Transfers.razor.cs
@@ -0,0 +1,199 @@
+using Microsoft.AspNetCore.Components;
+using Microsoft.AspNetCore.Components.Authorization;
+using Microsoft.AspNetCore.Identity;
+using Microsoft.EntityFrameworkCore;
+using MudBlazor;
+using Treasury.App.Application.Transfers;
+using Treasury.App.Contracts.Transactions;
+using Treasury.App.Domain;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.App.Pages;
+
+public partial class Transfers
+{
+    [Inject] public TreasuryDbContext DbContext { get; set; } = default!;
+    [Inject] public ISnackbar Snackbar { get; set; } = default!;
+    [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
+    [Inject] public UserManager<ApplicationUser> UserManager { get; set; } = default!;
+    [Inject] public TransferCreationService TransferCreationService { get; set; } = default!;
+
+    private ApplicationUser? _currentUser;
+    private readonly List<AccountChoice> _accounts = new();
+    private readonly List<TransferHistoryRow> _transfers = new();
+    private readonly NewTransferForm _newTransfer = new();
+    private readonly Dictionary<Guid, string> _visibleAccountNames = new();
+    private string? _loadError;
+
+    protected override async Task OnInitializedAsync()
+    {
+        await LoadAsync();
+    }
+
+    private async Task LoadAsync()
+    {
+        _loadError = null;
+
+        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
+        _currentUser = await UserManager.GetUserAsync(authState.User);
+        if (_currentUser is null)
+        {
+            _accounts.Clear();
+            _transfers.Clear();
+            _visibleAccountNames.Clear();
+            return;
+        }
+
+        try
+        {
+            var householdAccounts = await DbContext.Accounts
+                .Where(x => x.HouseholdId == _currentUser.HouseholdId)
+                .OrderBy(x => x.Name)
+                .ToListAsync();
+
+            _visibleAccountNames.Clear();
+            foreach (var account in householdAccounts)
+            {
+                _visibleAccountNames[account.Id] = account.Name;
+            }
+
+            _accounts.Clear();
+            _accounts.AddRange(householdAccounts
+                .Where(x => x.OwnerUserId == _currentUser.Id && x.IsActive)
+                .Select(x => new AccountChoice
+                {
+                    Id = x.Id,
+                    Name = x.Name,
+                    Currency = x.Currency
+                }));
+
+            _transfers.Clear();
+            var transfers = await DbContext.Transfers
+                .Where(x => x.HouseholdId == _currentUser.HouseholdId)
+                .OrderByDescending(x => x.TransferDate)
+                .ThenByDescending(x => x.CreatedAt)
+                .Take(10)
+                .ToListAsync();
+
+            _transfers.AddRange(transfers.Select(x => new TransferHistoryRow
+            {
+                Id = x.Id,
+                FromAccountName = ResolveAccountName(x.FromAccountId),
+                ToAccountName = ResolveAccountName(x.ToAccountId),
+                Description = x.Description,
+                Amount = x.Amount,
+                Currency = x.Currency,
+                TransferDate = x.TransferDate
+            }));
+
+            if (_accounts.Count > 0)
+            {
+                if (string.IsNullOrWhiteSpace(_newTransfer.FromAccountId) || !_accounts.Any(x => x.Id.ToString() == _newTransfer.FromAccountId))
+                {
+                    _newTransfer.FromAccountId = _accounts[0].Id.ToString();
+                }
+
+                if (string.IsNullOrWhiteSpace(_newTransfer.ToAccountId) || !_accounts.Any(x => x.Id.ToString() == _newTransfer.ToAccountId))
+                {
+                    _newTransfer.ToAccountId = _accounts[Math.Min(1, _accounts.Count - 1)].Id.ToString();
+                }
+            }
+        }
+        catch (Exception ex)
+        {
+            _accounts.Clear();
+            _transfers.Clear();
+            _visibleAccountNames.Clear();
+            _loadError = "Unable to load transfers.";
+            Snackbar.Add($"Unable to load transfers: {ex.Message}", Severity.Error);
+        }
+    }
+
+    private async Task CreateTransferAsync()
+    {
+        if (_currentUser is null)
+        {
+            Snackbar.Add("Sign in first.", Severity.Warning);
+            return;
+        }
+
+        if (_accounts.Count == 0)
+        {
+            Snackbar.Add("Create an active account before transferring funds.", Severity.Warning);
+            return;
+        }
+
+        if (!Guid.TryParse(_newTransfer.FromAccountId, out var fromAccountId) ||
+            !Guid.TryParse(_newTransfer.ToAccountId, out var toAccountId) ||
+            fromAccountId == Guid.Empty ||
+            toAccountId == Guid.Empty ||
+            fromAccountId == toAccountId ||
+            _newTransfer.Amount <= 0m)
+        {
+            Snackbar.Add("Choose two different active accounts and a positive amount.", Severity.Warning);
+            return;
+        }
+
+        var result = await TransferCreationService.CreateAsync(_currentUser, new CreateTransferRequest
+        {
+            FromAccountId = fromAccountId,
+            ToAccountId = toAccountId,
+            Amount = _newTransfer.Amount,
+            Currency = string.Empty,
+            Description = _newTransfer.Description,
+            TransferDate = _newTransfer.TransferDate ?? DateTime.UtcNow
+        }, CancellationToken.None);
+
+        if (!result.Succeeded)
+        {
+            var severity = result.Status == TransferCreationStatus.Forbidden || result.Status == TransferCreationStatus.NotFound
+                ? Severity.Error
+                : Severity.Warning;
+            var message = result.Message ?? "Unable to create transfer.";
+            if (result.Issues.Count > 0)
+            {
+                message = result.Issues[0].Message;
+            }
+
+            Snackbar.Add(message, severity);
+            return;
+        }
+
+        _newTransfer.Description = string.Empty;
+        _newTransfer.Amount = 0m;
+        _newTransfer.TransferDate = DateTime.UtcNow;
+
+        Snackbar.Add("Transfer saved.", Severity.Success);
+        await LoadAsync();
+    }
+
+    private string ResolveAccountName(Guid accountId) =>
+        _visibleAccountNames.TryGetValue(accountId, out var name) ? name : "Unknown account";
+
+    private sealed class AccountChoice
+    {
+        public Guid Id { get; set; }
+        public string Name { get; set; } = string.Empty;
+        public string Currency { get; set; } = string.Empty;
+    }
+
+    private sealed class NewTransferForm
+    {
+        public string FromAccountId { get; set; } = string.Empty;
+        public string ToAccountId { get; set; } = string.Empty;
+        public decimal Amount { get; set; }
+        public DateTime? TransferDate { get; set; } = DateTime.UtcNow;
+        public string Description { get; set; } = string.Empty;
+    }
+
+    private sealed class TransferHistoryRow
+    {
+        public Guid Id { get; set; }
+        public string FromAccountName { get; set; } = string.Empty;
+        public string ToAccountName { get; set; } = string.Empty;
+        public string Description { get; set; } = string.Empty;
+        public decimal Amount { get; set; }
+        public string Currency { get; set; } = string.Empty;
+        public DateTime TransferDate { get; set; }
+    }
+}
diff --git a/src/Treasury.App/Program.cs b/src/Treasury.App/Program.cs
index 484429e..2542cd4 100644
--- a/src/Treasury.App/Program.cs
+++ b/src/Treasury.App/Program.cs
@@ -1,18 +1,20 @@
 using System.Text.Json;
 using FastEndpoints;
 using Microsoft.AspNetCore.Components.Authorization;
 using Microsoft.AspNetCore.Identity;
 using Microsoft.EntityFrameworkCore;
 using MudBlazor.Services;
 using Treasury.App.Application.Dashboard;
 using Treasury.App.Application.Accounts;
+using Treasury.App.Application.Transfers;
+using Treasury.App.Application.Transactions;
 using Treasury.App.Components;
 using Treasury.App.Contracts.Accounts;
 using Treasury.App.Contracts.Bills;
 using Treasury.App.Contracts.Budgets;
 using Treasury.App.Contracts.Rates;
 using Treasury.App.Contracts.Tags;
 using Treasury.App.Contracts.Transactions;
 using Treasury.App.Contracts.Valuations;
 using Treasury.App.Application.Valuations;
 using Treasury.App.Domain;
@@ -86,37 +88,45 @@ builder.Services.AddAuthorization(options =>
     options.AddPolicy(Policies.OwnerOnly, policy => policy.RequireAuthenticatedUser());
     options.AddPolicy(Policies.SharedReadOnly, policy => policy.RequireAuthenticatedUser());
 });
 
 builder.Services.AddHealthChecks();
 builder.Services.AddRazorComponents().AddInteractiveServerComponents();
 builder.Services.AddCascadingAuthenticationState();
 builder.Services.AddFastEndpoints();
 builder.Services.AddMudServices();
 builder.Services.AddScoped<AccountSharingService>();
+builder.Services.AddScoped<TransferCreationService>();
+builder.Services.AddScoped<TransactionEditingService>();
+builder.Services.AddScoped<AccountBalanceRecalculationService>();
 
 var app = builder.Build();
 
 using (var scope = app.Services.CreateScope())
 {
     var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
 
     if (db.Database.IsRelational())
     {
         await db.Database.MigrateAsync();
     }
     else
     {
         await db.Database.EnsureCreatedAsync();
     }
 
     await InitialSeed.SeedAsync(db);
+
+    // Recompute every account once at startup so any 0-default balance rows are repaired
+    // while preserving each account's opening balance baseline.
+    var balanceRecalculationService = scope.ServiceProvider.GetRequiredService<AccountBalanceRecalculationService>();
+    await balanceRecalculationService.RecalculateAllAccountsAsync(CancellationToken.None);
 }
 
 app.MapHealthChecks("/health");
 
 app.MapPost("/auth/login-submit", async (HttpContext httpContext, SignInManager<ApplicationUser> signInManager) =>
 {
     var payload = await ReadAuthPayloadAsync(httpContext.Request);
     var email = payload.Email;
     var password = payload.Password;
 
diff --git a/src/Treasury.App/_Imports.razor b/src/Treasury.App/_Imports.razor
index 43b2c3b..48deda9 100644
--- a/src/Treasury.App/_Imports.razor
+++ b/src/Treasury.App/_Imports.razor
@@ -7,14 +7,16 @@
 @using Microsoft.AspNetCore.Components.Routing
 @using Microsoft.AspNetCore.Components.Web
 @using Microsoft.AspNetCore.Components.Web.Virtualization
 @using static Microsoft.AspNetCore.Components.Web.RenderMode
 @using Microsoft.AspNetCore.Identity
 @using Microsoft.AspNetCore.WebUtilities
 @using Microsoft.EntityFrameworkCore
 @using Microsoft.JSInterop
 @using MudBlazor
 @using Treasury.App
+@using Treasury.App.Application.Transactions
 @using Treasury.App.Application.Dashboard
+@using Treasury.App.Contracts.Transactions
 @using Treasury.App.Domain
 @using Treasury.App.Infrastructure.Data
 @using Treasury.App.Theme
diff --git a/tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs b/tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
new file mode 100644
index 0000000..ebc89ae
--- /dev/null
+++ b/tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
@@ -0,0 +1,280 @@
+using System.Net;
+using System.Net.Http.Json;
+using System.Text.Json;
+using FluentAssertions;
+using Microsoft.AspNetCore.Mvc.Testing;
+using Microsoft.EntityFrameworkCore;
+using Microsoft.Extensions.DependencyInjection;
+using Treasury.App.Infrastructure.Data;
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
+    public async Task Balance_Correction_Recomputes_Running_Balance()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, name: "Correction account", bankAccountNumber: "1234567890123456");
+
+        var transactionResponse = await client.PostAsJsonAsync("/api/transactions", new
+        {
+            AccountId = accountId,
+            Description = "Initial expense",
+            Category = "General",
+            Amount = 10m,
+            Currency = "PLN",
+            Type = "expense",
+            TransactionDate = DateTime.UtcNow.AddDays(-1)
+        });
+        transactionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        var correctionResponse = await client.PostAsJsonAsync($"/api/accounts/{accountId}/balance-correction", new
+        {
+            NewBalance = 25m,
+            Description = "Manual correction"
+        });
+        correctionResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        await using var scope = app.Services.CreateAsyncScope();
+        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
+
+        var account = await db.Accounts.SingleAsync(x => x.Id == accountId);
+        var correctionTransaction = await db.Transactions.SingleAsync(x => x.AccountId == accountId && x.Type == "balance-correction");
+
+        account.CurrentBalance.Should().Be(25m);
+        correctionTransaction.BalanceAfterTransaction.Should().Be(25m);
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
diff --git a/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs b/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
index f3e206c..577eeb6 100644
--- a/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
+++ b/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
@@ -100,20 +100,42 @@ public class SharedReadOnlyUiPermissionTests
             Description = "Attempt by shared user",
             Category = "General",
             Amount = 10m,
             Currency = "PLN",
             Type = "expense",
             TransactionDate = DateTime.UtcNow
         });
 
         postTransaction.StatusCode.Should().Be(HttpStatusCode.Forbidden);
 
+        var ownerTransactionResponse = await ownerClient.PostAsJsonAsync("/api/transactions", new
+        {
+            AccountId = accountId,
+            Description = "Owner transaction",
+            Category = "General",
+            Amount = 15m,
+            Currency = "PLN",
+            Type = "expense",
+            TransactionDate = DateTime.UtcNow
+        });
+        ownerTransactionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
+        using var transactionJson = JsonDocument.Parse(await ownerTransactionResponse.Content.ReadAsStringAsync());
+        var transactionId = transactionJson.RootElement.GetProperty("id").GetGuid();
+
+        var editTransaction = await sharedClient.PutAsJsonAsync($"/api/transactions/{transactionId}", new
+        {
+            Id = transactionId,
+            Description = "Shared user edit attempt"
+        });
+
+        editTransaction.StatusCode.Should().Be(HttpStatusCode.Forbidden);
+
         // Additional guard: shared users must not be able to perform owner-only mutations such as balance correction
         var postBalanceCorrection = await sharedClient.PostAsJsonAsync($"/api/accounts/{accountId}/balance-correction", new
         {
             Amount = 100.00m,
             Reason = "Malicious correction by shared user"
         });
 
         // Expect that the mutation is forbidden for shared users
         postBalanceCorrection.StatusCode.Should().Be(HttpStatusCode.Forbidden);
     }
diff --git a/tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs b/tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs
new file mode 100644
index 0000000..5faca19
--- /dev/null
+++ b/tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs
@@ -0,0 +1,288 @@
+using System.Net;
+using System.Net.Http.Json;
+using System.Text.Json;
+using FluentAssertions;
+using Microsoft.AspNetCore.Mvc.Testing;
+using Microsoft.EntityFrameworkCore;
+using Microsoft.Extensions.DependencyInjection;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.IntegrationTests;
+
+public class TransactionEditingRulesTests
+{
+    [Fact]
+    public async Task Create_Transaction_Stores_BalanceAfterTransaction()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, "Balance tracking");
+
+        var response = await client.PostAsJsonAsync("/api/transactions", new
+        {
+            AccountId = accountId,
+            Description = "Salary deposit",
+            Category = "Income",
+            Amount = 1200m,
+            Currency = "PLN",
+            Type = "income",
+            TransactionDate = DateTime.UtcNow
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
+        var transactionId = payload.RootElement.GetProperty("id").GetGuid();
+
+        await using var scope = app.Services.CreateAsyncScope();
+        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
+
+        var transaction = await db.Transactions.SingleAsync(x => x.Id == transactionId);
+        var account = await db.Accounts.SingleAsync(x => x.Id == accountId);
+
+        transaction.BalanceAfterTransaction.Should().Be(account.CurrentBalance);
+    }
+
+    [Fact]
+    public async Task Create_Backdated_Transaction_Recomputes_Later_Balances()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, "Backdated balance");
+
+        var laterTransactionId = await CreateTransactionAsync(client, accountId, "Later income", 100m, "income", DateTime.UtcNow.AddDays(-1));
+
+        var earlierResponse = await client.PostAsJsonAsync("/api/transactions", new
+        {
+            AccountId = accountId,
+            Description = "Earlier expense",
+            Category = "General",
+            Amount = 25m,
+            Currency = "PLN",
+            Type = "expense",
+            TransactionDate = DateTime.UtcNow.AddDays(-3)
+        });
+
+        earlierResponse.StatusCode.Should().Be(HttpStatusCode.Created);
+        using var earlierPayload = JsonDocument.Parse(await earlierResponse.Content.ReadAsStringAsync());
+        var earlierTransactionId = earlierPayload.RootElement.GetProperty("id").GetGuid();
+
+        await using var scope = app.Services.CreateAsyncScope();
+        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
+
+        var earlierTransaction = await db.Transactions.SingleAsync(x => x.Id == earlierTransactionId);
+        var laterTransaction = await db.Transactions.SingleAsync(x => x.Id == laterTransactionId);
+        var account = await db.Accounts.SingleAsync(x => x.Id == accountId);
+
+        earlierTransaction.BalanceAfterTransaction.Should().Be(-25m);
+        laterTransaction.BalanceAfterTransaction.Should().Be(75m);
+        account.CurrentBalance.Should().Be(75m);
+    }
+
+    [Fact]
+    public async Task Edit_NonLatest_Rejects_Amount_Date_Type_Changes()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, "Historical edits");
+
+        var firstTransactionId = await CreateTransactionAsync(client, accountId, "First", 10m, "expense", DateTime.UtcNow.AddDays(-2));
+        _ = await CreateTransactionAsync(client, accountId, "Second", 20m, "expense", DateTime.UtcNow.AddDays(-1));
+
+        var response = await client.PutAsJsonAsync($"/api/transactions/{firstTransactionId}", new
+        {
+            Id = firstTransactionId,
+            Description = "First updated",
+            Category = "Updated",
+            Amount = 15m,
+            Type = "income",
+            TransactionDate = DateTime.UtcNow.AddDays(-3),
+            TagIds = Array.Empty<Guid>()
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
+        var body = await response.Content.ReadAsStringAsync();
+        body.Should().Contain("latest transaction");
+        body.Should().Contain("Amount can only be changed on the latest transaction.");
+        body.Should().Contain("Transaction date can only be changed on the latest transaction.");
+        body.Should().Contain("Type can only be changed on the latest transaction.");
+    }
+
+    [Fact]
+    public async Task Edit_Latest_Allows_Amount_And_Recomputes_Balance()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, "Latest edit");
+
+        _ = await CreateTransactionAsync(client, accountId, "Earlier", 10m, "expense", DateTime.UtcNow.AddDays(-2));
+        var latestTransactionId = await CreateTransactionAsync(client, accountId, "Latest", 5m, "expense", DateTime.UtcNow.AddDays(-1));
+
+        var response = await client.PutAsJsonAsync($"/api/transactions/{latestTransactionId}", new
+        {
+            Id = latestTransactionId,
+            Description = "Latest adjusted",
+            Category = "Updated",
+            Amount = 8m,
+            Type = "income",
+            TransactionDate = DateTime.UtcNow.AddDays(-3),
+            TagIds = Array.Empty<Guid>()
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        await using var scope = app.Services.CreateAsyncScope();
+        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
+
+        var account = await db.Accounts.SingleAsync(x => x.Id == accountId);
+        var editedTransaction = await db.Transactions.SingleAsync(x => x.Id == latestTransactionId);
+        var earlierTransaction = await db.Transactions.SingleAsync(x => x.Description == "Earlier" && x.AccountId == accountId);
+
+        account.CurrentBalance.Should().Be(-2m);
+        editedTransaction.Description.Should().Be("Latest adjusted");
+        editedTransaction.BalanceAfterTransaction.Should().Be(8m);
+        earlierTransaction.BalanceAfterTransaction.Should().Be(-2m);
+    }
+
+    [Fact]
+    public async Task Edit_Partial_Request_Leaves_Omitted_Fields_Unchanged()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, "Partial update");
+        var firstTransactionDate = DateTime.UtcNow.AddDays(-2);
+
+        var firstTransactionId = await CreateTransactionAsync(client, accountId, "First", 10m, "expense", firstTransactionDate);
+        _ = await CreateTransactionAsync(client, accountId, "Second", 20m, "income", DateTime.UtcNow.AddDays(-1));
+
+        var response = await client.PutAsJsonAsync($"/api/transactions/{firstTransactionId}", new
+        {
+            Id = firstTransactionId,
+            Description = "First renamed"
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        await using var scope = app.Services.CreateAsyncScope();
+        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
+
+        var transaction = await db.Transactions.SingleAsync(x => x.Id == firstTransactionId);
+        transaction.Description.Should().Be("First renamed");
+        transaction.Amount.Should().Be(10m);
+        transaction.Type.Should().Be("expense");
+        transaction.TransactionDate.Date.Should().Be(firstTransactionDate.Date);
+    }
+
+    [Fact]
+    public async Task Edit_TransferLinked_Rejects_Changes()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var fromAccountId = await CreateAccountAsync(client, "Transfer from");
+        var toAccountId = await CreateAccountAsync(client, "Transfer to");
+
+        var transferResponse = await client.PostAsJsonAsync("/api/transfers", new
+        {
+            FromAccountId = fromAccountId,
+            ToAccountId = toAccountId,
+            Amount = 25m,
+            Currency = "PLN",
+            Description = "Blocked transfer",
+            TransferDate = DateTime.UtcNow
+        });
+
+        transferResponse.StatusCode.Should().Be(HttpStatusCode.Created);
+        using var transferJson = JsonDocument.Parse(await transferResponse.Content.ReadAsStringAsync());
+        var outflowTransactionId = transferJson.RootElement.GetProperty("outflowTransactionId").GetGuid();
+
+        var updateResponse = await client.PutAsJsonAsync($"/api/transactions/{outflowTransactionId}", new
+        {
+            Id = outflowTransactionId,
+            Description = "Should fail"
+        });
+
+        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
+        var body = await updateResponse.Content.ReadAsStringAsync();
+        body.Should().Contain("Transfer-linked transactions cannot be edited");
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
+    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name)
+    {
+        var response = await client.PostAsJsonAsync("/api/accounts", new
+        {
+            Name = name,
+            Currency = "PLN",
+            AccountType = "cash-wallet"
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
+        return json.RootElement.GetProperty("id").GetGuid();
+    }
+
+    private static async Task<Guid> CreateTransactionAsync(
+        HttpClient client,
+        Guid accountId,
+        string description,
+        decimal amount,
+        string type,
+        DateTime transactionDate)
+    {
+        var response = await client.PostAsJsonAsync("/api/transactions", new
+        {
+            AccountId = accountId,
+            Description = description,
+            Category = "General",
+            Amount = amount,
+            Currency = "PLN",
+            Type = type,
+            TransactionDate = transactionDate
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
+        return json.RootElement.GetProperty("id").GetGuid();
+    }
+}
diff --git a/tests/Treasury.IntegrationTests/TransfersWorkflowTests.cs b/tests/Treasury.IntegrationTests/TransfersWorkflowTests.cs
new file mode 100644
index 0000000..9a92dc7
--- /dev/null
+++ b/tests/Treasury.IntegrationTests/TransfersWorkflowTests.cs
@@ -0,0 +1,239 @@
+using System.Net;
+using System.Net.Http.Json;
+using System.Text.Json;
+using FluentAssertions;
+using Microsoft.AspNetCore.Mvc.Testing;
+using Microsoft.EntityFrameworkCore;
+using Microsoft.Extensions.DependencyInjection;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.IntegrationTests;
+
+public class TransfersWorkflowTests
+{
+    [Fact]
+    public async Task Transfer_Creates_Transfer_And_Two_Linked_Transactions()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var fromAccountId = await CreateAccountAsync(client, "From account");
+        var toAccountId = await CreateAccountAsync(client, "To account");
+
+        var response = await client.PostAsJsonAsync("/api/transfers", new
+        {
+            FromAccountId = fromAccountId,
+            ToAccountId = toAccountId,
+            Amount = 25.50m,
+            Currency = "PLN",
+            Description = "Rent split",
+            TransferDate = DateTime.UtcNow
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
+        var transferId = payload.RootElement.GetProperty("id").GetGuid();
+        var outflowTransactionId = payload.RootElement.GetProperty("outflowTransactionId").GetGuid();
+        var inflowTransactionId = payload.RootElement.GetProperty("inflowTransactionId").GetGuid();
+
+        await using var scope = app.Services.CreateAsyncScope();
+        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
+
+        var transfer = await db.Transfers.SingleAsync(x => x.Id == transferId);
+        transfer.OutflowTransactionId.Should().Be(outflowTransactionId);
+        transfer.InflowTransactionId.Should().Be(inflowTransactionId);
+        transfer.FromAccountId.Should().Be(fromAccountId);
+        transfer.ToAccountId.Should().Be(toAccountId);
+
+        var outflow = await db.Transactions.SingleAsync(x => x.Id == outflowTransactionId);
+        var inflow = await db.Transactions.SingleAsync(x => x.Id == inflowTransactionId);
+
+        outflow.AccountId.Should().Be(fromAccountId);
+        outflow.Type.Should().Be("transfer-out");
+        outflow.BalanceAfterTransaction.Should().Be(-25.50m);
+        inflow.AccountId.Should().Be(toAccountId);
+        inflow.Type.Should().Be("transfer-in");
+        inflow.BalanceAfterTransaction.Should().Be(25.50m);
+
+        var fromAccount = await db.Accounts.SingleAsync(x => x.Id == fromAccountId);
+        var toAccount = await db.Accounts.SingleAsync(x => x.Id == toAccountId);
+
+        fromAccount.CurrentBalance.Should().Be(-25.50m);
+        toAccount.CurrentBalance.Should().Be(25.50m);
+    }
+
+    [Fact]
+    public async Task Backdated_Transfer_Recomputes_Both_Account_Balances()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var fromAccountId = await CreateAccountAsync(client, "Backdated from");
+        var toAccountId = await CreateAccountAsync(client, "Backdated to");
+
+        var fromIncomeResponse = await client.PostAsJsonAsync("/api/transactions", new
+        {
+            AccountId = fromAccountId,
+            Description = "Later income",
+            Category = "General",
+            Amount = 100m,
+            Currency = "PLN",
+            Type = "income",
+            TransactionDate = DateTime.UtcNow.AddDays(-1)
+        });
+        fromIncomeResponse.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        var toIncomeResponse = await client.PostAsJsonAsync("/api/transactions", new
+        {
+            AccountId = toAccountId,
+            Description = "Later income",
+            Category = "General",
+            Amount = 50m,
+            Currency = "PLN",
+            Type = "income",
+            TransactionDate = DateTime.UtcNow.AddDays(-1)
+        });
+        toIncomeResponse.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        var response = await client.PostAsJsonAsync("/api/transfers", new
+        {
+            FromAccountId = fromAccountId,
+            ToAccountId = toAccountId,
+            Amount = 20m,
+            Currency = "PLN",
+            Description = "Backdated transfer",
+            TransferDate = DateTime.UtcNow.AddDays(-3)
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
+        var outflowTransactionId = payload.RootElement.GetProperty("outflowTransactionId").GetGuid();
+        var inflowTransactionId = payload.RootElement.GetProperty("inflowTransactionId").GetGuid();
+
+        await using var scope = app.Services.CreateAsyncScope();
+        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
+
+        var outflow = await db.Transactions.SingleAsync(x => x.Id == outflowTransactionId);
+        var inflow = await db.Transactions.SingleAsync(x => x.Id == inflowTransactionId);
+        var fromLater = await db.Transactions.SingleAsync(x => x.AccountId == fromAccountId && x.Description == "Later income");
+        var toLater = await db.Transactions.SingleAsync(x => x.AccountId == toAccountId && x.Description == "Later income");
+        var fromAccount = await db.Accounts.SingleAsync(x => x.Id == fromAccountId);
+        var toAccount = await db.Accounts.SingleAsync(x => x.Id == toAccountId);
+
+        outflow.BalanceAfterTransaction.Should().Be(-20m);
+        inflow.BalanceAfterTransaction.Should().Be(20m);
+        fromLater.BalanceAfterTransaction.Should().Be(80m);
+        toLater.BalanceAfterTransaction.Should().Be(70m);
+        fromAccount.CurrentBalance.Should().Be(80m);
+        toAccount.CurrentBalance.Should().Be(70m);
+    }
+
+    [Fact]
+    public async Task Transfer_Rejects_Inactive_Accounts()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var fromAccountId = await CreateAccountAsync(client, "From account");
+        var toAccountId = await CreateAccountAsync(client, "To account");
+
+        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{fromAccountId}/active-state", new
+        {
+            IsActive = false
+        });
+        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        var response = await client.PostAsJsonAsync("/api/transfers", new
+        {
+            FromAccountId = fromAccountId,
+            ToAccountId = toAccountId,
+            Amount = 10m,
+            Currency = "PLN",
+            Description = "Blocked transfer",
+            TransferDate = DateTime.UtcNow
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
+        var body = await response.Content.ReadAsStringAsync();
+        body.Should().Contain("inactive");
+    }
+
+    [Fact]
+    public async Task TransfersPage_Shows_Recent_History()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var fromAccountId = await CreateAccountAsync(client, "History from");
+        var toAccountId = await CreateAccountAsync(client, "History to");
+
+        var response = await client.PostAsJsonAsync("/api/transfers", new
+        {
+            FromAccountId = fromAccountId,
+            ToAccountId = toAccountId,
+            Amount = 12.34m,
+            Currency = "PLN",
+            Description = "History transfer",
+            TransferDate = DateTime.UtcNow
+        });
+        response.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        var pageResponse = await client.GetAsync("/transfers");
+        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        var html = await pageResponse.Content.ReadAsStringAsync();
+        html.Should().Contain("Transfers");
+        html.Should().Contain("History transfer");
+        html.Should().Contain("History from");
+        html.Should().Contain("History to");
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
+    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name)
+    {
+        var response = await client.PostAsJsonAsync("/api/accounts", new
+        {
+            Name = name,
+            Currency = "PLN",
+            AccountType = "cash-wallet"
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
+        return json.RootElement.GetProperty("id").GetGuid();
+    }
+}
