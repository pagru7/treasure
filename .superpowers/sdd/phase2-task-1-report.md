# Phase 2 Task 1 Report

## What changed
- Added `IsActive` and `BankAccountNumber` to the account domain model.
- Added lifecycle DTOs and endpoints for updating account details, toggling active state, and deleting accounts.
- Updated `/api/accounts` to hide inactive accounts by default and support `?includeInactive=true`.
- Added bank account number validation: optional, trimmed, digits-only, length 16..34.
- Updated the Accounts page to support showing inactive accounts, displaying bank account numbers, and owner actions to deactivate/reactivate or remove accounts.
- Added an EF Core migration for the new account columns.
- Added focused lifecycle integration tests.

## Tests run
- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests"`
  - Result: passed, 4/4 tests.
- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsEndpointTests|FullyQualifiedName~AccountsSharingUiTests|FullyQualifiedName~SharedReadOnlyUiPermissionTests"`
  - Result: passed, 8/8 tests.

## Files changed
- `src/Treasury.App/Domain/Account.cs`
- `src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs`
- `src/Treasury.App/Contracts/Accounts/AccountResponse.cs`
- `src/Treasury.App/Contracts/Accounts/UpdateAccountRequest.cs`
- `src/Treasury.App/Contracts/Accounts/SetAccountActiveStateRequest.cs`
- `src/Treasury.App/Application/Accounts/AccountLifecycleValidation.cs`
- `src/Treasury.App/Application/Accounts/AccountSharingService.cs`
- `src/Treasury.App/Endpoints/Accounts/CreateAccountEndpoint.cs`
- `src/Treasury.App/Endpoints/Accounts/GetAccountsEndpoint.cs`
- `src/Treasury.App/Endpoints/Accounts/UpdateAccountEndpoint.cs`
- `src/Treasury.App/Endpoints/Accounts/SetAccountActiveStateEndpoint.cs`
- `src/Treasury.App/Endpoints/Accounts/DeleteAccountEndpoint.cs`
- `src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs`
- `src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.cs`
- `src/Treasury.App/Infrastructure/Migrations/20260903122916_AddAccountLifecycleFields.Designer.cs`
- `src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs`
- `src/Treasury.App/Pages/Accounts.razor`
- `src/Treasury.App/Pages/Accounts.razor.cs`
- `tests/Treasury.IntegrationTests/AccountsLifecycleTests.cs`
- `tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs`

## Concerns
- The Accounts page still uses server-side data access for owner actions, while the API endpoints provide the same lifecycle behavior for external callers.
- No dedicated inline edit UI was added for account name changes; the update endpoint is in place for API use and future UI work.
