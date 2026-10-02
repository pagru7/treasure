# Transaction Non-Latest Edit Rules Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce and lock the editing contract where non-latest transactions can change only Description, Category, and Tags.

**Architecture:** Keep `TransactionEditingService` as the single source of enforcement and align `Transactions.razor` explanatory text with actual behavior. Add one focused integration test that proves non-latest edits can change Description/Category/Tags while Amount/Type/Date remain unchanged. This avoids broad refactors and protects balance integrity.

**Tech Stack:** .NET 9, ASP.NET Core, Razor Components (MudBlazor), EF Core, xUnit, FluentAssertions

## Global Constraints

- **Latest transaction:** Description, Category, Tags, Amount, Type, Date can be edited.
- **Non-latest transaction:** Description, Category, Tags can be edited.
- **Non-latest transaction:** Amount, Type, Date cannot be edited.
- Keep transfer-linked transaction restrictions unchanged.
- Keep existing endpoint routes and payload contracts unchanged.

---

### Task 1: Add regression test for non-latest allowed fields

**Files:**
- Modify: `tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs`
- Test: `tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs`

**Interfaces:**
- Consumes: `PUT /api/transactions/{id}` with `UpdateTransactionRequest` (`Description`, `CategoryId` or `Category`, `TagIds`, optional `Amount/Type/TransactionDate`)
- Produces: test `Edit_NonLatest_Allows_Description_Category_And_Tags_Only`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Edit_NonLatest_Allows_Description_Category_And_Tags_Only()
{
    await using var app = new TreasuryHostFactory();
    var client = CreateAuthenticatedClient(app);
    await RegisterAndSignInAsync(client);

    var accountId = await CreateAccountAsync(client, "Historical editable fields");
    var firstDate = DateTime.UtcNow.AddDays(-2);
    var firstTransactionId = await CreateTransactionAsync(client, accountId, "First", 10m, "expense", firstDate);
    _ = await CreateTransactionAsync(client, accountId, "Second", 20m, "expense", DateTime.UtcNow.AddDays(-1));

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
    var user = await db.Users.SingleAsync();
    var category = new Treasury.App.Domain.Category { HouseholdId = user.HouseholdId, Name = "Updated", IsActive = true };
    var tag = new Treasury.App.Domain.Tag { HouseholdId = user.HouseholdId, Name = "bill", Color = "#3B82F6" };
    db.Categories.Add(category);
    db.Tags.Add(tag);
    await db.SaveChangesAsync();

    var response = await client.PutAsJsonAsync($"/api/transactions/{firstTransactionId}", new
    {
        Id = firstTransactionId,
        Description = "First updated",
        CategoryId = category.Id,
        TagIds = new[] { tag.Id }
    });

    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var updated = await db.Transactions.SingleAsync(x => x.Id == firstTransactionId);
    updated.Description.Should().Be("First updated");
    updated.Category.Should().Be("Updated");
    updated.Amount.Should().Be(10m);
    updated.Type.Should().Be("expense");
    updated.TransactionDate.Date.Should().Be(firstDate.Date);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Edit_NonLatest_Allows_Description_Category_And_Tags_Only"`
Expected: FAIL if rule is not enforced consistently.

- [ ] **Step 3: Write minimal implementation**

```csharp
// TransactionEditingService.UpdateAsync(...)
// Keep this behavior:
// - non-latest: reject amount/type/date changes
// - allow description/category/tag changes
// Ensure non-latest path does not reject CategoryId/TagIds updates.
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Edit_NonLatest_Allows_Description_Category_And_Tags_Only|FullyQualifiedName~Edit_NonLatest_Rejects_Amount_Date_Type_Changes"`
Expected: PASS for both tests (allowed-fields and blocked-fields contracts).

- [ ] **Step 5: Commit**

```bash
git add tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs src/Treasury.App/Application/Transactions/TransactionEditingService.cs
git commit -m "test(transactions): lock non-latest editable fields contract"
```

### Task 2: Align transaction edit UI message with enforced contract

**Files:**
- Modify: `src/Treasury.App/Pages/Transactions.razor`
- Test: `tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs`

**Interfaces:**
- Consumes: `_editingTransaction.IsLatest`
- Produces: explicit non-latest copy: “only description, category, and tags can be edited”

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Transactions_Page_Edit_Hint_For_NonLatest_Mentions_Only_Description_Category_And_Tags()
{
    await using var app = new TreasuryHostFactory();
    var client = CreateAuthenticatedClient(app);
    await RegisterAndSignInAsync(client);

    var accountId = await CreateAccountAsync(client, "Hint account");
    _ = await CreateTransactionAsync(client, accountId, "First", 10m, "expense", DateTime.UtcNow.AddDays(-2));
    _ = await CreateTransactionAsync(client, accountId, "Second", 20m, "expense", DateTime.UtcNow.AddDays(-1));

    var page = await client.GetAsync("/transactions");
    var html = await page.Content.ReadAsStringAsync();
    html.Should().Contain("only description, category, and tags can be edited");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Transactions_Page_Edit_Hint_For_NonLatest_Mentions_Only_Description_Category_And_Tags"`
Expected: FAIL if UI copy diverges.

- [ ] **Step 3: Write minimal implementation**

```razor
@(_editingTransaction.IsLatest
    ? "This is the latest transaction for the account, so amount, date, and type can be edited."
    : "This is not the latest transaction, so only description, category, and tags can be edited.")
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~TransactionEditingRulesTests.Edit_"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App/Pages/Transactions.razor tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs
git commit -m "feat(transactions): clarify non-latest edit hint copy"
```
