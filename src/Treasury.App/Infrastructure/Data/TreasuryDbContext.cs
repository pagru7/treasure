using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Treasury.App.Domain;

namespace Treasury.App.Infrastructure.Data;

public class TreasuryDbContext : IdentityDbContext<ApplicationUser>
{
    public TreasuryDbContext(DbContextOptions<TreasuryDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
        {
            return;
        }

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Database=treasury;Username=postgres;Password=postgres";

        optionsBuilder.UseNpgsql(connectionString);
    }

    public DbSet<Household> Households => Set<Household>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();
    public DbSet<Bill> Bills => Set<Bill>();
}
