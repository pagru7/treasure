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

        modelBuilder.Entity<CurrencyRate>()
            .HasIndex(x => new { x.HouseholdId, x.FromCurrency, x.ToCurrency })
            .IsUnique();

        modelBuilder.Entity<VisibilityRule>()
            .HasKey(x => new { x.AccountId, x.ViewerUserId });

        modelBuilder.Entity<VisibilityRule>()
            .HasOne(x => x.Account)
            .WithMany(x => x.VisibilityRules)
            .HasForeignKey(x => x.AccountId);

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

        modelBuilder.Entity<Account>()
            .Property(x => x.IsActive)
            .HasDefaultValue(true);

        modelBuilder.Entity<Account>()
            .Property(x => x.BankAccountNumber)
            .HasMaxLength(34);
    }

    public DbSet<Household> Households => Set<Household>();
    public DbSet<AccountTypeDefinition> AccountTypes => Set<AccountTypeDefinition>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<VisibilityRule> VisibilityRules => Set<VisibilityRule>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();
    public DbSet<AssetValuation> AssetValuations => Set<AssetValuation>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<TransactionTag> TransactionTags => Set<TransactionTag>();
    public DbSet<BudgetCategory> BudgetCategories => Set<BudgetCategory>();
    public DbSet<Bill> Bills => Set<Bill>();
}
