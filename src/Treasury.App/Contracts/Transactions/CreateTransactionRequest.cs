namespace Treasury.App.Contracts.Transactions;

public sealed class CreateTransactionRequest
{
    public Guid AccountId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "PLN";
    public string Type { get; set; } = "expense";
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public List<Guid> TagIds { get; set; } = new();
}