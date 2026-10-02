# Accounts Description + Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add optional account notes (max 1000 chars) and a new household-scoped Feedback feature with create/list/update status+description flows.

**Architecture:** Extend the existing account model and write flows with a nullable `Description` field, then add a new `FeedbackItem` aggregate with FastEndpoints for create/list/update. Expose Feedback via a dedicated MudBlazor page and navigation entry, while preserving household isolation and existing auth patterns.

**Tech Stack:** ASP.NET Core, FastEndpoints, Entity Framework Core (Npgsql + InMemory tests), Blazor Server + MudBlazor, xUnit + FluentAssertions

## Global Constraints

- `Account.Description` is optional and max 1000 characters.
- Feedback permissions are household-wide: all authenticated household users can create and update feedback in their household.
- Feedback description is required and max 1000 characters.
- Feedback status values are exactly `New`, `Implementing`, `Done`.
- `POST /api/feedback` always defaults status to `New`.
- All feedback reads/writes must be filtered by current user `HouseholdId`.

---

### Task 1: Add persistence for account description and feedback entities

**Files:**
- Create: `src/Treasury.App/Domain/FeedbackStatus.cs`
- Create: `src/Treasury.App/Domain/FeedbackItem.cs`
- Modify: `src/Treasury.App/Domain/Account.cs`
- Modify: `src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs`
- Create: `src/Treasury.App/Migrations/<timestamp>_accounts_description_feedback.cs`
- Modify: `src/Treasury.App/Migrations/TreasuryDbContextModelSnapshot.cs`
- Test: `tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs`

