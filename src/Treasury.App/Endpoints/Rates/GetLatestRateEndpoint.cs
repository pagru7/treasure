using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Rates;

public sealed class GetLatestRateRequest
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}

public sealed class GetLatestRateEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<GetLatestRateRequest>
{
    public override void Configure()
    {
        Get("/api/rates/latest");
    }

    public override async Task HandleAsync(GetLatestRateRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var fromCurrency = request.From.Trim().ToUpperInvariant();
        var toCurrency = request.To.Trim().ToUpperInvariant();

        var rate = await db.CurrencyRates.SingleOrDefaultAsync(x =>
            x.HouseholdId == user.HouseholdId
            && x.FromCurrency == fromCurrency
            && x.ToCurrency == toCurrency, ct);

        if (rate is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendOkAsync(new
        {
            rate.FromCurrency,
            rate.ToCurrency,
            rate.Rate,
            rate.EffectiveAt
        }, ct);
    }
}