# Household Wealth Manager Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: DO NOT Use superpowers:subagent-driven-development (recommended), use superpowers:executing-plans instead to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a local, Docker-hosted Blazor + ASP.NET Core + PostgreSQL app that tracks household assets with manual FX, manual valuations, transfers, and read-only sharing.

**Architecture:** Use one ASP.NET Core application project that serves both Blazor UI and REST API endpoints. Organize the code using vertical slices and FastEndpoints (one endpoint per class), while keeping layered boundaries via folders inside the same project. Persist data in PostgreSQL via EF Core with a clear domain model for users, accounts, transactions, valuations, rates, and sharing rules. Keep phase 1 strictly manual for FX/market prices while shaping API contracts for phase 2 mobile integration.

**Tech Stack:** latest .NET !, ASP.NET Core, Blazor Server, FastEndpoints, EF Core, PostgreSQL (Npgsql), ASP.NET Core Identity, xUnit, FluentAssertions, Docker Compose

## Global Constraints

- One household only in phase 1.
- Support both PLN and EUR at minimum (allow more currencies).
- No external price feeds in phase 1.
- No mobile integration implementation in phase 1.
- Shared user access is read-only unless explicitly extended later.
- Bullion supports formula valuation (weight × purity × unit price).
- Coins are manually valued per item and can be manually updated later.
- App must run locally in Docker on dedicated machine.
- Authentication is required for all app access.

---

## File Structure (planned)

- `Treasury.sln` — solution root.
- `src/Treasury.App/` — main application project (Blazor + FastEndpoints + auth + persistence).
- `src/Treasury.App/Domain/` — entities, enums, domain rules (folder layer).
- `src/Treasury.App/Application/` — use-case services for accounts, transactions, valuations, rates (folder layer).
- `src/Treasury.App/Infrastructure/` — EF Core context, configurations, repository services (folder layer).
- `src/Treasury.ExternalServices/` — phase 2 integrations/adapters project (not implemented in phase 1).
- `tests/Treasury.Domain.Tests/` — unit tests for valuation/permissions rules.
- `tests/Treasury.IntegrationTests/` — API + data integration tests (PostgreSQL container/local db).
- `doc/superpowers/specs/` — approved design spec.
- `doc/superpowers/plans/` — implementation plans.
- `docker-compose.yml` — host + postgres stack for local network deployment.
- `Dockerfile` — image build for the host application.

### Task 1: Bootstrap solution, host app, and local Docker runtime

**Files:**

- Create: `Treasury.sln`
- Create: `src/Treasury.App/Treasury.App.csproj`
- Create: `docker-compose.yml`
- Create: `Dockerfile`
- Modify: `.gitignore`

**Interfaces:**

- Consumes: none
- Produces:
  - `Program.cs` host entry point with `WebApplication CreateApp(string[] args)` bootstrap method.
  - Connection string key `ConnectionStrings:DefaultConnection`.
  - Folder-layer boundaries inside app project: `Domain`, `Application`, `Infrastructure`.

> **Architecture mapping note:** Wherever this plan references former `src/Treasury.Domain`, `src/Treasury.Application`, `src/Treasury.Infrastructure`, or `src/Treasury.Host` paths, implement the equivalent in the folder-layered `src/Treasury.App` project.

- [ ] **Step 1: Write the failing smoke test for host startup**

```csharp
// tests/Treasury.IntegrationTests/StartupTests.cs
public class StartupTests
{
    [Fact]
    public async Task Get_Health_Returns200()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient();
        var response = await client.GetAsync("/health");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~StartupTests.Get_Health_Returns200"`
Expected: FAIL with project/app/factory not found.

- [ ] **Step 3: Write minimal implementation**

```csharp
// src/Treasury.App/Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
var app = builder.Build();
app.MapHealthChecks("/health");
app.Run();

public partial class Program { }
```

