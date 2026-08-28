# Household Wealth Manager — Phase 1 Design

## 1. Purpose

This project is a local, home-hosted personal finance application for tracking all assets and liabilities in one place. The first version is intended for a single household with up to two users, but it must be designed so a spouse can later be granted read-only access to selected assets and accounts.

The system must support:

- cash held in wallet and safe
- bank accounts
- investment accounts, funds, and securities
- precious metals and coins
- transfers between accounts
- manual value updates for market-sensitive holdings
- multi-currency totals
- local hosting in Docker on a dedicated machine
- a Blazor-based web UI

## 2. Goals

### Primary goals

- Record all major personal assets and liabilities in one system.
- Let the user define custom account types instead of restricting the app to a fixed list.
- Track transaction history and current balances accurately.
- Allow a second user to see selected holdings and balances with read-only access.
- Support both PLN and EUR at minimum, with room for more currencies.

### Non-goals for phase 1

- Automatic external price feeds from third-party services.
- Full mobile app integration.
- Advanced analytics or forecasting.
- Multi-household or SaaS mode.

## 3. Users and household model

The app operates in a single household context.

### Roles

- Owner: primary user and administrator.
- Shared user: spouse or future household member with limited access.

### Access model

- A user can create and manage their own accounts and transactions.
- A user can share selected accounts or holdings with another user.
- Shared access is read-only unless explicitly extended later.
- Each account has an owner and an optional visibility policy.

This keeps the system simple in phase 1 while still supporting the “shared view, private control” requirement.

## 4. Account model

The app should not hardcode a small set of fixed account types. Instead, it should support a flexible account catalog with an account type definition.

### Core account fields

- id
- household id
- owner user id
- account type id
- name
- currency
- current balance/value
- is active
- created at
- updated at

### Example account types

- cash wallet
- safe cash
- bank account
- savings account
- investment account
- stock/fund position
- precious metals holding
- coin collection
- loan or debt account
- other custom account type

### Account behavior

- Bank and cash accounts are driven primarily by transaction entries.
- Investment and commodity holdings are driven by valuation snapshots and manual updates.
- A custom account type can define whether it is balance-driven or value-driven.

## 5. Transaction and transfer model

The system stores a ledger of financial movements.

### Transaction fields

- id
- account id
- type (income, expense, transfer, balance correction, valuation adjustment)
- amount
- currency
- date
- description
- category or tag
- source/destination account (for transfers)
- created by user
- created at

### Required behaviors

- The user can add expenses and income.
- The user can create transfers between accounts.
- The user can record manual balance corrections when the real-world movement is known but the exact origin is not.
- Large valuation changes, such as market-driven revaluation, should be recorded as explicit valuation adjustments rather than hidden in the account balance.

## 6. Valuation rules

The valuation model must support both cash-like accounts and asset-like holdings.

### 6.1 Cash and bank accounts

- These values are derived from transaction history and current balance entries.
- For example: a bank account is updated through deposits, withdrawals, transfers, and manual corrections.

### 6.2 Stocks, funds, and securities

- These should be represented as holdings with a quantity and a manually entered market value or last-known value.
- The app should support a value update action for each holding or grouped holding set.
- This is the first-phase approach because external APIs are out of scope.

### 6.3 Precious metals

The app must support two metal valuation patterns:

1. Commodity-style bullion / bars / ingots

- If the user defines weight, purity, and unit price, the app can calculate current value automatically using a formula.
- Example: total value = weight × purity × spot price per unit.
- If the user has no live market feed, the system uses the last manually entered spot price.
- This is a useful calculation-based workflow for bars or metal with known content.

2. Coins / collectible metal items

- These should be stored as manually valued items.
- The user enters the price paid at acquisition and can later update the current price per coin or per item manually.
- The system recalculates the current value from that user-defined value.
- A collector can maintain a list of specific coin entries and update each one individually as needed.

This approach satisfies the requirement that metal and coin values can be updated manually while still allowing formula-based calculation for bullion.

## 7. Multi-currency support

