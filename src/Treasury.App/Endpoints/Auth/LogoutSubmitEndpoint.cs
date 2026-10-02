using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Treasury.App.Domain;

namespace Treasury.App.Endpoints.Auth;

public sealed class LogoutSubmitEndpoint(SignInManager<ApplicationUser> signInManager) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/auth/logout-submit");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await signInManager.SignOutAsync();
        await SendResultAsync(Results.Redirect("/auth/login"));
    }
}
