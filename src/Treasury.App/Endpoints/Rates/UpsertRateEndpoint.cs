using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Contracts.Rates;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Rates;

public sealed class UpsertRateEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<UpsertRateRequest>
{
    public override void Configure()
    {
        Post("/api/rates");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(UpsertRateRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var fromCurrency = request.FromCurrency?.Trim().ToUpperInvariant();
        var toCurrency = request.ToCurrency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(fromCurrency) || string.IsNullOrWhiteSpace(toCurrency) || request.Rate <= 0m || fromCurrency == toCurrency)
        {
            AddError("Invalid currency pair or rate.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var existing = await db.CurrencyRates.SingleOrDefaultAsync(x =>
            x.HouseholdId == user.HouseholdId
            && x.FromCurrency == fromCurrency
            && x.ToCurrency == toCurrency, ct);

        if (existing is null)
        {
            db.CurrencyRates.Add(new CurrencyRate
            {
                HouseholdId = user.HouseholdId,
                FromCurrency = fromCurrency,
                ToCurrency = toCurrency,
                Rate = request.Rate,
                EffectiveAt = request.EffectiveAt == default ? DateTime.UtcNow : request.EffectiveAt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Rate = request.Rate;
            existing.EffectiveAt = request.EffectiveAt == default ? DateTime.UtcNow : request.EffectiveAt;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await SendOkAsync(ct);
    }
}
