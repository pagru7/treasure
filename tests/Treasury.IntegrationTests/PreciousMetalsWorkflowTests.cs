using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Treasury.App.Infrastructure.Data;

namespace Treasury.IntegrationTests;

public class PreciousMetalsWorkflowTests
{
    [Fact]
    public async Task Create_PreciousMetal_Saves_Item_And_Initial_Value()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        var email = await RegisterAndSignInAsync(client);
        var purchaseDate = DateTime.UtcNow.AddDays(-5);

        var response = await client.PostAsJsonAsync("/api/precious-metals", new
        {
            Name = "Gold Bar 100g",
            PurchasePrice = 25000m,
            PurchaseDate = purchaseDate,
            CurrentValue = 26250m
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        var asset = await db.Set<Treasury.App.Domain.PreciousMetalAsset>()
            .SingleAsync(x => x.HouseholdId == user.HouseholdId && x.Name == "Gold Bar 100g");
        var entries = await db.Set<Treasury.App.Domain.PreciousMetalValueEntry>()
            .Where(x => x.PreciousMetalAssetId == asset.Id)
            .ToListAsync();

        asset.CurrentValue.Should().Be(26250m);
        entries.Should().HaveCount(1);
        entries[0].Value.Should().Be(26250m);
    }

    [Fact]
    public async Task Update_PreciousMetal_Value_On_Different_Day_Appends_History()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        _ = await RegisterAndSignInAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/precious-metals", new
        {
            Name = "Silver Coins Lot",
            PurchasePrice = 4000m,
            PurchaseDate = DateTime.UtcNow.AddDays(-20),
            CurrentValue = 4200m
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var assetId = created.RootElement.GetProperty("id").GetGuid();
        var updateDate = DateTime.UtcNow.AddDays(-1);

        var updateResponse = await client.PostAsJsonAsync($"/api/precious-metals/{assetId}/values", new
        {
            CurrentValue = 4350m,
            ValueDate = updateDate
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
        var asset = await db.Set<Treasury.App.Domain.PreciousMetalAsset>().SingleAsync(x => x.Id == assetId);
        var entries = await db.Set<Treasury.App.Domain.PreciousMetalValueEntry>()
            .Where(x => x.PreciousMetalAssetId == assetId)
            .OrderBy(x => x.ValueDate)
            .ToListAsync();

        asset.CurrentValue.Should().Be(4350m);
        entries.Should().HaveCount(2);
        entries.Should().Contain(x => x.Value == 4350m);
    }

    [Fact]
    public async Task PreciousMetals_List_Has_Detail_Link_And_Detail_Page_Shows_Price_History()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        _ = await RegisterAndSignInAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/precious-metals", new
        {
            Name = "Maple Leaf",
            PurchasePrice = 1000m,
            PurchaseDate = DateTime.UtcNow.AddDays(-10),
            CurrentValue = 1200m
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var assetId = created.RootElement.GetProperty("id").GetGuid();

        var updateResponse = await client.PostAsJsonAsync($"/api/precious-metals/{assetId}/values", new
        {
            CurrentValue = 1300m,
            ValueDate = DateTime.UtcNow
        });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listPage = await client.GetAsync("/precious-metals");
        listPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var listHtml = await listPage.Content.ReadAsStringAsync();
        listHtml.Should().Contain($"/precious-metals/{assetId}");

        var detailPage = await client.GetAsync($"/precious-metals/{assetId}");
        detailPage.StatusCode.Should().Be(HttpStatusCode.OK);
        var detailHtml = await detailPage.Content.ReadAsStringAsync();
        detailHtml.Should().Contain("Price history");
        detailHtml.Should().Contain("Maple Leaf");
        detailHtml.Should().Contain("Profit at date");
    }

    [Fact]
    public async Task Update_PreciousMetal_Value_Rejects_Future_ValueDate()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        _ = await RegisterAndSignInAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/precious-metals", new
        {
            Name = "Future blocked",
            PurchasePrice = 1000m,
            PurchaseDate = DateTime.UtcNow.AddDays(-10),
            CurrentValue = 1050m
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var assetId = created.RootElement.GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync($"/api/precious-metals/{assetId}/values", new
        {
            CurrentValue = 1100m,
            ValueDate = DateTime.UtcNow.AddDays(1)
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_PreciousMetal_Value_Same_Day_Updates_Existing_Daily_Entry()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        _ = await RegisterAndSignInAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/precious-metals", new
        {
            Name = "One per day",
            PurchasePrice = 1000m,
            PurchaseDate = DateTime.UtcNow.AddDays(-10),
            CurrentValue = 1200m
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var assetId = created.RootElement.GetProperty("id").GetGuid();

        var day = DateTime.UtcNow.Date.AddDays(-1);
        var firstResponse = await client.PostAsJsonAsync($"/api/precious-metals/{assetId}/values", new
        {
            CurrentValue = 1210m,
            ValueDate = day.AddHours(8)
        });
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await client.PostAsJsonAsync($"/api/precious-metals/{assetId}/values", new
        {
            CurrentValue = 1250m,
            ValueDate = day.AddHours(16)
        });
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
        var asset = await db.Set<Treasury.App.Domain.PreciousMetalAsset>().SingleAsync(x => x.Id == assetId);
        var entriesForDay = await db.Set<Treasury.App.Domain.PreciousMetalValueEntry>()
            .Where(x => x.PreciousMetalAssetId == assetId && x.ValueDate.Date == day.Date)
            .ToListAsync();

        asset.CurrentValue.Should().Be(1250m);
        entriesForDay.Should().HaveCount(1);
        entriesForDay[0].Value.Should().Be(1250m);
    }

    private static HttpClient CreateAuthenticatedClient(TreasuryHostFactory app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task<string> RegisterAndSignInAsync(HttpClient client)
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!";

        var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
        {
            Email = email,
            Password = password,
            ConfirmPassword = password,
            HouseholdNameOrId = $"household-{Guid.NewGuid():N}"
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
        {
            Email = email,
            Password = password
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        return email;
    }
}
