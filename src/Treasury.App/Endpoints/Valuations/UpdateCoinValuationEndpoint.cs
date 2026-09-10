using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Treasury.App.Contracts.Valuations;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Valuations;

public sealed class UpdateCoinValuationEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<UpdateCoinValueRequest>
{
    public override void Configure()
    {
        Post("/api/valuations/coin");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(UpdateCoinValueRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.AssetName) || request.Quantity <= 0m || request.CurrentUnitValue <= 0m)
        {
            if (string.IsNullOrWhiteSpace(request.AssetName))
            {
                AddError(x => x.AssetName, "Asset name is required.");
            }

            if (request.Quantity <= 0m)
            {
                AddError(x => x.Quantity, "Quantity must be greater than zero.");
            }

            if (request.CurrentUnitValue <= 0m)
            {
                AddError(x => x.CurrentUnitValue, "Current unit value must be greater than zero.");
            }

            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var valuation = new AssetValuation
        {
            HouseholdId = user.HouseholdId,
            AssetName = request.AssetName.Trim(),
            Kind = ValuationKind.CoinManual,
            Quantity = request.Quantity,
            Weight = 0m,
            Purity = 0m,
            CurrentUnitValue = request.CurrentUnitValue,
            CurrentTotalValue = decimal.Round(request.Quantity * request.CurrentUnitValue, 4, MidpointRounding.AwayFromZero),
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
            ValuationDate = request.ValuationDate == default ? DateTime.UtcNow : request.ValuationDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.AssetValuations.Add(valuation);
        await db.SaveChangesAsync(ct);

        await SendAsync(new
        {
            valuation.Id,
            valuation.AssetName,
            valuation.Kind,
            valuation.Quantity,
            valuation.CurrentUnitValue,
            valuation.CurrentTotalValue,
            valuation.Currency,
            valuation.ValuationDate
        }, StatusCodes.Status201Created, ct);
    }
}