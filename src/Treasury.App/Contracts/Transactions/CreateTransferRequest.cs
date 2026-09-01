namespace Treasury.App.Contracts.Transactions;

public sealed class CreateTransferRequest
{
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "PLN";
    public string Description { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; } = DateTime.UtcNow;
}
