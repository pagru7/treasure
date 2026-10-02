# Categories and Budget Linking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add managed household categories and make transactions, budgets, and dashboard budget health operate on category IDs instead of fragile free-text matching.

**Architecture:** Introduce a new `Category` aggregate with household scope and active state, then attach both `Transaction` and `BudgetCategory` to it with foreign keys. Keep existing string fields as display snapshots for compatibility, but use FK columns as canonical references for validation and calculations. Update UI pages (`/categories`, `/transactions`, `/budgets`, `/`) and API endpoints to enforce active-category rules and per-category budget uniqueness.

**Tech Stack:** .NET 9, ASP.NET Core Razor Components, MudBlazor, FastEndpoints, ASP.NET Core Identity, Entity Framework Core, xUnit, FluentAssertions

## Global Constraints

- New Categories management page (CRUD + disable/enable + guarded delete).
- Strong FK links: `Transaction -> Category`, `BudgetCategory -> Category`.
- Budgets created/updated per selected category (unique per household+category).
- Dashboard budget health aggregation by category ID.
- Keep disabled categories as read-only history references.
- No multi-period budgets, no rollovers, no category hierarchy changes.

---

### Task 1: Add category domain model and database schema migration

**Files:**
- Create: `src/Treasury.App/Domain/Category.cs`
- Modify: `src/Treasury.App/Domain/Transaction.cs`
- Modify: `src/Treasury.App/Domain/BudgetCategory.cs`
- Modify: `src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs`
- Create: `src/Treasury.App/Infrastructure/Migrations/<timestamp>_AddCategoriesAndCategoryLinks.cs`
- Modify: `src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs`
- Test: `tests/Treasury.IntegrationTests/CategoryBudgetLinkingTests.cs`

