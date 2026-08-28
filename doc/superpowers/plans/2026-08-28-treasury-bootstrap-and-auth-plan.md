# Treasury Bootstrap and Auth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bootstrap a local household finance app in a single ASP.NET Core project, verify the app starts, and add auth plus household seed before building the dashboard and protected app shell.

**Architecture:** Use one ASP.NET Core application project (`src/Treasury.App`) that serves both the Blazor UI and FastEndpoints API. Keep internal layer boundaries with `Domain`, `Application`, and `Infrastructure` folders, and persist data in PostgreSQL via EF Core. Start with health-check startup validation, then add identity/authentication, then dashboard + shell UI.

**Tech Stack:** ASP.NET Core, Blazor Server, MudBlazor, FastEndpoints, EF Core, PostgreSQL, Npgsql, ASP.NET Core Identity, xUnit, FluentAssertions, Docker Compose

## Global Constraints

- Single household only in phase 1.
- Support both PLN and EUR at minimum; allow more currencies.
- No external price feeds in phase 1.
- Shared user access is read-only unless explicitly extended later.
- App is for local Docker deployment and private home usage only.
- Authentication is required for all app access.
- The canonical project path is `src/Treasury.App`.

---

### Task 1: Bootstrap solution, app project, and Docker runtime

**Files:**
- Create: `Treasury.sln`
- Create: `src/Treasury.App/Treasury.App.csproj`
- Create: `src/Treasury.App/Program.cs`
- Create: `docker-compose.yml`
- Create: `Dockerfile`
- Modify: `.gitignore`
- Test: `tests/Treasury.IntegrationTests/StartupTests.cs`

**Interfaces:**
- Consumes: none
- Produces:
  - `Program.cs` host entry point with `WebApplication CreateApp(string[] args)` bootstrap method.
  - `ConnectionStrings:DefaultConnection` key.
  - `GET /health` returning HTTP 200.

- [ ] **Step 1: Write the failing smoke test**

```csharp
using System.Net;
using FluentAssertions;

namespace Treasury.IntegrationTests;

public class StartupTests
{
    [Fact]
    public async Task Get_Health_Returns200()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient();
        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~StartupTests.Get_Health_Returns200"`
Expected: FAIL with missing project, app factory, or `/health` endpoint.

- [ ] **Step 3: Create the minimal application and runtime files**

```csharp
// src/Treasury.App/Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.Run();

public partial class Program { }
```

```xml
<!-- src/Treasury.App/Treasury.App.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

```yaml
# docker-compose.yml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: treasury
      POSTGRES_USER: treasury
      POSTGRES_PASSWORD: treasury
    ports:
      - "5432:5432"

  treasury-app:
    build: .
    environment:
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=treasury;Username=treasury;Password=treasury
    ports:
      - "8080:8080"
    depends_on:
      - postgres
```

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "src/Treasury.App/Treasury.App.csproj"
RUN dotnet publish "src/Treasury.App/Treasury.App.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Treasury.App.dll"]
```

```text
# .gitignore
bin/
obj/
.vs/
*.user
*.suo
```

```csharp
// tests/Treasury.IntegrationTests/TreasuryHostFactory.cs
using Microsoft.AspNetCore.Mvc.Testing;

namespace Treasury.IntegrationTests;

public class TreasuryHostFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}
```

- [ ] **Step 4: Run the smoke test to verify it passes**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~StartupTests.Get_Health_Returns200"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Treasury.sln src/Treasury.App tests/Treasury.IntegrationTests docker-compose.yml Dockerfile .gitignore
git commit -m "chore: bootstrap treasury app and docker runtime"
```

### Task 2: Add authentication, default household seed, and authorization policies

**Files:**
- Modify: `src/Treasury.App/Program.cs`
- Create: `src/Treasury.App/Domain/Household.cs`
- Create: `src/Treasury.App/Domain/ApplicationUser.cs`
- Create: `src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs`
- Create: `src/Treasury.App/Infrastructure/Data/Seed/InitialSeed.cs`
- Create: `src/Treasury.App/Infrastructure/Auth/Policies.cs`
- Test: `tests/Treasury.IntegrationTests/AuthTests.cs`

**Interfaces:**
- Consumes:
  - `ConnectionStrings:DefaultConnection`
- Produces:
  - `Policies.OwnerOnly`
  - `Policies.SharedReadOnly`
  - seeded default household record with stable id `11111111-1111-1111-1111-111111111111`

- [ ] **Step 1: Write failing auth and household tests**

```csharp
using System.Net;
using FluentAssertions;

