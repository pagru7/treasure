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

