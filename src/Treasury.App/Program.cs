using System.Text.Json;
using FastEndpoints;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Treasury.App.Application.Dashboard;
using Treasury.App.Components;
using Treasury.App.Contracts.Accounts;
using Treasury.App.Contracts.Bills;
using Treasury.App.Contracts.Budgets;
using Treasury.App.Contracts.Rates;
using Treasury.App.Contracts.Tags;
using Treasury.App.Contracts.Transactions;
using Treasury.App.Contracts.Valuations;
using Treasury.App.Application.Valuations;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Auth;
using Treasury.App.Infrastructure.Data;
using Treasury.App.Infrastructure.Data.Seed;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

var shouldUseInMemoryDatabase = builder.Environment.IsEnvironment("Testing")
    || builder.Environment.IsDevelopment()
    || string.IsNullOrWhiteSpace(connectionString);

if (shouldUseInMemoryDatabase)
{
    var inMemoryDbName = $"TreasuryDb-{Guid.NewGuid()}";
    builder.Services.AddDbContext<TreasuryDbContext>(options =>
        options.UseInMemoryDatabase(inMemoryDbName));
}
else
{
    builder.Services.AddDbContext<TreasuryDbContext>(options =>
        options.UseNpgsql(connectionString));
}

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<TreasuryDbContext>()
    .AddDefaultTokenProviders()
    .AddSignInManager<SignInManager<ApplicationUser>>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
    })
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect("/auth/login");
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect("/auth/login");
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.OwnerOnly, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(Policies.SharedReadOnly, policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddHealthChecks();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddFastEndpoints();
builder.Services.AddMudServices();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();

    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }

    await InitialSeed.SeedAsync(db);
}

app.MapHealthChecks("/health");

app.MapPost("/auth/login-submit", async (HttpContext httpContext, SignInManager<ApplicationUser> signInManager) =>
{
    var payload = await ReadAuthPayloadAsync(httpContext.Request);
    var email = payload.Email;
    var password = payload.Password;

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect("/auth/login?error=Please+enter+your+email+and+password.");
    }

    var result = await signInManager.PasswordSignInAsync(email.Trim(), password, true, false);
    if (!result.Succeeded)
    {
        return Results.Redirect("/auth/login?error=Invalid+email+or+password.");
    }

    return Results.Redirect("/");
});

app.MapPost("/auth/register-submit", async (HttpContext httpContext, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, TreasuryDbContext dbContext) =>
{
    var payload = await ReadAuthPayloadAsync(httpContext.Request);
    var email = payload.Email;
    var password = payload.Password;
    var confirmPassword = payload.ConfirmPassword;

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
    {
        return Results.Redirect("/auth/register?error=Please+complete+all+fields.");
    }

    if (password != confirmPassword)
    {
        return Results.Redirect("/auth/register?error=Passwords+do+not+match.");
    }

    if (password.Length < 6)
    {
        return Results.Redirect("/auth/register?error=Password+must+be+at+least+6+characters.");
    }

    var householdId = dbContext.Households.OrderBy(x => x.Id).Select(x => x.Id).FirstOrDefault();
    if (householdId == Guid.Empty)
    {
        householdId = Guid.Parse("11111111-1111-1111-1111-111111111111");
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
        return Results.Redirect($"/auth/register?error={Uri.EscapeDataString(message)}");
    }

    await signInManager.SignInAsync(user, isPersistent: true);
    return Results.Redirect("/");
});

app.MapPost("/auth/logout-submit", async (HttpContext httpContext, SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/auth/login");
});

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    if (path == "/" && context.User.Identity?.IsAuthenticated != true)
    {
        context.Response.Redirect("/auth/login");
        return;
    }

    if ((path == "/auth/login" || path == "/auth/register") && context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.Redirect("/");
        return;
    }

    await next();
});

app.UseAntiforgery();
app.UseFastEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

async Task<AuthRequestPayload> ReadAuthPayloadAsync(HttpRequest request)
{
    if (request.HasFormContentType)
    {
        var form = await request.ReadFormAsync();
        return new AuthRequestPayload(
            form["Email"].ToString(),
            form["Password"].ToString(),
            form["ConfirmPassword"].ToString());
    }

    if (request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
    {
        var raw = await new StreamReader(request.Body, leaveOpen: true).ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new AuthRequestPayload(string.Empty, string.Empty, string.Empty);
        }

        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;

        return new AuthRequestPayload(
            GetStringProperty(root, "Email"),
            GetStringProperty(root, "Password"),
            GetStringProperty(root, "ConfirmPassword"));
    }

    return new AuthRequestPayload(string.Empty, string.Empty, string.Empty);
}

string? GetStringProperty(JsonElement root, string propertyName)
{
    if (root.ValueKind != JsonValueKind.Object)
    {
        return null;
    }

    if (!root.TryGetProperty(propertyName, out var property))
    {
        foreach (var candidate in root.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                break;
            }
        }

        if (property.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }
    }

    return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
}

public sealed record AuthRequestPayload(string? Email, string? Password, string? ConfirmPassword);

public partial class Program { }
