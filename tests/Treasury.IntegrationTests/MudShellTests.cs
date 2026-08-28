using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Treasury.IntegrationTests;

public class MudShellTests
{
    [Fact]
    public async Task Anonymous_User_Is_Redirected_To_Login()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/auth/login");
    }

    [Fact]
    public async Task Login_Page_Loads_Without_Antiforgery_Error()
    {
        await using var app = new TreasuryHostFactory();
        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/auth/login");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
