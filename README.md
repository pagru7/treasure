# Treasury - Household Wealth Manager (Phase 1)

Local Blazor + ASP.NET Core + PostgreSQL app for household finance tracking with manual workflows.

## Prerequisites

- Docker Desktop (Compose v2)
- .NET SDK 9.0 (for local build/test)

## Local run with Docker

```powershell
docker compose up -d --build
```

App URLs:
- App: `http://localhost:8080`
- Health: `http://localhost:8080/health`

Helpful restart script:

```powershell
.\scripts\Restart-Docker.ps1
```

## Default credentials bootstrap

There are no hardcoded default credentials.  
Create the first user from `http://localhost:8080/auth/register`.

The app seeds a single default household and sample data (accounts, budgets, bills, tags, rates) for that household.

## How to add manual FX rates

1. Open **Rates** page.
2. Fill **From**, **To**, and **Rate**.
3. Click **Save rate**.

API equivalents:
- `POST /api/rates`
- `GET /api/rates/latest?from=PLN&to=EUR`

## How to update bullion and coin valuations

1. Open **Valuations** page.
2. For bullion: fill asset name, weight, purity, unit price, then save.
3. For coins: fill asset name, quantity, unit value, then save.

API equivalents:
- `POST /api/valuations/bullion`
- `POST /api/valuations/coin`

## Notes

- In Docker production mode, PostgreSQL is used and migrations are applied automatically on startup.
- DataProtection keys are persisted in a Docker volume to keep auth/antiforgery tokens stable across restarts.

## Behavior and UI notes

- Inactive accounts are hidden by default and blocked from creating new transfers or transactions.
- Transfers page (UI): `/transfers` — use this page to create and view transfer records between accounts.
- Transaction editing rule: only the latest transaction on an account may change amount, date, or type; older transactions may only have description, category, and tags edited.

Operator guidance above reflects Phase 2 lifecycle and is enforced by acceptance/domain rules and integration tests.
