namespace Treasury.App.Domain;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "PLN";
    public string AccountType { get; set; } = "cash-wallet";
    public decimal CurrentBalance { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<VisibilityRule> VisibilityRules { get; set; } = new List<VisibilityRule>();
}
