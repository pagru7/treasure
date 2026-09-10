# Phase 2 Accounts, Transfers, and Transactions design

## Goal

Implement the next finance slice as one spec with phased delivery:

1. Accounts lifecycle improvements.
2. Dedicated transfers workflow.
3. Transaction correctness enhancements.

This scope prioritizes safe balance invariants and practical household usage over advanced financial modeling.

## Scope

### In scope

- Accounts:
  - deactivate/reactivate account
  - remove account under explicit rules
  - rename account
  - optional bank account number with basic validation
  - active-only list by default, with show-inactive toggle
- Transfers:
  - dedicated transfers page and form
  - transfer persisted as one transfer record plus two linked transactions
  - transfer record stores references to outflow and inflow transaction IDs
- Transactions:
  - add `BalanceAfterTransaction` field
  - transaction editing with latest vs non-latest constraints

### Out of scope

- External integrations or market feeds
- Strict IBAN checksum validation
- Historical migration/backfill tooling
- Changes to sharing permission model

## Architecture and boundaries

Delivery will follow vertical phases while keeping endpoint-per-class structure and existing auth/sharing policies.

### Phase 1: Accounts lifecycle

- Extend account model with:
  - `IsActive` (default true)
  - optional `BankAccountNumber`
- Add account operations:
  - rename
  - deactivate/reactivate
  - remove with rule enforcement
- UI behavior:
  - account list shows only active by default
  - toggle adds inactive accounts to view

### Phase 2: Transfers workflow

- Keep transfer as explicit domain operation, not just category-tagged transaction.
- Add dedicated Transfers page with form and validation.
- Persist every transfer as three linked records:
  - `Transfer` envelope record
  - `Transaction` outflow from source account
  - `Transaction` inflow to destination account
- `Transfer` stores references to both transaction IDs for traceability.

### Phase 3: Transaction rules

- Add `BalanceAfterTransaction` on write path.
- Enforce edit policy:
  - latest transaction for account: full edit (amount/type/date/category/description/tags)
  - non-latest transaction: only description/category/tags editable

## Data flow and rules

### Accounts

1. Page loads active accounts by default.
2. User can toggle show-inactive to include inactive accounts.
3. Remove account checks:
   - remove allowed when no transactions exist, or balance is zero
   - otherwise remove rejected and user should use deactivate
4. Inactive accounts remain in history and totals but are blocked for new operations.

### Transfers

1. User submits source account, destination account, amount, date, optional description.
2. Validation:
   - source and destination must be different
   - both accounts must be active
   - amount must be > 0
   - caller must have owner permission
3. In one atomic DB transaction:
   - create `Transfer`
   - create outflow transaction
   - create inflow transaction
   - compute/store `BalanceAfterTransaction` for both new transactions
   - update both account balances
4. Save `Transfer.OutflowTransactionId` and `Transfer.InflowTransactionId`.

### Transactions

1. Create transaction computes and stores `BalanceAfterTransaction`.
2. Edit transaction determines whether the record is the latest for account (date + deterministic tiebreaker).
3. If latest, amount/date/type edits are allowed and balances are recomputed accordingly.
4. If non-latest, amount/date/type edits are rejected with explicit validation errors; description/category/tags remain editable.

## Error handling

- Account remove blocked case returns explicit validation error explaining why deactivate is required.
- Invalid bank account number returns field-level validation error.
- Inactive account selected for new transfer/transaction returns validation error.
- Transfer persistence must be atomic: partial writes are not allowed.
- Transaction edit violations return explicit field-level validation messages.

## Testing strategy

Integration-first coverage:

1. Accounts tests:
   - rename success
   - remove allowed: no transactions
   - remove allowed: balance zero
   - remove blocked: transactions + non-zero balance
   - deactivate/reactivate behavior
   - active-only default and show-inactive toggle
2. Transfers tests:
   - creates one transfer + two linked transactions
   - transfer stores outflow/inflow transaction references
   - balances update correctly for both accounts
   - inactive accounts rejected as source/destination
3. Transaction tests:
   - `BalanceAfterTransaction` stored on create
   - latest edit allows amount/date changes and recomputes balances
   - non-latest edit blocks amount/date/type, allows description/category/tags
4. Regression tests:
   - sharing/read-only behavior remains unchanged.

## Delivery plan

Implement in this order:

1. Accounts lifecycle and account number support.
2. Transfers page and linked transfer persistence.
3. Transaction edit constraints and `BalanceAfterTransaction`.

Definition of done:

- all three phases implemented across endpoint + app/service + UI paths
- all new integration tests pass
- existing sharing/read-only tests remain green

