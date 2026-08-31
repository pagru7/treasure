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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TransactionTag>()
            .HasKey(x => new { x.TransactionId, x.TagId });

        modelBuilder.Entity<TransactionTag>()
            .HasOne(x => x.Transaction)
            .WithMany(x => x.TransactionTags)
            .HasForeignKey(x => x.TransactionId);

        modelBuilder.Entity<TransactionTag>()
            .HasOne(x => x.Tag)
            .WithMany(x => x.TransactionTags)
            .HasForeignKey(x => x.TagId);
    }

    public DbSet<Household> Households => Set<Household>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TransactionTag> TransactionTags => Set<TransactionTag>();
    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();
    public DbSet<Bill> Bills => Set<Bill>();
}
