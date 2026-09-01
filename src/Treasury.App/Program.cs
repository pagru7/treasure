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

app.MapGet("/api/dashboard/summary", async (TreasuryDbContext db) =>
{
    var accounts = await db.Accounts
        .Select(x => new AccountValueItem(x.Currency, x.CurrentBalance))
        .ToListAsync();

    var rates = new Dictionary<string, decimal>
    {
        ["EUR"] = 4.6m,
        ["USD"] = 3.9m,
        ["GBP"] = 5.3m
    };

    var summary = new DashboardSummaryService().BuildSummary(accounts, rates);
    return Results.Ok(summary);
});

app.MapGet("/api/tags", async (HttpContext httpContext, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var tags = await db.Tags
        .Where(x => x.HouseholdId == user.HouseholdId)
        .OrderBy(x => x.Name)
        .Select(x => new
        {
            x.Id,
            x.Name,
            x.Color
        })
        .ToListAsync();

    return Results.Ok(tags);
}).RequireAuthorization();

app.MapPost("/api/tags", async (HttpContext httpContext, CreateTagRequest request, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var name = request.Name?.Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["name"] = ["Tag name is required."]
        });
    }

    var exists = await db.Tags.AnyAsync(x => x.HouseholdId == user.HouseholdId && x.Name.ToLower() == name.ToLower());
    if (exists)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["name"] = ["This tag already exists for this household."]
        });
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
    await db.SaveChangesAsync();

    return Results.Created($"/api/tags/{tag.Id}", new
    {
        tag.Id,
        tag.Name,
        tag.Color
    });
}).RequireAuthorization();

app.MapGet("/api/budgets", async (HttpContext httpContext, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var budgets = await db.BudgetCategories
        .Where(x => x.HouseholdId == user.HouseholdId)
        .OrderBy(x => x.Name)
        .Select(x => new
        {
            x.Id,
            x.Name,
            x.MonthlyLimit,
            x.Currency,
            x.Notes
        })
        .ToListAsync();

    return Results.Ok(budgets);
}).RequireAuthorization();

app.MapPost("/api/budgets", async (HttpContext httpContext, CreateBudgetCategoryRequest request, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.Name) || request.MonthlyLimit <= 0m)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["name"] = ["Budget category is required."],
            ["monthlyLimit"] = ["Monthly limit must be greater than zero."]
        });
    }

    var budget = new BudgetCategory
    {
        HouseholdId = user.HouseholdId,
        Name = request.Name.Trim(),
        MonthlyLimit = request.MonthlyLimit,
        Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
        Notes = request.Notes?.Trim() ?? string.Empty,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    db.BudgetCategories.Add(budget);
    await db.SaveChangesAsync();

    return Results.Created($"/api/budgets/{budget.Id}", new
    {
        budget.Id,
        budget.Name,
        budget.MonthlyLimit,
        budget.Currency,
        budget.Notes
    });
}).RequireAuthorization();

app.MapGet("/api/bills", async (HttpContext httpContext, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var bills = await db.Bills
        .Where(x => x.HouseholdId == user.HouseholdId)
        .OrderBy(x => x.DueDay)
        .ThenBy(x => x.Name)
        .Select(x => new
        {
            x.Id,
            x.Name,
            x.Category,
            x.Amount,
            x.Currency,
            x.DueDay,
            x.IsPaid,
            x.Notes
        })
        .ToListAsync();

    return Results.Ok(bills);
}).RequireAuthorization();

app.MapPost("/api/bills", async (HttpContext httpContext, CreateBillRequest request, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
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
            ["name"] = ["Bill name is required."]
        });
    }

    var bill = new Bill
    {
        HouseholdId = user.HouseholdId,
        Name = request.Name.Trim(),
        Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
        Amount = request.Amount,
        Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
        DueDay = request.DueDay <= 0 ? 1 : Math.Min(request.DueDay, 31),
        IsPaid = request.IsPaid,
        Notes = request.Notes?.Trim() ?? string.Empty,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    db.Bills.Add(bill);
    await db.SaveChangesAsync();

    return Results.Created($"/api/bills/{bill.Id}", new
    {
        bill.Id,
        bill.Name,
        bill.Category,
        bill.Amount,
        bill.Currency,
        bill.DueDay,
        bill.IsPaid,
        bill.Notes
    });
}).RequireAuthorization();

