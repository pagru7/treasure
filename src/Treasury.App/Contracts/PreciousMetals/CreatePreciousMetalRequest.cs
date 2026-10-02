namespace Treasury.App.Contracts.PreciousMetals;

public sealed class CreatePreciousMetalRequest
{
    public string Name { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
    public decimal CurrentValue { get; set; }
}
