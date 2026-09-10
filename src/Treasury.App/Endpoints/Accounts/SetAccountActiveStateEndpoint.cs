using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Accounts;

public sealed class SetAccountActiveStateRouteRequest
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
}

public sealed class SetAccountActiveStateEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<SetAccountActiveStateRouteRequest>
{
    public override void Configure()
    {
        Put("/api/accounts/{id:guid}/active-state");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(SetAccountActiveStateRouteRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
        if (account is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (account.OwnerUserId != user.Id)
        {
            await SendForbiddenAsync(ct);
            return;
        }

        account.IsActive = request.IsActive;
        account.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await SendOkAsync(new
        {
            account.Id,
            account.IsActive
        }, ct);
    }
}