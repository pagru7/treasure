# Accounts Description + Feedback Feature Design

## Scope

This design introduces two user-facing capabilities:

1. Account-level notes (`Description`) with create/edit support.
2. A new household-scoped Feedback feature with title, description, status workflow, and editing.

The design follows existing app patterns (MudBlazor pages + FastEndpoints + EF Core + household filtering).

## Goals

1. Allow users to store optional account notes up to 1000 characters.
2. Provide a simple feedback tracker where users can:
   1. Create feedback with title + description.
   2. Track status as `New`, `Implementing`, `Done`.
   3. Update status and description from the feedback list.
3. Keep strict household isolation and consistent validation across API and UI.

## Non-Goals

1. No comments/discussion thread per feedback item.
2. No assignee, due date, priority, voting, or attachments.
3. No soft-delete/archive in this iteration.

## Data Model

## Account extension

`Account` gets:

- `Description` (`string?`, optional, max 1000 chars validated in write flows)

## Feedback entity

New `FeedbackItem` entity:

- `Id : Guid`
- `HouseholdId : Guid`
- `Title : string`
- `Description : string`
- `Status : FeedbackStatus` enum
- `CreatedAt : DateTime`
- `UpdatedAt : DateTime`

New enum `FeedbackStatus`:

- `New = 0`
- `Implementing = 1`
- `Done = 2`

## API Design

All feedback endpoints require authenticated user and are household-filtered.

## Accounts

Update existing account write APIs to accept/persist `Description`:

1. `POST /api/accounts` (create account) accepts optional description.
2. `PUT /api/accounts/{id}` (update details) accepts optional description.

Validation:

- If provided, description length must be `<= 1000`.

## Feedback

1. `POST /api/feedback`
   - Request: `title`, `description`
   - Behavior: creates row with status forced to `New`.
2. `GET /api/feedback`
   - Returns current household items sorted by `CreatedAt DESC`.
3. `PUT /api/feedback/{id}`
   - Request: `status`, `description`
   - Behavior: updates allowed fields only for item in same household.

Validation:

- `title` required on create.
- `description` required and max 1000 chars on create/update.
- `status` must be one of enum values.

## UI Design

## Accounts page

1. Create form:
   - Add multiline `Description` field (optional, max 1000).
2. Account card:
   - Display description section only when non-empty.
3. Edit details section:
   - Add editable multiline description input.

## Feedback page (`/feedback`)

Single page with:

1. Create form:
   - Title input
   - Description multiline input
   - Save button
2. Feedback list/table:
   - Created date
   - Status dropdown (`New`, `Implementing`, `Done`)
   - Title
   - Description editable field
   - Save/update action

Navigation:

- Add `Feedback` link in top nav and drawer for authorized users.

## Security and Authorization

1. Feedback permissions: all authenticated household users can create and update feedback entries within their own household.
2. All reads/writes are filtered by `HouseholdId` from current user.
3. Cross-household access attempts return not-found/forbidden consistent with existing endpoint style.

## Persistence and Migration

Add EF migration that:

1. Adds nullable `Description` column to `Accounts`.
2. Creates `FeedbackItems` table with:
   - PK on `Id`
   - index on `HouseholdId`
   - `Status` stored as integer enum
3. Updates snapshot accordingly.

## Error Handling

1. API validation returns field-level errors (existing FastEndpoints pattern).
2. UI uses snackbar + inline constraints consistent with current pages.
3. Description length errors use explicit text indicating 1000-char limit.

## Testing Strategy

Integration tests:

1. Account create persists description.
2. Account update modifies description.
3. Feedback create sets default status `New`.
4. Feedback update changes status and description.
5. Feedback household isolation blocks cross-household access.

Optional UI-level checks can remain in existing page/integration style; API integration tests are minimum acceptance gate.

## Rollout Notes

1. Backward-compatible for accounts (new nullable column).
2. Feedback starts empty for all households.
3. No data migration needed beyond schema creation.
