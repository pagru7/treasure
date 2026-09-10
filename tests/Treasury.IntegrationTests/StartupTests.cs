using FluentAssertions;
using System.Net;

namespace Treasury.IntegrationTests;

public class StartupTests
{
    [Fact]
    public async Task Get_Health_Returns200()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient();
        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}