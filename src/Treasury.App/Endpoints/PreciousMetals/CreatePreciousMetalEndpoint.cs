using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Contracts.PreciousMetals;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.PreciousMetals;

public sealed class CreatePreciousMetalEndpoint(
    TreasuryDbContext db,
    UserManager<ApplicationUser> userManager)
    : Endpoint<CreatePreciousMetalRequest>
{
    public override void Configure()
    {
        Post("/api/precious-metals");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(CreatePreciousMetalRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            AddError(x => x.Name, "Name is required.");
        }

        if (request.PurchasePrice <= 0m)
        {
            AddError(x => x.PurchasePrice, "Purchase price must be greater than zero.");
        }

        if (request.CurrentValue <= 0m)
        {
            AddError(x => x.CurrentValue, "Current value must be greater than zero.");
        }

        if (ValidationFailed)
        {
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var utcNow = DateTime.UtcNow;
        var purchaseDate = request.PurchaseDate == default ? utcNow : request.PurchaseDate;

        var asset = new PreciousMetalAsset
        {
            HouseholdId = user.HouseholdId,
            Name = request.Name.Trim(),
            PurchasePrice = request.PurchasePrice,
            PurchaseDate = purchaseDate,
            CurrentValue = request.CurrentValue,
            CurrentValueDate = utcNow,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        var valueEntry = new PreciousMetalValueEntry
        {
            PreciousMetalAssetId = asset.Id,
            Value = request.CurrentValue,
            ValueDate = utcNow,
            CreatedAt = utcNow
        };

        db.Set<PreciousMetalAsset>().Add(asset);
        db.Set<PreciousMetalValueEntry>().Add(valueEntry);
        await db.SaveChangesAsync(ct);

        await SendAsync(new
        {
            asset.Id,
            asset.Name,
            asset.PurchasePrice,
            asset.PurchaseDate,
            asset.CurrentValue,
            asset.CurrentValueDate,
            Profit = asset.CurrentValue - asset.PurchasePrice
        }, StatusCodes.Status201Created, ct);
    }
}
