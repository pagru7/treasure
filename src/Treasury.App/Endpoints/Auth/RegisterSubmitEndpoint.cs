using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Endpoints.Auth;

public sealed class RegisterSubmitEndpoint(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TreasuryDbContext dbContext)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/auth/register-submit");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var payload = await AuthRequestReader.ReadAsync(HttpContext.Request);
        var email = payload.Email;
        var password = payload.Password;
        var confirmPassword = payload.ConfirmPassword;
        var householdNameOrId = payload.HouseholdNameOrId;

        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(confirmPassword)
            || string.IsNullOrWhiteSpace(householdNameOrId))
        {
            await SendResultAsync(Results.Redirect("/auth/register?error=Please+complete+all+fields."));
            return;
        }

        if (password != confirmPassword)
        {
            await SendResultAsync(Results.Redirect("/auth/register?error=Passwords+do+not+match."));
            return;
        }

        if (password.Length < 6)
        {
            await SendResultAsync(Results.Redirect("/auth/register?error=Password+must+be+at+least+6+characters."));
            return;
        }

        var householdInput = householdNameOrId.Trim();
        Guid householdId;

        if (Guid.TryParse(householdInput, out var existingHouseholdId))
        {
            var exists = await dbContext.Households
                .AnyAsync(x => x.Id == existingHouseholdId, ct);

            if (!exists)
            {
                await SendResultAsync(Results.Redirect("/auth/register?error=Provided+household+id+does+not+exist."));
                return;
            }

            householdId = existingHouseholdId;
        }
        else
        {
            var household = new Household
            {
                Name = householdInput
            };

            dbContext.Households.Add(household);
            await dbContext.SaveChangesAsync(ct);

            householdId = household.Id;
        }

        var user = new ApplicationUser
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            HouseholdId = householdId
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var message = string.Join(" ", result.Errors.Select(x => x.Description));
            await SendResultAsync(Results.Redirect($"/auth/register?error={Uri.EscapeDataString(message)}"));
            return;
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        await SendResultAsync(Results.Redirect("/"));
    }
}