namespace Treasury.IntegrationTests;

public class AuthTests
{
    [Fact]
    public async Task Anonymous_Request_To_Accounts_Returns401()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Seed_Creates_Single_Default_Household()
    {
        await using var app = new TreasuryHostFactory();

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();

        var households = await db.Households.ToListAsync();

        households.Should().HaveCount(1);
        households[0].Name.Should().Be("Default Household");
    }
}
```

- [ ] **Step 2: Run the auth tests to verify they fail**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AuthTests"`
Expected: FAIL with missing auth configuration, db context, or seed.

- [ ] **Step 3: Add the minimal auth + seed implementation**

```csharp
// src/Treasury.App/Infrastructure/Auth/Policies.cs
public static class Policies
{
    public const string OwnerOnly = "OwnerOnly";
    public const string SharedReadOnly = "SharedReadOnly";
}
```

```csharp
// src/Treasury.App/Domain/Household.cs
public class Household
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

```csharp
// src/Treasury.App/Domain/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser
{
    public Guid HouseholdId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

```csharp
// src/Treasury.App/Infrastructure/Data/TreasuryDbContext.cs
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public class TreasuryDbContext : IdentityDbContext<ApplicationUser>
{
    public TreasuryDbContext(DbContextOptions<TreasuryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Household> Households => Set<Household>();
}
```

```csharp
// src/Treasury.App/Infrastructure/Data/Seed/InitialSeed.cs
using Microsoft.EntityFrameworkCore;

public static class InitialSeed
{
    public static async Task SeedAsync(TreasuryDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Households.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Households.Add(new Household
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Default Household"
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
```

```csharp
// src/Treasury.App/Program.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=treasury;Username=treasury;Password=treasury";

builder.Services.AddDbContext<TreasuryDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<TreasuryDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.OwnerOnly, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(Policies.SharedReadOnly, policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
    await InitialSeed.SeedAsync(db);
}

app.MapHealthChecks("/health");
app.UseAuthentication();
app.UseAuthorization();
app.Run();

public partial class Program { }
```

- [ ] **Step 4: Run the auth tests to verify they pass**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AuthTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App tests/Treasury.IntegrationTests
git commit -m "feat: add auth policies and household seed"
```

### Task 3: Add the MudBlazor app shell and auth pages

**Files:**
- Create: `src/Treasury.App/Components/Layout/AppShell.razor`
- Create: `src/Treasury.App/Components/Layout/NavMenu.razor`
- Create: `src/Treasury.App/Pages/Auth/Login.razor`
- Create: `src/Treasury.App/Pages/Auth/Register.razor`
- Create: `src/Treasury.App/Theme/TreasuryTheme.cs`
- Modify: `src/Treasury.App/Program.cs`
- Test: `tests/Treasury.IntegrationTests/MudShellTests.cs`

**Interfaces:**
- Consumes:
  - authenticated and anonymous user states
  - `Policies.OwnerOnly` and `Policies.SharedReadOnly`
- Produces:
  - `/auth/login`
  - `/auth/register`
  - responsive `MudLayout` app shell with drawer navigation

- [ ] **Step 1: Write the failing shell tests**

```csharp
using FluentAssertions;

namespace Treasury.IntegrationTests;

public class MudShellTests
{
    [Fact]
    public async Task Anonymous_User_Is_Redirected_To_Login()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/auth/login");
    }
}
```

- [ ] **Step 2: Run the shell tests to verify they fail**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~MudShellTests"`
Expected: FAIL with missing Blazor app shell or auth route.

- [ ] **Step 3: Add the minimal Blazor app shell and pages**

```csharp
// src/Treasury.App/Program.cs
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddMudBlazorDialog();
builder.Services.AddMudBlazorSnackbar();
builder.Services.AddMudBlazorResizeListener();
```

