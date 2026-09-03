# Review Package

Base: 875b9a2fced19ed71f2dbb2171936173a7064bab
Head: 2631764f1b0e058edd8fafd7f5a370f93e3d4edd

## Commits

2631764 feat: add inline account sharing ui

## Diff Stat

 .superpowers/sdd/task-2-report.md                  |  19 +++
 .../Application/Accounts/AccountSharingService.cs  |  87 ++++++++++
 .../Accounts/ShareAccountReadOnlyEndpoint.cs       |  68 ++------
 src/Treasury.App/Pages/Accounts.razor              | 117 +++++---------
 src/Treasury.App/Pages/Accounts.razor.cs           | 178 +++++++++++++++++++++
 src/Treasury.App/Program.cs                        |   2 +
 6 files changed, 343 insertions(+), 128 deletions(-)

## Full Diff (-U10)

diff --git a/.superpowers/sdd/task-2-report.md b/.superpowers/sdd/task-2-report.md
new file mode 100644
index 0000000..f156e15
--- /dev/null
+++ b/.superpowers/sdd/task-2-report.md
@@ -0,0 +1,19 @@
+# Task 2 Report
+
+## Summary
+Implemented the account-sharing service, delegated the share endpoint to it, and refactored the Accounts page into a code-behind component with inline read-only sharing controls.
+
+## Changes
+- Added `AccountSharingService` with visible-account, household-user, shared-viewer, and share-readonly operations.
+- Registered the service in `Program.cs`.
+- Updated `ShareAccountReadOnlyEndpoint` to delegate sharing to the service while preserving not-found/forbidden behavior.
+- Split `Accounts.razor` into markup plus `Accounts.razor.cs` and kept the create-account flow.
+- Added inline per-account sharing UI with shared-user chips and a household-user picker.
+
+## Validation
+- Passed: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsSharingUiTests.Accounts_Page_Shows_Shared_Users_And_Household_Picker"`
+- Passed: related integration tests for `/api/accounts` and shared-readonly permissions.
+
+## Notes
+- The Accounts page now shows only household users for sharing.
+- Shared users still see read-only accounts, but do not see share controls for accounts they do not own.
diff --git a/src/Treasury.App/Application/Accounts/AccountSharingService.cs b/src/Treasury.App/Application/Accounts/AccountSharingService.cs
new file mode 100644
index 0000000..3c27b35
--- /dev/null
+++ b/src/Treasury.App/Application/Accounts/AccountSharingService.cs
@@ -0,0 +1,87 @@
+using Microsoft.AspNetCore.Identity;
+using Microsoft.EntityFrameworkCore;
+using Treasury.App.Domain;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.App.Application.Accounts;
+
+public sealed record HouseholdUserChoice(string Email, string DisplayName);
+public sealed record AccountViewerChoice(string ViewerUserId, string Email, string DisplayName);
+public sealed record ShareReadOnlyResult(bool Succeeded, string? ErrorMessage);
+
+public sealed class AccountSharingService(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+{
+    public Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct) =>
+        db.Accounts
+            .Where(x =>
+                x.HouseholdId == user.HouseholdId
+                && (x.OwnerUserId == user.Id
+                    || x.OwnerUserId == "seed"
+                    || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
+            .OrderBy(x => x.Name)
+            .ToListAsync(ct);
+
+    public Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
+        db.Users
+            .Where(x => x.HouseholdId == user.HouseholdId && x.Id != user.Id)
+            .OrderBy(x => x.Email ?? x.UserName ?? x.Id)
+            .Select(x => new HouseholdUserChoice(
+                x.Email ?? x.UserName ?? x.Id,
+                x.Email ?? x.UserName ?? x.Id))
+            .ToListAsync(ct);
+
+    public Task<List<AccountViewerChoice>> GetSharedViewersAsync(ApplicationUser user, Guid accountId, CancellationToken ct) =>
+        (from rule in db.VisibilityRules
+         join viewer in db.Users on rule.ViewerUserId equals viewer.Id
+         where rule.AccountId == accountId && viewer.HouseholdId == user.HouseholdId
+         orderby viewer.Email ?? viewer.UserName ?? viewer.Id
+         select new AccountViewerChoice(
+             viewer.Id,
+             viewer.Email ?? viewer.UserName ?? viewer.Id,
+             viewer.Email ?? viewer.UserName ?? viewer.Id)).ToListAsync(ct);
+
+    public async Task<ShareReadOnlyResult> ShareReadOnlyAsync(ApplicationUser user, Guid accountId, string viewerEmail, CancellationToken ct)
+    {
+        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == accountId && x.HouseholdId == user.HouseholdId, ct);
+        if (account is null)
+        {
+            return new ShareReadOnlyResult(false, "Account not found.");
+        }
+
+        if (account.OwnerUserId != user.Id)
+        {
+            return new ShareReadOnlyResult(false, "Only the owner can share this account.");
+        }
+
+        var normalizedEmail = viewerEmail.Trim();
+        var viewer = await userManager.FindByEmailAsync(normalizedEmail);
+        if (viewer is null || viewer.HouseholdId != user.HouseholdId)
+        {
+            return new ShareReadOnlyResult(false, "Choose a user from the current household.");
+        }
+
+        if (viewer.Id == user.Id)
+        {
+            return new ShareReadOnlyResult(false, "You already own this account.");
+        }
+
+        var existingRule = await db.VisibilityRules.SingleOrDefaultAsync(x => x.AccountId == accountId && x.ViewerUserId == viewer.Id, ct);
+        if (existingRule is null)
+        {
+            db.VisibilityRules.Add(new VisibilityRule
+            {
+                AccountId = accountId,
+                ViewerUserId = viewer.Id,
+                IsReadOnly = true,
+                CreatedAt = DateTime.UtcNow
+            });
+        }
+        else
+        {
+            existingRule.IsReadOnly = true;
+        }
+
+        await db.SaveChangesAsync(ct);
+        return new ShareReadOnlyResult(true, null);
+    }
+}
diff --git a/src/Treasury.App/Endpoints/Accounts/ShareAccountReadOnlyEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/ShareAccountReadOnlyEndpoint.cs
index d1310a1..3546a4a 100644
--- a/src/Treasury.App/Endpoints/Accounts/ShareAccountReadOnlyEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Accounts/ShareAccountReadOnlyEndpoint.cs
@@ -1,94 +1,58 @@
 using FastEndpoints;
 using Microsoft.AspNetCore.Identity;
-using Microsoft.EntityFrameworkCore;
 using Treasury.App.Domain;
-using Treasury.App.Infrastructure.Data;
+using Treasury.App.Application.Accounts;
 
 namespace Treasury.App.Endpoints.Accounts;
 
 public sealed class ShareAccountReadOnlyRouteRequest
 {
     public Guid Id { get; set; }
     public string Email { get; set; } = string.Empty;
 }
 
-public sealed class ShareAccountReadOnlyEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+public sealed class ShareAccountReadOnlyEndpoint(AccountSharingService accountSharingService, UserManager<ApplicationUser> userManager)
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
 
-        var email = request.Email?.Trim();
-        if (string.IsNullOrWhiteSpace(email))
+        var result = await accountSharingService.ShareReadOnlyAsync(user, request.Id, request.Email, ct);
+        if (!result.Succeeded)
         {
-            AddError(x => x.Email, "Viewer email is required.");
-            await SendErrorsAsync(cancellation: ct);
-            return;
-        }
-
-        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
-        if (account is null)
-        {
-            await SendNotFoundAsync(ct);
-            return;
-        }
-
-        if (account.OwnerUserId != user.Id)
-        {
-            await SendForbiddenAsync(ct);
-            return;
-        }
+            if (result.ErrorMessage == "Account not found.")
+            {
+                await SendNotFoundAsync(ct);
+                return;
+            }
 
-        var viewer = await userManager.FindByEmailAsync(email);
-        if (viewer is null || viewer.HouseholdId != user.HouseholdId)
-        {
-            AddError(x => x.Email, "Viewer must be an existing user from the same household.");
-            await SendErrorsAsync(cancellation: ct);
-            return;
-        }
+            if (result.ErrorMessage == "Only the owner can share this account.")
+            {
+                await SendForbiddenAsync(ct);
+                return;
+            }
 
-        if (viewer.Id == user.Id)
-        {
-            AddError(x => x.Email, "You already own this account.");
+            AddError(x => x.Email, result.ErrorMessage ?? "Unable to share this account.");
             await SendErrorsAsync(cancellation: ct);
             return;
         }
 
-        var existingRule = await db.VisibilityRules.SingleOrDefaultAsync(x => x.AccountId == account.Id && x.ViewerUserId == viewer.Id, ct);
-        if (existingRule is null)
-        {
-            db.VisibilityRules.Add(new VisibilityRule
-            {
-                AccountId = account.Id,
-                ViewerUserId = viewer.Id,
-                IsReadOnly = true,
-                CreatedAt = DateTime.UtcNow
-            });
-        }
-        else
-        {
-            existingRule.IsReadOnly = true;
-        }
-
-        await db.SaveChangesAsync(ct);
-
         await SendOkAsync(new
         {
-            AccountId = account.Id,
-            ViewerUserId = viewer.Id,
+            AccountId = request.Id,
             IsReadOnly = true
         }, ct);
     }
 }
