# Final Review Package

Base: 0bb45f0435a7cc6968a4439b5b90bf034bd95424
Head: 42eb509a56b7406ece91741ef2713beea64fe616

## Commits

42eb509 Fix Task 3: assert owner-only balance-correction endpoint is forbidden for shared users\n\nReplace ineffective PUT /api/accounts/{id} check with POST /api/accounts/{id}/balance-correction assertion
fa62627 chore: untrack .superpowers/sdd/task-3-report.md (Task 3 cleanup)
21ccf47 test: cover account sharing UI and permissions
25501b2 test: cover account sharing ui and permissions
dab7ac4 fix: harden accounts sharing loads
2631764 feat: add inline account sharing ui
875b9a2 chore: untrack workflow artifact .superpowers/sdd/task-1-report.md
64db3f2 test: strengthen accounts sharing ui assertions (picker semantics; no manual email entry)
026e307 docs: append Task 1 fix report summary
8ad50dd test: strengthen accounts sharing ui test (verify shared-client behavior, household picker; dispose JsonDocument)\n\nCo-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
2a0620c test: add regression for account sharing ui

## Diff Stat

 .gitignore                                         |   3 +
 .superpowers/sdd/task-2-report.md                  |  28 ++
 .../Application/Accounts/AccountSharingService.cs  | 108 ++++++++
 .../Accounts/ShareAccountReadOnlyEndpoint.cs       |  68 ++---
 src/Treasury.App/Pages/Accounts.razor              | 131 ++++-----
 src/Treasury.App/Pages/Accounts.razor.cs           | 219 +++++++++++++++
 src/Treasury.App/Program.cs                        |   2 +
 .../AccountsSharingUiTests.cs                      | 301 +++++++++++++++++++++
 .../SharedReadOnlyUiPermissionTests.cs             |  10 +
 9 files changed, 742 insertions(+), 128 deletions(-)

## Full Diff (-U10)

diff --git a/.gitignore b/.gitignore
index e40f379..e43aad8 100644
--- a/.gitignore
+++ b/.gitignore
@@ -1,7 +1,10 @@
 bin/
 obj/
 .vs/
 *.user
 *.suo
 .worktrees/
 worktrees/
