using FluentAssertions;
using System.Net;

namespace Treasury.IntegrationTests;

public class AccountsEndpointTests
{
    [Fact]
    public async Task Accounts_Endpoint_Requires_Authentication()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}