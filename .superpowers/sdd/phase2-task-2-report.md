# Phase 2 Task 2 Report

## What changed
- Added `OutflowTransactionId` and `InflowTransactionId` to the transfer domain model.
- Updated `POST /api/transfers` to persist the transfer and both linked transactions atomically, then write back the transaction IDs.
- Added a dedicated `/transfers` page with a creation form and recent transfer history only, matching the approved option C scope.
- Added transfers navigation entries in the top bar and drawer.
- Added an EF Core migration for the new transfer link columns and updated the model snapshot.
- Added `TransfersWorkflowTests` integration coverage for successful linked transfers, inactive-account rejection, and transfers page rendering.

## Validation
- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransfersWorkflowTests"`
  - Result: passed, 3/3 tests.
- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests"`
  - Result: passed, 7/7 tests.

## Notes
- The page uses direct server-side database access for the form/history workflow, while the API endpoint remains the canonical write path for transfer creation behavior and validation.
- No balance summary widgets were added, per the scope clarification.

## Commit
- 4a56c75

## Follow-up fixes
- Extracted a shared `TransferCreationService` so the transfers page and `POST /api/transfers` use the same canonical write path.
- Changed transfer link columns to nullable so pre-existing rows are not backfilled with `Guid.Empty` placeholders.
- Kept atomic persistence, active-account checks, and paired transfer transaction creation intact.

## Follow-up validation
- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransfersWorkflowTests"` — passed, 3/3 tests.
- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests"` — passed, 7/7 tests.