app.MapPost("/api/valuations/bullion", async (HttpContext httpContext, UpdateBullionValueRequest request, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.AssetName) || request.Weight <= 0m || request.Purity <= 0m || request.Purity > 1m || request.UnitPrice <= 0m)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["assetName"] = ["Asset name is required."],
            ["weight"] = ["Weight must be greater than zero."],
            ["purity"] = ["Purity must be between 0 and 1."],
            ["unitPrice"] = ["Unit price must be greater than zero."]
        });
    }

    var total = BullionFormulaCalculator.Calculate(request.Weight, request.Purity, request.UnitPrice);
    var valuation = new AssetValuation
    {
        HouseholdId = user.HouseholdId,
        AssetName = request.AssetName.Trim(),
        Kind = ValuationKind.BullionFormula,
        Quantity = 1m,
        Weight = request.Weight,
        Purity = request.Purity,
        CurrentUnitValue = request.UnitPrice,
        CurrentTotalValue = total,
        Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
        ValuationDate = request.ValuationDate == default ? DateTime.UtcNow : request.ValuationDate,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    db.AssetValuations.Add(valuation);
    await db.SaveChangesAsync();

    return Results.Created($"/api/valuations/{valuation.Id}", new
    {
        valuation.Id,
        valuation.AssetName,
        valuation.Kind,
        valuation.CurrentUnitValue,
        valuation.CurrentTotalValue,
        valuation.Currency,
        valuation.ValuationDate
    });
}).RequireAuthorization(Policies.OwnerOnly);

app.MapPost("/api/valuations/coin", async (HttpContext httpContext, UpdateCoinValueRequest request, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.AssetName) || request.Quantity <= 0m || request.CurrentUnitValue <= 0m)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["assetName"] = ["Asset name is required."],
            ["quantity"] = ["Quantity must be greater than zero."],
            ["currentUnitValue"] = ["Current unit value must be greater than zero."]
        });
    }

    var valuation = new AssetValuation
    {
        HouseholdId = user.HouseholdId,
        AssetName = request.AssetName.Trim(),
        Kind = ValuationKind.CoinManual,
        Quantity = request.Quantity,
        Weight = 0m,
        Purity = 0m,
        CurrentUnitValue = request.CurrentUnitValue,
        CurrentTotalValue = decimal.Round(request.Quantity * request.CurrentUnitValue, 4, MidpointRounding.AwayFromZero),
        Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
        ValuationDate = request.ValuationDate == default ? DateTime.UtcNow : request.ValuationDate,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    db.AssetValuations.Add(valuation);
    await db.SaveChangesAsync();

    return Results.Created($"/api/valuations/{valuation.Id}", new
    {
        valuation.Id,
        valuation.AssetName,
        valuation.Kind,
        valuation.Quantity,
        valuation.CurrentUnitValue,
        valuation.CurrentTotalValue,
        valuation.Currency,
        valuation.ValuationDate
    });
}).RequireAuthorization(Policies.OwnerOnly);

