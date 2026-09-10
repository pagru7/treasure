using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Treasury.App.Application.Valuations;
using Treasury.App.Infrastructure.Data;

namespace Treasury.IntegrationTests;

public class Phase1AcceptanceTests
{
    [Fact]
    public async Task Phase1_Seed_Contains_Default_AccountTypes_And_FxRates()
    {
        await using var app = new TreasuryHostFactory();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();

        var accountTypes = await db.AccountTypes
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync();
        var rates = await db.CurrencyRates
            .OrderBy(x => x.ToCurrency)
            .Select(x => new { x.FromCurrency, x.ToCurrency })
            .ToListAsync();

        accountTypes.Should().Contain(["bank-account", "cash-wallet", "credit-card", "savings"]);
        rates.Should().Contain(x => x.FromCurrency == "PLN" && x.ToCurrency == "EUR");
        rates.Should().Contain(x => x.FromCurrency == "PLN" && x.ToCurrency == "USD");
    }

    [Fact]
    public void BullionFormula_Uses_Weight_Purity_UnitPrice()
    {
        var value = BullionFormulaCalculator.Calculate(100m, 0.9999m, 320m);
        value.Should().Be(31996.8m);
    }

    [Fact]
    public async Task Phase1_Write_Endpoints_Are_Protected_For_Anonymous_User()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient();

        var accounts = await client.GetAsync("/api/accounts");
        var accountTypes = await client.GetAsync("/api/account-types");
        var transfers = await client.PostAsJsonAsync("/api/transfers", new
        {
            FromAccountId = Guid.NewGuid(),
            ToAccountId = Guid.NewGuid(),
            Amount = 1m
        });
        var valuations = await client.PostAsJsonAsync("/api/valuations/coin", new
        {
            AssetName = "x",
            Quantity = 1m,
            CurrentUnitValue = 1m
        });

        accounts.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        accountTypes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        transfers.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        valuations.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}