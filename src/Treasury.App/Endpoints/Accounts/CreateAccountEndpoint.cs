using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Contracts.Accounts;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Accounts;

public sealed class CreateAccountEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<CreateAccountRequest>
{
    public override void Configure()
    {
        Post("/api/accounts");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
    }

    public override async Task HandleAsync(CreateAccountRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            AddError(x => x.Name, "Account name is required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var accountType = string.IsNullOrWhiteSpace(request.AccountType) ? "cash-wallet" : request.AccountType.Trim().ToLowerInvariant();
        var isKnownType = await db.AccountTypes.AnyAsync(x => x.HouseholdId == user.HouseholdId && x.Name == accountType, ct);
        if (!isKnownType)
        {
            AddError(x => x.AccountType, "Unknown account type for this household.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var account = new Account
        {
            HouseholdId = user.HouseholdId,
            OwnerUserId = user.Id,
            Name = request.Name.Trim(),
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
            AccountType = accountType,
            CurrentBalance = 0m
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);

        var response = new AccountResponse
        {
            Id = account.Id,
            Name = account.Name,
            Currency = account.Currency,
            AccountType = account.AccountType,
            CurrentBalance = account.CurrentBalance,
            IsReadOnly = false
        };

        await SendAsync(response, StatusCodes.Status201Created, ct);
    }
}