app.MapGet("/api/accounts", async (HttpContext httpContext, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var accounts = await db.Accounts
        .Where(x =>
            x.HouseholdId == user.HouseholdId
            && (x.OwnerUserId == user.Id
                || x.OwnerUserId == "seed"
                || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
        .OrderBy(x => x.Name)
        .Select(x => new AccountResponse
        {
            Id = x.Id,
            Name = x.Name,
            Currency = x.Currency,
            AccountType = x.AccountType,
            CurrentBalance = x.CurrentBalance,
            IsReadOnly = x.OwnerUserId != user.Id
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

    var accountType = string.IsNullOrWhiteSpace(request.AccountType) ? "cash-wallet" : request.AccountType.Trim().ToLowerInvariant();
    var isKnownType = await db.AccountTypes.AnyAsync(x => x.HouseholdId == user.HouseholdId && x.Name == accountType);
    if (!isKnownType)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["accountType"] = ["Unknown account type for this household."]
        });
    }

    var account = new Account
    {
        HouseholdId = user.HouseholdId,
        OwnerUserId = user.Id,
        Name = request.Name.Trim(),
        Currency = string.IsNullOrWhiteSpace(request.Currency) ? "PLN" : request.Currency.Trim().ToUpperInvariant(),
        AccountType = accountType,
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
        CurrentBalance = account.CurrentBalance,
        IsReadOnly = false
    };

    return Results.Created($"/api/accounts/{account.Id}", response);
}).RequireAuthorization();

app.MapPost("/api/accounts/{id:guid}/share-readonly", async (HttpContext httpContext, Guid id, ShareAccountReadOnlyRequest request, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var email = request.Email?.Trim();
    if (string.IsNullOrWhiteSpace(email))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["email"] = ["Viewer email is required."]
        });
    }

    var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == user.HouseholdId);
    if (account is null)
    {
        return Results.NotFound();
    }

    if (account.OwnerUserId != user.Id)
    {
        return Results.Forbid();
    }

    var viewer = await userManager.FindByEmailAsync(email);
    if (viewer is null || viewer.HouseholdId != user.HouseholdId)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["email"] = ["Viewer must be an existing user from the same household."]
        });
    }

    if (viewer.Id == user.Id)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["email"] = ["You already own this account."]
        });
    }

    var existingRule = await db.VisibilityRules.SingleOrDefaultAsync(x => x.AccountId == account.Id && x.ViewerUserId == viewer.Id);
    if (existingRule is null)
    {
        db.VisibilityRules.Add(new VisibilityRule
        {
            AccountId = account.Id,
            ViewerUserId = viewer.Id,
            IsReadOnly = true,
            CreatedAt = DateTime.UtcNow
        });
    }
    else
    {
        existingRule.IsReadOnly = true;
    }

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        AccountId = account.Id,
        ViewerUserId = viewer.Id,
        IsReadOnly = true
    });
}).RequireAuthorization(Policies.OwnerOnly);

app.MapGet("/api/accounts/{id:guid}/transactions", async (HttpContext httpContext, Guid id, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var canAccess = await db.Accounts.AnyAsync(x =>
        x.Id == id
        && x.HouseholdId == user.HouseholdId
        && (x.OwnerUserId == user.Id || x.OwnerUserId == "seed" || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)));
    if (!canAccess)
    {
        return Results.NotFound();
    }

    var transactions = await db.Transactions
        .Where(x => x.HouseholdId == user.HouseholdId && x.AccountId == id)
        .OrderByDescending(x => x.TransactionDate)
        .Select(x => new
        {
            x.Id,
            x.AccountId,
            x.Description,
            x.Category,
            x.Amount,
            x.Currency,
            x.Type,
            x.TransactionDate
        })
        .ToListAsync();

    return Results.Ok(transactions);
}).RequireAuthorization();

