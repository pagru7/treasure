# Phase 2 Accounts, Transfers, and Transactions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement accounts lifecycle controls, dedicated transfers workflow, and transaction balance/edit rules with strict invariants.

**Architecture:** Keep the existing single-project vertical-slice style and extend it with focused endpoint classes, contracts, and UI pages. Persist invariants in the write paths (not only UI) so account state, transfer linking, and transaction balance history remain correct even with direct API usage. Use integration-first tests per phase and keep sharing/read-only behavior unchanged.

**Tech Stack:** .NET 9, ASP.NET Core, Blazor Server, FastEndpoints, EF Core, PostgreSQL, ASP.NET Core Identity, xUnit, FluentAssertions, MudBlazor

## Global Constraints

- One household only in phase 1.
- Shared user access is read-only unless explicitly extended later.
- Authentication is required for all app access.
- Use only household users in the share picker; no manual email entry in UI.
- Keep existing `POST /api/accounts/{id:guid}/share-readonly` permission rules intact.
- Deactivated accounts are hidden by default, still counted in totals/history, and blocked from new transfers/transactions.
- Bank account number is optional; if provided, it must be digits-only with basic length validation.
- Transfer must persist one transfer record plus two linked transaction records.
- Non-latest transaction can edit only description/category/tags; amount/date/type edits are forbidden.

---

## File Structure (planned)

- `src/Treasury.App/Domain/Account.cs` — add `IsActive`, `BankAccountNumber`.
- `src/Treasury.App/Domain/Transfer.cs` — add `OutflowTransactionId`, `InflowTransactionId`.
- `src/Treasury.App/Domain/Transaction.cs` — add `BalanceAfterTransaction`.
- `src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs` — rename + bank account number update contract.
- `src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs` — deactivate/reactivate contract.
- `src/Treasury.App/Contracts/Transactions/UpdateTransactionRequest.cs` — edit transaction contract with tag list.
- `src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs` — owner update endpoint.
- `src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs` — owner delete endpoint with rules.
- `src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs` — owner active-state endpoint.
- `src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs` — add link fields, inactive-account validation, and `BalanceAfterTransaction` persistence.
- `src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs` — inactive-account validation + `BalanceAfterTransaction`.
- `src/Treasury.App/Endpoints/Transactions/UpdateTransactionEndpoint.cs` — latest/non-latest edit policy.
- `src/Treasury.App/Pages/Accounts.razor(.cs)` — show inactive toggle, rename/deactivate/delete actions, bank account number field.
- `src/Treasury.App/Pages/Transfers.razor(.cs)` — dedicated transfer form and history.
- `src/Treasury.App/Pages/Transactions.razor` — edit action and UI restrictions.
- `src/Treasury.App/Components/Layout/MainLayout.razor` — add Transfers nav links.
- `src/Treasury.App/Infrastructure/Migrations/*` — schema migration for new columns.
- `tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs` — lifecycle rule coverage.
- `tests/Treasury.IntegrationTests/TransfersWorkflowTests.cs` — linked transfer persistence coverage.
- `tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs` — `BalanceAfterTransaction` + edit constraints.

### Task 1: Implement accounts lifecycle and bank account number support

**Files:**
- Create: `src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs`
- Create: `src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs`
- Create: `src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs`
- Create: `src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs`
- Create: `src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs`
- Modify: `src/Treasury.App/Domain/Account.cs`
- Modify: `src/Treasury.App/Pages/Accounts.razor`
- Modify: `src/Treasury.App/Pages/Accounts.razor.cs`
- Modify: `src/Treasury.App/Infrastructure/Migrations/*` (new migration)
- Test: `tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs`

**Interfaces:**
- Consumes:
  - `AccountSharingService.GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct)`
  - `Policies.OwnerOnly`
- Produces:
  - `PUT /api/accounts/{id:guid}`
  - `PUT /api/accounts/{id:guid}/active-state`
  - `DELETE /api/accounts/{id:guid}`
  - Account fields: `bool IsActive`, `string? BankAccountNumber`

