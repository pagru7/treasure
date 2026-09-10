# Account Sharing UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an inline account-sharing panel on the Accounts page so owners can see current read-only viewers and share with other users from the same household.

**Architecture:** Keep the existing sharing endpoint and permission rules, but move the shared validation and lookup logic into a small application service so the Accounts page and API stay consistent. The Accounts page should load the current user, visible accounts, household users, and per-account share state, then render a compact share panel inside each owned account card.

**Tech Stack:** .NET 9, Blazor Server, MudBlazor, ASP.NET Core Identity, EF Core, FastEndpoints, xUnit, FluentAssertions

## Global Constraints

- One household only in phase 1.
- Shared user access is read-only unless explicitly extended later.
- Authentication is required for all app access.
- Use only household users in the share picker; do not reintroduce manual email entry in the UI.
- Keep the existing `POST /api/accounts/{id:guid}/share-readonly` permission rules intact.

---

## File Structure (planned)

- `src/Treasury.App/Application/Accounts/AccountSharingService.cs` — centralizes share-target lookup, share-recipient lookup, and the read-only sharing operation.
- `src/Treasury.App/Pages/Accounts.razor` — markup for account cards, share status, and the inline picker/button UI.
- `src/Treasury.App/Pages/Accounts.razor.cs` — page state and event handlers for loading accounts, users, and share state.
- `src/Treasury.App/Program.cs` — registers the shared account-sharing service in DI.
- `src/Treasury.App/Endpoints/Accounts/ShareAccountReadOnlyEndpoint.cs` — delegates share validation to the shared service.
- `tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs` — verifies the Accounts page exposes the share panel and household-user picker.
- `tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs` — keeps the owner/shared-user permission flow covered after the page refactor.

### Task 1: Write the failing Accounts-page share-panel regression test

**Files:**

- Create: `tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs`

**Interfaces:**

- Consumes:
  - `TreasuryHostFactory`
  - `/auth/register-submit`
  - `/auth/login-submit`
  - `POST /api/accounts`
  - `POST /api/accounts/{id:guid}/share-readonly`
  - `GET /accounts`
- Produces:
  - A regression test proving the Accounts page still does not show the required share panel before implementation.

- [ ] **Step 1: Write the failing test**

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;

namespace Treasury.IntegrationTests;

public class AccountsSharingUiTests
{
    [Fact]
    public async Task Accounts_Page_Shows_Shared_Users_And_Household_Picker()
    {
        await using var app = new TreasuryHostFactory();

        var ownerClient = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var sharedClient = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        var sharedEmail = $"shared-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!";

        await RegisterAndSignInAsync(ownerClient, ownerEmail, password);
        await RegisterAndSignInAsync(sharedClient, sharedEmail, password);

        var accountResponse = await ownerClient.PostAsJsonAsync("/api/accounts", new
        {
            Name = "Shared account",
            Currency = "PLN",
            AccountType = "cash-wallet"
        });
        accountResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var accountJson = JsonDocument.Parse(await accountResponse.Content.ReadAsStringAsync());
        var accountId = accountJson.RootElement.GetProperty("id").GetGuid();

        var shareResponse = await ownerClient.PostAsJsonAsync($"/api/accounts/{accountId}/share-readonly", new
        {
            Email = sharedEmail
        });
        shareResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pageResponse = await ownerClient.GetAsync("/accounts");
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await pageResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Shared with:");
        body.Should().Contain(sharedEmail);
        body.Should().Contain("Share read-only");
        body.Should().NotContain(ownerEmail);
    }

