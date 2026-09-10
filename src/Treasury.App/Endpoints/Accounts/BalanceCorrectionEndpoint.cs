using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Application.Transactions;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Accounts;

public sealed class BalanceCorrectionRouteRequest
{
    public Guid Id { get; set; }
    public decimal NewBalance { get; set; }
    public string Description { get; set; } = "Balance correction";
}

public sealed class BalanceCorrectionEndpoint(
    TreasuryDbContext db,
    UserManager<ApplicationUser> userManager,
    AccountBalanceRecalculationService balanceRecalculationService)
    : Endpoint<BalanceCorrectionRouteRequest>
{
    public override void Configure()
    {
        Post("/api/accounts/{id:guid}/balance-correction");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(BalanceCorrectionRouteRequest request, CancellationToken ct)
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

        var utcNow = DateTime.UtcNow;
        var delta = request.NewBalance - account.CurrentBalance;

        account.CurrentBalance = request.NewBalance;
        account.UpdatedAt = utcNow;

        db.Transactions.Add(new Transaction
        {
            HouseholdId = user.HouseholdId,
            AccountId = account.Id,
            Description = string.IsNullOrWhiteSpace(request.Description) ? "Balance correction" : request.Description.Trim(),
            Category = "Correction",
            Amount = delta,
            Currency = account.Currency,
            Type = "balance-correction",
            TransactionDate = utcNow,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        });

        async Task PersistAsync()
        {
            await db.SaveChangesAsync(ct);
            await balanceRecalculationService.RecalculateAccountAsync(account.Id, ct);
        }

        if (db.Database.IsRelational())
        {
            await using var transactionScope = await db.Database.BeginTransactionAsync(ct);
            await PersistAsync();
            await transactionScope.CommitAsync(ct);
        }
        else
        {
            await PersistAsync();
        }

        await SendOkAsync(new
        {
            account.Id,
            account.CurrentBalance
        }, ct);
    }
}