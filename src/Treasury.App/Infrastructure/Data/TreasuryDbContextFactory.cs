using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Treasury.App.Infrastructure.Data;

public sealed class TreasuryDbContextFactory : IDesignTimeDbContextFactory<TreasuryDbContext>
{
    public TreasuryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=treasury;Username=treasury;Password=treasury";

        var optionsBuilder = new DbContextOptionsBuilder<TreasuryDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new TreasuryDbContext(optionsBuilder.Options);
    }
}
