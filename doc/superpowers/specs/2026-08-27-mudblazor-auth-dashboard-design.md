# Treasury — MudBlazor Auth & Responsive Dashboard Design

## 1. Purpose

Define the UI/UX redesign for the Treasury app using MudBlazor so the app:
- starts with login/register when opened anonymously,
- presents a modern responsive app shell,
- shows top portfolio summaries (native currency totals + PLN converted total),
- works well on desktop and mobile.

This design applies to phase 1 and keeps the existing vertical-slice architecture.

## 2. Scope

### In scope
- Replace basic HTML navigation/pages with MudBlazor app shell and components.
- Add register/login/logout flow using email + password.
- Enforce auth redirect from `/` to login/register for anonymous users.
- Add dashboard summary endpoint and dashboard cards.
- Ensure responsive behavior with app bar + hamburger drawer.

### Out of scope
- External market feeds.
- Multi-household onboarding.
- Theme customization system beyond initial MudBlazor setup.
- Role expansion beyond existing owner/shared read-only behavior.

## 3. UX Requirements

## 3.1 Anonymous entry flow
- Anonymous user visiting `/` is redirected to `/auth/login`.
- Login page has centered card layout and link to register page.
- Register page has centered card layout with:
  - email,
  - password,
  - confirm password,
  - validation feedback.
- Successful registration signs user in and redirects to `/`.
- Authenticated user requesting `/auth/login` or `/auth/register` is redirected to `/`.

## 3.2 Authenticated app shell
- Use `MudLayout` with:
  - `MudAppBar` header with app title and primary navigation,
  - desktop top links,
  - mobile hamburger icon opening `MudDrawer`.
- Primary navigation entries:
  - Dashboard,
  - Accounts,
  - Transactions,
  - Valuations,
  - Rates,
  - Shared Portfolio.
- Include logout action in header/drawer.

## 3.3 Dashboard
- Top summary area contains:
  - one card per native currency total (PLN, EUR, etc.),
  - one card for grand total in PLN.
- PLN grand total is calculated from latest manual rates.
- If a conversion rate is missing, summary shows explicit “missing rate” state for affected value.
- Secondary area includes:
  - recent transactions list,
  - quick action buttons for main workflows.

## 3.4 Responsiveness
- Breakpoint behavior:
  - `md` and above: permanent horizontal menu in app bar.
  - below `md`: hamburger + temporary drawer.
- Dashboard cards collapse to single-column on small screens.
- Forms use constrained width panels and readable spacing for mobile.

## 4. Technical Design

## 4.1 Frontend component structure (within `src/Treasury.App`)
- `Components/Layout/AppShell.razor`
  - Mud layout host (app bar, drawer, nav links, logout).
- `Pages/Auth/Login.razor`
- `Pages/Auth/Register.razor`
- `Pages/Dashboard.razor`
- Existing feature pages migrated to MudBlazor controls:
  - `Pages/Accounts.razor`
  - `Pages/Transactions.razor`
  - `Pages/Valuations.razor`
  - `Pages/Rates.razor`
  - `Pages/SharedPortfolio.razor`
- Shared styling/theme:
  - `Theme/TreasuryTheme.cs` (initial palette/typography).

## 4.2 Backend/API additions
- Auth endpoints (FastEndpoints):
  - `POST /api/auth/register`
  - `POST /api/auth/login`
  - `POST /api/auth/logout`
- Dashboard endpoint:
  - `GET /api/dashboard/summary`
  - response includes:
    - totals grouped by currency,
    - converted PLN total,
    - missing-rate indicators.

## 4.3 Summary calculation rules
- Native totals:
  - sum visible account current values by account currency.
- PLN conversion:
  - for PLN accounts: direct value.
  - for non-PLN accounts: use latest available rate `currency -> PLN`.
- Missing rate handling:
  - exclude missing-conversion accounts from converted PLN aggregate,
  - return metadata listing currencies/accounts requiring manual rates.

## 4.4 Security and authorization alignment
- Existing policies remain unchanged:
  - owner endpoints require `OwnerOnly`,
  - shared read endpoints require `SharedReadOnly`.
- Dashboard summary respects visibility:
  - owner sees owned + shared-visible accounts,
  - shared user sees shared-visible accounts only.

## 5. Error Handling and UX States

- Login/register invalid input:
  - inline field validation + form-level error alert.
- Unauthorized API responses:
  - UI redirects to login when session expires.
- Missing rates:
  - show non-blocking warning in dashboard summary section.
- Empty states:
  - first-run dashboard shows action prompts (create account, add transaction, add rate).

## 6. Testing Strategy

## 6.1 Integration tests
- Anonymous `/` request redirects to login route.
- Register creates user and allows authenticated access to dashboard.
- Dashboard summary endpoint returns grouped totals and PLN conversion.
- Missing-rate scenario returns warning metadata.
- Shared user cannot access owner-edit endpoints.

## 6.2 UI/component behavior checks
- Layout renders app bar and drawer toggle.
- Drawer navigation works in mobile breakpoint simulation.
- Summary cards render correctly for:
  - multi-currency,
  - PLN-only,
  - missing-rate cases.

## 7. Rollout Notes

- Introduce MudBlazor package and services first.
- Migrate shell + auth pages before feature pages.
- Keep existing APIs stable except explicit auth/dashboard additions.
- Preserve current data model and valuation logic.

## 8. Acceptance Criteria

Design is considered implemented when:
1. Opening `http://localhost:8080/` as anonymous user shows login/register flow (redirect to login).
2. User can register with email+password and is signed in.
3. Authenticated home shows dashboard summary cards:
   - grouped native currency totals,
   - PLN converted total.
4. Header app bar + responsive drawer navigation are operational.
5. Accounts/transactions/valuations/rates/shared pages use MudBlazor and are mobile-usable.
6. Shared-user read-only restrictions continue to hold.
