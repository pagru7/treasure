using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Pages;

public partial class Transfers
{
    [Inject] public TreasuryDbContext DbContext { get; set; } = default!;
    [Inject] public ISnackbar Snackbar { get; set; } = default!;
    [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] public UserManager<ApplicationUser> UserManager { get; set; } = default!;

    private ApplicationUser? _currentUser;
    private readonly List<AccountChoice> _accounts = new();
    private readonly List<TransferHistoryRow> _transfers = new();
    private readonly NewTransferForm _newTransfer = new();
    private readonly Dictionary<Guid, string> _visibleAccountNames = new();
    private string? _loadError;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loadError = null;

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _currentUser = await UserManager.GetUserAsync(authState.User);
        if (_currentUser is null)
        {
            _accounts.Clear();
            _transfers.Clear();
            _visibleAccountNames.Clear();
            return;
        }

        try
        {
            var householdAccounts = await DbContext.Accounts
                .Where(x => x.HouseholdId == _currentUser.HouseholdId)
                .OrderBy(x => x.Name)
                .ToListAsync();

            _visibleAccountNames.Clear();
            foreach (var account in householdAccounts)
            {
                _visibleAccountNames[account.Id] = account.Name;
            }

            _accounts.Clear();
            _accounts.AddRange(householdAccounts
                .Where(x => x.OwnerUserId == _currentUser.Id && x.IsActive)
                .Select(x => new AccountChoice
                {
                    Id = x.Id,
                    Name = x.Name,
                    Currency = x.Currency
                }));

            _transfers.Clear();
            var transfers = await DbContext.Transfers
                .Where(x => x.HouseholdId == _currentUser.HouseholdId)
                .OrderByDescending(x => x.TransferDate)
                .ThenByDescending(x => x.CreatedAt)
                .Take(10)
                .ToListAsync();

            _transfers.AddRange(transfers.Select(x => new TransferHistoryRow
            {
                Id = x.Id,
                FromAccountName = ResolveAccountName(x.FromAccountId),
                ToAccountName = ResolveAccountName(x.ToAccountId),
                Description = x.Description,
                Amount = x.Amount,
                Currency = x.Currency,
                TransferDate = x.TransferDate
            }));

            if (_accounts.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(_newTransfer.FromAccountId) || !_accounts.Any(x => x.Id.ToString() == _newTransfer.FromAccountId))
                {
                    _newTransfer.FromAccountId = _accounts[0].Id.ToString();
                }

                if (string.IsNullOrWhiteSpace(_newTransfer.ToAccountId) || !_accounts.Any(x => x.Id.ToString() == _newTransfer.ToAccountId))
                {
                    _newTransfer.ToAccountId = _accounts[Math.Min(1, _accounts.Count - 1)].Id.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            _accounts.Clear();
            _transfers.Clear();
            _visibleAccountNames.Clear();
            _loadError = "Unable to load transfers.";
            Snackbar.Add($"Unable to load transfers: {ex.Message}", Severity.Error);
        }
    }

