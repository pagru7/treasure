namespace Treasury.App.Domain;

public sealed class PreciousMetalValueEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PreciousMetalAssetId { get; set; }
    public decimal Value { get; set; }
    public DateTime ValueDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
