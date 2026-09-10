using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Treasury.App.Application.Accounts;
using Treasury.App.Application.Transactions;
using Treasury.App.Application.Transfers;
using Treasury.App.Common;
using Treasury.App.Components;
using Treasury.App.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDatabase(builder.Configuration, builder.Environment);

builder.Services.AddAuthenticationAndAuthorization();

builder.Services.AddHealthChecks();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddFastEndpoints();
builder.Services.AddMudServices();
builder.Services.AddScoped<AccountSharingService>();
builder.Services.AddScoped<TransferCreationService>();
builder.Services.AddScoped<TransactionEditingService>();
builder.Services.AddScoped<AccountBalanceRecalculationService>();

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

    // Recompute every account once at startup so any 0-default balance rows are repaired
    // while preserving each account's opening balance baseline.
    var balanceRecalculationService = scope.ServiceProvider.GetRequiredService<AccountBalanceRecalculationService>();
    await balanceRecalculationService.RecalculateAllAccountsAsync(CancellationToken.None);
}

app.MapHealthChecks("/health");

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

public partial class Program
{ }