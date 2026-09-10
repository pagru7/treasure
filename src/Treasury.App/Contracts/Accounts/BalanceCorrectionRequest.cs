namespace Treasury.App.Contracts.Accounts;

public sealed class BalanceCorrectionRequest
{
    public decimal NewBalance { get; set; }
    public string Description { get; set; } = "Balance correction";
}