    private static async Task RegisterAndSignInAsync(HttpClient client, string email, string password)
    {
        var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
        {
            Email = email,
            Password = password,
            ConfirmPassword = password
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
        {
            Email = email,
            Password = password
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }
}
```

- [ ] **Step 2: Run the test to confirm it fails**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsSharingUiTests.Accounts_Page_Shows_Shared_Users_And_Household_Picker"`

Expected: FAIL because the Accounts page does not yet render the share panel or household-user picker.

- [ ] **Step 3: Commit the red test**

```bash
git add tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
git commit -m "test: add regression for account sharing ui"
```

### Task 2: Implement the account-sharing service and refactor the Accounts page

**Files:**

- Create: `src/Treasury.App/Application/Accounts/AccountSharingService.cs`
- Create: `src/Treasury.App/Pages/Accounts.razor.cs`
- Modify: `src/Treasury.App/Pages/Accounts.razor`
- Modify: `src/Treasury.App/Program.cs`
- Modify: `src/Treasury.App/Endpoints/Accounts/ShareAccountReadOnlyEndpoint.cs`

**Interfaces:**

- Consumes:
  - `TreasuryDbContext`
  - `UserManager<ApplicationUser>`
  - the existing `VisibilityRule` and `Account` entities
- Produces:
  - `AccountSharingService.GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct)`
  - `AccountSharingService.GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct)`
  - `AccountSharingService.GetSharedViewersAsync(ApplicationUser user, Guid accountId, CancellationToken ct)`
  - `AccountSharingService.ShareReadOnlyAsync(ApplicationUser user, Guid accountId, string viewerEmail, CancellationToken ct)`
  - an Accounts page that shows per-account share recipients and a household-user picker

- [ ] **Step 1: Add the shared application service**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Application.Accounts;

public sealed record HouseholdUserChoice(string Email, string DisplayName);
public sealed record AccountViewerChoice(string ViewerUserId, string Email, string DisplayName);
public sealed record ShareReadOnlyResult(bool Succeeded, string? ErrorMessage);

public sealed class AccountSharingService(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
{
    public Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct) =>
        db.Accounts
            .Where(x =>
                x.HouseholdId == user.HouseholdId
                && (x.OwnerUserId == user.Id
                    || x.OwnerUserId == "seed"
                    || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
        db.Users
            .Where(x => x.HouseholdId == user.HouseholdId && x.Id != user.Id)
            .OrderBy(x => x.Email)
            .Select(x => new HouseholdUserChoice(
                x.Email ?? x.UserName ?? x.Id,
                x.Email ?? x.UserName ?? x.Id))
            .ToListAsync(ct);

    public Task<List<AccountViewerChoice>> GetSharedViewersAsync(ApplicationUser user, Guid accountId, CancellationToken ct) =>
        (from rule in db.VisibilityRules
         join viewer in db.Users on rule.ViewerUserId equals viewer.Id
         where rule.AccountId == accountId && viewer.HouseholdId == user.HouseholdId
         orderby viewer.Email
         select new AccountViewerChoice(
             viewer.Id,
             viewer.Email ?? viewer.UserName ?? viewer.Id,
             viewer.Email ?? viewer.UserName ?? viewer.Id)).ToListAsync(ct);

    public async Task<ShareReadOnlyResult> ShareReadOnlyAsync(ApplicationUser user, Guid accountId, string viewerEmail, CancellationToken ct)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == accountId && x.HouseholdId == user.HouseholdId, ct);
        if (account is null)
        {
            return new ShareReadOnlyResult(false, "Account not found.");
        }

        if (account.OwnerUserId != user.Id)
        {
            return new ShareReadOnlyResult(false, "Only the owner can share this account.");
        }

        var normalizedEmail = viewerEmail.Trim();
        var viewer = await userManager.FindByEmailAsync(normalizedEmail);
        if (viewer is null || viewer.HouseholdId != user.HouseholdId)
        {
            return new ShareReadOnlyResult(false, "Choose a user from the current household.");
        }

        if (viewer.Id == user.Id)
        {
            return new ShareReadOnlyResult(false, "You already own this account.");
        }

        var existingRule = await db.VisibilityRules.SingleOrDefaultAsync(x => x.AccountId == accountId && x.ViewerUserId == viewer.Id, ct);
        if (existingRule is null)
        {
            db.VisibilityRules.Add(new VisibilityRule
            {
                AccountId = accountId,
                ViewerUserId = viewer.Id,
                IsReadOnly = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existingRule.IsReadOnly = true;
        }

        await db.SaveChangesAsync(ct);
        return new ShareReadOnlyResult(true, null);
    }
}
```

- [ ] **Step 2: Register the service and delegate the endpoint to it**

```csharp
// src/Treasury.App/Program.cs
builder.Services.AddScoped<AccountSharingService>();
```

```csharp
// src/Treasury.App/Endpoints/Accounts/ShareAccountReadOnlyEndpoint.cs
public sealed class ShareAccountReadOnlyEndpoint(AccountSharingService accountSharingService, UserManager<ApplicationUser> userManager)
    : Endpoint<ShareAccountReadOnlyRouteRequest>
{
    public override void Configure()
    {
        Post("/api/accounts/{id:guid}/share-readonly");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(ShareAccountReadOnlyRouteRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var result = await accountSharingService.ShareReadOnlyAsync(user, request.Id, request.Email, ct);
        if (!result.Succeeded)
        {
            AddError(x => x.Email, result.ErrorMessage ?? "Unable to share this account.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        await SendOkAsync(new
        {
            AccountId = request.Id,
            IsReadOnly = true
        }, ct);
    }
}
```

- [ ] **Step 3: Refactor the Accounts page into state plus markup**

```razor
@page "/accounts"
@attribute [Authorize]
@inject ISnackbar Snackbar
@inject AuthenticationStateProvider AuthenticationStateProvider
@inject UserManager<ApplicationUser> UserManager
@inject AccountSharingService AccountSharingService

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="pa-4 pa-sm-6">
    <MudStack Spacing="3">
        <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
            <MudText Typo="Typo.h4">Accounts</MudText>
            <MudButton Variant="Variant.Filled" Color="Color.Tertiary" StartIcon="@Icons.Material.Filled.Add" OnClick="ToggleCreateForm">Add account</MudButton>
        </MudStack>

        @if (_accounts.Count == 0)
        {
            <MudAlert Severity="Severity.Info">No accounts yet. Create your first household account to get started.</MudAlert>
        }
        else
        {
            <MudGrid>
                @foreach (var account in _accounts)
                {
                    <MudItem xs="12" md="6" lg="4">
                        <MudPaper Class="pa-4 rounded-xl" Elevation="2">
                            <MudText Typo="Typo.h6">@account.Name</MudText>
                            <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mt-1">@account.AccountType</MudText>
                            <MudDivider Class="my-3" />
                            <MudText Typo="Typo.h5">@account.CurrentBalance.ToString("N2") @account.Currency</MudText>

                            @if (account.IsOwner)
                            {
                                <MudDivider Class="my-3" />
                                <MudText Typo="Typo.subtitle2">Shared with:</MudText>
                                @if (account.SharedWith.Count == 0)
                                {
                                    <MudText Typo="Typo.body2" Color="Color.Secondary">Nobody yet.</MudText>
                                }
                                else
                                {
                                    <MudChipSet>
                                        @foreach (var viewer in account.SharedWith)
                                        {
                                            <MudChip Color="Color.Primary" Variant="Variant.Outlined">@viewer.DisplayName</MudChip>
                                        }
                                    </MudChipSet>
                                }

                                <MudSelect T="string" Label="Share with household user" @bind-Value="account.SelectedShareEmail">
                                    @foreach (var user in _householdUsers)
                                    {
                                        <MudSelectItem Value="@user.Email">@user.DisplayName</MudSelectItem>
                                    }
                                </MudSelect>

                                @if (_householdUsers.Count == 0)
                                {
                                    <MudAlert Class="mt-3" Severity="Severity.Info">No other household users are available to share with.</MudAlert>
                                }

                                <MudButton Class="mt-3" Variant="Variant.Filled" Color="Color.Tertiary" OnClick="() => ShareAccountAsync(account)">
                                    Share read-only
                                </MudButton>
                            }
                            else
                            {
                                <MudAlert Class="mt-3" Severity="Severity.Info">Read-only shared account.</MudAlert>
                            }
                        </MudPaper>
                    </MudItem>
                }
            </MudGrid>
        }
    </MudStack>
</MudContainer>
```

```csharp
// src/Treasury.App/Pages/Accounts.razor.cs
public partial class Accounts
{
    [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] public UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] public AccountSharingService AccountSharingService { get; set; } = default!;

    private ApplicationUser? _currentUser;
    private List<AccountCardVm> _accounts = new();
    private List<HouseholdUserChoice> _householdUsers = new();

    private sealed class AccountCardVm
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Currency { get; set; } = "PLN";
        public string AccountType { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public bool IsOwner { get; set; }
        public string SelectedShareEmail { get; set; } = string.Empty;
        public List<AccountViewerChoice> SharedWith { get; set; } = new();
    }
}
```

- [ ] **Step 4: Load current user, visible accounts, household users, and share state**

```csharp
protected override async Task OnInitializedAsync()
{
    var auth = await AuthenticationStateProvider.GetAuthenticationStateAsync();
    _currentUser = await UserManager.GetUserAsync(auth.User);
    if (_currentUser is null)
    {
        _accounts.Clear();
        return;
    }

    await LoadAccountsAsync();
    await LoadHouseholdUsersAsync();
}
```

```csharp
private async Task LoadAccountsAsync()
{
    var accounts = await AccountSharingService.GetVisibleAccountsAsync(_currentUser!, CancellationToken.None);
    var sharedViewers = new Dictionary<Guid, List<AccountViewerChoice>>();

    foreach (var account in accounts)
    {
        sharedViewers[account.Id] = await AccountSharingService.GetSharedViewersAsync(_currentUser!, account.Id, CancellationToken.None);
    }

    _accounts = accounts.Select(account => new AccountCardVm
    {
        Id = account.Id,
        Name = account.Name,
        Currency = account.Currency,
        AccountType = account.AccountType,
        CurrentBalance = account.CurrentBalance,
        IsOwner = account.OwnerUserId == _currentUser!.Id,
        SharedWith = sharedViewers.TryGetValue(account.Id, out var viewers) ? viewers : new()
    }).ToList();
}

private async Task LoadHouseholdUsersAsync()
{
    _householdUsers = await AccountSharingService.GetHouseholdUsersAsync(_currentUser!, CancellationToken.None);
}
```

```csharp
private async Task ShareAccountAsync(AccountCardVm account)
{
    if (string.IsNullOrWhiteSpace(account.SelectedShareEmail))
    {
        Snackbar.Add("Choose a household user first.", Severity.Warning);
        return;
    }

    var result = await AccountSharingService.ShareReadOnlyAsync(_currentUser!, account.Id, account.SelectedShareEmail, CancellationToken.None);
    if (!result.Succeeded)
    {
        Snackbar.Add(result.ErrorMessage ?? "Unable to share this account.", Severity.Error);
        return;
    }

    Snackbar.Add("Account shared read-only.", Severity.Success);
    await LoadAccountsAsync();
}
```

- [ ] **Step 5: Run the targeted test to verify it passes**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsSharingUiTests.Accounts_Page_Shows_Shared_Users_And_Household_Picker"`

Expected: PASS, with the Accounts page rendering the share section and household-user picker.

- [ ] **Step 6: Commit the implementation**

```bash
git add src/Treasury.App/Application/Accounts/AccountSharingService.cs src/Treasury.App/Pages/Accounts.razor src/Treasury.App/Pages/Accounts.razor.cs src/Treasury.App/Program.cs src/Treasury.App/Endpoints/Accounts/ShareAccountReadOnlyEndpoint.cs
git commit -m "feat: add inline account sharing ui"
```

### Task 3: Keep the sharing flow covered end to end

**Files:**

- Modify: `tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs`
- Modify: `tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs`

**Interfaces:**

- Consumes:
  - the new account-sharing UI and shared service
  - the existing account sharing endpoint
- Produces:
  - regression coverage proving the owner can see shared recipients, the picker excludes the current user, and the shared user still gets read-only access

- [ ] **Step 1: Expand the UI regression test to check the current user is not in the picker**

```csharp
body.Should().Contain(sharedEmail);
body.Should().Contain("Share read-only");
body.Should().NotContain(ownerEmail);
```

- [ ] **Step 2: Keep the read-only access test passing**

```csharp
[Fact]
public async Task Shared_User_Cannot_Post_To_Owner_Edit_Endpoints()
{
    var postTransaction = await sharedClient.PostAsJsonAsync("/api/transactions", new
    {
        AccountId = accountId,
        Description = "Attempt by shared user",
        Category = "General",
        Amount = 10m,
        Currency = "PLN",
        Type = "expense",
        TransactionDate = DateTime.UtcNow
    });

    postTransaction.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

- [ ] **Step 3: Run the full integration suite and verify the new UI behavior stays stable**

Run: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug`

Expected: PASS.

- [ ] **Step 4: Commit the finished feature**

```bash
git add tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
git commit -m "test: cover account sharing ui and permissions"
```
