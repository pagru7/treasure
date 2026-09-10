namespace Treasury.App.Contracts.Transactions;

public sealed class UpdateTransactionRequest
{
    public Guid Id { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal? Amount { get; set; }
    public string? Type { get; set; }
    public DateTime? TransactionDate { get; set; }
    public List<Guid>? TagIds { get; set; }
}