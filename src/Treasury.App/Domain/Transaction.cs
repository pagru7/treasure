namespace Treasury.App.Domain;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid AccountId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "PLN";
    public string Type { get; set; } = "expense";
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public decimal BalanceAfterTransaction { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TransactionTag> TransactionTags { get; set; } = new List<TransactionTag>();
}