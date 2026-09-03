using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Accounts;

public sealed class DeleteAccountRouteRequest
{
    public Guid Id { get; set; }
}

public sealed class DeleteAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<DeleteAccountRouteRequest>
{
    public override void Configure()
    {
        Delete("/api/accounts/{id:guid}");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(DeleteAccountRouteRequest request, CancellationToken ct)
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

        var hasTransactions = await db.Transactions.AnyAsync(x => x.AccountId == account.Id, ct);
        if (hasTransactions && account.CurrentBalance != 0m)
        {
            AddError("Cannot remove account with transaction history and non-zero balance. Deactivate it instead.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        db.Accounts.Remove(account);
        await db.SaveChangesAsync(ct);
        await SendNoContentAsync(ct);
    }
}