app.MapGet("/api/transactions", async (HttpContext httpContext, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var visibleAccountIds = db.Accounts
        .Where(x =>
            x.HouseholdId == user.HouseholdId
            && (x.OwnerUserId == user.Id
                || x.OwnerUserId == "seed"
                || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
        .Select(x => x.Id);

    var transactions = await db.Transactions
        .Where(x => x.HouseholdId == user.HouseholdId && visibleAccountIds.Contains(x.AccountId))
        .OrderByDescending(x => x.TransactionDate)
        .Select(x => new
        {
            x.Id,
            x.AccountId,
            x.Description,
            x.Category,
            x.Amount,
            x.Currency,
            x.Type,
            x.TransactionDate,
            Tags = x.TransactionTags.Select(tt => new
            {
                tt.Tag.Id,
                tt.Tag.Name,
                tt.Tag.Color
            }).ToList()
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
    if (account.OwnerUserId != user.Id)
    {
        return Results.Forbid();
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
        Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
        Amount = request.Amount,
        Currency = string.IsNullOrWhiteSpace(request.Currency) ? account.Currency : request.Currency.Trim().ToUpperInvariant(),
        Type = normalizedType,
        TransactionDate = request.TransactionDate == default ? DateTime.UtcNow : request.TransactionDate,
    };

    var validTagIds = await db.Tags
        .Where(x => x.HouseholdId == user.HouseholdId && request.TagIds.Contains(x.Id))
        .Select(x => x.Id)
        .ToListAsync();

    foreach (var tagId in validTagIds)
    {
        transaction.TransactionTags.Add(new TransactionTag
        {
            Transaction = transaction,
            TagId = tagId
        });
    }

    db.Transactions.Add(transaction);
    account.CurrentBalance += delta;
    account.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Created($"/api/transactions/{transaction.Id}", new
    {
        transaction.Id,
        transaction.AccountId,
        transaction.Description,
        transaction.Category,
        transaction.Amount,
        transaction.Currency,
        transaction.Type,
        transaction.TransactionDate,
        Tags = validTagIds
    });
}).RequireAuthorization();

app.MapPost("/api/transfers", async (HttpContext httpContext, CreateTransferRequest request, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    if (request.FromAccountId == Guid.Empty || request.ToAccountId == Guid.Empty || request.FromAccountId == request.ToAccountId || request.Amount <= 0m)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["fromAccountId"] = ["A valid source account is required."],
            ["toAccountId"] = ["A valid destination account is required."],
            ["amount"] = ["Transfer amount must be greater than zero."]
        });
    }

    var fromAccount = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.FromAccountId && x.HouseholdId == user.HouseholdId);
    var toAccount = await db.Accounts.SingleOrDefaultAsync(x => x.Id == request.ToAccountId && x.HouseholdId == user.HouseholdId);
    if (fromAccount is null || toAccount is null)
    {
        return Results.NotFound();
    }

    if (fromAccount.OwnerUserId != user.Id || toAccount.OwnerUserId != user.Id)
    {
        return Results.Forbid();
    }

    var transferDate = request.TransferDate == default ? DateTime.UtcNow : request.TransferDate;
    var currency = string.IsNullOrWhiteSpace(request.Currency) ? fromAccount.Currency : request.Currency.Trim().ToUpperInvariant();
    var description = string.IsNullOrWhiteSpace(request.Description) ? "Account transfer" : request.Description.Trim();

    var transfer = new Transfer
    {
        HouseholdId = user.HouseholdId,
        FromAccountId = fromAccount.Id,
        ToAccountId = toAccount.Id,
        Amount = request.Amount,
        Currency = currency,
        Description = description,
        TransferDate = transferDate,
        CreatedAt = DateTime.UtcNow
    };

    var outflow = new Transaction
    {
        HouseholdId = user.HouseholdId,
        AccountId = fromAccount.Id,
        Description = $"{description} -> {toAccount.Name}",
        Category = "Transfer",
        Amount = request.Amount,
        Currency = currency,
        Type = "transfer-out",
        TransactionDate = transferDate,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    var inflow = new Transaction
    {
        HouseholdId = user.HouseholdId,
        AccountId = toAccount.Id,
        Description = $"{description} <- {fromAccount.Name}",
        Category = "Transfer",
        Amount = request.Amount,
        Currency = currency,
        Type = "transfer-in",
        TransactionDate = transferDate,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    fromAccount.CurrentBalance -= Math.Abs(request.Amount);
    toAccount.CurrentBalance += Math.Abs(request.Amount);
    fromAccount.UpdatedAt = DateTime.UtcNow;
    toAccount.UpdatedAt = DateTime.UtcNow;

    db.Transfers.Add(transfer);
    db.Transactions.Add(outflow);
    db.Transactions.Add(inflow);
    await db.SaveChangesAsync();

    return Results.Created($"/api/transfers/{transfer.Id}", new
    {
        transfer.Id,
        transfer.FromAccountId,
        transfer.ToAccountId,
        transfer.Amount,
        transfer.Currency,
        transfer.TransferDate
    });
}).RequireAuthorization(Policies.OwnerOnly);

app.MapPost("/api/accounts/{id:guid}/balance-correction", async (HttpContext httpContext, Guid id, BalanceCorrectionRequest request, TreasuryDbContext db, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == id && x.HouseholdId == user.HouseholdId);
    if (account is null)
    {
        return Results.NotFound();
    }

    if (account.OwnerUserId != user.Id)
    {
        return Results.Forbid();
    }

    var delta = request.NewBalance - account.CurrentBalance;
    account.CurrentBalance = request.NewBalance;
    account.UpdatedAt = DateTime.UtcNow;

    db.Transactions.Add(new Transaction
    {
        HouseholdId = user.HouseholdId,
        AccountId = account.Id,
        Description = string.IsNullOrWhiteSpace(request.Description) ? "Balance correction" : request.Description.Trim(),
        Category = "Correction",
        Amount = delta,
        Currency = account.Currency,
        Type = "balance-correction",
        TransactionDate = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    });

    await db.SaveChangesAsync();

    return Results.Ok(new
    {
        account.Id,
        account.CurrentBalance
    });
}).RequireAuthorization(Policies.OwnerOnly);

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