**Interfaces:**
- Produces: `public class Category { Guid Id; Guid HouseholdId; string Name; bool IsActive; DateTime CreatedAt; DateTime UpdatedAt; }`
- Produces: `Transaction.CategoryId: Guid` (required FK)
- Produces: `BudgetCategory.CategoryId: Guid` (required FK)
- Produces: `DbSet<Category> Categories`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Creating_Transaction_With_CategoryId_Persists_Category_Link()
{
    await using var app = new TreasuryHostFactory();
    var client = CreateAuthenticatedClient(app);
    await RegisterAndSignInAsync(client);

    var accountId = await CreateAccountAsync(client, "Main");

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
    var user = await db.Users.SingleAsync();
    var category = new Category { HouseholdId = user.HouseholdId, Name = "Groceries", IsActive = true };
    db.Categories.Add(category);
    await db.SaveChangesAsync();

    var response = await client.PostAsJsonAsync("/api/transactions", new
    {
        AccountId = accountId,
        Description = "Market",
        CategoryId = category.Id,
        Amount = 40m,
        Type = "expense",
        TransactionDate = DateTime.UtcNow
    });

    response.StatusCode.Should().Be(HttpStatusCode.Created);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Creating_Transaction_With_CategoryId_Persists_Category_Link"`
Expected: FAIL with compile/runtime failure because `Category` entity and `CategoryId` links do not exist yet.

- [ ] **Step 3: Write minimal implementation**

```csharp
// Category.cs
namespace Treasury.App.Domain;
public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// Transaction.cs
public Guid CategoryId { get; set; }

// BudgetCategory.cs
public Guid CategoryId { get; set; }

// TreasuryDbContext.OnModelCreating
modelBuilder.Entity<Category>()
    .HasIndex(x => new { x.HouseholdId, x.Name })
    .IsUnique();

modelBuilder.Entity<Transaction>()
    .HasOne<Category>()
    .WithMany()
    .HasForeignKey(x => x.CategoryId)
    .OnDelete(DeleteBehavior.Restrict);

modelBuilder.Entity<BudgetCategory>()
    .HasOne<Category>()
    .WithMany()
    .HasForeignKey(x => x.CategoryId)
    .OnDelete(DeleteBehavior.Restrict);

modelBuilder.Entity<BudgetCategory>()
    .HasIndex(x => new { x.HouseholdId, x.CategoryId })
    .IsUnique();
```

Migration must:
1. create `Categories`,
2. insert categories from existing transaction/budget names per household,
3. backfill `Transactions.CategoryId` and `BudgetCategories.CategoryId`,
4. create `"General"` when source category text is blank.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Creating_Transaction_With_CategoryId_Persists_Category_Link"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App/Domain/Category.cs src/Treasury.App/Domain/Transaction.cs src/Treasury.App/Domain/BudgetCategory.cs src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs src/Treasury.App/Infrastructure/Migrations tests/Treasury.IntegrationTests/CategoryBudgetLinkingTests.cs
git commit -m "feat(categories): add category entity and FK links"
```

### Task 2: Build categories management page with guarded lifecycle actions

**Files:**
- Create: `src/Treasury.App/Pages/Categories.razor`
- Modify: `src/Treasury.App/Components/Layout/MainLayout.razor`
- Test: `tests/Treasury.IntegrationTests/CategoryBudgetLinkingTests.cs`

**Interfaces:**
- Produces page route: `@page "/categories"`
- Produces actions:
  - `Task CreateCategoryAsync()`
  - `Task RenameCategoryAsync(Guid id, string newName)`
  - `Task ToggleCategoryAsync(Guid id)`
  - `Task DeleteCategoryAsync(Guid id)` with reference checks

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Categories_Page_Allows_Disable_But_Blocks_Delete_When_Referenced()
{
    await using var app = new TreasuryHostFactory();
    var client = CreateAuthenticatedClient(app);
    await RegisterAndSignInAsync(client);

    var page = await client.GetAsync("/categories");
    page.StatusCode.Should().Be(HttpStatusCode.OK);

    var body = await page.Content.ReadAsStringAsync();
    body.Should().Contain("Categories");
    body.Should().Contain("Disable");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Categories_Page_Allows_Disable_But_Blocks_Delete_When_Referenced"`
Expected: FAIL because `/categories` page and actions are missing.

- [ ] **Step 3: Write minimal implementation**

```razor
@page "/categories"
@attribute [Authorize]
@inject TreasuryDbContext DbContext
@inject UserManager<ApplicationUser> UserManager
@inject AuthenticationStateProvider AuthenticationStateProvider
@inject ISnackbar Snackbar

<MudText Typo="Typo.h4">Categories</MudText>
<!-- Add form + table like Tags page -->
```

```csharp
private async Task DeleteCategoryAsync(Guid categoryId)
{
    var hasTransactions = await DbContext.Transactions.AnyAsync(x => x.CategoryId == categoryId);
    var hasBudgets = await DbContext.BudgetCategories.AnyAsync(x => x.CategoryId == categoryId);
    if (hasTransactions || hasBudgets)
    {
        Snackbar.Add("Cannot delete category with transactions or budgets.", Severity.Warning);
        return;
    }

    var category = await DbContext.Categories.SingleAsync(x => x.Id == categoryId);
    DbContext.Categories.Remove(category);
    await DbContext.SaveChangesAsync();
}
```

Add `Categories` nav link in both app bar and drawer.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Categories_Page_Allows_Disable_But_Blocks_Delete_When_Referenced"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App/Pages/Categories.razor src/Treasury.App/Components/Layout/MainLayout.razor tests/Treasury.IntegrationTests/CategoryBudgetLinkingTests.cs
git commit -m "feat(categories): add categories management page"
```

### Task 3: Switch transaction API and Transactions page to category IDs

**Files:**
- Modify: `src/Treasury.App/Contracts/Transactions/CreateTransactionRequest.cs`
- Modify: `src/Treasury.App/Contracts/Transactions/UpdateTransactionRequest.cs`
- Modify: `src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs`
- Modify: `src/Treasury.App/Application/Transactions/TransactionEditingService.cs`
- Modify: `src/Treasury.App/Pages/Transactions.razor`
- Test: `tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs`
- Test: `tests/Treasury.IntegrationTests/CategoryBudgetLinkingTests.cs`

**Interfaces:**
- Produces: `CreateTransactionRequest.CategoryId: Guid`
- Produces: `UpdateTransactionRequest.CategoryId: Guid?`
- Produces validation contract:
  - unknown category -> 404/validation failure
  - inactive category -> validation failure

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Create_Transaction_With_Inactive_Category_Is_Rejected()
{
    await using var app = new TreasuryHostFactory();
    var client = CreateAuthenticatedClient(app);
    await RegisterAndSignInAsync(client);
    var accountId = await CreateAccountAsync(client, "Main");

    var categoryId = await CreateCategoryAsync(app, "Travel", isActive: false);

    var response = await client.PostAsJsonAsync("/api/transactions", new
    {
        AccountId = accountId,
        Description = "Trip",
        CategoryId = categoryId,
        Amount = 100m,
        Type = "expense",
        TransactionDate = DateTime.UtcNow
    });

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Create_Transaction_With_Inactive_Category_Is_Rejected"`
Expected: FAIL because transaction flow still accepts free-text categories and has no inactive-category check.

- [ ] **Step 3: Write minimal implementation**

```csharp
// CreateTransactionRequest
public Guid CategoryId { get; set; }

// CreateTransactionEndpoint validation
var category = await db.Categories.SingleOrDefaultAsync(
    x => x.Id == request.CategoryId && x.HouseholdId == user.HouseholdId, ct);
if (category is null)
{
    AddError(x => x.CategoryId, "A valid category is required.");
    await SendErrorsAsync(cancellation: ct);
    return;
}
if (!category.IsActive)
{
    AddError(x => x.CategoryId, "Category is disabled.");
    await SendErrorsAsync(cancellation: ct);
    return;
}

transaction.CategoryId = category.Id;
transaction.Category = category.Name;
```

```razor
<!-- Transactions.razor create/edit form -->
<MudSelect T="string" Label="Category" @bind-Value="_newTransaction.CategoryId" Required="true">
    @foreach (var category in _activeCategories)
    {
        <MudSelectItem Value="@category.Id.ToString()">@category.Name</MudSelectItem>
    }
</MudSelect>
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Create_Transaction_With_Inactive_Category_Is_Rejected|FullyQualifiedName~Transaction_"`
Expected: PASS for new inactive-category test and existing transaction-editing rules.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App/Contracts/Transactions/CreateTransactionRequest.cs src/Treasury.App/Contracts/Transactions/UpdateTransactionRequest.cs src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs src/Treasury.App/Application/Transactions/TransactionEditingService.cs src/Treasury.App/Pages/Transactions.razor tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs tests/Treasury.IntegrationTests/CategoryBudgetLinkingTests.cs
git commit -m "feat(transactions): enforce category id selection and active-category validation"
```

### Task 4: Switch budgets to category IDs and enforce per-category uniqueness

**Files:**
- Modify: `src/Treasury.App/Contracts/Budgets/CreateBudgetCategoryRequest.cs`
- Modify: `src/Treasury.App/Endpoints/Budgets/CreateBudgetEndpoint.cs`
- Modify: `src/Treasury.App/Endpoints/Budgets/GetBudgetsEndpoint.cs`
- Modify: `src/Treasury.App/Pages/Budgets.razor`
- Test: `tests/Treasury.IntegrationTests/CategoryBudgetLinkingTests.cs`

**Interfaces:**
- Produces: `CreateBudgetCategoryRequest.CategoryId: Guid`
- Produces upsert rule: one budget per `(HouseholdId, CategoryId)`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Saving_Budget_Twice_For_Same_Category_Updates_Existing_Row()
{
    await using var app = new TreasuryHostFactory();
    var client = CreateAuthenticatedClient(app);
    await RegisterAndSignInAsync(client);

    var categoryId = await CreateCategoryAsync(app, "Utilities", isActive: true);

    var first = await client.PostAsJsonAsync("/api/budgets", new
    {
        CategoryId = categoryId,
        MonthlyLimit = 500m,
        Currency = "PLN",
        Notes = "Base"
    });
    first.StatusCode.Should().Be(HttpStatusCode.Created);

    var second = await client.PostAsJsonAsync("/api/budgets", new
    {
        CategoryId = categoryId,
        MonthlyLimit = 650m,
        Currency = "PLN",
        Notes = "Updated"
    });

    second.StatusCode.Should().Be(HttpStatusCode.OK);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Saving_Budget_Twice_For_Same_Category_Updates_Existing_Row"`
Expected: FAIL because current budget API creates independent rows using free-text names.

- [ ] **Step 3: Write minimal implementation**

```csharp
// CreateBudgetCategoryRequest
public Guid CategoryId { get; set; }
public decimal MonthlyLimit { get; set; }
public string Currency { get; set; } = "PLN";
public string Notes { get; set; } = string.Empty;

// CreateBudgetEndpoint
var category = await db.Categories.SingleOrDefaultAsync(
    x => x.Id == request.CategoryId && x.HouseholdId == user.HouseholdId, ct);
if (category is null)
{
    AddError(x => x.CategoryId, "A valid category is required.");
    await SendErrorsAsync(cancellation: ct);
    return;
}
if (!category.IsActive)
{
    AddError(x => x.CategoryId, "Category is disabled.");
    await SendErrorsAsync(cancellation: ct);
    return;
}

var existing = await db.BudgetCategories.SingleOrDefaultAsync(
    x => x.HouseholdId == user.HouseholdId && x.CategoryId == request.CategoryId, ct);

if (existing is null)
{
    var budget = new BudgetCategory { HouseholdId = user.HouseholdId, CategoryId = category.Id, Name = category.Name, ... };
    db.BudgetCategories.Add(budget);
    await db.SaveChangesAsync(ct);
    await SendAsync(new { budget.Id, budget.Name, budget.MonthlyLimit, budget.Currency, budget.Notes }, StatusCodes.Status201Created, ct);
    return;
}

existing.Name = category.Name;
existing.MonthlyLimit = request.MonthlyLimit;
existing.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant();
existing.Notes = request.Notes?.Trim() ?? string.Empty;
existing.UpdatedAt = DateTime.UtcNow;
await db.SaveChangesAsync(ct);
await SendOkAsync(new { existing.Id, existing.Name, existing.MonthlyLimit, existing.Currency, existing.Notes }, ct);
```

Budgets page must use category selector and not free-text category input.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Saving_Budget_Twice_For_Same_Category_Updates_Existing_Row|FullyQualifiedName~GetBudgets"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App/Contracts/Budgets/CreateBudgetCategoryRequest.cs src/Treasury.App/Endpoints/Budgets/CreateBudgetEndpoint.cs src/Treasury.App/Endpoints/Budgets/GetBudgetsEndpoint.cs src/Treasury.App/Pages/Budgets.razor tests/Treasury.IntegrationTests/CategoryBudgetLinkingTests.cs
git commit -m "feat(budgets): link budgets to categories and upsert per category"
```

### Task 5: Update dashboard budget health to aggregate by category ID

**Files:**
- Modify: `src/Treasury.App/Pages/Index.razor`
- Test: `tests/Treasury.IntegrationTests/CategoryBudgetLinkingTests.cs`

**Interfaces:**
- Consumes: `Transaction.CategoryId`, `BudgetCategory.CategoryId`, `Category.Name`
- Produces: budget-health rows with correct spent/limit status based on FK grouping

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Dashboard_Budget_Health_Uses_CategoryId_Not_Name_Text()
{
    await using var app = new TreasuryHostFactory();
    var client = CreateAuthenticatedClient(app);
    await RegisterAndSignInAsync(client);

    var householdId = await GetHouseholdIdAsync(app);
    var groceriesCategoryId = await CreateCategoryAsync(app, householdId, "Groceries", isActive: true);
    await UpsertBudgetAsync(client, groceriesCategoryId, 100m, "PLN", "Monthly groceries");
    var accountId = await CreateAccountAsync(client, "Household wallet");
    await CreateExpenseAsync(client, accountId, groceriesCategoryId, 130m, "Big shopping");
    await RenameCategoryAsync(app, groceriesCategoryId, "Food");

    var response = await client.GetAsync("/");
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var body = await response.Content.ReadAsStringAsync();
    body.Should().Contain("Budget health");
    body.Should().Contain("Food");
    body.Should().Contain("130.00 PLN / 100.00 PLN");
    body.Should().Contain("Over budget");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Dashboard_Budget_Health_Uses_CategoryId_Not_Name_Text"`
Expected: FAIL because dashboard currently compares transaction category text against budget name.

- [ ] **Step 3: Write minimal implementation**

```csharp
var thisMonthExpenses = await DbContext.Transactions
    .Where(x => x.HouseholdId == householdId && x.TransactionDate >= monthStart && x.Type == "expense")
    .ToListAsync();

var budgets = await DbContext.BudgetCategories
    .Where(x => x.HouseholdId == householdId)
    .OrderBy(x => x.Name)
    .ToListAsync();

var spentByCategoryId = thisMonthExpenses
    .GroupBy(x => x.CategoryId)
    .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

foreach (var budget in budgets)
{
    var spent = spentByCategoryId.GetValueOrDefault(budget.CategoryId, 0m);
    _budgetStatus.Add(new BudgetStatusItem(budget.Name, budget.MonthlyLimit, budget.Currency, spent, spent > budget.MonthlyLimit));
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Dashboard_Budget_Health_Uses_CategoryId_Not_Name_Text|FullyQualifiedName~Dashboard_"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App/Pages/Index.razor tests/Treasury.IntegrationTests/CategoryBudgetLinkingTests.cs
git commit -m "feat(dashboard): compute budget health by category id"
```

### Task 6: Full targeted regression for touched functional areas

**Files:**
- Modify: none (verification only)
- Test: `tests/Treasury.IntegrationTests/CategoryBudgetLinkingTests.cs`
- Test: `tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs`
- Test: `tests/Treasury.IntegrationTests/TransfersWorkflowTests.cs`
- Test: `tests/Treasury.IntegrationTests/AuthTests.cs`

**Interfaces:**
- Consumes all changed contracts and pages.
- Produces confidence that category/budget linking did not regress unrelated behavior.

- [ ] **Step 1: Run category-budget feature tests**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~CategoryBudgetLinkingTests"`
Expected: PASS.

- [ ] **Step 2: Run transaction and transfer regression slice**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~TransactionEditingRulesTests|FullyQualifiedName~TransfersWorkflowTests"`
Expected: PASS.

- [ ] **Step 3: Run auth regression slice**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~AuthTests"`
Expected: PASS.

- [ ] **Step 4: Commit (if code changed during fixes)**

```bash
git status --short
```

Expected: clean working tree for planned files, or only intentional follow-up edits committed.