diff --git a/src/Treasury.App/Pages/Accounts.razor b/src/Treasury.App/Pages/Accounts.razor
index b07edf4..5489b2b 100644
--- a/src/Treasury.App/Pages/Accounts.razor
+++ b/src/Treasury.App/Pages/Accounts.razor
@@ -1,14 +1,12 @@
 @page "/accounts"
 @attribute [Authorize]
-@inject TreasuryDbContext DbContext
-@inject ISnackbar Snackbar
 
 <MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="pa-4 pa-sm-6">
     <MudStack Spacing="3">
         <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center">
             <MudText Typo="Typo.h4">Accounts</MudText>
             <MudButton Variant="Variant.Filled" Color="Color.Tertiary" StartIcon="@Icons.Material.Filled.Add" OnClick="ToggleCreateForm">Add account</MudButton>
         </MudStack>
 
         @if (_showCreateForm)
         {
@@ -53,91 +51,58 @@
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
+
+                            @if (account.IsOwner)
+                            {
+                                <MudDivider Class="my-3" />
+                                <MudText Typo="Typo.subtitle2">Shared with:</MudText>
+                                @if (account.SharedWith.Count == 0)
+                                {
+                                    <MudText Typo="Typo.body2" Color="Color.Secondary">Nobody yet.</MudText>
+                                }
+                                else
+                                {
+                                    <div class="d-flex flex-wrap gap-2">
+                                        @foreach (var viewer in account.SharedWith)
+                                        {
+                                            <MudChip T="string" Color="Color.Primary" Variant="Variant.Outlined">@viewer.DisplayName</MudChip>
+                                        }
+                                    </div>
+                                }
+
+                                @if (_householdUsers.Count == 0)
+                                {
+                                    <MudAlert Class="mt-3" Severity="Severity.Info">No other household users are available to share with.</MudAlert>
+                                }
+                                else
+                                {
+                                    <MudSelect T="string" Class="mt-3" Label="Share with household user" @bind-Value="account.SelectedShareEmail">
+                                        @foreach (var user in _householdUsers)
+                                        {
+                                            <MudSelectItem Value="@user.Email">@user.DisplayName</MudSelectItem>
+                                        }
+                                    </MudSelect>
+
+                                    <MudButton Class="mt-3" Variant="Variant.Filled" Color="Color.Tertiary" OnClick="() => ShareAccountAsync(account)">
+                                        Share read-only
+                                    </MudButton>
+                                }
+                            }
+                            else
+                            {
+                                <MudAlert Class="mt-3" Severity="Severity.Info">Read-only shared account.</MudAlert>
+                            }
                         </MudPaper>
                     </MudItem>
                 }
             </MudGrid>
         }
     </MudStack>
 </MudContainer>
