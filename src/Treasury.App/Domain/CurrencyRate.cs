namespace Treasury.App.Domain;

public sealed class CurrencyRate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string FromCurrency { get; set; } = "PLN";
    public string ToCurrency { get; set; } = "EUR";
    public decimal Rate { get; set; }
    public DateTime EffectiveAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