- [ ] **Step 1: Write failing lifecycle tests**

```csharp
// tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
public class AccountsLifecycleTests
{
    [Fact]
    public async Task Delete_Allows_Account_With_Zero_Balance() { /* create owner + account, call DELETE, expect 204 */ }

    [Fact]
    public async Task Delete_Blocks_Account_With_Transactions_And_NonZero_Balance() { /* expect validation error */ }

    [Fact]
    public async Task Deactivate_Hides_Account_From_Default_List() { /* set inactive, GET /api/accounts, expect missing by default */ }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests"`
Expected: FAIL with missing endpoints/fields/behavior.

- [ ] **Step 3: Add account fields and contracts**

```csharp
// src/Treasury.App/Domain/Account.cs
public bool IsActive { get; set; } = true;
public string? BankAccountNumber { get; set; }
```

```csharp
// src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs
namespace Treasury.App.Contracts.Accounts;
public sealed class UpdateAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public string? BankAccountNumber { get; set; }
}
```

```csharp
// src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs
namespace Treasury.App.Contracts.Accounts;
public sealed class SetAccountActiveStateRequest
{
    public bool IsActive { get; set; }
}
```

- [ ] **Step 4: Implement account lifecycle endpoints**

```csharp
// src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs
public sealed class UpdateAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<UpdateAccountRequest>
{
    public override void Configure()
    {
        Put("/api/accounts/{id:guid}");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(UpdateAccountRequest req, CancellationToken ct)
    {
        if (!Route<Guid>("id", out var id))
        {
            await SendNotFoundAsync(ct);
            return;
        }
        // load user + account, owner check, validate name and bank number (digits 16-34), save, return 200
    }
}
```

```csharp
// src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs
public override async Task HandleAsync(CancellationToken ct)
{
    // owner-only account lookup
    // if has transactions and CurrentBalance != 0 => AddError("Cannot remove account with transaction history and non-zero balance. Deactivate it instead.")
    // else delete and return 204
}
```

```csharp
// src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs
public override async Task HandleAsync(SetAccountActiveStateRequest req, CancellationToken ct)
{
    // owner-only account lookup, set IsActive, UpdatedAt, save, return 200
}
```

- [ ] **Step 5: Wire Accounts page controls**

```razor
<MudSwitch T="bool" Label="Show inactive" @bind-Value="_showInactive" />
<MudTextField Label="Bank account number (optional)" @bind-Value="_newAccount.BankAccountNumber" />
<MudButton OnClick="() => ToggleActiveStateAsync(account)">@((account.IsActive) ? "Deactivate" : "Reactivate")</MudButton>
<MudButton Color="Color.Error" OnClick="() => DeleteAccountAsync(account)">Remove</MudButton>
```

```csharp
// Accounts.razor.cs
private bool _showInactive;
private async Task LoadAccountsAsync()
{
    var accounts = await AccountSharingService.GetVisibleAccountsAsync(_currentUser!, CancellationToken.None);
    if (!_showInactive) accounts = accounts.Where(x => x.IsActive).ToList();
    // map to card VM including IsActive + BankAccountNumber
}
```

- [ ] **Step 6: Add migration and run tests**

