using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Treasury.App.Contracts.Bills;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Bills;

public sealed class CreateBillEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : Endpoint<CreateBillRequest>
{
    public override void Configure()
    {
        Post("/api/bills");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
    }

    public override async Task HandleAsync(CreateBillRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            AddError(x => x.Name, "Bill name is required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var bill = new Bill
        {
            HouseholdId = user.HouseholdId,
            Name = request.Name.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
            DueDay = request.DueDay <= 0 ? 1 : Math.Min(request.DueDay, 31),
            IsPaid = request.IsPaid,
            Notes = request.Notes?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Bills.Add(bill);
        await db.SaveChangesAsync(ct);

        await SendAsync(new
        {
            bill.Id,
            bill.Name,
            bill.Category,
            bill.Amount,
            bill.Currency,
            bill.DueDay,
            bill.IsPaid,
            bill.Notes
        }, StatusCodes.Status201Created, ct);
    }
}
