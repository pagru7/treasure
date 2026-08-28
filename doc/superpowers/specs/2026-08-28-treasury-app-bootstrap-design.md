# Treasury App Bootstrap Design

## 1. Purpose

This document defines the initial implementation direction for the Treasury household finance app. It standardizes the project structure and the first build steps so the team can ship a working baseline without drifting between conflicting architecture conventions.

The key decision is to use a single ASP.NET Core application project, `src/Treasury.App`, as the canonical home for the Blazor UI, FastEndpoints API, authorization, and persistence code. This matches the later design documents and removes the mismatch between the older `Host`/`Domain` naming and the final app structure.

## 2. Scope

### In scope
- Bootstrap the .NET solution and app project.
- Add a working health endpoint and Docker/PostgreSQL local runtime.
- Add ASP.NET Core auth and authorization policies.
- Seed the default household for single-household phase 1 use.
- Add the MudBlazor app shell and auth entry flow.
- Create the dashboard summary API and UI cards.
- Preserve manual FX and valuation logic as the core money model.

### Out of scope
- External market feeds.
- Multi-household onboarding.
- Mobile app integration.
- Full role expansion beyond owner + shared read-only.
- Advanced reporting beyond phase 1 dashboard summary.

## 3. Recommended architecture

### 3.1 Project structure

Use a single project rooted at:

- `src/Treasury.App/`
  - `Domain/` for entities and domain rules
  - `Application/` for use cases and services
  - `Infrastructure/` for EF Core and persistence concerns
  - `Components/` for Blazor UI components
  - `Pages/` for page routes and auth/dashboard pages
  - `Program.cs` for app bootstrapping

This keeps the app easy to navigate while still respecting a layered architecture inside one project.

### 3.2 Runtime stack

- ASP.NET Core
- Blazor Server (for the app shell and dashboard UI)
- MudBlazor for layout, cards, tables, forms, and responsive nav
- FastEndpoints for API endpoints
- PostgreSQL in Docker Compose
- EF Core + Npgsql
- ASP.NET Core Identity for the auth model
- xUnit + FluentAssertions for integration tests

### 3.3 Authentication model

- Require authentication for the app shell and protected endpoints.
- Use a household-scoped user model.
- Keep authorization policy names explicit and stable:
  - `Policies.OwnerOnly`
  - `Policies.SharedReadOnly`
- Seed a single default household record to simplify phase 1 behavior.

## 4. Initial implementation phases

### Phase 1: bootstrap and baseline runtime

1. Create the solution and `src/Treasury.App` project.
2. Add Docker Compose with PostgreSQL.
3. Add a health endpoint for startup smoke tests.
4. Ensure the app starts locally and responds on `/health`.

### Phase 2: auth + household setup

1. Add ASP.NET Core Identity and cookie auth.
2. Add authorization policies.
3. Add a default household seed.
4. Add anonymous redirect behavior for `/` to `/auth/login`.

### Phase 3: MudBlazor shell + dashboard

1. Add `MudLayout`, app bar, drawer nav, and responsive breakpoints.
2. Add login/register pages with email/password flow.
3. Add dashboard summary cards for native currency totals and PLN total.
4. Add missing-rate warnings and empty-state prompts.

### Phase 4: finance domain features

1. Accounts and account types.
2. Transactions and transfers.
3. Manual valuation and rate tracking.
4. Shared read-only visibility rules.

## 5. Design decisions

### 5.1 Why keep a single project?

This reduces confusion between the original plan and the finalized design. The app is still modular internally, but it does not create a brittle multi-project split from the start.

### 5.2 Why use MudBlazor early?

The app must be user-friendly and mobile-ready from the start. MudBlazor gives a consistent shell and component system that aligns with the design docs.

### 5.3 Why start with health-check tests?

This creates a stable and verifiable baseline before auth, dashboard logic, and database integration are added.

## 6. Quality bar

The bootstrap is complete when:
- `dotnet test` passes for the smoke startup check.
- the app boots locally with PostgreSQL available.
- the `/health` endpoint returns HTTP 200.
- auth policies exist and are wired into the application pipeline.
- the default household is seeded.
- the app shell can route to login/register and dashboard pages.

## 7. Risks and mitigations

### Risk: conflicting project structure
Mitigation: standardize on `src/Treasury.App` across implementation and docs.

### Risk: too much scope early
Mitigation: focus first on runtime, auth, and shell; do not build advanced valuation logic before the base app is stable.

### Risk: auth/db complexity slowing progress
Mitigation: use the minimal identity + cookie setup and a single default household seed.

## 8. Success criteria

The first implementation milestone is successful when the app can run locally in Docker, returns health status, and exposes a secure authenticated shell with login and dashboard entry points. At that point, the domain features can be layered in without reworking the overall architecture.
