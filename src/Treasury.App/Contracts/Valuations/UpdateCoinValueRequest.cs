namespace Treasury.App.Contracts.Valuations;

public sealed class UpdateCoinValueRequest
{
    public string AssetName { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1m;
    public decimal CurrentUnitValue { get; set; }
    public string Currency { get; set; } = "PLN";
    public DateTime ValuationDate { get; set; } = DateTime.UtcNow;
}
