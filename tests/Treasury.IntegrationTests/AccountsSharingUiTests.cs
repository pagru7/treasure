using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using Treasury.App.Application.Accounts;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.IntegrationTests;

public class AccountsSharingUiTests
{
    [Fact]
    public async Task Accounts_Page_Shows_Shared_Users_And_Household_Picker()
    {
        await using var app = new TreasuryHostFactory();

        var ownerClient = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
        var sharedClient = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var ownerEmail = $"owner-{Guid.NewGuid():N}@example.com";
        var sharedEmail = $"shared-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!";

        await RegisterAndSignInAsync(ownerClient, ownerEmail, password);
        await RegisterAndSignInAsync(sharedClient, sharedEmail, password);

        var accountResponse = await ownerClient.PostAsJsonAsync("/api/accounts", new
        {
            Name = "Shared account",
            Currency = "PLN",
            AccountType = "cash-wallet"
        });
        accountResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        using var accountJson = JsonDocument.Parse(await accountResponse.Content.ReadAsStringAsync());
        var accountId = accountJson.RootElement.GetProperty("id").GetGuid();

        var shareResponse = await ownerClient.PostAsJsonAsync($"/api/accounts/{accountId}/share-readonly", new
        {
            Email = sharedEmail
        });
        shareResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Owner's accounts page should show shared-user and (eventually) a household picker in the UI
        var pageResponse = await ownerClient.GetAsync("/accounts");
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await pageResponse.Content.ReadAsStringAsync();
        // Core expectations preserved from the brief
        body.Should().Contain("Shared with:");
        body.Should().Contain(sharedEmail);
        // Explicit picker semantics expected in the UI
        body.Should().Contain("Share with household user");
        body.Should().Contain("Share read-only");
        body.Should().NotContain(ownerEmail);
        // Explicit picker exclusion check — the owner's email should not appear as a selectable household option
        body.Should().NotContain($"<option value=\"{ownerEmail}\"");

        // No manual email-entry should be present for household sharing
        body.Should().NotContain("input type=\"email\"");
        body.Should().NotContain("Enter email");
        body.Should().NotContain("Invite by email");

        // Verify shared user can see the shared account but does not see owner-only share controls
        var sharedPageResponse = await sharedClient.GetAsync("/accounts");
        sharedPageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var sharedBody = await sharedPageResponse.Content.ReadAsStringAsync();
        sharedBody.Should().Contain("Shared account");
        // Shared users should not see the 'Share read-only' control for accounts they only have read access to
        sharedBody.Should().NotContain("Share read-only");
    }

    [Fact]
    public async Task Accounts_Page_Renders_When_Household_Users_Fail_To_Load()
    {
        await using var app = new AccountsPageHostFactory(new HouseholdUsersFailingAccountSharingService());

        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        await RegisterAndSignInAsync(client, $"owner-{Guid.NewGuid():N}@example.com", "Password123!");

        var pageResponse = await client.GetAsync("/accounts");
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await pageResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Loaded account");
        body.Should().Contain("Unable to load household users.");
    }

    [Fact]
    public async Task Accounts_Page_Renders_Accounts_When_Shared_Viewers_Fail_To_Load()
    {
        await using var app = new AccountsPageHostFactory(new SharedViewersFailingAccountSharingService());

        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        await RegisterAndSignInAsync(client, $"owner-{Guid.NewGuid():N}@example.com", "Password123!");

        var pageResponse = await client.GetAsync("/accounts");
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await pageResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Loaded account");
        body.Should().Contain("Unable to load shared viewer details.");
        body.Should().Contain("Shared viewer details unavailable.");
        body.Should().NotContain("Nobody yet.");
    }