```razor
<!-- src/Treasury.App/Components/Layout/AppShell.razor -->
<MudLayout>
    <MudAppBar Dense="true" Elevation="1">
        <MudIconButton Icon="@Icons.Material.Filled.Menu" OnClick="ToggleDrawer" />
        <MudText Typo="Typo.h6">Treasury</MudText>
        <MudSpacer />
        <MudButton Variant="Variant.Text" Href="/">Dashboard</MudButton>
        <MudButton Variant="Variant.Text" Href="/accounts">Accounts</MudButton>
        <MudButton Variant="Variant.Text" Href="/transactions">Transactions</MudButton>
        <MudButton Variant="Variant.Text" Href="/rates">Rates</MudButton>
        <MudButton Variant="Variant.Text" Color="Color.Error" OnClick="Logout">Logout</MudButton>
    </MudAppBar>

    <MudDrawer @bind-Open="_drawerOpen" Variant="DrawerVariant.Temporary">
        <MudNavMenu>
            <MudNavLink Href="/">Dashboard</MudNavLink>
            <MudNavLink Href="/accounts">Accounts</MudNavLink>
            <MudNavLink Href="/transactions">Transactions</MudNavLink>
            <MudNavLink Href="/valuations">Valuations</MudNavLink>
            <MudNavLink Href="/rates">Rates</MudNavLink>
            <MudNavLink Href="/shared-portfolio">Shared Portfolio</MudNavLink>
            <MudNavLink OnClick="Logout">Logout</MudNavLink>
        </MudNavMenu>
    </MudDrawer>

    <MudMainContent>
        @Body
    </MudMainContent>
</MudLayout>
```

```razor
<!-- src/Treasury.App/Pages/Auth/Login.razor -->
@page "/auth/login"

<MudContainer MaxWidth="MaxWidth.Small" Class="pa-6">
    <MudCard>
        <MudCardContent>
            <MudText Typo="Typo.h4">Log in</MudText>
            <MudTextField Label="Email" />
            <MudTextField Label="Password" InputType="InputType.Password" />
        </MudCardContent>
        <MudCardActions>
            <MudButton Variant="Variant.Filled" Color="Color.Primary">Log in</MudButton>
        </MudCardActions>
    </MudCard>
</MudContainer>
```

```razor
<!-- src/Treasury.App/Pages/Auth/Register.razor -->
@page "/auth/register"

<MudContainer MaxWidth="MaxWidth.Small" Class="pa-6">
    <MudCard>
        <MudCardContent>
            <MudText Typo="Typo.h4">Create account</MudText>
            <MudTextField Label="Email" />
            <MudTextField Label="Password" InputType="InputType.Password" />
            <MudTextField Label="Confirm password" InputType="InputType.Password" />
        </MudCardContent>
        <MudCardActions>
            <MudButton Variant="Variant.Filled" Color="Color.Primary">Register</MudButton>
        </MudCardActions>
    </MudCard>
</MudContainer>
```

```csharp
// src/Treasury.App/Theme/TreasuryTheme.cs
using MudBlazor;

public static class TreasuryTheme
{
    public static MudTheme Create() => new()
    {
        Palette = new PaletteLight
        {
            Primary = Colors.Blue.Default,
            Secondary = Colors.Green.Default,
            AppbarBackground = Colors.Shades.White,
            Background = Colors.Grey.Lighten5,
            Surface = Colors.Shades.White
        },
        Typography = new Typography
        {
            Default = new TypographyDefinition { FontFamily = new[] { "Segoe UI", "sans-serif" } }
        }
    };
}
```

- [ ] **Step 4: Run the shell tests to verify they pass**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~MudShellTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App tests/Treasury.IntegrationTests
git commit -m "feat: add mudblazor shell and auth pages"
```

### Task 4: Add the dashboard summary contract and view model

**Files:**
- Create: `src/Treasury.App/Application/Dashboard/DashboardSummaryResponse.cs`
- Create: `src/Treasury.App/Application/Dashboard/DashboardSummaryService.cs`
- Create: `src/Treasury.App/Endpoints/DashboardSummaryEndpoint.cs`
- Create: `src/Treasury.App/Pages/Dashboard.razor`
- Test: `tests/Treasury.IntegrationTests/DashboardSummaryTests.cs`

**Interfaces:**
- Consumes:
  - account values grouped by currency
  - manual FX rates keyed by `currency -> PLN`
- Produces:
  - `GET /api/dashboard/summary`
  - `DashboardSummaryResponse` object with totals, converted PLN total, and missing-rate metadata

- [ ] **Step 1: Write the failing dashboard tests**

```csharp
using System.Net;
using FluentAssertions;

