using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Application.Accounts;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Accounts;

public sealed class UpdateAccountRouteRequest
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? BankAccountNumber { get; set; }
}

public sealed class UpdateAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<UpdateAccountRouteRequest>
{
    public override void Configure()
    {
        Put("/api/accounts/{id:guid}");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(UpdateAccountRouteRequest request, CancellationToken ct)
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

        var hasErrors = false;
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            AddError(x => x.Name, "Account name is required.");
            hasErrors = true;
        }

        if (!AccountLifecycleValidation.TryNormalizeOptionalBankAccountNumber(request.BankAccountNumber, out var bankAccountNumber, out var bankAccountNumberError))
        {
            AddError(x => x.BankAccountNumber, bankAccountNumberError!);
            hasErrors = true;
        }

        if (hasErrors)
        {
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        account.Name = request.Name.Trim();
        account.BankAccountNumber = bankAccountNumber;
        account.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await SendOkAsync(new
        {
            account.Id,
            account.Name,
            account.BankAccountNumber
        }, ct);
    }
}
