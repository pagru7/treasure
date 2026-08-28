using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;

namespace Treasury.App.Infrastructure.Data;

public class TreasuryDbContext : IdentityDbContext<ApplicationUser>
{
    public TreasuryDbContext(DbContextOptions<TreasuryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Household> Households => Set<Household>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
}
