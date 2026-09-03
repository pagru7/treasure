### Task 3: Keep the sharing flow covered end to end

**Files:**

- Modify: `tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs`
- Modify: `tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs`

**Interfaces:**

- Consumes:
  - the new account-sharing UI and shared service
  - the existing account sharing endpoint
- Produces:
  - regression coverage proving the owner can see shared recipients, the picker excludes the current user, and the shared user still gets read-only access

- [ ] **Step 1: Expand the UI regression test to check the current user is not in the picker**

```csharp
body.Should().Contain(sharedEmail);
body.Should().Contain("Share read-only");
body.Should().NotContain(ownerEmail);
```

- [ ] **Step 2: Keep the read-only access test passing**

```csharp
[Fact]
public async Task Shared_User_Cannot_Post_To_Owner_Edit_Endpoints()
{
    var postTransaction = await sharedClient.PostAsJsonAsync("/api/transactions", new
    {
        AccountId = accountId,
        Description = "Attempt by shared user",
        Category = "General",
        Amount = 10m,
        Currency = "PLN",
        Type = "expense",
        TransactionDate = DateTime.UtcNow
    });

    postTransaction.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

- [ ] **Step 3: Run the full integration suite and verify the new UI behavior stays stable**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug`

Expected: PASS.

- [ ] **Step 4: Commit the finished feature**

```bash
git add tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
git commit -m "test: cover account sharing ui and permissions"
```
