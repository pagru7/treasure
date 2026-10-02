using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Treasury.App.Contracts.Feedback;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Feedback;

public sealed class CreateFeedbackEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
    : Endpoint<CreateFeedbackRequest>
{
    public override void Configure()
    {
        Post("/api/feedback");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
    }

    public override async Task HandleAsync(CreateFeedbackRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            AddError(x => x.Title, "Title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            AddError(x => x.Description, "Description is required.");
        }
        else if (request.Description.Length > 1000)
        {
            AddError(x => x.Description, "Description must be at most 1000 characters.");
        }

        if (ValidationFailed)
        {
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var utcNow = DateTime.UtcNow;
        var item = new FeedbackItem
        {
            HouseholdId = user.HouseholdId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Status = FeedbackStatus.New,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        db.FeedbackItems.Add(item);
        await db.SaveChangesAsync(ct);

        await SendAsync(new
        {
            item.Id,
            item.Title,
            item.Description,
            Status = ToApiStatus(item.Status),
            item.CreatedAt,
            item.UpdatedAt
        }, StatusCodes.Status201Created, ct);
    }

    private static string ToApiStatus(FeedbackStatus status) =>
        status switch
        {
            FeedbackStatus.New => "new",
            FeedbackStatus.Implementing => "implementing",
            FeedbackStatus.Done => "done",
            _ => "new"
        };
}