The system must support multiple currencies in phase 1, with PLN and EUR required at minimum.

### Currency model

- Each account has a currency.
- Each transaction is stored in its own currency.
- The app stores manual exchange-rate entries with:
  - source currency
  - target currency
  - rate
  - effective date
  - updated by user

### Reporting behavior

- The system can show account totals in:
  - native account currency
  - household home currency (default: PLN)
  - a selected reporting currency
- Conversion is manual and user-controlled in phase 1.

### Design choice

- USD/EUR/PLN are supported, but no external API integration is required in phase 1.
- The system should not assume that all accounts have the same currency.

## 8. Data model summary

### Core entities

- Household
- User
- AccountType
- Account
- Transaction
- Transfer
- CurrencyRate
- AssetValuation
- VisibilityRule

### Relationships

- Household has many users.
- User belongs to one household.
- Account belongs to one household and one owner user.
- Account has many transactions.
- Account can have many valuation records.
- User can be granted read-only visibility to one or more accounts or holdings.

## 9. Local deployment and technology fit

### Hosting

- The app is hosted locally in the home network.
- Preferred deployment model: Docker container on a dedicated machine.
- PostgreSQL runs locally in Docker; the web app runs in a separate container or alongside it.

### Frontend

- Blazor is the chosen framework for the UI, with MudBlazor as a UI Design and components visual style provider
- The web app will be local, secure, and accessible inside the home network.
- Authentication must be built in so the app is not public by default.

### Backend

- ASP.NET Core API serves the app and persist data in PostgreSQL.
- Use vertical slice architecture with FastEndpoints so each endpoint is implemented in its own class.
- Layer the application in folders inside the main app project (domain, application, infrastructure concerns kept as folder boundaries, not separate projects).
- Business logic is organized around accounts, transactions, valuations, and user visibility.

### Project structure direction

- Phase 1 uses one main application project.
- A second project for external services/integrations is planned for phase 2.

## 10. Security and authorization

The app must be protected by login-based authentication.

### Requirements

- Only authenticated users can access the app.
- Each user has a role or permission level.
- Shared users can view only the records permitted by the visibility rules.
- Editing remains restricted to the account owner or an admin-level user.

### Phase 1 simplification

- A single private household is assumed.
- The security boundary is not broad enterprise multi-tenancy; it is a simple, local household access model.

## 11. User experience flow

### Account creation

- User selects the account type.
- User chooses the currency.
- User defines whether the account is private or shared.
- User assigns owner and visibility.

### Transaction entry

- User selects an account.
- User enters amount, currency, and transaction type.
- User chooses whether the action is income, expense, transfer, or balance correction.
- User confirms and saves.

### Value update flow

- For metals/bullion: user enters the updated spot price or valuation source.
- For coins: user updates custom value manually for each coin entry.
- For stocks/funds: user updates the current price or holding value manually.

### Shared household view

- The spouse logs in.
- The system displays only shared or allowed assets.
- The spouse can read totals and asset values but cannot edit the owner’s private or restricted accounts.

## 12. Risks and trade-offs

### Risk: manual rates are error-prone

Mitigation: require rate date, source, and manual confirmation when using FX or metal prices.

### Risk: over-flexible account model

Mitigation: keep account types simple and make value behavior explicit.

### Risk: sharing logic becomes too complex

Mitigation: phase 1 supports only one household and read-only sharing of selected holdings.

## 13. Phase 1 acceptance criteria

The design is successful when the app can:

- create custom account types
- add transactions and transfer records
- update balance values manually
- store multiple currencies
- manually update exchange rates
- track bullion with formula-based valuing
- track coins with manual per-item value updates
- show household totals and read-only shared values for another user
- run locally in Docker
- use a Blazor UI with ASP.NET Core backend and PostgreSQL

## 14. Recommendation

This design intentionally keeps phase 1 practical and flexible: manual valuation, multi-currency support, and household sharing are built in without forcing external API integration or premium enterprise complexity. It delivers the core money-tracking workflow you need while leaving room for future enhancements like live market feeds, mobile sync, and richer reporting.
