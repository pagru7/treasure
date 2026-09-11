# Transaction editing non-latest rules design

## Goal

Clarify and lock transaction editing permissions so non-latest transactions allow editing only safe fields while preserving current balance-protection rules.

## Scope

In scope:
- Transaction editing rule clarification
- UI wording alignment on Transactions page
- Explicit integration regression test for non-latest edit behavior

Out of scope:
- New transaction model fields
- Changes to latest-transaction edit capabilities
- Changes to transfer-linked transaction restrictions

## Approved behavior

- **Latest transaction:** Description, Category, Tags, Amount, Type, Date can be edited.
- **Non-latest transaction:** Description, Category, Tags can be edited.
- **Non-latest transaction:** Amount, Type, Date cannot be edited.

## Implementation design

1. Keep `TransactionEditingService` as source of truth for authorization and latest/non-latest rule enforcement.
2. Keep the Transactions page edit controls for amount/type/date disabled when transaction is not latest.
3. Keep tags editable for non-latest transactions.
4. Update the explanatory copy in the edit section so it explicitly states non-latest supports Description, Category, and Tags edits.

## Test design

Add an integration test in `tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs`:
- Setup account with two transactions so the first is non-latest.
- Update first transaction with only Description + Category + TagIds.
- Assert success response.
- Assert Description and Category changed.
- Assert tags changed.
- Assert Amount, Type, and Date remain unchanged.

## Risk and mitigation

Risk: UI message and backend behavior diverge over time.
Mitigation: explicit integration test that codifies the non-latest editing contract.
