using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;

namespace Treasury.App.Infrastructure.Data.Seed;

public static class InitialSeed
{
    public static async Task SeedAsync(TreasuryDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Households.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Households.Add(new Household
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Default Household"
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