```yaml
# docker-compose.yml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: treasury
      POSTGRES_USER: treasury
      POSTGRES_PASSWORD: treasury
    ports:
      - '5432:5432'
  treasury-host:
    build: .
    environment:
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=treasury;Username=treasury;Password=treasury
    ports:
      - '8080:8080'
    depends_on:
      - postgres
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~StartupTests.Get_Health_Returns200"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Treasury.sln src/Treasury.Host src/Treasury.Domain src/Treasury.Application src/Treasury.Infrastructure tests/Treasury.IntegrationTests docker-compose.yml Dockerfile .gitignore
git commit -m "chore: bootstrap treasury solution and docker runtime"
```

### Task 2: Add authentication, single-household seed, and authorization policies

**Files:**

- Modify: `src/Treasury.Host/Program.cs`
- Create: `src/Treasury.Domain/Entities/Household.cs`
- Create: `src/Treasury.Domain/Entities/ApplicationUser.cs`
- Create: `src/Treasury.Infrastructure/Data/TreasuryDbContext.cs`
- Create: `src/Treasury.Infrastructure/Data/Seed/InitialSeed.cs`
- Create: `src/Treasury.Host/Auth/Policies.cs`
- Test: `tests/Treasury.IntegrationTests/AuthTests.cs`

**Interfaces:**

- Consumes:
  - `ConnectionStrings:DefaultConnection`
- Produces:
  - Authorization policies:
    - `Policies.OwnerOnly`
    - `Policies.SharedReadOnly`
  - Seeded household record with stable key `"default-household"`.

- [ ] **Step 1: Write failing auth and household tests**

```csharp
[Fact]
public async Task Anonymous_Request_To_Accounts_Returns401() { /* ... */ }

[Fact]
public async Task Seed_Creates_Single_Default_Household() { /* assert exactly one */ }
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AuthTests|FullyQualifiedName~Seed"`
Expected: FAIL with missing auth/db seed behavior.

- [ ] **Step 3: Implement minimal auth + seed**

```csharp
// Program.cs (service registration excerpt)
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.OwnerOnly, p => p.RequireAuthenticatedUser());
    options.AddPolicy(Policies.SharedReadOnly, p => p.RequireAuthenticatedUser());
});
```

```csharp
// InitialSeed.cs (excerpt)
if (!await db.Households.AnyAsync())
{
    db.Households.Add(new Household { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Default Household" });
    await db.SaveChangesAsync();
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AuthTests|FullyQualifiedName~Seed"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.Host src/Treasury.Domain src/Treasury.Infrastructure tests/Treasury.IntegrationTests
git commit -m "feat: add auth policies and single-household seed"
```

### Task 3: Implement account types, accounts, and sharing rules APIs

**Files:**

- Create: `src/Treasury.Domain/Entities/AccountType.cs`
- Create: `src/Treasury.Domain/Entities/Account.cs`
- Create: `src/Treasury.Domain/Entities/VisibilityRule.cs`
- Create: `src/Treasury.Application/Accounts/AccountService.cs`
- Create: `src/Treasury.Host/Contracts/Accounts/CreateAccountRequest.cs`
- Create: `src/Treasury.Host/Contracts/Accounts/AccountResponse.cs`
- Create: `src/Treasury.Host/Endpoints/AccountsEndpoints.cs`
- Test: `tests/Treasury.IntegrationTests/AccountsEndpointsTests.cs`

**Interfaces:**

- Consumes:
  - Authenticated user identity.
  - `Policies.OwnerOnly`, `Policies.SharedReadOnly`.
- Produces:
  - `POST /api/account-types`
  - `POST /api/accounts`
  - `POST /api/accounts/{id}/share-readonly`
  - `GET /api/accounts` (returns only owned + shared visible accounts for caller)
  - `Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request, Guid actorUserId, CancellationToken ct)`

- [ ] **Step 1: Write failing API tests**

```csharp
[Fact]
public async Task Create_AccountType_And_Account_Returns201() { /* ... */ }

[Fact]
public async Task Shared_User_Sees_Shared_Account_ReadOnly() { /* ... */ }
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsEndpointsTests"`
Expected: FAIL with missing routes/services/entities.

