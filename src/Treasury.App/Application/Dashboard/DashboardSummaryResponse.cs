namespace Treasury.App.Application.Dashboard;

public sealed class DashboardSummaryResponse
{
    public Decimal TotalPln { get; set; }
    public Dictionary<string, decimal> TotalsByCurrency { get; set; } = new();
    public List<string> MissingRateCurrencies { get; set; } = new();
}