Run: `dotnet ef migrations add AddAccountLifecycleFields --project src\Treasury.App\Treasury.App.csproj`
Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Treasury.App/Domain/Account.cs src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs src/Treasury.App/Pages/Accounts.razor src/Treasury.App/Pages/Accounts.razor.cs src/Treasury.App/Infrastructure/Migrations tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
git commit -m "feat: add account lifecycle controls and bank account number"
```

### Task 2: Implement dedicated transfers page with linked transfer records

**Files:**
- Modify: `src/Treasury.App/Domain/Transfer.cs`
- Modify: `src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs`
- Create: `src/Treasury.App/Pages/Transfers.razor`
- Create: `src/Treasury.App/Pages/Transfers.razor.cs`
- Modify: `src/Treasury.App/Components/Layout/MainLayout.razor`
- Modify: `src/Treasury.App/Infrastructure/Migrations/*` (new migration)
- Test: `tests/Treasury.IntegrationTests/TransfersWorkflowTests.cs`

**Interfaces:**
- Consumes:
  - `POST /api/transfers`
  - Account fields `IsActive`, `CurrentBalance`
- Produces:
  - `Transfer.OutflowTransactionId`
  - `Transfer.InflowTransactionId`
  - Transfers page at `/transfers`

- [ ] **Step 1: Write failing transfer-link tests**

```csharp
// tests/Treasury.IntegrationTests/TransfersWorkflowTests.cs
[Fact]
public async Task Transfer_Creates_Transfer_And_Two_Linked_Transactions()
{
    // call POST /api/transfers
    // assert transfer row exists with OutflowTransactionId and InflowTransactionId
    // assert referenced transactions exist and balances changed
}
```

```csharp
[Fact]
public async Task Transfer_Rejects_Inactive_Accounts()
{
    // deactivate source or destination, POST /api/transfers, expect validation error
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransfersWorkflowTests"`
Expected: FAIL due to missing link fields / inactive-account checks.

- [ ] **Step 3: Add transfer link fields and endpoint logic**

```csharp
// src/Treasury.App/Domain/Transfer.cs
public Guid OutflowTransactionId { get; set; }
public Guid InflowTransactionId { get; set; }
```

```csharp
// CreateTransferEndpoint.cs (core additions)
if (!fromAccount.IsActive || !toAccount.IsActive)
{
    AddError("Transfers are allowed only between active accounts.");
    await SendErrorsAsync(cancellation: ct);
    return;
}

await using var tx = await db.Database.BeginTransactionAsync(ct);
db.Transfers.Add(transfer);
db.Transactions.Add(outflow);
db.Transactions.Add(inflow);
await db.SaveChangesAsync(ct);
transfer.OutflowTransactionId = outflow.Id;
transfer.InflowTransactionId = inflow.Id;
await db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
```

- [ ] **Step 4: Add dedicated Transfers page and navigation**

```razor
@page "/transfers"
@attribute [Authorize]
<MudText Typo="Typo.h4">Transfers</MudText>
<!-- Source, destination, amount, date, description form -->
<!-- Recent transfers table -->
```

```csharp
// Transfers.razor.cs
private async Task CreateTransferAsync()
{
    // validate active accounts only, call POST /api/transfers, reload balances/transfers, show snackbar
}
```

```razor
// MainLayout.razor additions
<MudButton Variant="Variant.Text" Href="/transfers">Transfers</MudButton>
<MudNavLink Href="/transfers">Transfers</MudNavLink>
```

- [ ] **Step 5: Add migration and run tests**

Run: `dotnet ef migrations add AddTransferTransactionLinks --project src\Treasury.App\Treasury.App.csproj`
Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransfersWorkflowTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Treasury.App/Domain/Transfer.cs src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs src/Treasury.App/Pages/Transfers.razor src/Treasury.App/Pages/Transfers.razor.cs src/Treasury.App/Components/Layout/MainLayout.razor src/Treasury.App/Infrastructure/Migrations tests/Treasury.IntegrationTests/TransfersWorkflowTests.cs
git commit -m "feat: add linked transfers workflow and transfers page"
```

### Task 3: Implement BalanceAfterTransaction and transaction edit constraints

**Files:**
- Create: `src/Treasury.App/Contracts/Transactions/UpdateTransactionRequest.cs`
- Create: `src/Treasury.App/Endpoints/Transactions/UpdateTransactionEndpoint.cs`
- Modify: `src/Treasury.App/Domain/Transaction.cs`
- Modify: `src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs`
- Modify: `src/Treasury.App/Pages/Transactions.razor`
- Modify: `src/Treasury.App/Infrastructure/Migrations/*` (new migration)
- Test: `tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs`

**Interfaces:**
- Consumes:
  - `CreateTransactionRequest`
  - account current balance and transaction ordering
- Produces:
  - `Transaction.BalanceAfterTransaction`
  - `PUT /api/transactions/{id:guid}`

- [ ] **Step 1: Write failing transaction-rule tests**

```csharp
// tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs
[Fact]
public async Task Create_Transaction_Stores_BalanceAfterTransaction() { /* assert saved value */ }

