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

