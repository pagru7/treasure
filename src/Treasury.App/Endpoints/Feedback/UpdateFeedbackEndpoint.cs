using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Contracts.Feedback;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Feedback;

public sealed class UpdateFeedbackEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<UpdateFeedbackRequest>
{
    public override void Configure()
    {
        Put("/api/feedback/{id:guid}");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
    }

    public override async Task HandleAsync(UpdateFeedbackRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (request.Id == Guid.Empty)
        {
            AddError(x => x.Id, "A valid feedback item is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            AddError(x => x.Description, "Description is required.");
        }
        else if (request.Description.Length > 1000)
        {
            AddError(x => x.Description, "Description must be at most 1000 characters.");
        }

        var parsedStatus = ParseStatus(request.Status);
        if (parsedStatus is null)
        {
            AddError(x => x.Status, "Invalid status.");
        }

        if (ValidationFailed)
        {
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var item = await db.FeedbackItems
            .SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
        if (item is null)
        {
            await SendNotFoundAsync(ct);
            return;
        }

        item.Status = parsedStatus!.Value;
        item.Description = request.Description.Trim();
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await SendOkAsync(new
        {
            item.Id,
            item.Title,
            item.Description,
            Status = ToApiStatus(item.Status),
            item.CreatedAt,
            item.UpdatedAt
        }, ct);
    }

    private static string ToApiStatus(FeedbackStatus status) =>
        status switch
        {
            FeedbackStatus.New => "new",
            FeedbackStatus.Implementing => "implementing",
            FeedbackStatus.Done => "done",
            _ => "new"
        };

    private static FeedbackStatus? ParseStatus(string? status) =>
        status?.Trim().ToLowerInvariant() switch
        {
            "new" => FeedbackStatus.New,
            "implementing" => FeedbackStatus.Implementing,
            "done" => FeedbackStatus.Done,
            _ => null
        };
}
