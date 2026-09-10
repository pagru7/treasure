# Account sharing UI design

## Goal

Make account sharing visible and actionable from the Accounts page itself. For each account the owner should be able to:

1. See which household users already have read-only access.
2. Share the account with another user from the same household.
3. Avoid typing emails manually when a household user already exists.

## Scope

This change is UI-focused and keeps the existing sharing API and permission rules intact.

- In scope:
  - Add a share section to each owned account card.
  - Show current read-only viewers for that account.
  - Show a household-user picker for sharing.
  - Call the existing `POST /api/accounts/{id}/share-readonly` endpoint.
- Out of scope:
  - Changing the backend sharing model.
  - Allowing users outside the household to be shared in.
  - Adding edit/remove-share controls.

## Proposed UI

Each owned account card on `/accounts` will gain a small share panel below the balance.

The panel will show:

- A line of current access, such as `Shared with: Alice, Bob` or `Shared with: nobody yet`.
- A dropdown of household users excluding the current user.
- A `Share read-only` button.

The picker should be disabled or hidden for accounts the current user does not own.

## Data flow

The Accounts page will load three datasets:

1. Visible accounts for the current user.
2. Household users for the current household.
3. Visibility rules for each account so the page can display who already has access.

When the owner shares an account:

1. The page sends the selected user email to the existing share endpoint.
2. The endpoint validates ownership and household membership.
3. On success, the page reloads the accounts and share state.
4. On failure, the page shows a snackbar error with the returned message.

## Behavior rules

- Only the owner can share an account.
- The share target must come from the current household user list.
- The current user must not appear in their own share picker.
- Re-sharing the same user should be safe and keep the account shared read-only.
- The shared user list should reflect the latest saved visibility rules after each share.

## Error handling

- If no household users exist besides the owner, the picker should show an informative empty state.
- If the endpoint rejects a share request, the UI should show a clear snackbar message instead of silently failing.
- If the account list or household user query fails, the page should keep rendering the rest of the page and surface the failure where possible.

## Testing

Add or update integration coverage for the share flow so the UI/API contract stays stable:

- owner can share an account with another user from the same household
- shared user appears in the account’s access list
- the shared user can view the account through the shared portfolio path
- a non-owner still cannot perform owner-only actions