+
+# Workflow artifacts produced by superpowers sessions
+.superpowers/sdd/task-1-report.md
diff --git a/.superpowers/sdd/task-2-report.md b/.superpowers/sdd/task-2-report.md
new file mode 100644
index 0000000..da7f968
--- /dev/null
+++ b/.superpowers/sdd/task-2-report.md
@@ -0,0 +1,28 @@
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
+
+## Review Fixes
+- Split accounts and household-user loading into separate, scoped error handlers so one failure no longer crashes the whole page.
+- Added a batched shared-viewers query and grouped the results by account to remove the per-account N+1 loop.
+- Added integration coverage for household-user load failure, shared-viewer load failure, and batched shared-viewer loading.
+
+## Validation
+- Passed: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsSharingUiTests|FullyQualifiedName~SharedReadOnlyUiPermissionTests"`
+- Passed: `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug`
diff --git a/src/Treasury.App/Application/Accounts/AccountSharingService.cs b/src/Treasury.App/Application/Accounts/AccountSharingService.cs
new file mode 100644
index 0000000..588c9af
--- /dev/null
+++ b/src/Treasury.App/Application/Accounts/AccountSharingService.cs
@@ -0,0 +1,108 @@
+using Microsoft.AspNetCore.Identity;
+using Microsoft.EntityFrameworkCore;
+using Treasury.App.Domain;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.App.Application.Accounts;
+
+public sealed record HouseholdUserChoice(string Email, string DisplayName);
+public sealed record AccountViewerChoice(string ViewerUserId, string Email, string DisplayName);
+public sealed record AccountViewerAssignment(Guid AccountId, string ViewerUserId, string Email, string DisplayName);
+public sealed record ShareReadOnlyResult(bool Succeeded, string? ErrorMessage);
+
+public class AccountSharingService(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+{
+    public virtual Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct) =>
+        db.Accounts
+            .Where(x =>
+                x.HouseholdId == user.HouseholdId
+                && (x.OwnerUserId == user.Id
+                    || x.OwnerUserId == "seed"
+                    || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
+            .OrderBy(x => x.Name)
+            .ToListAsync(ct);
+
+    public virtual Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
+        db.Users
+            .Where(x => x.HouseholdId == user.HouseholdId && x.Id != user.Id)
+            .OrderBy(x => x.Email ?? x.UserName ?? x.Id)
+            .Select(x => new HouseholdUserChoice(
+                x.Email ?? x.UserName ?? x.Id,
+                x.Email ?? x.UserName ?? x.Id))
+            .ToListAsync(ct);
+
+    public virtual Task<List<AccountViewerChoice>> GetSharedViewersAsync(ApplicationUser user, Guid accountId, CancellationToken ct) =>
+        (from rule in db.VisibilityRules
+         join viewer in db.Users on rule.ViewerUserId equals viewer.Id
+         where rule.AccountId == accountId && viewer.HouseholdId == user.HouseholdId
+         orderby viewer.Email ?? viewer.UserName ?? viewer.Id
+         select new AccountViewerChoice(
+             viewer.Id,
+             viewer.Email ?? viewer.UserName ?? viewer.Id,
+             viewer.Email ?? viewer.UserName ?? viewer.Id)).ToListAsync(ct);
+
+    public virtual async Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
+    {
+        var distinctAccountIds = accountIds.Distinct().ToArray();
+        if (distinctAccountIds.Length == 0)
+        {
+            return [];
+        }
+
+        return await (from rule in db.VisibilityRules
+                join viewer in db.Users on rule.ViewerUserId equals viewer.Id
+                where distinctAccountIds.Contains(rule.AccountId) && viewer.HouseholdId == user.HouseholdId
+                orderby rule.AccountId, viewer.Email ?? viewer.UserName ?? viewer.Id
+                select new AccountViewerAssignment(
+                    rule.AccountId,
+                    viewer.Id,
+                    viewer.Email ?? viewer.UserName ?? viewer.Id,
+                    viewer.Email ?? viewer.UserName ?? viewer.Id))
+            .ToListAsync(ct);
+    }
+
+    public virtual async Task<ShareReadOnlyResult> ShareReadOnlyAsync(ApplicationUser user, Guid accountId, string viewerEmail, CancellationToken ct)
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
index b07edf4..d23ce97 100644
--- a/src/Treasury.App/Pages/Accounts.razor
+++ b/src/Treasury.App/Pages/Accounts.razor
@@ -1,22 +1,30 @@
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
 
+        @if (!string.IsNullOrWhiteSpace(_accountsLoadError))
+        {
+            <MudAlert Severity="Severity.Error">@_accountsLoadError</MudAlert>
+        }
+
+        @if (!string.IsNullOrWhiteSpace(_householdUsersLoadError))
+        {
+            <MudAlert Severity="Severity.Error">@_householdUsersLoadError</MudAlert>
+        }
+
         @if (_showCreateForm)
         {
             <MudPaper Class="pa-4 rounded-xl" Elevation="2">
                 <MudGrid>
                     <MudItem xs="12" md="4">
                         <MudTextField Label="Account name" @bind-Value="_newAccount.Name" Required="true" />
                     </MudItem>
                     <MudItem xs="12" md="3">
                         <MudSelect T="string" Label="Currency" @bind-Value="_newAccount.Currency">
                             <MudSelectItem Value="@("PLN")">PLN</MudSelectItem>
@@ -53,91 +61,62 @@
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
+                                @if (!string.IsNullOrWhiteSpace(_householdUsersLoadError))
+                                {
+                                    <MudAlert Class="mt-3" Severity="Severity.Error">Unable to load household users.</MudAlert>
+                                }
+                                else if (_householdUsers.Count == 0)
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
index 0000000..12ea949
--- /dev/null
+++ b/src/Treasury.App/Pages/Accounts.razor.cs
@@ -0,0 +1,219 @@
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
+    private string? _accountsLoadError;
+    private string? _householdUsersLoadError;
+    private bool _showCreateForm;
+    private readonly NewAccountForm _newAccount = new();
+
+    protected override async Task OnInitializedAsync()
+    {
+        _accountsLoadError = null;
+        _householdUsersLoadError = null;
+
+        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
+        _currentUser = await UserManager.GetUserAsync(authState.User);
+        if (_currentUser is null)
+        {
+            _accounts.Clear();
+            _householdUsers.Clear();
+            return;
+        }
+
+        await Task.WhenAll(LoadAccountsAsync(), LoadHouseholdUsersAsync());
+    }
+
+    private async Task LoadAccountsAsync()
+    {
+        if (_currentUser is null)
+        {
+            _accounts.Clear();
+            _accountsLoadError = null;
+            return;
+        }
+
+        try
+        {
+            _accountsLoadError = null;
+            var accounts = await AccountSharingService.GetVisibleAccountsAsync(_currentUser, CancellationToken.None);
+            Dictionary<Guid, List<AccountViewerChoice>> viewersByAccountId;
+
+            try
+            {
+                var sharedViewers = await AccountSharingService.GetSharedViewersAsync(
+                    _currentUser,
+                    accounts.Select(account => account.Id).ToArray(),
+                    CancellationToken.None);
+
+                viewersByAccountId = sharedViewers
+                    .GroupBy(viewer => viewer.AccountId)
+                    .ToDictionary(group => group.Key, group => group
+                        .Select(viewer => new AccountViewerChoice(viewer.ViewerUserId, viewer.Email, viewer.DisplayName))
+                        .ToList());
+            }
+            catch (Exception ex)
+            {
+                viewersByAccountId = new Dictionary<Guid, List<AccountViewerChoice>>();
+                _accountsLoadError = "Unable to load shared viewer details.";
+                Snackbar.Add($"Unable to load shared viewer details: {ex.Message}", Severity.Error);
+            }
+
+            _accounts.Clear();
+            _accounts.AddRange(accounts.Select(account => new AccountCardVm
+            {
+                Id = account.Id,
+                Name = account.Name,
+                Currency = account.Currency,
+                AccountType = account.AccountType,
+                CurrentBalance = account.CurrentBalance,
+                IsOwner = account.OwnerUserId == _currentUser.Id,
+                SharedWith = viewersByAccountId.TryGetValue(account.Id, out var viewers) ? viewers : new List<AccountViewerChoice>()
+            }));
+        }
+        catch (Exception ex)
+        {
+            _accounts.Clear();
+            _accountsLoadError = "Unable to load accounts.";
+            Snackbar.Add($"Unable to load accounts: {ex.Message}", Severity.Error);
+        }
+    }
+
+    private async Task LoadHouseholdUsersAsync()
+    {
+        if (_currentUser is null)
+        {
+            _householdUsers = new List<HouseholdUserChoice>();
+            _householdUsersLoadError = null;
+            return;
+        }
+
+        try
+        {
+            _householdUsers = await AccountSharingService.GetHouseholdUsersAsync(_currentUser, CancellationToken.None);
+            _householdUsersLoadError = null;
+        }
+        catch (Exception ex)
+        {
+            _householdUsers = new List<HouseholdUserChoice>();
+            _householdUsersLoadError = "Unable to load household users.";
+            Snackbar.Add($"Unable to load household users: {ex.Message}", Severity.Error);
+        }
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
diff --git a/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
new file mode 100644
index 0000000..5455833
--- /dev/null
+++ b/tests/Treasury.IntegrationTests/AccountsSharingUiTests.cs
@@ -0,0 +1,301 @@
+using System.Net;
+using System.Net.Http.Json;
+using FluentAssertions;
+using Microsoft.AspNetCore.Mvc.Testing;
+using Microsoft.Extensions.DependencyInjection;
+using Microsoft.Extensions.DependencyInjection.Extensions;
+using System.Text.Json;
+using Treasury.App.Application.Accounts;
+using Treasury.App.Domain;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.IntegrationTests;
+
+public class AccountsSharingUiTests
+{
+    [Fact]
+    public async Task Accounts_Page_Shows_Shared_Users_And_Household_Picker()
+    {
+        await using var app = new TreasuryHostFactory();
+
+        var ownerClient = app.CreateClient(new WebApplicationFactoryClientOptions
+        {
+            AllowAutoRedirect = false,
+            HandleCookies = true
+        });
+        var sharedClient = app.CreateClient(new WebApplicationFactoryClientOptions
+        {
+            AllowAutoRedirect = false,
+            HandleCookies = true
+        });
+
+        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
+        var sharedEmail = $"shared-{Guid.NewGuid():N}@example.com";
+        const string password = "Password123!";
+
+        await RegisterAndSignInAsync(ownerClient, ownerEmail, password);
+        await RegisterAndSignInAsync(sharedClient, sharedEmail, password);
+
+        var accountResponse = await ownerClient.PostAsJsonAsync("/api/accounts", new
+        {
+            Name = "Shared account",
+            Currency = "PLN",
+            AccountType = "cash-wallet"
+        });
+        accountResponse.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        using var accountJson = JsonDocument.Parse(await accountResponse.Content.ReadAsStringAsync());
+        var accountId = accountJson.RootElement.GetProperty("id").GetGuid();
+
+        var shareResponse = await ownerClient.PostAsJsonAsync($"/api/accounts/{accountId}/share-readonly", new
+        {
+            Email = sharedEmail
+        });
+        shareResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        // Owner's accounts page should show shared-user and (eventually) a household picker in the UI
+        var pageResponse = await ownerClient.GetAsync("/accounts");
+        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        var body = await pageResponse.Content.ReadAsStringAsync();
+        // Core expectations preserved from the brief
+        body.Should().Contain("Shared with:");
+        body.Should().Contain(sharedEmail);
+        // Explicit picker semantics expected in the UI
+        body.Should().Contain("Share with household user");
+        body.Should().Contain("Share read-only");
+        body.Should().NotContain(ownerEmail);
+        // Explicit picker exclusion check — the owner's email should not appear as a selectable household option
+        body.Should().NotContain($"<option value=\"{ownerEmail}\"");
+
+        // No manual email-entry should be present for household sharing
+        body.Should().NotContain("input type=\"email\"");
+        body.Should().NotContain("Enter email");
+        body.Should().NotContain("Invite by email");
+
+        // Verify shared user can see the shared account but does not see owner-only share controls
+        var sharedPageResponse = await sharedClient.GetAsync("/accounts");
+        sharedPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+        var sharedBody = await sharedPageResponse.Content.ReadAsStringAsync();
+        sharedBody.Should().Contain("Shared account");
+        // Shared users should not see the 'Share read-only' control for accounts they only have read access to
+        sharedBody.Should().NotContain("Share read-only");
+    }
+
+    [Fact]
+    public async Task Accounts_Page_Renders_When_Household_Users_Fail_To_Load()
+    {
+        await using var app = new AccountsPageHostFactory(new HouseholdUsersFailingAccountSharingService());
+
+        var client = app.CreateClient(new WebApplicationFactoryClientOptions
+        {
+            AllowAutoRedirect = false,
+            HandleCookies = true
+        });
+
+        await RegisterAndSignInAsync(client, $"owner-{Guid.NewGuid():N}@example.com", "Password123!");
+
+        var pageResponse = await client.GetAsync("/accounts");
+        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        var body = await pageResponse.Content.ReadAsStringAsync();
+        body.Should().Contain("Loaded account");
+        body.Should().Contain("Unable to load household users.");
+    }
+
+    [Fact]
+    public async Task Accounts_Page_Renders_Accounts_When_Shared_Viewers_Fail_To_Load()
+    {
+        await using var app = new AccountsPageHostFactory(new SharedViewersFailingAccountSharingService());
+
+        var client = app.CreateClient(new WebApplicationFactoryClientOptions
+        {
+            AllowAutoRedirect = false,
+            HandleCookies = true
+        });
+
+        await RegisterAndSignInAsync(client, $"owner-{Guid.NewGuid():N}@example.com", "Password123!");
+
+        var pageResponse = await client.GetAsync("/accounts");
+        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        var body = await pageResponse.Content.ReadAsStringAsync();
+        body.Should().Contain("Loaded account");
+        body.Should().Contain("Unable to load shared viewer details.");
+    }
+
+    [Fact]
+    public async Task Accounts_Page_Loads_Shared_Viewers_In_One_Batch()
+    {
+        var service = new BatchedSharedViewerAccountSharingService();
+        await using var app = new AccountsPageHostFactory(service);
+
+        var client = app.CreateClient(new WebApplicationFactoryClientOptions
+        {
+            AllowAutoRedirect = false,
+            HandleCookies = true
+        });
+
+        await RegisterAndSignInAsync(client, $"owner-{Guid.NewGuid():N}@example.com", "Password123!");
+
+        var pageResponse = await client.GetAsync("/accounts");
+        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        var body = await pageResponse.Content.ReadAsStringAsync();
+        body.Should().Contain("Account A");
+        body.Should().Contain("Viewer One");
+        body.Should().Contain("Viewer Two");
+        service.SharedViewerBatchCalls.Should().Be(1);
+        service.SharedViewerSingleCalls.Should().Be(0);
+    }
+
+    private static async Task RegisterAndSignInAsync(HttpClient client, string email, string password)
+    {
+        var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
+        {
+            Email = email,
+            Password = password,
+            ConfirmPassword = password
+        });
+
+        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
+
+        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
+        {
+            Email = email,
+            Password = password
+        });
+
+        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
+    }
+
+    private sealed class AccountsPageHostFactory(AccountSharingService accountSharingService) : TreasuryHostFactory
+    {
+        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
+        {
+            base.ConfigureWebHost(builder);
+
+            builder.ConfigureServices(services =>
+            {
+                services.RemoveAll<AccountSharingService>();
+                services.AddSingleton(accountSharingService);
+            });
+        }
+    }
+
+    private sealed class HouseholdUsersFailingAccountSharingService() : AccountSharingService(null!, null!)
+    {
+        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct) =>
+            Task.FromResult(new List<Account>
+            {
+                new()
+                {
+                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
+                    HouseholdId = user.HouseholdId,
+                    OwnerUserId = user.Id,
+                    Name = "Loaded account",
+                    Currency = "PLN",
+                    AccountType = "cash-wallet",
+                    CurrentBalance = 12.34m
+                }
+            });
+
+        public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
+            throw new InvalidOperationException("household users query failed");
+
+        public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct) =>
+            Task.FromResult(new List<AccountViewerAssignment>
+            {
+                new(
+                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
+                    user.Id,
+                    "viewer@example.com",
+                    "Viewer One")
+            });
+    }
+
+    private sealed class SharedViewersFailingAccountSharingService() : AccountSharingService(null!, null!)
+    {
+        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct) =>
+            Task.FromResult(new List<Account>
+            {
+                new()
+                {
+                    Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
+                    HouseholdId = user.HouseholdId,
+                    OwnerUserId = user.Id,
+                    Name = "Loaded account",
+                    Currency = "PLN",
+                    AccountType = "cash-wallet",
+                    CurrentBalance = 12.34m
+                }
+            });
+
+        public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
+            Task.FromResult(new List<HouseholdUserChoice>
+            {
+                new("viewer@example.com", "Viewer")
+            });
+
+        public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct) =>
+            throw new InvalidOperationException("shared viewers query failed");
+    }
+
+    private sealed class BatchedSharedViewerAccountSharingService() : AccountSharingService(null!, null!)
+    {
+        public int SharedViewerBatchCalls { get; private set; }
+        public int SharedViewerSingleCalls { get; private set; }
+
+        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct) =>
+            Task.FromResult(new List<Account>
+            {
+                new()
+                {
+                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
+                    HouseholdId = user.HouseholdId,
+                    OwnerUserId = user.Id,
+                    Name = "Account A",
+                    Currency = "PLN",
+                    AccountType = "cash-wallet",
+                    CurrentBalance = 1m
+                },
+                new()
+                {
+                    Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
+                    HouseholdId = user.HouseholdId,
+                    OwnerUserId = user.Id,
+                    Name = "Account B",
+                    Currency = "PLN",
+                    AccountType = "cash-wallet",
+                    CurrentBalance = 2m
+                }
+            });
+
+        public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
+            Task.FromResult(new List<HouseholdUserChoice>
+            {
+                new("viewer-one@example.com", "Viewer One"),
+                new("viewer-two@example.com", "Viewer Two")
+            });
+
+        public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
+        {
+            SharedViewerBatchCalls++;
+
+            var sharedViewers = new List<AccountViewerAssignment>();
+            foreach (var accountId in accountIds)
+            {
+                sharedViewers.Add(new AccountViewerAssignment(accountId, "viewer-1", "viewer-one@example.com", "Viewer One"));
+                sharedViewers.Add(new AccountViewerAssignment(accountId, "viewer-2", "viewer-two@example.com", "Viewer Two"));
+            }
+
+            return Task.FromResult(sharedViewers);
+        }
+
+        public override Task<List<AccountViewerChoice>> GetSharedViewersAsync(ApplicationUser user, Guid accountId, CancellationToken ct)
+        {
+            SharedViewerSingleCalls++;
+            throw new InvalidOperationException("single-account shared viewer query should not be used");
+        }
+    }
+}
diff --git a/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs b/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
index 4998e29..c0f28bf 100644
--- a/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
+++ b/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
@@ -59,20 +59,30 @@ public class SharedReadOnlyUiPermissionTests
             AccountId = accountId,
             Description = "Attempt by shared user",
             Category = "General",
             Amount = 10m,
             Currency = "PLN",
             Type = "expense",
             TransactionDate = DateTime.UtcNow
         });
 
         postTransaction.StatusCode.Should().Be(HttpStatusCode.Forbidden);
+
+        // Additional guard: shared users must not be able to perform owner-only mutations such as balance correction
+        var postBalanceCorrection = await sharedClient.PostAsJsonAsync($"/api/accounts/{accountId}/balance-correction", new
+        {
+            Amount = 100.00m,
+            Reason = "Malicious correction by shared user"
+        });
+
+        // Expect that the mutation is forbidden for shared users
+        postBalanceCorrection.StatusCode.Should().Be(HttpStatusCode.Forbidden);
     }
 
     private static async Task RegisterAndSignInAsync(HttpClient client, string email, string password)
     {
         var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
         {
             Email = email,
             Password = password,
             ConfirmPassword = password
         });
