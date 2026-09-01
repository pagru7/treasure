namespace Treasury.App.Contracts.Rates;

public sealed class UpsertRateRequest
{
    public string FromCurrency { get; set; } = "PLN";
    public string ToCurrency { get; set; } = "EUR";
    public decimal Rate { get; set; }
    public DateTime EffectiveAt { get; set; } = DateTime.UtcNow;
}
