namespace Treasury.App.Domain;

public class TransactionTag
{
    public Guid TransactionId { get; set; }
    public Guid TagId { get; set; }

    public Transaction Transaction { get; set; } = default!;
    public Tag Tag { get; set; } = default!;
}
