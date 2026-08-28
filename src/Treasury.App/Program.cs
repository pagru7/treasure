using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Treasury.App.Application.Dashboard;
using Treasury.App.Components;
using Treasury.App.Contracts.Accounts;
using Treasury.App.Contracts.Transactions;
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

app.MapGet("/api/dashboard/summary", async (TreasuryDbContext db) =>
{
    var accounts = await db.Accounts
        .Select(x => new AccountValueItem(x.Currency, x.CurrentBalance))
        .ToListAsync();

    var rates = new Dictionary<string, decimal>
    {
        ["EUR"] = 4.6m,
        ["USD"] = 3.9m
    };

    var summary = new DashboardSummaryService().BuildSummary(accounts, rates);
    return Results.Ok(summary);
});

app.MapGet("/api/accounts", async (HttpContext httpContext, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var accounts = await db.Accounts
        .Where(x => x.HouseholdId == user.HouseholdId)
        .OrderBy(x => x.Name)
        .Select(x => new AccountResponse
        {
            Id = x.Id,
            Name = x.Name,
            Currency = x.Currency,
            AccountType = x.AccountType,
            CurrentBalance = x.CurrentBalance
        })
        .ToListAsync();

    return Results.Ok(accounts);
}).RequireAuthorization();

app.MapPost("/api/accounts", async (HttpContext httpContext, CreateAccountRequest request, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["name"] = ["Account name is required."]
        });
    }

    var account = new Account
    {
        HouseholdId = user.HouseholdId,
        OwnerUserId = user.Id,
        Name = request.Name.Trim(),
        Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
        AccountType = string.IsNullOrWhiteSpace(request.AccountType) ? "cash-wallet" : request.AccountType.Trim(),
        CurrentBalance = 0m
    };

    db.Accounts.Add(account);
    await db.SaveChangesAsync();

    var response = new AccountResponse
    {
        Id = account.Id,
        Name = account.Name,
        Currency = account.Currency,
        AccountType = account.AccountType,
        CurrentBalance = account.CurrentBalance
    };

    return Results.Created($"/api/accounts/{account.Id}", response);
}).RequireAuthorization();

app.MapGet("/api/transactions", async (HttpContext httpContext, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var transactions = await db.Transactions
        .Where(x => x.HouseholdId == user.HouseholdId)
        .OrderByDescending(x => x.TransactionDate)
        .Select(x => new
        {
            x.Id,
            x.AccountId,
            x.Description,
            x.Amount,
            x.Currency,
            x.Type,
            x.TransactionDate
        })
        .ToListAsync();

    return Results.Ok(transactions);
}).RequireAuthorization();

app.MapPost("/api/transactions", async (HttpContext httpContext, CreateTransactionRequest request, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    if (request.AccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.Description))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["accountId"] = ["A valid account is required."],
            ["description"] = ["Description is required."]
        });
    }

    var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.AccountId && x.HouseholdId == user.HouseholdId);
    if (account is null)
    {
        return Results.NotFound();
    }

    var normalizedType = (request.Type ?? "expense").Trim().ToLowerInvariant();
    var delta = request.Amount;
    if (normalizedType == "expense")
    {
        delta = -Math.Abs(request.Amount);
    }
    else if (normalizedType == "income")
    {
        delta = Math.Abs(request.Amount);
    }
    else if (normalizedType == "transfer")
    {
        delta = request.Amount;
    }

    var transaction = new Transaction
    {
        HouseholdId = user.HouseholdId,
        AccountId = account.Id,
        Description = request.Description.Trim(),
        Amount = request.Amount,
        Currency = string.IsNullOrWhiteSpace(request.Currency) ? account.Currency : request.Currency.Trim().ToUpperInvariant(),
        Type = normalizedType,
        TransactionDate = request.TransactionDate == default ? DateTime.UtcNow : request.TransactionDate,
    };

    db.Transactions.Add(transaction);
    account.CurrentBalance += delta;
    account.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Created($"/api/transactions/{transaction.Id}", new
    {
        transaction.Id,
        transaction.AccountId,
        transaction.Description,
        transaction.Amount,
        transaction.Currency,
        transaction.Type,
        transaction.TransactionDate
    });
}).RequireAuthorization();

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

    if (request.ContentLength is > 0 && request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
    {
        var raw = await new StreamReader(request.Body, leaveOpen: true).ReadToEndAsync();
        request.Body.Position = 0;

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
    if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var property))
    {
        return null;
    }

    return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
}

public sealed record AuthRequestPayload(string? Email, string? Password, string? ConfirmPassword);

public partial class Program { }
