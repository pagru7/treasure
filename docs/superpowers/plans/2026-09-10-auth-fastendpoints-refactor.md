# Auth Submit Endpoints FastEndpoints Refactor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move all `/auth/*-submit` minimal API handlers out of `Program.cs` into dedicated FastEndpoints classes without changing runtime behavior.

**Architecture:** Introduce a focused `Endpoints/Auth` unit with one endpoint class per submit route and a shared request-reader helper for form/JSON parsing. Keep redirect-style auth UX exactly as today. Reduce `Program.cs` to host/bootstrap/middleware wiring plus `partial Program` test hook.

**Tech Stack:** .NET 9, ASP.NET Core, FastEndpoints, ASP.NET Core Identity, Entity Framework Core, xUnit, FluentAssertions

## Global Constraints

- Keep route paths and methods unchanged: `POST /auth/login-submit`, `POST /auth/register-submit`, `POST /auth/logout-submit`.
- Keep redirect/error semantics unchanged (query-string `error` on login/register pages).
- Keep form + JSON payload compatibility for auth submit endpoints.
- Keep current registration household behavior unchanged (`HouseholdNameOrId` GUID join-or-reject, non-GUID create by name, empty reject).
- No DB schema or migration changes.
- No UI changes.

---

### Task 1: Add auth endpoint contract and parser unit

**Files:**
- Create: `src/Treasury.App/Endpoints/Auth/AuthRequestPayload.cs`
- Create: `src/Treasury.App/Endpoints/Auth/AuthRequestReader.cs`
- Test: `tests/Treasury.IntegrationTests/AuthTests.cs`

