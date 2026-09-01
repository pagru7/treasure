namespace Treasury.App.Contracts.Valuations;

public sealed class UpdateBullionValueRequest
{
    public string AssetName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Purity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "PLN";
    public DateTime ValuationDate { get; set; } = DateTime.UtcNow;
}