-
-@code {
-    private List<Treasury.App.Domain.Account> _accounts = new();
-    private bool _showCreateForm;
-    private readonly NewAccountForm _newAccount = new();
-
-    protected override async Task OnInitializedAsync()
-    {
-        await LoadAccountsAsync();
-    }
-
-    private async Task LoadAccountsAsync()
-    {
-        _accounts = await DbContext.Accounts.OrderBy(x => x.Name).ToListAsync();
-    }
-
-    private void ToggleCreateForm()
-    {
-        _showCreateForm = !_showCreateForm;
-        if (!_showCreateForm)
-        {
-            _newAccount.Name = string.Empty;
-            _newAccount.Currency = "PLN";
-            _newAccount.AccountType = "cash-wallet";
-            _newAccount.InitialBalance = 0m;
-        }
-    }
-
-    private async Task CreateAccountAsync()
-    {
-        if (string.IsNullOrWhiteSpace(_newAccount.Name))
-        {
-            Snackbar.Add("Account name is required.", Severity.Warning);
-            return;
-        }
-
-        var householdId = await DbContext.Households
-            .OrderBy(x => x.Id)
-            .Select(x => x.Id)
-            .FirstOrDefaultAsync();
-
-        var account = new Treasury.App.Domain.Account
-        {
-            HouseholdId = householdId == Guid.Empty ? Guid.Parse("11111111-1111-1111-1111-111111111111") : householdId,
-            OwnerUserId = string.Empty,
-            Name = _newAccount.Name.Trim(),
-            Currency = string.IsNullOrWhiteSpace(_newAccount.Currency) ? "PLN" : _newAccount.Currency.Trim().ToUpperInvariant(),
-            AccountType = string.IsNullOrWhiteSpace(_newAccount.AccountType) ? "cash-wallet" : _newAccount.AccountType.Trim(),
-            CurrentBalance = _newAccount.InitialBalance,
-            CreatedAt = DateTime.UtcNow,
-            UpdatedAt = DateTime.UtcNow
-        };
-
-        DbContext.Accounts.Add(account);
-        await DbContext.SaveChangesAsync();
-
-        _showCreateForm = false;
-        _newAccount.Name = string.Empty;
-        _newAccount.Currency = "PLN";
-        _newAccount.AccountType = "cash-wallet";
-        _newAccount.InitialBalance = 0m;
-
-        Snackbar.Add("Account created.", Severity.Success);
-        await LoadAccountsAsync();
-    }
-
-    private sealed class NewAccountForm
-    {
-        public string Name { get; set; } = string.Empty;
-        public string Currency { get; set; } = "PLN";
-        public string AccountType { get; set; } = "cash-wallet";
-        public decimal InitialBalance { get; set; }
-    }
-}
diff --git a/src/Treasury.App/Pages/Accounts.razor.cs b/src/Treasury.App/Pages/Accounts.razor.cs
new file mode 100644
index 0000000..920bad9
--- /dev/null
+++ b/src/Treasury.App/Pages/Accounts.razor.cs
@@ -0,0 +1,178 @@
+using Microsoft.AspNetCore.Components;
+using Microsoft.AspNetCore.Components.Authorization;
+using Microsoft.AspNetCore.Identity;
+using Microsoft.EntityFrameworkCore;
+using MudBlazor;
+using Treasury.App.Application.Accounts;
+using Treasury.App.Domain;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.App.Pages;
+
+public partial class Accounts
+{
+    [Inject] public TreasuryDbContext DbContext { get; set; } = default!;
+    [Inject] public ISnackbar Snackbar { get; set; } = default!;
+    [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
+    [Inject] public UserManager<ApplicationUser> UserManager { get; set; } = default!;
+    [Inject] public AccountSharingService AccountSharingService { get; set; } = default!;
+
+    private ApplicationUser? _currentUser;
+    private readonly List<AccountCardVm> _accounts = new();
+    private List<HouseholdUserChoice> _householdUsers = new();
+    private bool _showCreateForm;
+    private readonly NewAccountForm _newAccount = new();
+
+    protected override async Task OnInitializedAsync()
+    {
+        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
+        _currentUser = await UserManager.GetUserAsync(authState.User);
+        if (_currentUser is null)
+        {
+            _accounts.Clear();
+            _householdUsers.Clear();
+            return;
+        }
+
+        await LoadAccountsAsync();
+        await LoadHouseholdUsersAsync();
+    }
+
+    private async Task LoadAccountsAsync()
+    {
+        if (_currentUser is null)
+        {
+            _accounts.Clear();
+            return;
+        }
+
+        var accounts = await AccountSharingService.GetVisibleAccountsAsync(_currentUser, CancellationToken.None);
+        var sharedViewers = new Dictionary<Guid, List<AccountViewerChoice>>();
+
+        foreach (var account in accounts)
+        {
+            sharedViewers[account.Id] = await AccountSharingService.GetSharedViewersAsync(_currentUser, account.Id, CancellationToken.None);
+        }
+
+        _accounts.Clear();
+        _accounts.AddRange(accounts.Select(account => new AccountCardVm
+        {
+            Id = account.Id,
+            Name = account.Name,
+            Currency = account.Currency,
+            AccountType = account.AccountType,
+            CurrentBalance = account.CurrentBalance,
+            IsOwner = account.OwnerUserId == _currentUser.Id,
+            SharedWith = sharedViewers.TryGetValue(account.Id, out var viewers) ? viewers : new List<AccountViewerChoice>()
+        }));
+    }
+
+    private async Task LoadHouseholdUsersAsync()
+    {
+        if (_currentUser is null)
+        {
+            _householdUsers = new List<HouseholdUserChoice>();
+            return;
+        }
+
+        _householdUsers = await AccountSharingService.GetHouseholdUsersAsync(_currentUser, CancellationToken.None);
+    }
+
+    private void ToggleCreateForm()
+    {
+        _showCreateForm = !_showCreateForm;
+        if (!_showCreateForm)
+        {
+            ResetCreateForm();
+        }
+    }
+
+    private async Task CreateAccountAsync()
+    {
+        if (_currentUser is null)
+        {
+            Snackbar.Add("Sign in first.", Severity.Warning);
+            return;
+        }
+
+        if (string.IsNullOrWhiteSpace(_newAccount.Name))
+        {
+            Snackbar.Add("Account name is required.", Severity.Warning);
+            return;
+        }
+
+        var account = new Treasury.App.Domain.Account
+        {
+            HouseholdId = _currentUser.HouseholdId,
+            OwnerUserId = _currentUser.Id,
+            Name = _newAccount.Name.Trim(),
+            Currency = string.IsNullOrWhiteSpace(_newAccount.Currency) ? "PLN" : _newAccount.Currency.Trim().ToUpperInvariant(),
+            AccountType = string.IsNullOrWhiteSpace(_newAccount.AccountType) ? "cash-wallet" : _newAccount.AccountType.Trim(),
+            CurrentBalance = _newAccount.InitialBalance,
+            CreatedAt = DateTime.UtcNow,
+            UpdatedAt = DateTime.UtcNow
+        };
+
+        DbContext.Accounts.Add(account);
+        await DbContext.SaveChangesAsync();
+
+        _showCreateForm = false;
+        ResetCreateForm();
+
+        Snackbar.Add("Account created.", Severity.Success);
+        await LoadAccountsAsync();
+    }
+
+    private async Task ShareAccountAsync(AccountCardVm account)
+    {
+        if (_currentUser is null)
+        {
+            Snackbar.Add("Sign in first.", Severity.Warning);
+            return;
+        }
+
+        if (string.IsNullOrWhiteSpace(account.SelectedShareEmail))
+        {
+            Snackbar.Add("Choose a household user first.", Severity.Warning);
+            return;
+        }
+
+        var result = await AccountSharingService.ShareReadOnlyAsync(_currentUser, account.Id, account.SelectedShareEmail, CancellationToken.None);
+        if (!result.Succeeded)
+        {
+            Snackbar.Add(result.ErrorMessage ?? "Unable to share this account.", Severity.Error);
+            return;
+        }
+
+        Snackbar.Add("Account shared read-only.", Severity.Success);
+        await LoadAccountsAsync();
+    }
+
+    private void ResetCreateForm()
+    {
+        _newAccount.Name = string.Empty;
+        _newAccount.Currency = "PLN";
+        _newAccount.AccountType = "cash-wallet";
+        _newAccount.InitialBalance = 0m;
+    }
+
+    private sealed class NewAccountForm
+    {
+        public string Name { get; set; } = string.Empty;
+        public string Currency { get; set; } = "PLN";
+        public string AccountType { get; set; } = "cash-wallet";
+        public decimal InitialBalance { get; set; }
+    }
+
+    private sealed class AccountCardVm
+    {
+        public Guid Id { get; set; }
+        public string Name { get; set; } = string.Empty;
+        public string Currency { get; set; } = string.Empty;
+        public string AccountType { get; set; } = string.Empty;
+        public decimal CurrentBalance { get; set; }
+        public bool IsOwner { get; set; }
+        public string SelectedShareEmail { get; set; } = string.Empty;
+        public List<AccountViewerChoice> SharedWith { get; set; } = new();
+    }
+}
diff --git a/src/Treasury.App/Program.cs b/src/Treasury.App/Program.cs
index d408711..484429e 100644
--- a/src/Treasury.App/Program.cs
+++ b/src/Treasury.App/Program.cs
@@ -1,17 +1,18 @@
 using System.Text.Json;
 using FastEndpoints;
 using Microsoft.AspNetCore.Components.Authorization;
 using Microsoft.AspNetCore.Identity;
 using Microsoft.EntityFrameworkCore;
 using MudBlazor.Services;
 using Treasury.App.Application.Dashboard;
+using Treasury.App.Application.Accounts;
 using Treasury.App.Components;
 using Treasury.App.Contracts.Accounts;
 using Treasury.App.Contracts.Bills;
 using Treasury.App.Contracts.Budgets;
 using Treasury.App.Contracts.Rates;
 using Treasury.App.Contracts.Tags;
 using Treasury.App.Contracts.Transactions;
 using Treasury.App.Contracts.Valuations;
 using Treasury.App.Application.Valuations;
 using Treasury.App.Domain;
@@ -84,20 +85,21 @@ builder.Services.AddAuthorization(options =>
 {
     options.AddPolicy(Policies.OwnerOnly, policy => policy.RequireAuthenticatedUser());
     options.AddPolicy(Policies.SharedReadOnly, policy => policy.RequireAuthenticatedUser());
 });
 
 builder.Services.AddHealthChecks();
 builder.Services.AddRazorComponents().AddInteractiveServerComponents();
 builder.Services.AddCascadingAuthenticationState();
 builder.Services.AddFastEndpoints();
 builder.Services.AddMudServices();
+builder.Services.AddScoped<AccountSharingService>();
 
 var app = builder.Build();
 
 using (var scope = app.Services.CreateScope())
 {
     var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
 
     if (db.Database.IsRelational())
     {
         await db.Database.MigrateAsync();