[Fact]
public async Task Edit_NonLatest_Rejects_Amount_Date_Type_Changes() { /* expect validation */ }

[Fact]
public async Task Edit_Latest_Allows_Amount_And_Recomputes_Balance() { /* assert new balance */ }
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransactionEditingRulesTests"`
Expected: FAIL due to missing field/endpoint/rules.

- [ ] **Step 3: Add field and create/edit endpoint behavior**

```csharp
// src/Treasury.App/Domain/Transaction.cs
public decimal BalanceAfterTransaction { get; set; }
```

```csharp
// in CreateTransactionEndpoint.cs
account.CurrentBalance += delta;
transaction.BalanceAfterTransaction = account.CurrentBalance;
```

```csharp
// src/Treasury.App/Endpoints/Transactions/UpdateTransactionEndpoint.cs
// 1) load tx + account + owner check
// 2) find latest transaction on account:
//    OrderByDescending(TransactionDate).ThenByDescending(CreatedAt).ThenByDescending(Id)
// 3) if not latest and amount/type/date changed => validation errors
// 4) apply allowed fields
// 5) recompute account balance + latest tx BalanceAfterTransaction when numeric fields changed
```

- [ ] **Step 4: Add transaction edit UI affordance**

```razor
// Transactions.razor
<MudButton Size="Size.Small" OnClick="() => StartEdit(context)">Edit</MudButton>
```

```csharp
// Transactions page code
private bool IsLatestTransaction(Guid transactionId, Guid accountId) { /* deterministic check */ }
private bool CanEditAmount(Transaction tx) => IsLatestTransaction(tx.Id, tx.AccountId);
```

- [ ] **Step 5: Add migration and run tests**

Run: `dotnet ef migrations add AddTransactionBalanceAfterAndEditRules --project src\Treasury.App\Treasury.App.csproj`
Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransactionEditingRulesTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/Treasury.App/Domain/Transaction.cs src/Treasury.App/Contracts/Transactions/UpdateTransactionRequest.cs src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs src/Treasury.App/Endpoints/Transactions/UpdateTransactionEndpoint.cs src/Treasury.App/Pages/Transactions.razor src/Treasury.App/Infrastructure/Migrations tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs
git commit -m "feat: add transaction balance tracking and edit constraints"
```

### Task 4: Final verification and regression safety

**Files:**
- Modify: `tests/Treasury.IntegrationTests/Phase1AcceptanceTests.cs` (only if route coverage requires update)
- Modify: `README.md` (accounts inactive rules + transfers page usage)

**Interfaces:**
- Consumes:
  - all new endpoints/pages/fields from Tasks 1-3
- Produces:
  - final validated branch state with updated operator guidance

- [ ] **Step 1: Add/adjust acceptance and docs checks**

```markdown
## README additions
- Inactive accounts are hidden by default and blocked from new transfer/transaction operations.
- Transfers page: `/transfers`
- Transaction editing rule: only latest transaction can change amount/date/type.
```

- [ ] **Step 2: Run full test suites**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug`
Run: `dotnet test tests\Treasury.Domain.Tests\Treasury.Domain.Tests.csproj -c Debug`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add README.md tests/Treasury.IntegrationTests/Phase1AcceptanceTests.cs
git commit -m "test/docs: finalize phase2 lifecycle transfer transaction coverage"
```

