# Auth minimal API to FastEndpoints design

## Goal

Move all auth minimal API declarations from `src/Treasury.App/Program.cs` to dedicated FastEndpoints classes, following the existing endpoint structure, while keeping behavior unchanged.

## Scope

In scope:
- `POST /auth/login-submit`
- `POST /auth/register-submit`
- `POST /auth/logout-submit`
- Shared auth payload parsing currently implemented as local functions in `Program.cs`

Out of scope:
- Any route/HTTP method changes
- Any UI changes
- Any DB schema changes
- Any auth behavior changes

## Current state

`Program.cs` currently contains:
- Three auth minimal API route mappings
- Local payload parsing helpers (`ReadAuthPayloadAsync`, `GetStringProperty`)
- `AuthRequestPayload` record used by handlers

Most other API surfaces already use dedicated FastEndpoints classes under `src/Treasury.App/Endpoints/*`.

## Proposed design

### 1) New endpoint classes

Create `src/Treasury.App/Endpoints/Auth/` with:
- `LoginSubmitEndpoint : EndpointWithoutRequest`
- `RegisterSubmitEndpoint : EndpointWithoutRequest`
- `LogoutSubmitEndpoint : EndpointWithoutRequest`

Each endpoint will:
- Configure the same route and HTTP method as today.
- Preserve current redirect/error behavior exactly.
- Use DI for `SignInManager<ApplicationUser>`, `UserManager<ApplicationUser>`, and `TreasuryDbContext` as needed.

### 2) Shared request parsing helper

Extract payload parsing from `Program.cs` into a shared helper in auth endpoints area, e.g.:
- `AuthRequestReader.ReadAsync(HttpRequest request)` returning `AuthRequestPayload`
- Internal helper for case-insensitive JSON property extraction

Parsing behavior remains unchanged:
- Supports form posts and JSON body
- Returns empty payload when body/content type is missing
- Reads `Email`, `Password`, `ConfirmPassword`, `HouseholdNameOrId`

### 3) Registration logic parity

`RegisterSubmitEndpoint` keeps the exact current semantics:
- Requires all fields
- Requires matching passwords and minimum length
- `HouseholdNameOrId` GUID means assign to existing household (error if missing)
- Non-GUID means create new household with provided name
- Creates identity user, signs in, redirects

### 4) Program.cs cleanup

Remove:
- `app.MapPost("/auth/*-submit", ...)` mappings
- local parsing functions and payload record

Keep:
- service registrations, middleware setup, endpoints discovery, and `public partial class Program`

## Error handling

Continue using redirect-based error responses for auth submit endpoints to match existing UI flow:
- `/auth/login?error=...`
- `/auth/register?error=...`

No conversion to FastEndpoints validation response format for these endpoints.

## Testing strategy

Run targeted integration tests covering auth submit flows (`AuthTests`) to confirm no behavior change and preserve recently added household join/create semantics.

## Risks and mitigations

Risk: subtle behavior drift in payload parsing when moved from local functions.
Mitigation: keep same parsing logic and verify through existing JSON registration tests.

Risk: auth endpoint discovery/config mismatch.
Mitigation: place classes under `Endpoints/Auth` and use standard FastEndpoints `Configure()` patterns already used in project.
