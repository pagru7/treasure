# Categories and budget linking design

## Goal

Make budgets actually drive spending control by introducing managed household categories, linking transactions and budgets to categories by ID, and updating dashboard budget health to use those links.

## Problem

Current behavior is name-based and disconnected:
- Budgets use free-text category names.
- Transactions use free-text category strings.
- Dashboard budget health matches by string equality.

This breaks traceability and makes category management impossible (rename/disable/delete cannot be done safely).

## Scope

In scope:
- New Categories management page (CRUD + disable/enable + guarded delete)
- Strong FK links: `Transaction -> Category`, `BudgetCategory -> Category`
- Budgets created/updated per selected category (unique per household+category)
- Dashboard budget health aggregation by category ID
- Migration to backfill category references from existing data
- Navigation updates
- Integration tests for core rules

Out of scope:
- Multi-period budgets (weekly/yearly)
- Budget rollovers
- Category hierarchies
- Tag system refactor

## Design decisions

1. **Category link model:** foreign keys by ID (`fk-id`) for both transactions and budgets.
2. **Budget creation mode:** manual from Budgets page (categories become selectable, not auto-budgeted).
3. **Disabled category behavior:** read-only history; existing references remain visible, but no new transaction/budget assignment.
4. **Budget uniqueness:** one budget per household and category.

## Data model changes

### New entity: `Category`

- `Id: Guid`
- `HouseholdId: Guid`
- `Name: string`
- `IsActive: bool` (default true)
- `CreatedAt: DateTime`
- `UpdatedAt: DateTime`

Indexes/constraints:
- Unique index on `(HouseholdId, Name)` (case-insensitive normalization in app logic)

### `Transaction` changes

- Add `CategoryId: Guid` (required)
- Keep existing `Category: string` as denormalized display snapshot for compatibility/history safety.
- Application logic treats `CategoryId` as source of truth.

### `BudgetCategory` changes

- Add `CategoryId: Guid` (required)
- Keep existing `Name: string` for compatibility/display, synchronized from category name.
- Unique index `(HouseholdId, CategoryId)`.

## Migration strategy

Single migration:
1. Create `Categories` table.
2. For each household, gather distinct names from:
   - existing `Transactions.Category`
   - existing `BudgetCategories.Name`
3. Insert categories for those names (active = true).
4. Backfill:
   - `Transactions.CategoryId` by household + normalized name mapping
   - `BudgetCategories.CategoryId` by household + normalized name mapping
5. Add non-null constraints and unique indexes.

Fallback rule for missing/blank transaction categories during migration:
- map to/create `"General"` category in that household.

## UI and behavior changes

### New page: `/categories`

Pattern: same style and interaction approach as `/tags`.

Features:
- Add category
- Rename category name
- Disable/enable category
- Delete category only when **no** referencing transactions and **no** referencing budgets exist

Feedback:
- Snackbar success/warning/error messages aligned with existing page behavior.

### Transactions page

- Replace create/edit category text fields with category selector from active categories in current household.
- Keep rendering category name in list.
- Existing transactions linked to disabled categories still show category name.
- New/edit operations cannot assign disabled categories.

### Budgets page

- Replace free-text category name with category selector from active categories.
- Create/update semantics:
  - if budget for selected category exists -> update monthly limit/currency/notes
  - otherwise create new budget row
- Display category active status in table; disabled category budgets remain visible for history but not selectable for new assignments.

### Dashboard budget health (`/`)

- Compute month spend by category ID:
  - monthly expenses where `Type == "expense"`
  - grouped by `CategoryId`
  - compared against budgets by `BudgetCategory.CategoryId`
- Remove string-equality joins between transaction category and budget name.

## Endpoint and service updates

Touchpoints:
- Transaction create/update flows validate category existence and `IsActive`.
- Budget create/update flow validates category existence and `IsActive`.
- Query paths include category data where needed for UI rendering.

Validation rules:
- unknown category -> reject request
- inactive category on new/edit assignment -> reject request
- category delete with references -> reject operation

## Error handling

- Follow existing project patterns: explicit validation errors or snackbar messages; no silent fallbacks.
- Reject invalid states early with clear messages.

## Testing strategy

Integration coverage:
1. Category CRUD:
   - create, rename, disable/enable
   - delete blocked when transactions exist
   - delete blocked when budgets exist
   - delete succeeds only when unreferenced
2. Transactions:
   - create/edit succeeds with active category
   - create/edit fails with inactive or unknown category
3. Budgets:
   - create budget for category
   - second save updates same row (uniqueness)
   - reject inactive/unknown category
4. Dashboard:
   - budget health uses category ID links, not string-name matching

## Risks and mitigations

Risk: migration complexity and category name normalization collisions.
Mitigation: normalize names consistently (`Trim` + case-insensitive comparison), enforce household-level uniqueness, and route blanks to `"General"`.

Risk: stale denormalized `Name`/`Category` snapshots.
Mitigation: treat FK as canonical for calculations; synchronize display fields in update flows where practical.

Risk: behavior drift in existing pages.
Mitigation: keep current UX patterns (MudBlazor layout, snackbar validation) and add targeted integration tests.
