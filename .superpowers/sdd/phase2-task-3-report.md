# Phase 2 Task 3 Report

## Summary

Implemented transaction running-balance tracking and edit-rule enforcement.

### Data/model
- Added `Transaction.BalanceAfterTransaction`.
- Generated EF migration `20260903134028_AddTransactionBalanceAfterTransaction`.
- Updated the model snapshot and seeded historical transactions with running-balance values.

### Write paths
- `CreateTransactionEndpoint` now stores `BalanceAfterTransaction` on new transactions.
- `TransferCreationService` now stores running balances for transfer outflow/inflow transactions.
- Added `PUT /api/transactions/{id:guid}` via `UpdateTransactionEndpoint`.

### Edit rules
- Non-latest transactions can only change description, category, and tags.
- Latest transactions can change amount, date, and type, and the account balance plus running balances are recomputed.
- Transactions page now shows an edit affordance and disables amount/date/type editing for historical rows.

### API/read updates
- Transaction list endpoints now return `BalanceAfterTransaction`.
- Transaction ordering is now deterministic with `TransactionDate`, `CreatedAt`, and `Id`.

## Tests

Passed:
- `dotnet test tests\\Treasury.IntegrationTests\\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransactionEditingRulesTests"`
- `dotnet test tests\\Treasury.IntegrationTests\\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests|FullyQualifiedName~TransfersWorkflowTests|FullyQualifiedName~SharedReadOnlyUiPermissionTests"`
- `dotnet test tests\\Treasury.IntegrationTests\\Treasury.IntegrationTests.csproj -c Debug`

## Notes

- The migration adds `BalanceAfterTransaction` with a default value of `0m` for existing rows.
- Latest-transaction edits recompute the full account running balance chain so date changes remain consistent.
