using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Treasury.IntegrationTests;

public class FeedbackWorkflowTests
{
    [Fact]
    public async Task Create_Feedback_Defaults_Status_To_New()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var response = await client.PostAsJsonAsync("/api/feedback", new
        {
            Title = "Support export",
            Description = "Please add data export."
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("status").GetString().Should().Be("new");
    }

    [Fact]
    public async Task Update_Feedback_Changes_Status_And_Description()
    {
        await using var app = new TreasuryHostFactory();
        var client = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/feedback", new
        {
            Title = "CSV import",
            Description = "Please add CSV import."
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("id").GetGuid();

        var updateResponse = await client.PutAsJsonAsync($"/api/feedback/{id}", new
        {
            Id = id,
            Status = "implementing",
            Description = "Implementation started."
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var listResponse = await client.GetAsync("/api/feedback");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var list = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var item = list.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
        item.GetProperty("status").GetString().Should().Be("implementing");
        item.GetProperty("description").GetString().Should().Be("Implementation started.");
    }

    [Fact]
    public async Task Feedback_Is_Household_Isolated()
    {
        await using var app = new TreasuryHostFactory();
        var clientA = CreateAuthenticatedClient(app);
        var clientB = CreateAuthenticatedClient(app);
        await RegisterAndSignInAsync(clientA, householdName: $"household-a-{Guid.NewGuid():N}");
        await RegisterAndSignInAsync(clientB, householdName: $"household-b-{Guid.NewGuid():N}");

        var createResponse = await clientA.PostAsJsonAsync("/api/feedback", new
        {
            Title = "Private feedback",
            Description = "Only in household A"
        });
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("id").GetGuid();

        var updateByOtherHousehold = await clientB.PutAsJsonAsync($"/api/feedback/{id}", new
        {
            Id = id,
            Status = "done",
            Description = "Unauthorized update"
        });
        updateByOtherHousehold.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static HttpClient CreateAuthenticatedClient(TreasuryHostFactory app) =>
        app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private static async Task RegisterAndSignInAsync(HttpClient client, string? householdName = null)
    {
        var email = $"owner-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!";
        var household = householdName ?? $"household-{Guid.NewGuid():N}";

        var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
        {
            Email = email,
            Password = password,
            ConfirmPassword = password,
            HouseholdNameOrId = household
        });
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
        {
            Email = email,
            Password = password
        });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }
}
