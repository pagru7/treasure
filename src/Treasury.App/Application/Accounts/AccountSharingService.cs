using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;
using Treasury.App.Infrastructure.Data;

namespace Treasury.App.Application.Accounts;

public sealed record HouseholdUserChoice(string Email, string DisplayName);
public sealed record AccountViewerChoice(string ViewerUserId, string Email, string DisplayName);
public sealed record AccountViewerAssignment(Guid AccountId, string ViewerUserId, string Email, string DisplayName);
public sealed record ShareReadOnlyResult(bool Succeeded, string? ErrorMessage);

public class AccountSharingService(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
{
    public virtual Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, CancellationToken ct) =>
        GetVisibleAccountsAsync(user, includeInactive: false, ct);

    public virtual Task<List<Account>> GetVisibleAccountsAsync(ApplicationUser user, bool includeInactive, CancellationToken ct) =>
        db.Accounts
            .Where(x =>
                x.HouseholdId == user.HouseholdId
                && (includeInactive || x.IsActive)
                && (x.OwnerUserId == user.Id
                    || x.OwnerUserId == "seed"
                    || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public virtual Task<List<HouseholdUserChoice>> GetHouseholdUsersAsync(ApplicationUser user, CancellationToken ct) =>
        db.Users
            .Where(x => x.HouseholdId == user.HouseholdId && x.Id != user.Id)
            .OrderBy(x => x.Email ?? x.UserName ?? x.Id)
            .Select(x => new HouseholdUserChoice(
                x.Email ?? x.UserName ?? x.Id,
                x.Email ?? x.UserName ?? x.Id))
            .ToListAsync(ct);

    public virtual Task<List<AccountViewerChoice>> GetSharedViewersAsync(ApplicationUser user, Guid accountId, CancellationToken ct) =>
        (from rule in db.VisibilityRules
         join viewer in db.Users on rule.ViewerUserId equals viewer.Id
         where rule.AccountId == accountId && viewer.HouseholdId == user.HouseholdId
         orderby viewer.Email ?? viewer.UserName ?? viewer.Id
         select new AccountViewerChoice(
             viewer.Id,
             viewer.Email ?? viewer.UserName ?? viewer.Id,
             viewer.Email ?? viewer.UserName ?? viewer.Id)).ToListAsync(ct);

    public virtual async Task<List<AccountViewerAssignment>> GetSharedViewersAsync(ApplicationUser user, IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
    {
        var distinctAccountIds = accountIds.Distinct().ToArray();
        if (distinctAccountIds.Length == 0)
        {
            return [];
        }

        return await (from rule in db.VisibilityRules
                join viewer in db.Users on rule.ViewerUserId equals viewer.Id
                where distinctAccountIds.Contains(rule.AccountId) && viewer.HouseholdId == user.HouseholdId
                orderby rule.AccountId, viewer.Email ?? viewer.UserName ?? viewer.Id
                select new AccountViewerAssignment(
                    rule.AccountId,
                    viewer.Id,
                    viewer.Email ?? viewer.UserName ?? viewer.Id,
                    viewer.Email ?? viewer.UserName ?? viewer.Id))
            .ToListAsync(ct);
    }

    public virtual async Task<ShareReadOnlyResult> ShareReadOnlyAsync(ApplicationUser user, Guid accountId, string viewerEmail, CancellationToken ct)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == accountId && x.HouseholdId == user.HouseholdId, ct);
        if (account is null)
        {
            return new ShareReadOnlyResult(false, "Account not found.");
        }

        if (account.OwnerUserId != user.Id)
        {
            return new ShareReadOnlyResult(false, "Only the owner can share this account.");
        }

        if (string.IsNullOrWhiteSpace(viewerEmail))
        {
            return new ShareReadOnlyResult(false, "Email is required.");
        }

        var normalizedEmail = viewerEmail.Trim();
        var viewer = await userManager.FindByEmailAsync(normalizedEmail);
        if (viewer is null || viewer.HouseholdId != user.HouseholdId)
        {
            return new ShareReadOnlyResult(false, "Choose a user from the current household.");
        }

        if (viewer.Id == user.Id)
        {
            return new ShareReadOnlyResult(false, "You already own this account.");
        }

        var existingRule = await db.VisibilityRules.SingleOrDefaultAsync(x => x.AccountId == accountId && x.ViewerUserId == viewer.Id, ct);
        if (existingRule is null)
        {
            db.VisibilityRules.Add(new VisibilityRule
            {
                AccountId = accountId,
                ViewerUserId = viewer.Id,
                IsReadOnly = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existingRule.IsReadOnly = true;
        }

        await db.SaveChangesAsync(ct);
        return new ShareReadOnlyResult(true, null);
    }
}
