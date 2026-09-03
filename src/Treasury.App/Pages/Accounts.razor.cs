using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using Treasury.App.Application.Accounts;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Pages;

public partial class Accounts
{
    [Inject] public TreasuryDbContext DbContext { get; set; } = default!;
    [Inject] public ISnackbar Snackbar { get; set; } = default!;
    [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] public UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] public AccountSharingService AccountSharingService { get; set; } = default!;

    private ApplicationUser? _currentUser;
    private readonly List<AccountCardVm> _accounts = new();
    private List<HouseholdUserChoice> _householdUsers = new();
    private bool _showCreateForm;
    private readonly NewAccountForm _newAccount = new();

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _currentUser = await UserManager.GetUserAsync(authState.User);
        if (_currentUser is null)
        {
            _accounts.Clear();
            _householdUsers.Clear();
            return;
        }

        await LoadAccountsAsync();
        await LoadHouseholdUsersAsync();
    }

    private async Task LoadAccountsAsync()
    {
        if (_currentUser is null)
        {
            _accounts.Clear();
            return;
        }

        var accounts = await AccountSharingService.GetVisibleAccountsAsync(_currentUser, CancellationToken.None);
        var sharedViewers = new Dictionary<Guid, List<AccountViewerChoice>>();

        foreach (var account in accounts)
        {
            sharedViewers[account.Id] = await AccountSharingService.GetSharedViewersAsync(_currentUser, account.Id, CancellationToken.None);
        }

        _accounts.Clear();
        _accounts.AddRange(accounts.Select(account => new AccountCardVm
        {
            Id = account.Id,
            Name = account.Name,
            Currency = account.Currency,
            AccountType = account.AccountType,
            CurrentBalance = account.CurrentBalance,
            IsOwner = account.OwnerUserId == _currentUser.Id,
            SharedWith = sharedViewers.TryGetValue(account.Id, out var viewers) ? viewers : new List<AccountViewerChoice>()
        }));
    }

    private async Task LoadHouseholdUsersAsync()
    {
        if (_currentUser is null)
        {
            _householdUsers = new List<HouseholdUserChoice>();
            return;
        }

        _householdUsers = await AccountSharingService.GetHouseholdUsersAsync(_currentUser, CancellationToken.None);
    }

    private void ToggleCreateForm()
    {
        _showCreateForm = !_showCreateForm;
        if (!_showCreateForm)
        {
            ResetCreateForm();
        }
    }

    private async Task CreateAccountAsync()
    {
        if (_currentUser is null)
        {
            Snackbar.Add("Sign in first.", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_newAccount.Name))
        {
            Snackbar.Add("Account name is required.", Severity.Warning);
            return;
        }

        var account = new Treasury.App.Domain.Account
        {
            HouseholdId = _currentUser.HouseholdId,
            OwnerUserId = _currentUser.Id,
            Name = _newAccount.Name.Trim(),
            Currency = string.IsNullOrWhiteSpace(_newAccount.Currency) ? "PLN" : _newAccount.Currency.Trim().ToUpperInvariant(),
            AccountType = string.IsNullOrWhiteSpace(_newAccount.AccountType) ? "cash-wallet" : _newAccount.AccountType.Trim(),
            CurrentBalance = _newAccount.InitialBalance,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        DbContext.Accounts.Add(account);
        await DbContext.SaveChangesAsync();

        _showCreateForm = false;
        ResetCreateForm();

        Snackbar.Add("Account created.", Severity.Success);
        await LoadAccountsAsync();
    }

    private async Task ShareAccountAsync(AccountCardVm account)
    {
        if (_currentUser is null)
        {
            Snackbar.Add("Sign in first.", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(account.SelectedShareEmail))
        {
            Snackbar.Add("Choose a household user first.", Severity.Warning);
            return;
        }

        var result = await AccountSharingService.ShareReadOnlyAsync(_currentUser, account.Id, account.SelectedShareEmail, CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(result.ErrorMessage ?? "Unable to share this account.", Severity.Error);
            return;
        }

        Snackbar.Add("Account shared read-only.", Severity.Success);
        await LoadAccountsAsync();
    }

    private void ResetCreateForm()
    {
        _newAccount.Name = string.Empty;
        _newAccount.Currency = "PLN";
        _newAccount.AccountType = "cash-wallet";
        _newAccount.InitialBalance = 0m;
    }

    private sealed class NewAccountForm
    {
        public string Name { get; set; } = string.Empty;
        public string Currency { get; set; } = "PLN";
        public string AccountType { get; set; } = "cash-wallet";
        public decimal InitialBalance { get; set; }
    }

    private sealed class AccountCardVm
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public bool IsOwner { get; set; }
        public string SelectedShareEmail { get; set; } = string.Empty;
        public List<AccountViewerChoice> SharedWith { get; set; } = new();
    }
}
