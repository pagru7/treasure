using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Treasury.App.Domain;

namespace Treasury.App.Endpoints.Auth;

public sealed class LoginSubmitEndpoint(SignInManager<ApplicationUser> signInManager) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/auth/login-submit");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var payload = await AuthRequestReader.ReadAsync(HttpContext.Request);
        var email = payload.Email;
        var password = payload.Password;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await SendResultAsync(Results.Redirect("/auth/login?error=Please+enter+your+email+and+password."));
            return;
        }

        var result = await signInManager.PasswordSignInAsync(email.Trim(), password, true, false);
        if (!result.Succeeded)
        {
            await SendResultAsync(Results.Redirect("/auth/login?error=Invalid+email+or+password."));
            return;
        }

        await SendResultAsync(Results.Redirect("/"));
    }
}