    [Fact]
    public async Task Accounts_Page_Loads_Shared_Viewers_In_One_Batch()
    {
        var service = new BatchedSharedViewerAccountSharingService();
        await using var app = new AccountsPageHostFactory(service);

        var client = app.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        await RegisterAndSignInAsync(client, $"owner-{Guid.NewGuid():N}@example.com", "Password123!");

        var pageResponse = await client.GetAsync("/accounts");
        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await pageResponse.Content.ReadAsStringAsync();
        body.Should().Contain("Account A");
        body.Should().Contain("Viewer One");
        body.Should().Contain("Viewer Two");
        service.SharedViewerBatchCalls.Should().Be(1);
        service.SharedViewerSingleCalls.Should().Be(0);
    }

    private static async Task RegisterAndSignInAsync(HttpClient client, string email, string password)
    {
        var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
        {
            Email = email,
            Password = password,
            ConfirmPassword = password
        });

        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
        {
            Email = email,
            Password = password
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    private sealed class AccountsPageHostFactory(AccountSharingService accountSharingService) : TreasuryHostFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<AccountSharingService>();
                services.AddSingleton(accountSharingService);
            });
        }
    }

    private sealed class HouseholdUsersFailingAccountSharingService() : AccountSharingService(null!, null!)
    {
        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct) =>
            Task.FromResult(new List<Account>
            {
                new()
                {
                    Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    HouseholdId = user.HouseholdId,
                    OwnerUserId = user.Id,
                    Name = "Loaded account",
                    Currency = "PLN",
                    AccountType = "cash-wallet",
                    CurrentBalance = 12.34m
                }
            });

        public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
            throw new InvalidOperationException("household users query failed");

        public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct) =>
            Task.FromResult(new List<AccountViewerAssignment>
            {
                new(
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    user.Id,
                    "viewer@example.com",
                    "Viewer One")
            });
    }

    private sealed class SharedViewersFailingAccountSharingService() : AccountSharingService(null!, null!)
    {
        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct) =>
            Task.FromResult(new List<Account>
            {
                new()
                {
                    Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    HouseholdId = user.HouseholdId,
                    OwnerUserId = user.Id,
                    Name = "Loaded account",
                    Currency = "PLN",
                    AccountType = "cash-wallet",
                    CurrentBalance = 12.34m
                }
            });

        public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
            Task.FromResult(new List<HouseholdUserChoice>
            {
                new("viewer@example.com", "Viewer")
            });

        public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct) =>
            throw new InvalidOperationException("shared viewers query failed");
    }

    private sealed class BatchedSharedViewerAccountSharingService() : AccountSharingService(null!, null!)
    {
        public int SharedViewerBatchCalls { get; private set; }
        public int SharedViewerSingleCalls { get; private set; }

        public override Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct) =>
            Task.FromResult(new List<Account>
            {
                new()
                {
                    Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                    HouseholdId = user.HouseholdId,
                    OwnerUserId = user.Id,
                    Name = "Account A",
                    Currency = "PLN",
                    AccountType = "cash-wallet",
                    CurrentBalance = 1m
                },
                new()
                {
                    Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                    HouseholdId = user.HouseholdId,
                    OwnerUserId = user.Id,
                    Name = "Account B",
                    Currency = "PLN",
                    AccountType = "cash-wallet",
                    CurrentBalance = 2m
                }
            });

        public override Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
            Task.FromResult(new List<HouseholdUserChoice>
            {
                new("viewer-one@example.com", "Viewer One"),
                new("viewer-two@example.com", "Viewer Two")
            });

        public override Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
        {
            SharedViewerBatchCalls++;

            var sharedViewers = new List<AccountViewerAssignment>();
            foreach (var accountId in accountIds)
            {
                sharedViewers.Add(new AccountViewerAssignment(accountId, "viewer-1", "viewer-one@example.com", "Viewer One"));
                sharedViewers.Add(new AccountViewerAssignment(accountId, "viewer-2", "viewer-two@example.com", "Viewer Two"));
            }

            return Task.FromResult(sharedViewers);
        }

        public override Task<List<AccountViewerChoice>> GetSharedViewersAsync(ApplicationUser user, Guid accountId, CancellationToken ct)
        {
            SharedViewerSingleCalls++;
            throw new InvalidOperationException("single-account shared viewer query should not be used");
        }
    }
}
