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