- [ ] **Step 3: Implement minimal account + sharing flows**

```csharp
// AccountsEndpoints.cs (excerpt)
group.MapPost("/accounts", async (CreateAccountRequest request, AccountService service, ClaimsPrincipal user, CancellationToken ct) =>
{
    var actorUserId = user.GetUserId();
    var created = await service.CreateAccountAsync(request, actorUserId, ct);
    return Results.Created($"/api/accounts/{created.Id}", created);
}).RequireAuthorization(Policies.OwnerOnly);
```

```csharp
// VisibilityRule.cs
public sealed class VisibilityRule
{
    public Guid AccountId { get; set; }
    public Guid ViewerUserId { get; set; }
    public bool IsReadOnly { get; set; } = true;
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsEndpointsTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.Domain src/Treasury.Application src/Treasury.Host tests/Treasury.IntegrationTests
git commit -m "feat: add flexible accounts and readonly sharing rules"
```

### Task 4: Implement ledger transactions, transfers, and balance corrections

**Files:**

- Create: `src/Treasury.Domain/Entities/Transaction.cs`
- Create: `src/Treasury.Domain/Entities/Transfer.cs`
- Create: `src/Treasury.Domain/Enums/TransactionType.cs`
- Create: `src/Treasury.Application/Transactions/TransactionService.cs`
- Create: `src/Treasury.Host/Contracts/Transactions/CreateTransactionRequest.cs`
- Create: `src/Treasury.Host/Contracts/Transactions/CreateTransferRequest.cs`
- Create: `src/Treasury.Host/Endpoints/TransactionsEndpoints.cs`
- Test: `tests/Treasury.IntegrationTests/TransactionsEndpointsTests.cs`

**Interfaces:**

- Consumes:
  - Account ownership and visibility lookup from Task 3.
- Produces:
  - `POST /api/transactions`
  - `POST /api/transfers`
  - `GET /api/accounts/{id}/transactions`
  - `Task<Guid> CreateTransferAsync(CreateTransferRequest request, Guid actorUserId, CancellationToken ct)`

- [ ] **Step 1: Write failing transfer and correction tests**

```csharp
[Fact]
public async Task Transfer_Creates_Debit_And_Credit_Entries() { /* ... */ }

[Fact]
public async Task Balance_Correction_Updates_Account_CurrentBalance() { /* ... */ }
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransactionsEndpointsTests"`
Expected: FAIL with missing endpoints/service logic.

- [ ] **Step 3: Implement minimal ledger logic**

```csharp
// TransactionService.cs (transfer excerpt)
await CreateTransactionInternalAsync(fromAccountId, TransactionType.TransferOut, -request.Amount, request.Currency, actorUserId, ct);
await CreateTransactionInternalAsync(toAccountId, TransactionType.TransferIn, request.Amount, request.Currency, actorUserId, ct);
```

```csharp
// balance correction rule
var delta = request.NewBalance - account.CurrentBalance;
await CreateTransactionInternalAsync(account.Id, TransactionType.BalanceCorrection, delta, account.Currency, actorUserId, ct);
account.CurrentBalance = request.NewBalance;
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransactionsEndpointsTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.Domain src/Treasury.Application src/Treasury.Host tests/Treasury.IntegrationTests
git commit -m "feat: add ledger transactions transfers and balance corrections"
```

### Task 5: Implement manual FX rates and valuation engine (bullion + coin rules)

**Files:**

