using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Treasury.App.Domain;
using Treasury.App.Application.Accounts;

namespace Treasury.App.Endpoints.Accounts;

public sealed class ShareAccountReadOnlyRouteRequest
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
}

public sealed class ShareAccountReadOnlyEndpoint(AccountSharingService accountSharingService, UserManager<ApplicationUser> userManager)
    : Endpoint<ShareAccountReadOnlyRouteRequest>
{
    public override void Configure()
    {
        Post("/api/accounts/{id:guid}/share-readonly");
        Policies(global::Treasury.App.Infrastructure.Auth.Policies.OwnerOnly);
    }

    public override async Task HandleAsync(ShareAccountReadOnlyRouteRequest request, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            AddError(x => x.Email, "Email is required.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        var result = await accountSharingService.ShareReadOnlyAsync(user, request.Id, request.Email.Trim(), ct);
        if (!result.Succeeded)
        {
            if (result.ErrorMessage == "Account not found.")
            {
                await SendNotFoundAsync(ct);
                return;
            }

            if (result.ErrorMessage == "Only the owner can share this account.")
            {
                await SendForbiddenAsync(ct);
                return;
            }

            AddError(x => x.Email, result.ErrorMessage ?? "Unable to share this account.");
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        await SendOkAsync(new
        {
            AccountId = request.Id,
            IsReadOnly = true
        }, ct);
    }
}
