using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Contracts.PreciousMetals;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.PreciousMetals;

public sealed class UpdatePreciousMetalValueEndpoint(
    TreasuryDbContext db,
    UserManager<ApplicationUser> userManager)
    : Endpoint<UpdatePreciousMetalValueRequest>
{
    public override void Configure()
    {
        Post("/api/precious-metals/{id:guid}/values");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(UpdatePreciousMetalValueRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (request.Id == Guid.Empty)
        {
            AddError(x => x.Id, "A valid item is required.");
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

        var asset = await db.Set<PreciousMetalAsset>()
            .SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
        if (asset is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        var valueDate = request.ValueDate == default ? DateTime.UtcNow : request.ValueDate;
        var valueDateUtc = valueDate.Kind == DateTimeKind.Utc ? valueDate : valueDate.ToUniversalTime();
        if (valueDateUtc.Date > DateTime.UtcNow.Date)
        {
            AddError(x => x.ValueDate, "Future value date is not allowed.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        asset.CurrentValue = request.CurrentValue;
        asset.CurrentValueDate = valueDateUtc;
        asset.UpdatedAt = DateTime.UtcNow;

        var dayStart = valueDateUtc.Date;
        var dayEnd = dayStart.AddDays(1);
        var existingDailyEntry = await db.Set<PreciousMetalValueEntry>()
            .SingleOrDefaultAsync(
                x => x.PreciousMetalAssetId == asset.Id
                    && x.ValueDate >= dayStart
                    && x.ValueDate < dayEnd,
                ct);

        if (existingDailyEntry is null)
        {
            db.Set<PreciousMetalValueEntry>().Add(new PreciousMetalValueEntry
            {
                PreciousMetalAssetId = asset.Id,
                Value = request.CurrentValue,
                ValueDate = valueDateUtc,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existingDailyEntry.Value = request.CurrentValue;
            existingDailyEntry.ValueDate = valueDateUtc;
        }

        await db.SaveChangesAsync(ct);

        await SendOkAsync(new
        {
            asset.Id,
            asset.CurrentValue,
            asset.CurrentValueDate,
            Profit = asset.CurrentValue - asset.PurchasePrice
        }, ct);
    }
}
