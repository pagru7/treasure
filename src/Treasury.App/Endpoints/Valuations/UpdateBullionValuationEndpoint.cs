using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Treasury.App.Application.Valuations;
using Treasury.App.Contracts.Valuations;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Valuations;

public sealed class UpdateBullionValuationEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<UpdateBullionValueRequest>
{
    public override void Configure()
    {
        Post("/api/valuations/bullion");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(UpdateBullionValueRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.AssetName) || request.Weight <= 0m || request.Purity <= 0m || request.Purity > 1m || request.UnitPrice <= 0m)
        {
            if (string.IsNullOrWhiteSpace(request.AssetName))
            {
                AddError(x => x.AssetName, "Asset name is required.");
            }

            if (request.Weight <= 0m)
            {
                AddError(x => x.Weight, "Weight must be greater than zero.");
            }

            if (request.Purity <= 0m || request.Purity > 1m)
            {
                AddError(x => x.Purity, "Purity must be between 0 and 1.");
            }

            if (request.UnitPrice <= 0m)
            {
                AddError(x => x.UnitPrice, "Unit price must be greater than zero.");
            }

            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var total = BullionFormulaCalculator.Calculate(request.Weight, request.Purity, request.UnitPrice);
        var valuation = new AssetValuation
        {
            HouseholdId = user.HouseholdId,
            AssetName = request.AssetName.Trim(),
            Kind = ValuationKind.BullionFormula,
            Quantity = 1m,
            Weight = request.Weight,
            Purity = request.Purity,
            CurrentUnitValue = request.UnitPrice,
            CurrentTotalValue = total,
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
            valuation.CurrentUnitValue,
            valuation.CurrentTotalValue,
            valuation.Currency,
            valuation.ValuationDate
        }, StatusCodes.Status201Created, ct);
    }
}
