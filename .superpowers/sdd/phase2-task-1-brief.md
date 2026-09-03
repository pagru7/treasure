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

