namespace Treasury.App.Contracts.PreciousMetals;

public sealed class UpdatePreciousMetalValueRequest
{
    public Guid Id { get; set; }
    public decimal CurrentValue { get; set; }
    public DateTime ValueDate { get; set; } = DateTime.UtcNow;
}
