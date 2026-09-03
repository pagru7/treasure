namespace Treasury.App.Contracts.Accounts;

public sealed class AccountResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? BankAccountNumber { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IsReadOnly { get; set; }
}
