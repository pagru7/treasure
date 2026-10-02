using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Feedback;

public sealed class GetFeedbackEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/feedback");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var items = await db.FeedbackItems
            .Where(x => x.HouseholdId == user.HouseholdId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.Title,
                x.Description,
                Status = x.Status,
                x.CreatedAt,
                x.UpdatedAt
            })
            .ToListAsync(ct);

        var response = items.Select(x => new
        {
            x.Id,
            x.Title,
            x.Description,
            Status = ToApiStatus(x.Status),
            x.CreatedAt,
            x.UpdatedAt
        });

        await SendOkAsync(response, ct);
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
