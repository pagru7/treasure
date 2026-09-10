using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Contracts.Tags;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Tags;

public sealed class CreateTagEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager) : Endpoint<CreateTagRequest>
{
    public override void Configure()
    {
        Post("/api/tags");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
    }

    public override async Task HandleAsync(CreateTagRequest request, CancellationToken ct)
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
            AddError(x => x.Name, "Tag name is required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var exists = await db.Tags.AnyAsync(x => x.HouseholdId == user.HouseholdId && x.Name.ToLower() == name.ToLower(), ct);
        if (exists)
        {
            AddError(x => x.Name, "This tag already exists for this household.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var tag = new Tag
        {
            HouseholdId = user.HouseholdId,
            Name = name,
            Color = string.IsNullOrWhiteSpace(request.Color) ? "#3B82F6" : request.Color.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);

        await SendAsync(new
        {
            tag.Id,
            tag.Name,
            tag.Color
        }, StatusCodes.Status201Created, ct);
    }
}