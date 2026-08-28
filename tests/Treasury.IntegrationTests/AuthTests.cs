using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
}
