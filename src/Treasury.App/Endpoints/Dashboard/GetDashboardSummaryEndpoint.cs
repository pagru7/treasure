using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Application.Dashboard;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Dashboard;

public sealed class GetDashboardSummaryEndpoint(TreasuryDbContext db) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/dashboard/summary");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var accounts = await db.Accounts
            .Select(x => new AccountValueItem(x.Currency, x.CurrentBalance))
            .ToListAsync(ct);

        var rates = new Dictionary<string, decimal>
        {
            ["EUR"] = 4.6m,
            ["USD"] = 3.9m,
            ["GBP"] = 5.3m
        };

        var summary = new DashboardSummaryService().BuildSummary(accounts, rates);
        await SendOkAsync(summary, ct);
    }
}