    private async Task CreateTransferAsync()
    {
        if (_currentUser is null)
        {
            Snackbar.Add("Sign in first.", Severity.Warning);
            return;
        }

        if (_accounts.Count == 0)
        {
            Snackbar.Add("Create an active account before transferring funds.", Severity.Warning);
            return;
        }

        if (!Guid.TryParse(_newTransfer.FromAccountId, out var fromAccountId) ||
            !Guid.TryParse(_newTransfer.ToAccountId, out var toAccountId) ||
            fromAccountId == Guid.Empty ||
            toAccountId == Guid.Empty ||
            fromAccountId == toAccountId ||
            _newTransfer.Amount <= 0m)
        {
            Snackbar.Add("Choose two different active accounts and a positive amount.", Severity.Warning);
            return;
        }

        var fromAccount = await DbContext.Accounts.SingleOrDefaultAsync(x =>
            x.Id == fromAccountId &&
            x.HouseholdId == _currentUser.HouseholdId &&
            x.OwnerUserId == _currentUser.Id, CancellationToken.None);

        var toAccount = await DbContext.Accounts.SingleOrDefaultAsync(x =>
            x.Id == toAccountId &&
            x.HouseholdId == _currentUser.HouseholdId &&
            x.OwnerUserId == _currentUser.Id, CancellationToken.None);

        if (fromAccount is null || toAccount is null)
        {
            Snackbar.Add("Transfer accounts were not found.", Severity.Error);
            return;
        }

        if (!fromAccount.IsActive || !toAccount.IsActive)
        {
            Snackbar.Add("Transfers are allowed only between active accounts.", Severity.Warning);
            return;
        }

        var transferDate = _newTransfer.TransferDate ?? DateTime.UtcNow;
        var description = string.IsNullOrWhiteSpace(_newTransfer.Description)
            ? "Account transfer"
            : _newTransfer.Description.Trim();
        var currency = fromAccount.Currency;

        var transfer = new Transfer
        {
            HouseholdId = _currentUser.HouseholdId,
            FromAccountId = fromAccount.Id,
            ToAccountId = toAccount.Id,
            Amount = _newTransfer.Amount,
            Currency = currency,
            Description = description,
            TransferDate = transferDate,
            CreatedAt = DateTime.UtcNow
        };

        var outflow = new Transaction
        {
            HouseholdId = _currentUser.HouseholdId,
            AccountId = fromAccount.Id,
            Description = $"{description} -> {toAccount.Name}",
            Category = "Transfer",
            Amount = _newTransfer.Amount,
            Currency = currency,
            Type = "transfer-out",
            TransactionDate = transferDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var inflow = new Transaction
        {
            HouseholdId = _currentUser.HouseholdId,
            AccountId = toAccount.Id,
            Description = $"{description} <- {fromAccount.Name}",
            Category = "Transfer",
            Amount = _newTransfer.Amount,
            Currency = currency,
            Type = "transfer-in",
            TransactionDate = transferDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        fromAccount.CurrentBalance -= Math.Abs(_newTransfer.Amount);
        toAccount.CurrentBalance += Math.Abs(_newTransfer.Amount);
        fromAccount.UpdatedAt = DateTime.UtcNow;
        toAccount.UpdatedAt = DateTime.UtcNow;

        async Task PersistAsync()
        {
            DbContext.Transfers.Add(transfer);
            DbContext.Transactions.Add(outflow);
            DbContext.Transactions.Add(inflow);
            await DbContext.SaveChangesAsync();

            transfer.OutflowTransactionId = outflow.Id;
            transfer.InflowTransactionId = inflow.Id;
            await DbContext.SaveChangesAsync();
        }

        if (DbContext.Database.IsRelational())
        {
            await using var transaction = await DbContext.Database.BeginTransactionAsync();
            await PersistAsync();
            await transaction.CommitAsync();
        }
        else
        {
            await PersistAsync();
        }

        _newTransfer.Description = string.Empty;
        _newTransfer.Amount = 0m;
        _newTransfer.TransferDate = DateTime.UtcNow;

        Snackbar.Add("Transfer saved.", Severity.Success);
        await LoadAsync();
    }

    private string ResolveAccountName(Guid accountId) =>
        _visibleAccountNames.TryGetValue(accountId, out var name) ? name : "Unknown account";

    private sealed class AccountChoice
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
    }

    private sealed class NewTransferForm
    {
        public string FromAccountId { get; set; } = string.Empty;
        public string ToAccountId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime? TransferDate { get; set; } = DateTime.UtcNow;
        public string Description { get; set; } = string.Empty;
    }

    private sealed class TransferHistoryRow
    {
        public Guid Id { get; set; }
        public string FromAccountName { get; set; } = string.Empty;
        public string ToAccountName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public DateTime TransferDate { get; set; }
    }
}
