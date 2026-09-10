using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.IntegrationTests;

public class AuthTests
{
    [Fact]
    public async Task Anonymous_Request_To_Accounts_Returns401()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Seed_Creates_Single_Default_Household()
    {
        await using var app = new TreasuryHostFactory();

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();

        var households = await db.Households.ToListAsync();

        households.Should().HaveCount(1);
        households[0].Name.Should().Be("Default Household");
    }

    [Fact]
    public async Task Anonymous_Request_To_Transactions_Page_Redirects_To_Login()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Register_Submit_Accepts_Json_Body_When_Household_Name_Provided()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var payload = new
        {
            Email = "json-register@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            HouseholdNameOrId = "Nowak Household"
        };

        var response = await client.PostAsJsonAsync("/auth/register-submit", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task Register_Submit_With_Existing_Household_Guid_Assigns_User_To_That_Household()
    {
        await using var app = new TreasuryHostFactory();

        await using (var setupScope = app.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
            db.Households.Add(new Household
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "Existing Household"
            });
            await db.SaveChangesAsync();
        }

        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var payload = new
        {
            Email = "existing-guid@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            HouseholdNameOrId = "22222222-2222-2222-2222-222222222222"
        };

        var response = await client.PostAsJsonAsync("/auth/register-submit", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        await using var assertScope = app.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
        var user = await assertDb.Users.SingleAsync(x => x.Email == "existing-guid@example.com");
        user.HouseholdId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    }

    [Fact]
    public async Task Register_Submit_With_Household_Name_Creates_New_Household_And_Assigns_User()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var payload = new
        {
            Email = "new-household@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            HouseholdNameOrId = "Fresh Household"
        };

        var response = await client.PostAsJsonAsync("/auth/register-submit", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        await using var assertScope = app.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
        var createdHousehold = await assertDb.Households.SingleAsync(x => x.Name == "Fresh Household");
        var user = await assertDb.Users.SingleAsync(x => x.Email == "new-household@example.com");

        user.HouseholdId.Should().Be(createdHousehold.Id);
    }

    [Fact]
    public async Task Register_Submit_With_Unknown_Household_Guid_Redirects_With_Error()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var payload = new
        {
            Email = "unknown-guid@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            HouseholdNameOrId = "33333333-3333-3333-3333-333333333333"
        };

        var response = await client.PostAsJsonAsync("/auth/register-submit", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/auth/register?error=");
    }

    [Fact]
    public async Task Register_Submit_With_Empty_Household_Name_Or_Id_Redirects_With_Error()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var payload = new
        {
            Email = "empty-household@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            HouseholdNameOrId = ""
        };

        var response = await client.PostAsJsonAsync("/auth/register-submit", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/auth/register?error=");
    }
}