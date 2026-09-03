namespace Treasury.App.Contracts.Accounts;

public sealed class UpdateAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public string? BankAccountNumber { get; set; }
}
