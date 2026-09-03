using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Contracts.Transactions;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Transfers;

public sealed class CreateTransferEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<CreateTransferRequest>
{
    public override void Configure()
    {
        Post("/api/transfers");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(CreateTransferRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (request.FromAccountId == Guid.Empty || request.ToAccountId == Guid.Empty || request.FromAccountId == request.ToAccountId || request.Amount <= 0m)
        {
            if (request.FromAccountId == Guid.Empty || request.FromAccountId == request.ToAccountId)
            {
                AddError(x => x.FromAccountId, "A valid source account is required.");
            }

            if (request.ToAccountId == Guid.Empty || request.FromAccountId == request.ToAccountId)
            {
                AddError(x => x.ToAccountId, "A valid destination account is required.");
            }

            if (request.Amount <= 0m)
            {
                AddError(x => x.Amount, "Transfer amount must be greater than zero.");
            }

            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var fromAccount = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.FromAccountId && x.HouseholdId == user.HouseholdId, ct);
        var toAccount = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.ToAccountId && x.HouseholdId == user.HouseholdId, ct);
        if (fromAccount is null || toAccount is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        if (fromAccount.OwnerUserId != user.Id || toAccount.OwnerUserId != user.Id)
        {
            await SendForbiddenAsync(ct);
            return;
        }

        // Block transfers involving inactive accounts
        if (!fromAccount.IsActive || !toAccount.IsActive)
        {
            if (!fromAccount.IsActive)
            {
                AddError(x => x.FromAccountId, "Source account is inactive.");
            }

            if (!toAccount.IsActive)
            {
                AddError(x => x.ToAccountId, "Destination account is inactive.");
            }

            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var transferDate = request.TransferDate == default ? DateTime.UtcNow : request.TransferDate;
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? fromAccount.Currency : request.Currency.Trim().ToUpperInvariant();
        var description = string.IsNullOrWhiteSpace(request.Description) ? "Account transfer" : request.Description.Trim();

        var transfer = new Transfer
        {
            HouseholdId = user.HouseholdId,
            FromAccountId = fromAccount.Id,
            ToAccountId = toAccount.Id,
            Amount = request.Amount,
            Currency = currency,
            Description = description,
            TransferDate = transferDate,
            CreatedAt = DateTime.UtcNow
        };

        var outflow = new Transaction
        {
            HouseholdId = user.HouseholdId,
            AccountId = fromAccount.Id,
            Description = $"{description} -> {toAccount.Name}",
            Category = "Transfer",
            Amount = request.Amount,
            Currency = currency,
            Type = "transfer-out",
            TransactionDate = transferDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var inflow = new Transaction
        {
            HouseholdId = user.HouseholdId,
            AccountId = toAccount.Id,
            Description = $"{description} <- {fromAccount.Name}",
            Category = "Transfer",
            Amount = request.Amount,
            Currency = currency,
            Type = "transfer-in",
            TransactionDate = transferDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        fromAccount.CurrentBalance -= Math.Abs(request.Amount);
        toAccount.CurrentBalance += Math.Abs(request.Amount);
        fromAccount.UpdatedAt = DateTime.UtcNow;
        toAccount.UpdatedAt = DateTime.UtcNow;

        async Task PersistAsync()
        {
            db.Transfers.Add(transfer);
            db.Transactions.Add(outflow);
            db.Transactions.Add(inflow);
            await db.SaveChangesAsync(ct);

            transfer.OutflowTransactionId = outflow.Id;
            transfer.InflowTransactionId = inflow.Id;
            await db.SaveChangesAsync(ct);
        }

        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await PersistAsync();
            await transaction.CommitAsync(ct);
        }
        else
        {
            await PersistAsync();
        }

        await SendAsync(new
        {
            transfer.Id,
            transfer.FromAccountId,
            transfer.ToAccountId,
            transfer.OutflowTransactionId,
            transfer.InflowTransactionId,
            transfer.Amount,
            transfer.Currency,
            transfer.TransferDate
        }, StatusCodes.Status201Created, ct);
    }
}