**Interfaces:**
- Consumes: existing `TreasuryDbContext`, `Account` entity, migration tooling.
- Produces:
  - `public string? Account.Description { get; set; }`
  - `public enum FeedbackStatus { New = 0, Implementing = 1, Done = 2 }`
  - `public sealed class FeedbackItem` with `Id, HouseholdId, Title, Description, Status, CreatedAt, UpdatedAt`
  - `DbSet<FeedbackItem> FeedbackItems` in `TreasuryDbContext`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Create_Account_Persists_Description()
{
    await using var app = new TreasuryHostFactory();
    var client = CreateAuthenticatedClient(app);
    await RegisterAndSignInAsync(client);

    var response = await client.PostAsJsonAsync("/api/accounts", new
    {
        Name = "Notes account",
        Currency = "PLN",
        AccountType = "cash-wallet",
        Description = "Emergency cash account"
    });

    response.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:  
`dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~AccountsLifecycleTests.Create_Account_Persists_Description"`

Expected: FAIL because account create flow does not yet expose/store `Description`.

- [ ] **Step 3: Write minimal implementation**

```csharp
// Domain/Account.cs
public string? Description { get; set; }

// Domain/FeedbackStatus.cs
public enum FeedbackStatus { New = 0, Implementing = 1, Done = 2 }

// Domain/FeedbackItem.cs
public sealed class FeedbackItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public FeedbackStatus Status { get; set; } = FeedbackStatus.New;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Infrastructure/Data/TreasuryDbContext.cs
public DbSet<FeedbackItem> FeedbackItems => Set<FeedbackItem>();
```

Create migration:

`dotnet ef migrations add accounts_description_feedback --project .\src\Treasury.App\Treasury.App.csproj --startup-project .\src\Treasury.App\Treasury.App.csproj`

- [ ] **Step 4: Run test to verify it passes**

Run:
1. `dotnet build .\src\Treasury.App\Treasury.App.csproj`
2. `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~AccountsLifecycleTests.Create_Account_Persists_Description"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App/Domain/Account.cs src/Treasury.App/Domain/FeedbackStatus.cs src/Treasury.App/Domain/FeedbackItem.cs src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs src/Treasury.App/Migrations tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
git commit -m "feat: add feedback persistence and account description column"
```

### Task 2: Wire account description through create/update endpoints and account DTOs

**Files:**
- Modify: `src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs`
- Modify: `src/Treasury.App/Contracts/Accounts/AccountResponse.cs`
- Modify: `src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs`
- Modify: `src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs`
- Modify: `src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs`
- Test: `tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs`

**Interfaces:**
- Consumes:
  - `Account.Description`
  - existing account endpoints/routes
- Produces:
  - `CreateAccountRequest.Description : string?`
  - update route request carries `Description`
  - 1000-char validation error for create/update
  - account responses include `Description`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task Update_Account_Description_Persists_Value()
{
    // create account then PUT /api/accounts/{id} with Description
    // assert GET /api/accounts includes updated description
}

[Fact]
public async Task Create_Account_Rejects_Too_Long_Description()
{
    var tooLong = new string('a', 1001);
    // POST /api/accounts with tooLong description
    // assert 400 BadRequest
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:  
`dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~AccountsLifecycleTests.Update_Account_Description_Persists_Value|FullyQualifiedName~AccountsLifecycleTests.Create_Account_Rejects_Too_Long_Description"`

Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

```csharp
// CreateAccountEndpoint + UpdateAccountEndpoint
if (!string.IsNullOrEmpty(request.Description) && request.Description.Length > 1000)
{
    AddError("Description must be at most 1000 characters.");
    await SendErrorsAsync(cancellation: ct);
    return;
}

account.Description = string.IsNullOrWhiteSpace(request.Description)
    ? null
    : request.Description.Trim();
```

Ensure `GetAccountsEndpoint` and `AccountResponse` include description field.

- [ ] **Step 4: Run tests to verify they pass**

Run:
1. `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~AccountsLifecycleTests.Update_Account_Description_Persists_Value|FullyQualifiedName~AccountsLifecycleTests.Create_Account_Rejects_Too_Long_Description"`
2. `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~AccountsLifecycleTests"`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App/Contracts/Accounts src/Treasury.App/Endpoints/Accounts tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs
git commit -m "feat: support account descriptions in account APIs"
```

### Task 3: Build feedback API endpoints with validation and household isolation

**Files:**
- Create: `src/Treasury.App/Contracts/Feedback/CreateFeedbackRequest.cs`
- Create: `src/Treasury.App/Contracts/Feedback/UpdateFeedbackRequest.cs`
- Create: `src/Treasury.App/Endpoints/Feedback/CreateFeedbackEndpoint.cs`
- Create: `src/Treasury.App/Endpoints/Feedback/GetFeedbackEndpoint.cs`
- Create: `src/Treasury.App/Endpoints/Feedback/UpdateFeedbackEndpoint.cs`
- Test: `tests/Treasury.IntegrationTests/FeedbackWorkflowTests.cs`

**Interfaces:**
- Consumes:
  - `FeedbackItem`
  - `FeedbackStatus`
  - current auth/household model (`UserManager<ApplicationUser>`)
- Produces:
  - `POST /api/feedback` (status forced to `New`)
  - `GET /api/feedback` (household scoped, newest first)
  - `PUT /api/feedback/{id}` (update `Status`, `Description`)

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task Create_Feedback_Defaults_Status_To_New() { /* assert created status */ }

[Fact]
public async Task Update_Feedback_Changes_Status_And_Description() { /* assert updated values */ }

[Fact]
public async Task Feedback_Is_Household_Isolated() { /* second household cannot update */ }

[Fact]
public async Task Create_Feedback_Rejects_Too_Long_Description() { /* 1001 => 400 */ }
```

- [ ] **Step 2: Run tests to verify they fail**

Run:  
`dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~FeedbackWorkflowTests"`

Expected: FAIL (missing contracts/endpoints).

- [ ] **Step 3: Write minimal implementation**

```csharp
// POST /api/feedback
var item = new FeedbackItem
{
    HouseholdId = user.HouseholdId,
    Title = request.Title.Trim(),
    Description = request.Description.Trim(),
    Status = FeedbackStatus.New,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow
};

// PUT /api/feedback/{id}
item.Status = request.Status;
item.Description = request.Description.Trim();
item.UpdatedAt = DateTime.UtcNow;
```

Validation rules in create/update endpoints:
- title required on create
- description required and length <= 1000
- always filter row by `Id` + `HouseholdId`

- [ ] **Step 4: Run tests to verify they pass**

Run:
1. `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~FeedbackWorkflowTests"`
2. `dotnet build .\src\Treasury.App\Treasury.App.csproj`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App/Contracts/Feedback src/Treasury.App/Endpoints/Feedback tests/Treasury.IntegrationTests/FeedbackWorkflowTests.cs
git commit -m "feat: add feedback API with status workflow"
```

### Task 4: Add feedback page and account description UI

**Files:**
- Create: `src/Treasury.App/Pages/Feedback.razor`
- Modify: `src/Treasury.App/Pages/Accounts.razor`
- Modify: `src/Treasury.App/Pages/Accounts.razor.cs`
- Modify: `src/Treasury.App/Components/Layout/MainLayout.razor`
- Test: `tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs`
- Test: `tests/Treasury.IntegrationTests/FeedbackWorkflowTests.cs`

**Interfaces:**
- Consumes:
  - account endpoints returning description
  - feedback endpoints from Task 3
- Produces:
  - `/feedback` page with create + list + update status/description
  - Accounts page create/edit/display description fields
  - Navigation links to Feedback

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public async Task Feedback_Page_Shows_Title_And_Status_Options() { /* GET /feedback */ }

[Fact]
public async Task Accounts_Page_Shows_Account_Description_When_Present() { /* GET /accounts contains description */ }
```

- [ ] **Step 2: Run tests to verify they fail**

Run:  
`dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Feedback_Page_Shows_Title_And_Status_Options|FullyQualifiedName~Accounts_Page_Shows_Account_Description_When_Present"`

Expected: FAIL.

- [ ] **Step 3: Write minimal implementation**

```razor
<!-- Accounts.razor create/edit form -->
<MudTextField Label="Description (optional)" @bind-Value="_newAccount.Description" Lines="4" />

<!-- card display -->
@if (!string.IsNullOrWhiteSpace(account.Description))
{
    <MudText Typo="Typo.body2">@account.Description</MudText>
}
```

```razor
<!-- Feedback.razor -->
@page "/feedback"
<MudText Typo="Typo.h4">Feedback</MudText>
<!-- create form + table with status select and description edit -->
```

- [ ] **Step 4: Run tests to verify they pass**

Run:
1. `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~FeedbackWorkflowTests|FullyQualifiedName~Accounts_Page_Shows_Account_Description_When_Present"`
2. `dotnet build .\src\Treasury.App\Treasury.App.csproj`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App/Pages/Feedback.razor src/Treasury.App/Pages/Accounts.razor src/Treasury.App/Pages/Accounts.razor.cs src/Treasury.App/Components/Layout/MainLayout.razor tests/Treasury.IntegrationTests
git commit -m "feat: add feedback page and account description UI"
```

### Task 5: Final verification and migration readiness

**Files:**
- Modify: `docs/superpowers/specs/2026-09-14-accounts-description-feedback-design.md` (only if behavior drift requires documented clarification)
- Test: `tests/Treasury.IntegrationTests/FeedbackWorkflowTests.cs`
- Test: `tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs`

**Interfaces:**
- Consumes: tasks 1-4 implementation.
- Produces: validated feature set ready for deployment migration.

- [ ] **Step 1: Run focused regression test set**

Run:
1. `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~FeedbackWorkflowTests|FullyQualifiedName~AccountsLifecycleTests"`
2. `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~AccountsSharingUiTests|FullyQualifiedName~SharedReadOnlyUiPermissionTests"`

Expected: PASS.

- [ ] **Step 2: Build app**

Run:  
`dotnet build .\src\Treasury.App\Treasury.App.csproj`

Expected: BUILD SUCCEEDED.

- [ ] **Step 3: Verify migration applies**

Run:  
`dotnet ef database update --project .\src\Treasury.App\Treasury.App.csproj --startup-project .\src\Treasury.App\Treasury.App.csproj`

Expected: update completes with new `Accounts.Description` and `FeedbackItems` schema applied.

- [ ] **Step 4: Commit final polish**

```bash
git add .
git commit -m "chore: finalize accounts description and feedback feature"
```