**Interfaces:**
- Consumes: `HttpRequest`
- Produces: `public sealed record AuthRequestPayload(string? Email, string? Password, string? ConfirmPassword, string? HouseholdNameOrId);`
- Produces: `public static class AuthRequestReader { public static Task<AuthRequestPayload> ReadAsync(HttpRequest request); }`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Register_Submit_Accepts_Lowercase_Json_Property_Names()
{
    await using var app = new TreasuryHostFactory();
    var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    var payload = new
    {
        email = "json-lowercase@example.com",
        password = "Password123!",
        confirmPassword = "Password123!",
        householdNameOrId = "Lowercase Household"
    };

    var response = await client.PostAsJsonAsync("/auth/register-submit", payload);

    response.StatusCode.Should().Be(HttpStatusCode.Redirect);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Register_Submit_Accepts_Lowercase_Json_Property_Names"`
Expected: FAIL because extracted parser behavior regresses if refactor is incomplete or incorrect.

- [ ] **Step 3: Write minimal implementation**

```csharp
// AuthRequestPayload.cs
namespace Treasury.App.Endpoints.Auth;
public sealed record AuthRequestPayload(string? Email, string? Password, string? ConfirmPassword, string? HouseholdNameOrId);

// AuthRequestReader.cs
using System.Text.Json;

namespace Treasury.App.Endpoints.Auth;

public static class AuthRequestReader
{
    public static async Task<AuthRequestPayload> ReadAsync(HttpRequest request)
    {
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            return new AuthRequestPayload(form["Email"], form["Password"], form["ConfirmPassword"], form["HouseholdNameOrId"]);
        }

        if (request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
        {
            var raw = await new StreamReader(request.Body, leaveOpen: true).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(raw))
                return new AuthRequestPayload(string.Empty, string.Empty, string.Empty, string.Empty);

            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            return new AuthRequestPayload(
                GetStringProperty(root, "Email"),
                GetStringProperty(root, "Password"),
                GetStringProperty(root, "ConfirmPassword"),
                GetStringProperty(root, "HouseholdNameOrId"));
        }

        return new AuthRequestPayload(string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private static string? GetStringProperty(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty(propertyName, out var property))
        {
            foreach (var candidate in root.EnumerateObject())
            {
                if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    property = candidate.Value;
                    break;
                }
            }
            if (property.ValueKind == JsonValueKind.Undefined) return null;
        }
        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Register_Submit_Accepts_Lowercase_Json_Property_Names"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add tests/Treasury.IntegrationTests/AuthTests.cs src/Treasury.App/Endpoints/Auth/AuthRequestPayload.cs src/Treasury.App/Endpoints/Auth/AuthRequestReader.cs
git commit -m "test(auth): lock parser behavior for auth submit payloads"
```

### Task 2: Move login and logout submit handlers to FastEndpoints

**Files:**
- Create: `src/Treasury.App/Endpoints/Auth/LoginSubmitEndpoint.cs`
- Create: `src/Treasury.App/Endpoints/Auth/LogoutSubmitEndpoint.cs`
- Modify: `src/Treasury.App/Program.cs`
- Test: `tests/Treasury.IntegrationTests/AuthTests.cs`

**Interfaces:**
- Consumes: `SignInManager<ApplicationUser>`, `HttpContext`, `AuthRequestReader.ReadAsync()`
- Produces: Endpoint classes:
  - `public sealed class LoginSubmitEndpoint : EndpointWithoutRequest`
  - `public sealed class LogoutSubmitEndpoint : EndpointWithoutRequest`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Login_Submit_With_Invalid_Credentials_Redirects_To_Login_With_Error()
{
    await using var app = new TreasuryHostFactory();
    var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    var response = await client.PostAsJsonAsync("/auth/login-submit", new
    {
        Email = "missing@example.com",
        Password = "Password123!"
    });

    response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    response.Headers.Location!.ToString().Should().Contain("/auth/login?error=Invalid+email+or+password.");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Login_Submit_With_Invalid_Credentials_Redirects_To_Login_With_Error"`
Expected: FAIL during transition phase until endpoint is fully wired.

- [ ] **Step 3: Write minimal implementation**

```csharp
// LoginSubmitEndpoint.cs
public sealed class LoginSubmitEndpoint(SignInManager<ApplicationUser> signInManager) : EndpointWithoutRequest
{
    public override void Configure() => Post("/auth/login-submit");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var payload = await AuthRequestReader.ReadAsync(HttpContext.Request);
        if (string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Password))
        {
            await SendRedirectAsync("/auth/login?error=Please+enter+your+email+and+password.", false, ct);
            return;
        }

        var result = await signInManager.PasswordSignInAsync(payload.Email.Trim(), payload.Password, true, false);
        await SendRedirectAsync(result.Succeeded ? "/" : "/auth/login?error=Invalid+email+or+password.", false, ct);
    }
}

// LogoutSubmitEndpoint.cs
public sealed class LogoutSubmitEndpoint(SignInManager<ApplicationUser> signInManager) : EndpointWithoutRequest
{
    public override void Configure() => Post("/auth/logout-submit");
    public override async Task HandleAsync(CancellationToken ct)
    {
        await signInManager.SignOutAsync();
        await SendRedirectAsync("/auth/login", false, ct);
    }
}
```

Also remove corresponding `app.MapPost("/auth/login-submit"...` and `app.MapPost("/auth/logout-submit"...` blocks from `Program.cs`.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Login_Submit_With_Invalid_Credentials_Redirects_To_Login_With_Error"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App/Endpoints/Auth/LoginSubmitEndpoint.cs src/Treasury.App/Endpoints/Auth/LogoutSubmitEndpoint.cs src/Treasury.App/Program.cs tests/Treasury.IntegrationTests/AuthTests.cs
git commit -m "refactor(auth): move login/logout submit handlers to FastEndpoints"
```

### Task 3: Move register submit handler to FastEndpoints and finish Program cleanup

**Files:**
- Create: `src/Treasury.App/Endpoints/Auth/RegisterSubmitEndpoint.cs`
- Modify: `src/Treasury.App/Program.cs`
- Test: `tests/Treasury.IntegrationTests/AuthTests.cs`

**Interfaces:**
- Consumes: `UserManager<ApplicationUser>`, `SignInManager<ApplicationUser>`, `TreasuryDbContext`, `AuthRequestReader.ReadAsync()`
- Produces: `public sealed class RegisterSubmitEndpoint : EndpointWithoutRequest`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public async Task Register_Submit_With_Unknown_Household_Guid_Redirects_With_Specific_Error()
{
    await using var app = new TreasuryHostFactory();
    var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    var response = await client.PostAsJsonAsync("/auth/register-submit", new
    {
        Email = "unknown-guid-fastendpoint@example.com",
        Password = "Password123!",
        ConfirmPassword = "Password123!",
        HouseholdNameOrId = "99999999-9999-9999-9999-999999999999"
    });

    response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    response.Headers.Location!.ToString().Should().Contain("Provided+household+id+does+not+exist.");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Register_Submit_With_Unknown_Household_Guid_Redirects_With_Specific_Error"`
Expected: FAIL during migration until route logic is fully moved and wired.

- [ ] **Step 3: Write minimal implementation**

```csharp
public sealed class RegisterSubmitEndpoint(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TreasuryDbContext dbContext) : EndpointWithoutRequest
{
    public override void Configure() => Post("/auth/register-submit");

    public override async Task HandleAsync(CancellationToken ct)
    {
        var payload = await AuthRequestReader.ReadAsync(HttpContext.Request);
        // Keep same field validation, password rules, and household resolution logic.
        // Keep same redirect targets and error messages.
        // Keep user creation + sign-in flow.
    }
}
```

Then remove `app.MapPost("/auth/register-submit"...` block and remove obsolete local helpers/record from `Program.cs`, retaining `public partial class Program { }`.

- [ ] **Step 4: Run tests to verify it passes**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Treasury.IntegrationTests.AuthTests"`
Expected: PASS for all AuthTests.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App/Endpoints/Auth/RegisterSubmitEndpoint.cs src/Treasury.App/Program.cs tests/Treasury.IntegrationTests/AuthTests.cs
git commit -m "refactor(auth): move register submit handler to FastEndpoints"
```

### Task 4: Final targeted regression pass

**Files:**
- Modify: none (verification only)
- Test: `tests/Treasury.IntegrationTests/AuthTests.cs`

**Interfaces:**
- Consumes: existing auth endpoint surface
- Produces: confidence that endpoint refactor preserved behavior

- [ ] **Step 1: Run targeted auth test suite**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Treasury.IntegrationTests.AuthTests"`
Expected: PASS.

- [ ] **Step 2: Run household registration subset**

Run: `dotnet test .\tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj --filter "FullyQualifiedName~Register_Submit"`
Expected: PASS.

- [ ] **Step 3: Commit (if no code changes, skip commit)**

```bash
git status --short
```

Expected: clean working tree for planned files.
