namespace Treasury.App.Contracts.Accounts;

public sealed class CreateAccountRequest
{
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "PLN";
    public string AccountType { get; set; } = "cash-wallet";
    public string? BankAccountNumber { get; set; }
}