namespace Treasury.IntegrationTests;

public class DashboardSummaryTests
{
    [Fact]
    public async Task Dashboard_Summary_Returns_Currency_Totals_And_Pln_Total()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/dashboard/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("totalPln");
        body.Should().Contain("totalsByCurrency");
    }
}
```

- [ ] **Step 2: Run the dashboard tests to verify they fail**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~DashboardSummaryTests"`
Expected: FAIL with missing endpoint or response model.

- [ ] **Step 3: Implement the minimal dashboard response and endpoint**

```csharp
// src/Treasury.App/Application/Dashboard/DashboardSummaryResponse.cs
public sealed class DashboardSummaryResponse
{
    public Dictionary<string, decimal> TotalsByCurrency { get; set; } = new();
    public decimal TotalPln { get; set; }
    public List<string> MissingRateCurrencies { get; set; } = new();
}
```

```csharp
// src/Treasury.App/Application/Dashboard/DashboardSummaryService.cs
public sealed class DashboardSummaryService
{
    public DashboardSummaryResponse BuildSummary(IEnumerable<AccountValueItem> accounts, IReadOnlyDictionary<string, decimal> rates)
    {
        var totals = accounts
            .GroupBy(x => x.Currency)
            .ToDictionary(x => x.Key, x => x.Sum(v => v.Amount));

        var totalPln = totals
            .Where(x => x.Key == "PLN")
            .Sum(x => x.Value);

        foreach (var group in totals.Where(x => x.Key != "PLN"))
        {
            if (rates.TryGetValue(group.Key, out var rate))
            {
                totalPln += group.Value * rate;
            }
            else
            {
                // intentionally omitted from PLN aggregate when rate is missing
            }
        }

        return new DashboardSummaryResponse
        {
            TotalsByCurrency = totals,
            TotalPln = totalPln,
            MissingRateCurrencies = totals.Keys
                .Where(currency => currency != "PLN" && !rates.ContainsKey(currency))
                .ToList()
        };
    }
}
```

```csharp
// src/Treasury.App/Endpoints/DashboardSummaryEndpoint.cs
public sealed class DashboardSummaryEndpoint : EndpointWithoutRequest<DashboardSummaryResponse>
{
    public override void Configure()
    {
        Get("/api/dashboard/summary");
        Policies(Policies.OwnerOnly);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var accounts = new List<AccountValueItem>
        {
            new("PLN", 1200m),
            new("EUR", 2000m)
        };

        var rates = new Dictionary<string, decimal>
        {
            ["EUR"] = 4.6m
        };

        var service = new DashboardSummaryService();
        var summary = service.BuildSummary(accounts, rates);

        await SendAsync(summary, cancellation: ct);
    }
}
```

```razor
<!-- src/Treasury.App/Pages/Dashboard.razor -->
@page "/"

<MudGrid>
    <MudItem xs="12" md="6" lg="3">
        <MudCard>
            <MudCardContent>
                <MudText Typo="Typo.subtitle2">PLN</MudText>
                <MudText Typo="Typo.h5">12,000.00 PLN</MudText>
            </MudCardContent>
        </MudCard>
    </MudItem>
    <MudItem xs="12" md="6" lg="3">
        <MudCard>
            <MudCardContent>
                <MudText Typo="Typo.subtitle2">EUR</MudText>
                <MudText Typo="Typo.h5">2,000.00 EUR</MudText>
            </MudCardContent>
        </MudCard>
    </MudItem>
</MudGrid>
```

- [ ] **Step 4: Run the dashboard tests to verify they pass**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~DashboardSummaryTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App tests/Treasury.IntegrationTests
git commit -m "feat: add dashboard summary contract and view"
```

### Task 5: Add the account domain and API surface for phase 1

**Files:**
- Create: `src/Treasury.App/Domain/AccountType.cs`
- Create: `src/Treasury.App/Domain/Account.cs`
- Create: `src/Treasury.App/Domain/VisibilityRule.cs`
- Create: `src/Treasury.App/Application/Accounts/AccountService.cs`
- Create: `src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs`
- Create: `src/Treasury.App/Contracts/Accounts/AccountResponse.cs`
- Create: `src/Treasury.App/Endpoints/AccountsEndpoint.cs`
- Test: `tests/Treasury.IntegrationTests/AccountsEndpointTests.cs`

**Interfaces:**
- Consumes:
  - authenticated user identity
  - `Policies.OwnerOnly`, `Policies.SharedReadOnly`
- Produces:
  - `POST /api/accounts`
  - `GET /api/accounts`
  - `POST /api/accounts/{id}/share-readonly`

- [ ] **Step 1: Write the failing account tests**

```csharp
using System.Net;
using FluentAssertions;

