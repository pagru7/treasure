namespace Treasury.App.Application.Dashboard;

public sealed record AccountValueItem(string Currency, decimal Amount);

public sealed class DashboardSummaryService
{
    public DashboardSummaryResponse BuildSummary(IEnumerable<AccountValueItem> accounts, IReadOnlyDictionary<string, decimal> rates)
    {
        var totals = accounts
            .GroupBy(x => x.Currency)
            .ToDictionary(x => x.Key, x => x.Sum(v => v.Amount));

        var totalPln = totals
            .Where(x => x.Key == "PLN")
            .Sum(x => x.Value);

        foreach (var item in totals.Where(x => x.Key != "PLN"))
        {
            if (rates.TryGetValue(item.Key, out var rate))
            {
                totalPln += item.Value * rate;
            }
        }

        return new DashboardSummaryResponse
        {
            TotalPln = totalPln,
            TotalsByCurrency = totals,
            MissingRateCurrencies = totals.Keys
                .Where(currency => currency != "PLN" && !rates.ContainsKey(currency))
                .ToList()
        };
    }
}
