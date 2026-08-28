using Microsoft.EntityFrameworkCore;
using Treasury.App.Domain;

namespace Treasury.App.Infrastructure.Data.Seed;

public static class InitialSeed
{
    public static async Task SeedAsync(TreasuryDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.Households.AnyAsync(cancellationToken))
        {
            db.Households.Add(new Household
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "Default Household"
            });
        }

        if (!await db.Accounts.AnyAsync(cancellationToken))
        {
            var householdId = Guid.Parse("11111111-1111-1111-1111-111111111111");

            db.Accounts.AddRange(
                new Account
                {
                    HouseholdId = householdId,
                    OwnerUserId = "seed",
                    Name = "Main wallet",
                    Currency = "PLN",
                    AccountType = "cash-wallet",
                    CurrentBalance = 12840.58m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Account
                {
                    HouseholdId = householdId,
                    OwnerUserId = "seed",
                    Name = "Family EUR",
                    Currency = "EUR",
                    AccountType = "bank-account",
                    CurrentBalance = 2840.00m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Account
                {
                    HouseholdId = householdId,
                    OwnerUserId = "seed",
                    Name = "Emergency fund",
                    Currency = "PLN",
                    AccountType = "savings",
                    CurrentBalance = 4300.00m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
        }

        if (!await db.Transactions.AnyAsync(cancellationToken))
        {
            var householdId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var mainWallet = await db.Accounts.Where(x => x.HouseholdId == householdId && x.Name == "Main wallet")
                .Select(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (mainWallet != Guid.Empty)
            {
                db.Transactions.AddRange(
                    new Transaction
                    {
                        HouseholdId = householdId,
                        AccountId = mainWallet,
                        Description = "Salary deposit",
                        Category = "Income",
                        Amount = 4200.00m,
                        Currency = "PLN",
                        Type = "income",
                        TransactionDate = DateTime.UtcNow.AddDays(-2),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Transaction
                    {
                        HouseholdId = householdId,
                        AccountId = mainWallet,
                        Description = "Groceries",
                        Category = "Groceries",
                        Amount = 480.32m,
                        Currency = "PLN",
                        Type = "expense",
                        TransactionDate = DateTime.UtcNow.AddDays(-1),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Transaction
                    {
                        HouseholdId = householdId,
                        AccountId = mainWallet,
                        Description = "Home savings transfer",
                        Category = "Savings",
                        Amount = 1000.00m,
                        Currency = "PLN",
                        Type = "transfer",
                        TransactionDate = DateTime.UtcNow.AddDays(-3),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
            }
        }

        if (!await db.BudgetCategories.AnyAsync(cancellationToken))
        {
            var householdId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            db.BudgetCategories.AddRange(
                new BudgetCategory
                {
                    HouseholdId = householdId,
                    Name = "Groceries",
                    MonthlyLimit = 2200.00m,
                    Currency = "PLN",
                    Notes = "Food and household supplies",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new BudgetCategory
                {
                    HouseholdId = householdId,
                    Name = "Housing",
                    MonthlyLimit = 3200.00m,
                    Currency = "PLN",
                    Notes = "Rent, mortgage and housing",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new BudgetCategory
                {
                    HouseholdId = householdId,
                    Name = "Utilities",
                    MonthlyLimit = 900.00m,
                    Currency = "PLN",
                    Notes = "Internet, electricity, water",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new BudgetCategory
                {
                    HouseholdId = householdId,
                    Name = "Leisure",
                    MonthlyLimit = 900.00m,
                    Currency = "PLN",
                    Notes = "Restaurants and fun spending",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
        }

        if (!await db.Bills.AnyAsync(cancellationToken))
        {
            var householdId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            db.Bills.AddRange(
                new Bill
                {
                    HouseholdId = householdId,
                    Name = "Rent",
                    Category = "Housing",
                    Amount = 2850.00m,
                    Currency = "PLN",
                    DueDay = 5,
                    IsPaid = false,
                    Notes = "Apartment rent",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Bill
                {
                    HouseholdId = householdId,
                    Name = "Internet",
                    Category = "Utilities",
                    Amount = 119.99m,
                    Currency = "PLN",
                    DueDay = 12,
                    IsPaid = false,
                    Notes = "Home internet bill",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new Bill
                {
                    HouseholdId = householdId,
                    Name = "Gym",
                    Category = "Health",
                    Amount = 190.00m,
                    Currency = "PLN",
                    DueDay = 18,
                    IsPaid = true,
                    Notes = "Family membership",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