namespace Treasury.IntegrationTests;

public class AccountsEndpointTests
{
    [Fact]
    public async Task Create_Account_Returns201()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient();

        var response = await client.PostAsJsonAsync("/api/accounts", new
        {
            name = "Main Wallet",
            currency = "PLN",
            accountType = "cash-wallet"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
```

- [ ] **Step 2: Run the account tests to verify they fail**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsEndpointTests"`
Expected: FAIL with missing endpoint/service/entities.

- [ ] **Step 3: Add the minimal account domain, service, and endpoint**

```csharp
// src/Treasury.App/Domain/Account.cs
public sealed class Account
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "PLN";
    public string AccountType { get; set; } = "cash-wallet";
    public decimal CurrentBalance { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

```csharp
// src/Treasury.App/Domain/VisibilityRule.cs
public sealed class VisibilityRule
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid ViewerUserId { get; set; }
    public bool IsReadOnly { get; set; } = true;
}
```

```csharp
// src/Treasury.App/Application/Accounts/AccountService.cs
public sealed class AccountService
{
    public Task<AccountResponse> CreateAccountAsync(CreateAccountRequest request, Guid actorUserId, CancellationToken ct)
    {
        var created = new AccountResponse
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Currency = request.Currency,
            AccountType = request.AccountType,
            OwnerUserId = actorUserId
        };

        return Task.FromResult(created);
    }
}
```

```csharp
// src/Treasury.App/Contracts/Accounts/CreateAccountRequest.cs
public sealed class CreateAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "PLN";
    public string AccountType { get; set; } = "cash-wallet";
}
```

```csharp
// src/Treasury.App/Contracts/Accounts/AccountResponse.cs
public sealed class AccountResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "PLN";
    public string AccountType { get; set; } = "cash-wallet";
    public Guid OwnerUserId { get; set; }
}
```

```csharp
// src/Treasury.App/Endpoints/AccountsEndpoint.cs
public sealed class AccountsEndpoint : EndpointWithoutRequest<IEnumerable<AccountResponse>>
{
    public override void Configure()
    {
        Get("/api/accounts");
        Policies(Policies.OwnerOnly);
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        return SendAsync(new List<AccountResponse>(), cancellation: ct);
    }
}
```

- [ ] **Step 4: Run the account tests to verify they pass**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsEndpointTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Treasury.App tests/Treasury.IntegrationTests
git commit -m "feat: add account domain and account API skeleton"
```

### Task 6: Make the Dockerized app runnable locally and verify the baseline

**Files:**
- Modify: `docker-compose.yml`
- Modify: `src/Treasury.App/Program.cs`
- Modify: `.gitignore`
- Test: `tests/Treasury.IntegrationTests/FullStartupTests.cs`

**Interfaces:**
- Consumes: previous tasks and runtime config
- Produces: a working local application stack with Postgres and startup health checks

- [ ] **Step 1: Write the final startup smoke test**

```csharp
public class FullStartupTests
{
    [Fact]
    public async Task App_Starts_In_Local_Docker_Style_Environment()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

- [ ] **Step 2: Run the final startup test to verify it passes**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~FullStartupTests"`
Expected: PASS.

- [ ] **Step 3: Validate the baseline locally**

Run:

```bash
docker compose up --build
dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug
```

Expected: startup and auth/dashboard smoke tests pass with the local stack running.

- [ ] **Step 4: Commit**

```bash
git add src/Treasury.App docker-compose.yml tests/Treasury.IntegrationTests
git commit -m "chore: validate local startup baseline"
```

---

## Implementation Notes

- Keep the first run intentionally simple: startup, health, auth, shell, summary API, and account skeleton.
- Add explicit tests before each milestone so baseline behavior is proven rather than assumed.
- Do not hide complexity in one huge commit; the app should remain readable and reviewable across tasks.
- The next logical milestone after this plan is the full domain model for transactions, rates, and valuations, followed by the shared-read-only visibility behavior.
