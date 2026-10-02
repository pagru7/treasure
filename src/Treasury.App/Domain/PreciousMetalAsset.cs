namespace Treasury.App.Domain;

public sealed class PreciousMetalAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
    public decimal CurrentValue { get; set; }
    public DateTime CurrentValueDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PreciousMetalValueEntry> ValueHistory { get; set; } = new List<PreciousMetalValueEntry>();
}