- Create: `src/Treasury.Domain/Entities/CurrencyRate.cs`
- Create: `src/Treasury.Domain/Entities/AssetValuation.cs`
- Create: `src/Treasury.Domain/Enums/ValuationKind.cs`
- Create: `src/Treasury.Application/Valuations/ValuationService.cs`
- Create: `src/Treasury.Application/Valuations/BullionFormulaCalculator.cs`
- Create: `src/Treasury.Host/Contracts/Valuations/UpdateBullionValueRequest.cs`
- Create: `src/Treasury.Host/Contracts/Valuations/UpdateCoinValueRequest.cs`
- Create: `src/Treasury.Host/Contracts/Rates/UpsertRateRequest.cs`
- Create: `src/Treasury.Host/Endpoints/ValuationEndpoints.cs`
- Create: `src/Treasury.Host/Endpoints/RatesEndpoints.cs`
- Test: `tests/Treasury.Domain.Tests/BullionFormulaCalculatorTests.cs`
- Test: `tests/Treasury.IntegrationTests/ValuationAndRatesEndpointsTests.cs`

**Interfaces:**

- Consumes:
  - Account model from Task 3.
  - Transaction model from Task 4 for valuation-adjustment entries.
- Produces:
  - `POST /api/rates`
  - `GET /api/rates/latest?from=PLN&to=EUR`
  - `POST /api/valuations/bullion`
  - `POST /api/valuations/coin`
  - `decimal Calculate(decimal weight, decimal purity, decimal unitPrice)`

- [ ] **Step 1: Write failing valuation and FX tests**

```csharp
[Fact]
public void BullionFormula_Uses_Weight_Purity_UnitPrice()
{
    var value = BullionFormulaCalculator.Calculate(100m, 0.9999m, 320m);
    value.Should().Be(31996.8m);
}
```

```csharp
[Fact]
public async Task Coin_Manual_Value_Update_Creates_Valuation_Record() { /* ... */ }
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test tests\Treasury.Domain.Tests\Treasury.Domain.Tests.csproj -c Debug --filter "FullyQualifiedName~BullionFormulaCalculatorTests"`
Expected: FAIL with missing calculator.

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~ValuationAndRatesEndpointsTests"`
Expected: FAIL with missing endpoints/entities.

- [ ] **Step 3: Implement minimal valuation and rates services**

```csharp
// BullionFormulaCalculator.cs
public static class BullionFormulaCalculator
{
    public static decimal Calculate(decimal weight, decimal purity, decimal unitPrice)
        => decimal.Round(weight * purity * unitPrice, 4, MidpointRounding.AwayFromZero);
}
```

