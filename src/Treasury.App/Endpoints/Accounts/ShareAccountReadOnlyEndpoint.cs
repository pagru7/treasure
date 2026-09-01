using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Accounts;

public sealed class ShareAccountReadOnlyRouteRequest
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
}

public sealed class ShareAccountReadOnlyEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<ShareAccountReadOnlyRouteRequest>
{
    public override void Configure()
    {
        Post("/api/accounts/{id:guid}/share-readonly");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(ShareAccountReadOnlyRouteRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            AddError(x => x.Email, "Viewer email is required.");
            await SendErrorsAsync(cancellation: ct);
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

        var viewer = await userManager.FindByEmailAsync(email);
        if (viewer is null || viewer.HouseholdId != user.HouseholdId)
        {
            AddError(x => x.Email, "Viewer must be an existing user from the same household.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        if (viewer.Id == user.Id)
        {
            AddError(x => x.Email, "You already own this account.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var existingRule = await db.VisibilityRules.SingleOrDefaultAsync(x => x.AccountId == account.Id && x.ViewerUserId == viewer.Id, ct);
        if (existingRule is null)
        {
            db.VisibilityRules.Add(new VisibilityRule
            {
                AccountId = account.Id,
                ViewerUserId = viewer.Id,
                IsReadOnly = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existingRule.IsReadOnly = true;
        }

        await db.SaveChangesAsync(ct);

        await SendOkAsync(new
        {
            AccountId = account.Id,
            ViewerUserId = viewer.Id,
            IsReadOnly = true
        }, ct);
    }
}
