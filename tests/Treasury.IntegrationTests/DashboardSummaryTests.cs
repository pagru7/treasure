using FluentAssertions;
using System.Net;

namespace Treasury.IntegrationTests;

public class DashboardSummaryTests
{
    [Fact]
    public async Task Dashboard_Summary_Returns_Currency_Totals_And_Pln_Total()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient();

        var response = await client.GetAsync("/api/dashboard/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("totalPln");
        body.Should().Contain("totalsByCurrency");
    }
}