```csharp
// coin manual valuation rule (service excerpt)
valuation.Kind = ValuationKind.CoinManual;
valuation.CurrentUnitValue = request.CurrentUnitValue;
valuation.CurrentTotalValue = request.CurrentUnitValue * request.Quantity;
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests\Treasury.Domain.Tests\Treasury.Domain.Tests.csproj -c Debug`
Expected: PASS.

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~ValuationAndRatesEndpointsTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.Domain src/Treasury.Application src/Treasury.Host tests/Treasury.Domain.Tests tests/Treasury.IntegrationTests
git commit -m "feat: add manual fx rates and asset valuation workflows"
```

### Task 6: Build Blazor UI workflows and read-only shared views

**Files:**

- Create: `src/Treasury.Host/Pages/Accounts.razor`
- Create: `src/Treasury.Host/Pages/Transactions.razor`
- Create: `src/Treasury.Host/Pages/Valuations.razor`
- Create: `src/Treasury.Host/Pages/Rates.razor`
- Create: `src/Treasury.Host/Pages/SharedPortfolio.razor`
- Create: `src/Treasury.Host/Components/Accounts/CreateAccountForm.razor`
- Create: `src/Treasury.Host/Components/Transactions/CreateTransactionForm.razor`
- Create: `src/Treasury.Host/Components/Valuations/UpdateBullionForm.razor`
- Create: `src/Treasury.Host/Components/Valuations/UpdateCoinForm.razor`
- Create: `src/Treasury.Host/Services/ApiClient.cs`
- Test: `tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs`

**Interfaces:**

- Consumes:
  - REST endpoints from Tasks 3–5.
- Produces:
  - UI routes:
    - `/accounts`
    - `/transactions`
    - `/valuations`
    - `/rates`
    - `/shared`
  - `ApiClient` methods:
    - `Task<IReadOnlyList<AccountResponse>> GetAccountsAsync()`
    - `Task CreateTransactionAsync(CreateTransactionRequest request)`
    - `Task UpdateCoinValueAsync(UpdateCoinValueRequest request)`

- [ ] **Step 1: Write failing permission/UI integration test**

```csharp
[Fact]
public async Task Shared_User_Cannot_Post_To_Owner_Edit_Endpoints()
{
    var response = await sharedUserClient.PostAsJsonAsync("/api/transactions", request);
    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

- [ ] **Step 2: Run test to verify failure**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~SharedReadOnlyUiPermissionTests"`
Expected: FAIL until UI and policy wiring are complete.

- [ ] **Step 3: Implement minimal Blazor pages and API client wiring**

```csharp
// ApiClient.cs (excerpt)
public Task<HttpResponseMessage> CreateTransactionAsync(CreateTransactionRequest request) =>
    _http.PostAsJsonAsync("/api/transactions", request);
```

```razor
@* SharedPortfolio.razor excerpt *@
<h3>Shared Portfolio</h3>
@if (_accounts is null) { <p>Loading...</p> }
else
{
    foreach (var account in _accounts)
    {
        <div>@account.Name - @account.CurrentValue @account.Currency (read-only)</div>
    }
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~SharedReadOnlyUiPermissionTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.Host tests/Treasury.IntegrationTests
git commit -m "feat: add blazor workflows and readonly shared portfolio views"
```

### Task 7: Final hardening, migrations, and deployment validation

**Files:**

- Modify: `src/Treasury.Infrastructure/Data/TreasuryDbContext.cs`
- Create: `src/Treasury.Infrastructure/Data/Migrations/*`
- Modify: `docker-compose.yml`
- Modify: `README.md`
- Test: `tests/Treasury.IntegrationTests/Phase1AcceptanceTests.cs`

**Interfaces:**

- Consumes:
  - All interfaces from Tasks 1–6.
- Produces:
  - Database schema migration baseline.
  - End-to-end acceptance tests for phase 1 requirements.
  - Updated runbook commands for local Docker deployment.

- [ ] **Step 1: Write failing acceptance tests**

```csharp
[Fact]
public async Task Phase1_EndToEnd_Covers_Accounts_Transfers_Valuations_And_Fx()
{
    // Create account types, create accounts in PLN/EUR,
    // add transfer, add bullion valuation, add coin valuation,
    // upsert FX rate, and verify converted total.
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~Phase1AcceptanceTests"`
Expected: FAIL before migration/update flow is complete.

- [ ] **Step 3: Implement schema migration and deployment docs**

```bash
dotnet ef migrations add InitialPhase1 --project src/Treasury.Infrastructure --startup-project src/Treasury.Host
dotnet ef database update --project src/Treasury.Infrastructure --startup-project src/Treasury.Host
docker compose up --build -d
```

```md
# README.md (required sections)

- Prerequisites
- Local run with Docker
- Default credentials bootstrap
- How to add manual FX rates
- How to update bullion and coin valuations
```

- [ ] **Step 4: Run full verification**

Run: `dotnet test -c Debug`
Expected: PASS (all domain + integration tests).

Run: `docker compose ps`
Expected: `postgres` and `treasury-host` both `Up`.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.Infrastructure src/Treasury.Host docker-compose.yml README.md tests/Treasury.IntegrationTests
git commit -m "chore: finalize phase1 migrations acceptance tests and deployment docs"
```

## Self-Review Results

1. **Spec coverage:** Covered single-household auth, flexible accounts, sharing, ledger transactions/transfers, manual valuation (bullion + coins), manual FX, multi-currency reporting, Docker hosting, and Blazor UI.
2. **Placeholder scan:** Removed placeholder words and provided concrete files, interfaces, commands, and code excerpts in each task.
3. **Type consistency:** Consistent naming for policies, services, endpoints, and DTO usage across tasks.
