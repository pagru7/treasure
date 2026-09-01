using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Contracts.Accounts;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.AccountTypes;

public sealed class CreateAccountTypeEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<CreateAccountTypeRequest>
{
    public override void Configure()
    {
        Post("/api/account-types");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(CreateAccountTypeRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            AddError("Account type name is required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var normalizedName = name.ToLowerInvariant();
        var exists = await db.AccountTypes.AnyAsync(x => x.HouseholdId == user.HouseholdId && x.Name.ToLower() == normalizedName, ct);
        if (exists)
        {
            AddError("This account type already exists for this household.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var accountType = new AccountTypeDefinition
        {
            HouseholdId = user.HouseholdId,
            Name = normalizedName,
            Description = request.Description?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.AccountTypes.Add(accountType);
        await db.SaveChangesAsync(ct);

        await SendAsync(new
        {
            accountType.Id,
            accountType.Name,
            accountType.Description
        }, StatusCodes.Status201Created, ct);
    }
}
