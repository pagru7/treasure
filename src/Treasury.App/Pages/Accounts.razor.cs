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
    private string? _accountsLoadError;
    private string? _householdUsersLoadError;
    private bool _showCreateForm;
    private bool _showInactive;
    private readonly NewAccountForm _newAccount = new();

    protected override async Task OnInitializedAsync()
    {
        _accountsLoadError = null;
        _householdUsersLoadError = null;

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
            _accountsLoadError = null;
            return;
        }

        try
        {
            _accountsLoadError = null;
            var accounts = _showInactive
                ? await AccountSharingService.GetVisibleAccountsAsync(_currentUser, includeInactive: true, CancellationToken.None)
                : await AccountSharingService.GetVisibleAccountsAsync(_currentUser, CancellationToken.None);
            Dictionary<Guid, List<AccountViewerChoice>> viewersByAccountId;
            var sharedViewerLoadFailed = false;

            try
            {
                var sharedViewers = await AccountSharingService.GetSharedViewersAsync(
                    _currentUser,
                    accounts.Select(account => account.Id).ToArray(),
                    CancellationToken.None);

                viewersByAccountId = sharedViewers
                    .GroupBy(viewer => viewer.AccountId)
                    .ToDictionary(group => group.Key, group => group
                        .Select(viewer => new AccountViewerChoice(viewer.ViewerUserId, viewer.Email, viewer.DisplayName))
                        .ToList());
            }
            catch (Exception ex)
            {
                viewersByAccountId = new Dictionary<Guid, List<AccountViewerChoice>>();
                sharedViewerLoadFailed = true;
                _accountsLoadError = "Unable to load shared viewer details.";
                Snackbar.Add($"Unable to load shared viewer details: {ex.Message}", Severity.Error);
            }

            _accounts.Clear();
            _accounts.AddRange(accounts.Select(account => new AccountCardVm
            {
                Id = account.Id,
                Name = account.Name,
                Currency = account.Currency,
                AccountType = account.AccountType,
                CurrentBalance = account.CurrentBalance,
                IsActive = account.IsActive,
                BankAccountNumber = account.BankAccountNumber,
                IsOwner = account.OwnerUserId == _currentUser.Id,
                EditingName = account.Name,
                EditingBankAccountNumber = account.BankAccountNumber,
                SharedWith = viewersByAccountId.TryGetValue(account.Id, out var viewers) ? viewers : new List<AccountViewerChoice>(),
                SharedWithLoadFailed = sharedViewerLoadFailed
            }));
        }
        catch (Exception ex)
        {
            _accounts.Clear();
            _accountsLoadError = "Unable to load accounts.";
            Snackbar.Add($"Unable to load accounts: {ex.Message}", Severity.Error);
        }
    }

    private async Task LoadHouseholdUsersAsync()
    {
        if (_currentUser is null)
        {
            _householdUsers = new List<HouseholdUserChoice>();
            _householdUsersLoadError = null;
            return;
        }

        try
        {
            _householdUsers = await AccountSharingService.GetHouseholdUsersAsync(_currentUser, CancellationToken.None);
            _householdUsersLoadError = null;
        }
        catch (Exception ex)
        {
            _householdUsers = new List<HouseholdUserChoice>();
            _householdUsersLoadError = "Unable to load household users.";
            Snackbar.Add($"Unable to load household users: {ex.Message}", Severity.Error);
        }
    }

    private void ToggleCreateForm()
    {
        _showCreateForm = !_showCreateForm;
        if (!_showCreateForm)
        {
            ResetCreateForm();
        }
    }

    private async Task OnShowInactiveChanged(bool value)
    {
        _showInactive = value;
        await LoadAccountsAsync();
    }

    private async Task ToggleActiveStateAsync(AccountCardVm account)
    {
        if (_currentUser is null || !account.IsOwner)
        {
            Snackbar.Add("Only the account owner can change account status.", Severity.Warning);
            return;
        }

        var entity = await DbContext.Accounts.SingleOrDefaultAsync(x => x.Id == account.Id && x.HouseholdId == _currentUser.HouseholdId, CancellationToken.None);
        if (entity is null)
        {
            Snackbar.Add("Account not found.", Severity.Error);
            return;
        }

        var deactivating = entity.IsActive;
        entity.IsActive = !entity.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await DbContext.SaveChangesAsync();

        if (deactivating && !_showInactive)
        {
            _showInactive = true;
            Snackbar.Add("Account deactivated. Showing inactive accounts so it remains visible.", Severity.Success);
        }
        else
        {
            Snackbar.Add(entity.IsActive ? "Account reactivated." : "Account deactivated.", Severity.Success);
        }

        await LoadAccountsAsync();
    }

    private async Task DeleteAccountAsync(AccountCardVm account)
    {
        if (_currentUser is null || !account.IsOwner)
        {
            Snackbar.Add("Only the account owner can remove an account.", Severity.Warning);
            return;
        }

        var entity = await DbContext.Accounts.SingleOrDefaultAsync(x => x.Id == account.Id && x.HouseholdId == _currentUser.HouseholdId, CancellationToken.None);
        if (entity is null)
        {
            Snackbar.Add("Account not found.", Severity.Error);
            return;
        }

        var hasTransactions = await DbContext.Transactions.AnyAsync(x => x.AccountId == entity.Id, CancellationToken.None);
        if (hasTransactions && entity.CurrentBalance != 0m)
        {
            Snackbar.Add("Cannot remove account with transaction history and non-zero balance. Deactivate it instead.", Severity.Error);
            return;
        }

        DbContext.Accounts.Remove(entity);
        await DbContext.SaveChangesAsync();
        Snackbar.Add("Account removed.", Severity.Success);
        await LoadAccountsAsync();
    }

    private void BeginEditAccount(AccountCardVm account)
    {
        foreach (var item in _accounts)
        {
            if (!ReferenceEquals(item, account))
            {
                item.IsEditing = false;
            }
        }

        account.IsEditing = true;
        account.EditingName = account.Name;
        account.EditingBankAccountNumber = account.BankAccountNumber ?? string.Empty;
    }

    private void CancelEditAccount(AccountCardVm account)
    {
        account.IsEditing = false;
        account.EditingName = account.Name;
        account.EditingBankAccountNumber = account.BankAccountNumber ?? string.Empty;
    }

    private async Task SaveAccountDetailsAsync(AccountCardVm account)
    {
        if (_currentUser is null || !account.IsOwner)
        {
            Snackbar.Add("Only the account owner can edit account details.", Severity.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(account.EditingName))
        {
            Snackbar.Add("Account name is required.", Severity.Warning);
            return;
        }

        if (!AccountLifecycleValidation.TryNormalizeOptionalBankAccountNumber(account.EditingBankAccountNumber, out var normalizedBankAccountNumber, out var bankAccountError))
        {
            Snackbar.Add(bankAccountError!, Severity.Warning);
            return;
        }

        var entity = await DbContext.Accounts.SingleOrDefaultAsync(x => x.Id == account.Id && x.HouseholdId == _currentUser.HouseholdId, CancellationToken.None);
        if (entity is null)
        {
            Snackbar.Add("Account not found.", Severity.Error);
            return;
        }

        if (entity.OwnerUserId != _currentUser.Id)
        {
            Snackbar.Add("Only the account owner can edit account details.", Severity.Warning);
            return;
        }

        entity.Name = account.EditingName.Trim();
        entity.BankAccountNumber = normalizedBankAccountNumber;
        entity.UpdatedAt = DateTime.UtcNow;
        await DbContext.SaveChangesAsync();

        Snackbar.Add("Account details updated.", Severity.Success);
        await LoadAccountsAsync();
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

        if (!AccountLifecycleValidation.TryNormalizeOptionalBankAccountNumber(_newAccount.BankAccountNumber, out var bankAccountNumber, out var bankAccountNumberError))
        {
            Snackbar.Add(bankAccountNumberError!, Severity.Warning);
            return;
        }

        var account = new Treasury.App.Domain.Account
        {
            HouseholdId = _currentUser.HouseholdId,
            OwnerUserId = _currentUser.Id,
            Name = _newAccount.Name.Trim(),
            Currency = string.IsNullOrWhiteSpace(_newAccount.Currency) ? "PLN" : _newAccount.Currency.Trim().ToUpperInvariant(),
            AccountType = string.IsNullOrWhiteSpace(_newAccount.AccountType) ? "cash-wallet" : _newAccount.AccountType.Trim(),
            BankAccountNumber = bankAccountNumber,
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
        _newAccount.BankAccountNumber = string.Empty;
    }

    private sealed class NewAccountForm
    {
        public string Name { get; set; } = string.Empty;
        public string Currency { get; set; } = "PLN";
        public string AccountType { get; set; } = "cash-wallet";
        public decimal InitialBalance { get; set; }
        public string? BankAccountNumber { get; set; }
    }

    private sealed class AccountCardVm
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public bool IsActive { get; set; }
        public bool IsOwner { get; set; }
        public string? BankAccountNumber { get; set; }
        public bool IsEditing { get; set; }
        public string EditingName { get; set; } = string.Empty;
        public string? EditingBankAccountNumber { get; set; }
        public string SelectedShareEmail { get; set; } = string.Empty;
        public List<AccountViewerChoice> SharedWith { get; set; } = new();
        public bool SharedWithLoadFailed { get; set; }
    }
}