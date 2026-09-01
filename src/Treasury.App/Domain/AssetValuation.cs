namespace Treasury.App.Domain;

public sealed class AssetValuation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public ValuationKind Kind { get; set; } = ValuationKind.CoinManual;
    public decimal Quantity { get; set; }
    public decimal Weight { get; set; }
    public decimal Purity { get; set; }
    public decimal CurrentUnitValue { get; set; }
    public decimal CurrentTotalValue { get; set; }
    public string Currency { get; set; } = "PLN";
    public DateTime ValuationDate { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
