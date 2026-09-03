# Review Package

Base: e5746bee3bf63a861ef79719f8fc7d7d3cd1e2d0
Head: ae09ae7b72395e4fd17e3ca6a00a55c8908de62d

## Commits

ae09ae7 feat: add transaction balance tracking and edit rules

## Diff Stat

 .superpowers/sdd/phase2-task-3-report.md           |  36 +
 .../Transfers/TransferCreationService.cs           |   2 +
 .../Transactions/UpdateTransactionRequest.cs       |  12 +
 src/Treasury.App/Domain/Transaction.cs             |   1 +
 .../Accounts/GetAccountTransactionsEndpoint.cs     |   5 +-
 .../Transactions/CreateTransactionEndpoint.cs      |   9 +-
 .../Transactions/GetTransactionsEndpoint.cs        |   3 +
 .../Transactions/UpdateTransactionEndpoint.cs      | 217 ++++++
 .../Infrastructure/Data/Seed/InitialSeed.cs        |   3 +
 ...dTransactionBalanceAfterTransaction.Designer.cs | 747 +++++++++++++++++++++
 ...134028_AddTransactionBalanceAfterTransaction.cs |  29 +
 .../Migrations/TreasuryDbContextModelSnapshot.cs   |   7 +-
 src/Treasury.App/Pages/Transactions.razor          | 346 +++++++++-
 .../TransactionEditingRulesTests.cs                | 184 +++++
 14 files changed, 1577 insertions(+), 24 deletions(-)

## Full Diff (-U10)

diff --git a/.superpowers/sdd/phase2-task-3-report.md b/.superpowers/sdd/phase2-task-3-report.md
new file mode 100644
index 0000000..3ffa572
--- /dev/null
+++ b/.superpowers/sdd/phase2-task-3-report.md
@@ -0,0 +1,36 @@
+# Phase 2 Task 3 Report
+
+## Summary
+
+Implemented transaction running-balance tracking and edit-rule enforcement.
+
+### Data/model
+- Added `Transaction.BalanceAfterTransaction`.
+- Generated EF migration `20260903134028_AddTransactionBalanceAfterTransaction`.
+- Updated the model snapshot and seeded historical transactions with running-balance values.
+
+### Write paths
+- `CreateTransactionEndpoint` now stores `BalanceAfterTransaction` on new transactions.
+- `TransferCreationService` now stores running balances for transfer outflow/inflow transactions.
+- Added `PUT /api/transactions/{id:guid}` via `UpdateTransactionEndpoint`.
+
+### Edit rules
+- Non-latest transactions can only change description, category, and tags.
+- Latest transactions can change amount, date, and type, and the account balance plus running balances are recomputed.
+- Transactions page now shows an edit affordance and disables amount/date/type editing for historical rows.
+
+### API/read updates
+- Transaction list endpoints now return `BalanceAfterTransaction`.
+- Transaction ordering is now deterministic with `TransactionDate`, `CreatedAt`, and `Id`.
+
+## Tests
+
+Passed:
+- `dotnet test tests\\Treasury.IntegrationTests\\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransactionEditingRulesTests"`
+- `dotnet test tests\\Treasury.IntegrationTests\\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests|FullyQualifiedName~TransfersWorkflowTests|FullyQualifiedName~SharedReadOnlyUiPermissionTests"`
+- `dotnet test tests\\Treasury.IntegrationTests\\Treasury.IntegrationTests.csproj -c Debug`
+
+## Notes
+
+- The migration adds `BalanceAfterTransaction` with a default value of `0m` for existing rows.
+- Latest-transaction edits recompute the full account running balance chain so date changes remain consistent.
diff --git a/src/Treasury.App/Application/Transfers/TransferCreationService.cs b/src/Treasury.App/Application/Transfers/TransferCreationService.cs
index 7c942a7..0436856 100644
--- a/src/Treasury.App/Application/Transfers/TransferCreationService.cs
+++ b/src/Treasury.App/Application/Transfers/TransferCreationService.cs
@@ -144,20 +144,22 @@ public class TransferCreationService(TreasuryDbContext db)
             Amount = request.Amount,
             Currency = currency,
             Type = "transfer-in",
             TransactionDate = transferDate,
             CreatedAt = utcNow,
             UpdatedAt = utcNow
         };
 
         fromAccount.CurrentBalance -= Math.Abs(request.Amount);
         toAccount.CurrentBalance += Math.Abs(request.Amount);
+        outflow.BalanceAfterTransaction = fromAccount.CurrentBalance;
+        inflow.BalanceAfterTransaction = toAccount.CurrentBalance;
         fromAccount.UpdatedAt = utcNow;
         toAccount.UpdatedAt = utcNow;
 
         async Task PersistAsync()
         {
             db.Transfers.Add(transfer);
             db.Transactions.Add(outflow);
             db.Transactions.Add(inflow);
             await db.SaveChangesAsync(ct);
 
diff --git a/src/Treasury.App/Contracts/Transactions/UpdateTransactionRequest.cs b/src/Treasury.App/Contracts/Transactions/UpdateTransactionRequest.cs
new file mode 100644
index 0000000..55149d4
--- /dev/null
+++ b/src/Treasury.App/Contracts/Transactions/UpdateTransactionRequest.cs
@@ -0,0 +1,12 @@
+namespace Treasury.App.Contracts.Transactions;
+
+public sealed class UpdateTransactionRequest
+{
+    public Guid Id { get; set; }
+    public string Description { get; set; } = string.Empty;
+    public string Category { get; set; } = "General";
+    public decimal Amount { get; set; }
+    public string Type { get; set; } = "expense";
+    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
+    public List<Guid> TagIds { get; set; } = new();
+}
diff --git a/src/Treasury.App/Domain/Transaction.cs b/src/Treasury.App/Domain/Transaction.cs
index ab920e5..0673a4c 100644
--- a/src/Treasury.App/Domain/Transaction.cs
+++ b/src/Treasury.App/Domain/Transaction.cs
@@ -4,15 +4,16 @@ public class Transaction
 {
     public Guid Id { get; set; } = Guid.NewGuid();
     public Guid HouseholdId { get; set; }
     public Guid AccountId { get; set; }
     public string Description { get; set; } = string.Empty;
     public string Category { get; set; } = "General";
     public decimal Amount { get; set; }
     public string Currency { get; set; } = "PLN";
     public string Type { get; set; } = "expense";
     public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
+    public decimal BalanceAfterTransaction { get; set; }
     public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
     public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
 
     public ICollection<TransactionTag> TransactionTags { get; set; } = new List<TransactionTag>();
 }
diff --git a/src/Treasury.App/Endpoints/Accounts/GetAccountTransactionsEndpoint.cs b/src/Treasury.App/Endpoints/Accounts/GetAccountTransactionsEndpoint.cs
index a67b401..eb2997d 100644
--- a/src/Treasury.App/Endpoints/Accounts/GetAccountTransactionsEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Accounts/GetAccountTransactionsEndpoint.cs
@@ -35,26 +35,29 @@ public sealed class GetAccountTransactionsEndpoint(TreasuryDbContext db, UserMan
             && (x.OwnerUserId == user.Id || x.OwnerUserId == "seed" || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)), ct);
         if (!canAccess)
         {
             await SendNotFoundAsync(ct);
             return;
         }
 
         var transactions = await db.Transactions
             .Where(x => x.HouseholdId == user.HouseholdId && x.AccountId == request.Id)
             .OrderByDescending(x => x.TransactionDate)
+            .ThenByDescending(x => x.CreatedAt)
+            .ThenByDescending(x => x.Id)
             .Select(x => new
             {
                 x.Id,
                 x.AccountId,
                 x.Description,
                 x.Category,
                 x.Amount,
                 x.Currency,
                 x.Type,
-                x.TransactionDate
+                x.TransactionDate,
+                x.BalanceAfterTransaction
             })
             .ToListAsync(ct);
 
         await SendOkAsync(transactions, ct);
     }
 }
diff --git a/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs b/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs
index 12cdc21..3f3ce6f 100644
--- a/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Transactions/CreateTransactionEndpoint.cs
@@ -70,55 +70,60 @@ public sealed class CreateTransactionEndpoint(TreasuryDbContext db, UserManager<
         }
         else if (normalizedType == "income")
         {
             delta = Math.Abs(request.Amount);
         }
         else if (normalizedType == "transfer")
         {
             delta = request.Amount;
         }
 
+        var utcNow = DateTime.UtcNow;
         var transaction = new Transaction
         {
             HouseholdId = user.HouseholdId,
             AccountId = account.Id,
             Description = request.Description.Trim(),
             Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
             Amount = request.Amount,
             Currency = string.IsNullOrWhiteSpace(request.Currency) ? account.Currency : request.Currency.Trim().ToUpperInvariant(),
             Type = normalizedType,
-            TransactionDate = request.TransactionDate == default ? DateTime.UtcNow : request.TransactionDate
+            TransactionDate = request.TransactionDate == default ? utcNow : request.TransactionDate,
+            CreatedAt = utcNow,
+            UpdatedAt = utcNow
         };
 
         var validTagIds = await db.Tags
             .Where(x => x.HouseholdId == user.HouseholdId && request.TagIds.Contains(x.Id))
             .Select(x => x.Id)
             .ToListAsync(ct);
 
         foreach (var tagId in validTagIds)
         {
             transaction.TransactionTags.Add(new TransactionTag
             {
                 Transaction = transaction,
                 TagId = tagId
             });
         }
 
         db.Transactions.Add(transaction);
         account.CurrentBalance += delta;
-        account.UpdatedAt = DateTime.UtcNow;
+        transaction.BalanceAfterTransaction = account.CurrentBalance;
+        account.UpdatedAt = utcNow;
         await db.SaveChangesAsync(ct);
 
         await SendAsync(new
         {
             transaction.Id,
             transaction.AccountId,
             transaction.Description,
             transaction.Category,
             transaction.Amount,
             transaction.Currency,
             transaction.Type,
             transaction.TransactionDate,
+            transaction.BalanceAfterTransaction,
             Tags = validTagIds
         }, StatusCodes.Status201Created, ct);
     }
 }
diff --git a/src/Treasury.App/Endpoints/Transactions/GetTransactionsEndpoint.cs b/src/Treasury.App/Endpoints/Transactions/GetTransactionsEndpoint.cs
index e31b52e..205dd76 100644
--- a/src/Treasury.App/Endpoints/Transactions/GetTransactionsEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Transactions/GetTransactionsEndpoint.cs
@@ -27,30 +27,33 @@ public sealed class GetTransactionsEndpoint(TreasuryDbContext db, UserManager<Ap
             .Where(x =>
                 x.HouseholdId == user.HouseholdId
                 && (x.OwnerUserId == user.Id
                     || x.OwnerUserId == "seed"
                     || x.VisibilityRules.Any(v => v.ViewerUserId == user.Id)))
             .Select(x => x.Id);
 
         var transactions = await db.Transactions
             .Where(x => x.HouseholdId == user.HouseholdId && visibleAccountIds.Contains(x.AccountId))
             .OrderByDescending(x => x.TransactionDate)
+            .ThenByDescending(x => x.CreatedAt)
+            .ThenByDescending(x => x.Id)
             .Select(x => new
             {
                 x.Id,
                 x.AccountId,
                 x.Description,
                 x.Category,
                 x.Amount,
                 x.Currency,
                 x.Type,
                 x.TransactionDate,
+                x.BalanceAfterTransaction,
                 Tags = x.TransactionTags.Select(tt => new
                 {
                     tt.Tag.Id,
                     tt.Tag.Name,
                     tt.Tag.Color
                 }).ToList()
             })
             .ToListAsync(ct);
 
         await SendOkAsync(transactions, ct);
diff --git a/src/Treasury.App/Endpoints/Transactions/UpdateTransactionEndpoint.cs b/src/Treasury.App/Endpoints/Transactions/UpdateTransactionEndpoint.cs
new file mode 100644
index 0000000..09d56d4
--- /dev/null
+++ b/src/Treasury.App/Endpoints/Transactions/UpdateTransactionEndpoint.cs
@@ -0,0 +1,217 @@
+using FastEndpoints;
+using Microsoft.AspNetCore.Identity;
+using Microsoft.EntityFrameworkCore;
+using Treasury.App.Contracts.Transactions;
+using Treasury.App.Domain;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.App.Endpoints.Transactions;
+
+public sealed class UpdateTransactionEndpoint(TreasuryDbContext db, UserManager<ApplicationUser> userManager)
+    : Endpoint<UpdateTransactionRequest>
+{
+    public override void Configure()
+    {
+        Put("/api/transactions/{id:guid}");
+        Policies(global::Treasury.App.Infrastructure.Auth.Policies.SharedReadOnly);
+    }
+
+    public override async Task HandleAsync(UpdateTransactionRequest request, CancellationToken ct)
+    {
+        var user = await userManager.GetUserAsync(User);
+        if (user is null)
+        {
+            await SendUnauthorizedAsync(ct);
+            return;
+        }
+
+        if (request.Id == Guid.Empty || string.IsNullOrWhiteSpace(request.Description))
+        {
+            if (request.Id == Guid.Empty)
+            {
+                AddError(x => x.Id, "A valid transaction is required.");
+            }
+
+            if (string.IsNullOrWhiteSpace(request.Description))
+            {
+                AddError(x => x.Description, "Description is required.");
+            }
+
+            await SendErrorsAsync(cancellation: ct);
+            return;
+        }
+
+        var transaction = await db.Transactions
+            .Include(x => x.TransactionTags)
+            .SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
+        if (transaction is null)
+        {
+            await SendNotFoundAsync(ct);
+            return;
+        }
+
+        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == transaction.AccountId && x.HouseholdId == user.HouseholdId, ct);
+        if (account is null)
+        {
+            await SendNotFoundAsync(ct);
+            return;
+        }
+
+        if (account.OwnerUserId != user.Id)
+        {
+            await SendForbiddenAsync(ct);
+            return;
+        }
+
+        var trackedTransactions = await db.Transactions
+            .Where(x => x.AccountId == account.Id)
+            .ToListAsync(ct);
+
+        var originalSnapshots = trackedTransactions
+            .Select(x => new TransactionBalanceSnapshot(x.Id, x.Amount, x.Type, x.TransactionDate, x.CreatedAt))
+            .ToList();
+
+        var latestTransactionId = originalSnapshots
+            .OrderByDescending(x => x.TransactionDate)
+            .ThenByDescending(x => x.CreatedAt)
+            .ThenByDescending(x => x.Id)
+            .Select(x => x.Id)
+            .FirstOrDefault();
+
+        var isLatest = latestTransactionId == transaction.Id;
+
+        var normalizedDescription = request.Description.Trim();
+        var normalizedCategory = string.IsNullOrWhiteSpace(request.Category) ? transaction.Category : request.Category.Trim();
+        var normalizedType = NormalizeType(string.IsNullOrWhiteSpace(request.Type) ? transaction.Type : request.Type);
+        var normalizedTransactionDate = request.TransactionDate == default ? transaction.TransactionDate : request.TransactionDate;
+
+        var hasErrors = false;
+
+        if (!isLatest)
+        {
+            if (request.Amount != transaction.Amount)
+            {
+                AddError(x => x.Amount, "Amount can only be changed on the latest transaction.");
+                hasErrors = true;
+            }
+
+            if (normalizedTransactionDate != transaction.TransactionDate)
+            {
+                AddError(x => x.TransactionDate, "Transaction date can only be changed on the latest transaction.");
+                hasErrors = true;
+            }
+
+            if (!string.Equals(normalizedType, transaction.Type, StringComparison.OrdinalIgnoreCase))
+            {
+                AddError(x => x.Type, "Type can only be changed on the latest transaction.");
+                hasErrors = true;
+            }
+        }
+        else
+        {
+            if (request.Amount <= 0m)
+            {
+                AddError(x => x.Amount, "Amount must be greater than zero.");
+                hasErrors = true;
+            }
+        }
+
+        if (hasErrors)
+        {
+            await SendErrorsAsync(cancellation: ct);
+            return;
+        }
+
+        transaction.Description = normalizedDescription;
+        transaction.Category = string.IsNullOrWhiteSpace(normalizedCategory) ? "General" : normalizedCategory;
+        transaction.Amount = isLatest ? request.Amount : transaction.Amount;
+        transaction.Type = isLatest ? normalizedType : transaction.Type;
+        transaction.TransactionDate = isLatest ? normalizedTransactionDate : transaction.TransactionDate;
+        transaction.UpdatedAt = DateTime.UtcNow;
+
+        var validTagIds = await db.Tags
+            .Where(x => x.HouseholdId == user.HouseholdId && request.TagIds.Contains(x.Id))
+            .Select(x => x.Id)
+            .ToListAsync(ct);
+
+        db.TransactionTags.RemoveRange(transaction.TransactionTags);
+        transaction.TransactionTags.Clear();
+
+        foreach (var tagId in validTagIds)
+        {
+            transaction.TransactionTags.Add(new TransactionTag
+            {
+                Transaction = transaction,
+                TagId = tagId
+            });
+        }
+
+        if (isLatest)
+        {
+            var updatedSnapshots = originalSnapshots
+                .Select(x => x.Id == transaction.Id
+                    ? x with
+                    {
+                        Amount = transaction.Amount,
+                        Type = transaction.Type,
+                        TransactionDate = transaction.TransactionDate
+                    }
+                    : x)
+                .ToList();
+
+            var initialBalance = account.CurrentBalance - originalSnapshots.Sum(x => GetDelta(x.Amount, x.Type));
+            var runningBalance = initialBalance;
+            var utcNow = DateTime.UtcNow;
+            var trackedById = trackedTransactions.ToDictionary(x => x.Id);
+
+            foreach (var snapshot in updatedSnapshots
+                .OrderBy(x => x.TransactionDate)
+                .ThenBy(x => x.CreatedAt)
+                .ThenBy(x => x.Id))
+            {
+                runningBalance += GetDelta(snapshot.Amount, snapshot.Type);
+                var trackedTransaction = trackedById[snapshot.Id];
+                trackedTransaction.BalanceAfterTransaction = runningBalance;
+                trackedTransaction.UpdatedAt = utcNow;
+            }
+
+            account.CurrentBalance = runningBalance;
+            account.UpdatedAt = utcNow;
+        }
+
+        await db.SaveChangesAsync(ct);
+
+        await SendAsync(new
+        {
+            transaction.Id,
+            transaction.AccountId,
+            transaction.Description,
+            transaction.Category,
+            transaction.Amount,
+            transaction.Currency,
+            transaction.Type,
+            transaction.TransactionDate,
+            transaction.BalanceAfterTransaction,
+            Tags = validTagIds
+        }, StatusCodes.Status200OK, ct);
+    }
+
+    private static string NormalizeType(string type) =>
+        string.IsNullOrWhiteSpace(type) ? "expense" : type.Trim().ToLowerInvariant();
+
+    private static decimal GetDelta(decimal amount, string type)
+    {
+        var normalizedType = NormalizeType(type);
+        return normalizedType switch
+        {
+            "expense" => -Math.Abs(amount),
+            "income" => Math.Abs(amount),
+            "transfer" => Math.Abs(amount),
+            "transfer-in" => Math.Abs(amount),
+            "transfer-out" => -Math.Abs(amount),
+            _ => amount
+        };
+    }
+
+    private sealed record TransactionBalanceSnapshot(Guid Id, decimal Amount, string Type, DateTime TransactionDate, DateTime CreatedAt);
+}
diff --git a/src/Treasury.App/Infrastructure/Data/Seed/InitialSeed.cs b/src/Treasury.App/Infrastructure/Data/Seed/InitialSeed.cs
index 1e48e5d..6150421 100644
--- a/src/Treasury.App/Infrastructure/Data/Seed/InitialSeed.cs
+++ b/src/Treasury.App/Infrastructure/Data/Seed/InitialSeed.cs
@@ -107,46 +107,49 @@ public static class InitialSeed
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
+                        BalanceAfterTransaction = 13320.90m,
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
+                        BalanceAfterTransaction = 12840.58m,
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
+                        BalanceAfterTransaction = 9120.90m,
                         CreatedAt = DateTime.UtcNow,
                         UpdatedAt = DateTime.UtcNow
                     });
             }
         }
 
         if (!await db.CurrencyRates.AnyAsync(cancellationToken))
         {
             var householdId = Guid.Parse("11111111-1111-1111-1111-111111111111");
             db.CurrencyRates.AddRange(
diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903134028_AddTransactionBalanceAfterTransaction.Designer.cs b/src/Treasury.App/Infrastructure/Migrations/20260903134028_AddTransactionBalanceAfterTransaction.Designer.cs
new file mode 100644
index 0000000..b7a4ea8
--- /dev/null
+++ b/src/Treasury.App/Infrastructure/Migrations/20260903134028_AddTransactionBalanceAfterTransaction.Designer.cs
@@ -0,0 +1,747 @@
+﻿// <auto-generated />
+using System;
+using Microsoft.EntityFrameworkCore;
+using Microsoft.EntityFrameworkCore.Infrastructure;
+using Microsoft.EntityFrameworkCore.Migrations;
+using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
+using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
+using Treasury.App.Infrastructure.Data;
+
+#nullable disable
+
+namespace Treasury.App.Infrastructure.Migrations
+{
+    [DbContext(typeof(TreasuryDbContext))]
+    [Migration("20260903134028_AddTransactionBalanceAfterTransaction")]
+    partial class AddTransactionBalanceAfterTransaction
+    {
+        /// <inheritdoc />
+        protected override void BuildTargetModel(ModelBuilder modelBuilder)
+        {
+#pragma warning disable 612, 618
+            modelBuilder
+                .HasAnnotation("ProductVersion", "9.0.0")
+                .HasAnnotation("Relational:MaxIdentifierLength", 63);
+
+            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
+                {
+                    b.Property<string>("Id")
+                        .HasColumnType("text");
+
+                    b.Property<string>("ConcurrencyStamp")
+                        .IsConcurrencyToken()
+                        .HasColumnType("text");
+
+                    b.Property<string>("Name")
+                        .HasMaxLength(256)
+                        .HasColumnType("character varying(256)");
+
+                    b.Property<string>("NormalizedName")
+                        .HasMaxLength(256)
+                        .HasColumnType("character varying(256)");
+
+                    b.HasKey("Id");
+
+                    b.HasIndex("NormalizedName")
+                        .IsUnique()
+                        .HasDatabaseName("RoleNameIndex");
+
+                    b.ToTable("AspNetRoles", (string)null);
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
+                {
+                    b.Property<int>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("integer");
+
+                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));
+
+                    b.Property<string>("ClaimType")
+                        .HasColumnType("text");
+
+                    b.Property<string>("ClaimValue")
+                        .HasColumnType("text");
+
+                    b.Property<string>("RoleId")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.HasKey("Id");
+
+                    b.HasIndex("RoleId");
+
+                    b.ToTable("AspNetRoleClaims", (string)null);
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
+                {
+                    b.Property<int>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("integer");
+
+                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));
+
+                    b.Property<string>("ClaimType")
+                        .HasColumnType("text");
+
+                    b.Property<string>("ClaimValue")
+                        .HasColumnType("text");
+
+                    b.Property<string>("UserId")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.HasKey("Id");
+
+                    b.HasIndex("UserId");
+
+                    b.ToTable("AspNetUserClaims", (string)null);
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
+                {
+                    b.Property<string>("LoginProvider")
+                        .HasColumnType("text");
+
+                    b.Property<string>("ProviderKey")
+                        .HasColumnType("text");
+
+                    b.Property<string>("ProviderDisplayName")
+                        .HasColumnType("text");
+
+                    b.Property<string>("UserId")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.HasKey("LoginProvider", "ProviderKey");
+
+                    b.HasIndex("UserId");
+
+                    b.ToTable("AspNetUserLogins", (string)null);
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
+                {
+                    b.Property<string>("UserId")
+                        .HasColumnType("text");
+
+                    b.Property<string>("RoleId")
+                        .HasColumnType("text");
+
+                    b.HasKey("UserId", "RoleId");
+
+                    b.HasIndex("RoleId");
+
+                    b.ToTable("AspNetUserRoles", (string)null);
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
+                {
+                    b.Property<string>("UserId")
+                        .HasColumnType("text");
+
+                    b.Property<string>("LoginProvider")
+                        .HasColumnType("text");
+
+                    b.Property<string>("Name")
+                        .HasColumnType("text");
+
+                    b.Property<string>("Value")
+                        .HasColumnType("text");
+
+                    b.HasKey("UserId", "LoginProvider", "Name");
+
+                    b.ToTable("AspNetUserTokens", (string)null);
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Account", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<string>("AccountType")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<string>("BankAccountNumber")
+                        .HasMaxLength(34)
+                        .HasColumnType("character varying(34)");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Currency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<decimal>("CurrentBalance")
+                        .HasColumnType("numeric");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<bool>("IsActive")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("boolean")
+                        .HasDefaultValue(true);
+
+                    b.Property<string>("Name")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<string>("OwnerUserId")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("Accounts");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.AccountTypeDefinition", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Description")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<string>("Name")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("AccountTypes");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.ApplicationUser", b =>
+                {
+                    b.Property<string>("Id")
+                        .HasColumnType("text");
+
+                    b.Property<int>("AccessFailedCount")
+                        .HasColumnType("integer");
+
+                    b.Property<string>("ConcurrencyStamp")
+                        .IsConcurrencyToken()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Email")
+                        .HasMaxLength(256)
+                        .HasColumnType("character varying(256)");
+
+                    b.Property<bool>("EmailConfirmed")
+                        .HasColumnType("boolean");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<bool>("LockoutEnabled")
+                        .HasColumnType("boolean");
+
+                    b.Property<DateTimeOffset?>("LockoutEnd")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("NormalizedEmail")
+                        .HasMaxLength(256)
+                        .HasColumnType("character varying(256)");
+
+                    b.Property<string>("NormalizedUserName")
+                        .HasMaxLength(256)
+                        .HasColumnType("character varying(256)");
+
+                    b.Property<string>("PasswordHash")
+                        .HasColumnType("text");
+
+                    b.Property<string>("PhoneNumber")
+                        .HasColumnType("text");
+
+                    b.Property<bool>("PhoneNumberConfirmed")
+                        .HasColumnType("boolean");
+
+                    b.Property<string>("SecurityStamp")
+                        .HasColumnType("text");
+
+                    b.Property<bool>("TwoFactorEnabled")
+                        .HasColumnType("boolean");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("UserName")
+                        .HasMaxLength(256)
+                        .HasColumnType("character varying(256)");
+
+                    b.HasKey("Id");
+
+                    b.HasIndex("NormalizedEmail")
+                        .HasDatabaseName("EmailIndex");
+
+                    b.HasIndex("NormalizedUserName")
+                        .IsUnique()
+                        .HasDatabaseName("UserNameIndex");
+
+                    b.ToTable("AspNetUsers", (string)null);
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.AssetValuation", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<string>("AssetName")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Currency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<decimal>("CurrentTotalValue")
+                        .HasColumnType("numeric");
+
+                    b.Property<decimal>("CurrentUnitValue")
+                        .HasColumnType("numeric");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<int>("Kind")
+                        .HasColumnType("integer");
+
+                    b.Property<decimal>("Purity")
+                        .HasColumnType("numeric");
+
+                    b.Property<decimal>("Quantity")
+                        .HasColumnType("numeric");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<DateTime>("ValuationDate")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<decimal>("Weight")
+                        .HasColumnType("numeric");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("AssetValuations");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Bill", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<decimal>("Amount")
+                        .HasColumnType("numeric");
+
+                    b.Property<string>("Category")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Currency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<int>("DueDay")
+                        .HasColumnType("integer");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<bool>("IsPaid")
+                        .HasColumnType("boolean");
+
+                    b.Property<string>("Name")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<string>("Notes")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("Bills");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.BudgetCategory", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Currency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<decimal>("MonthlyLimit")
+                        .HasColumnType("numeric");
+
+                    b.Property<string>("Name")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<string>("Notes")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("BudgetCategories");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.CurrencyRate", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<DateTime>("EffectiveAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("FromCurrency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<decimal>("Rate")
+                        .HasColumnType("numeric");
+
+                    b.Property<string>("ToCurrency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.HasIndex("HouseholdId", "FromCurrency", "ToCurrency")
+                        .IsUnique();
+
+                    b.ToTable("CurrencyRates");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Household", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Name")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("Households");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Tag", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<string>("Color")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<string>("Name")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("Tags");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Transaction", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid>("AccountId")
+                        .HasColumnType("uuid");
+
+                    b.Property<decimal>("Amount")
+                        .HasColumnType("numeric");
+
+                    b.Property<decimal>("BalanceAfterTransaction")
+                        .HasColumnType("numeric");
+
+                    b.Property<string>("Category")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Currency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<string>("Description")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<DateTime>("TransactionDate")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Type")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("UpdatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("Transactions");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.TransactionTag", b =>
+                {
+                    b.Property<Guid>("TransactionId")
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid>("TagId")
+                        .HasColumnType("uuid");
+
+                    b.HasKey("TransactionId", "TagId");
+
+                    b.HasIndex("TagId");
+
+                    b.ToTable("TransactionTags");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Transfer", b =>
+                {
+                    b.Property<Guid>("Id")
+                        .ValueGeneratedOnAdd()
+                        .HasColumnType("uuid");
+
+                    b.Property<decimal>("Amount")
+                        .HasColumnType("numeric");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<string>("Currency")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<string>("Description")
+                        .IsRequired()
+                        .HasColumnType("text");
+
+                    b.Property<Guid>("FromAccountId")
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid>("HouseholdId")
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid?>("InflowTransactionId")
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid?>("OutflowTransactionId")
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid>("ToAccountId")
+                        .HasColumnType("uuid");
+
+                    b.Property<DateTime>("TransferDate")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.HasKey("Id");
+
+                    b.ToTable("Transfers");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.VisibilityRule", b =>
+                {
+                    b.Property<Guid>("AccountId")
+                        .HasColumnType("uuid");
+
+                    b.Property<string>("ViewerUserId")
+                        .HasColumnType("text");
+
+                    b.Property<DateTime>("CreatedAt")
+                        .HasColumnType("timestamp with time zone");
+
+                    b.Property<bool>("IsReadOnly")
+                        .HasColumnType("boolean");
+
+                    b.HasKey("AccountId", "ViewerUserId");
+
+                    b.ToTable("VisibilityRules");
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
+                {
+                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
+                        .WithMany()
+                        .HasForeignKey("RoleId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
+                {
+                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
+                        .WithMany()
+                        .HasForeignKey("UserId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
+                {
+                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
+                        .WithMany()
+                        .HasForeignKey("UserId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
+                {
+                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole", null)
+                        .WithMany()
+                        .HasForeignKey("RoleId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+
+                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
+                        .WithMany()
+                        .HasForeignKey("UserId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+                });
+
+            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
+                {
+                    b.HasOne("Treasury.App.Domain.ApplicationUser", null)
+                        .WithMany()
+                        .HasForeignKey("UserId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.TransactionTag", b =>
+                {
+                    b.HasOne("Treasury.App.Domain.Tag", "Tag")
+                        .WithMany("TransactionTags")
+                        .HasForeignKey("TagId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+
+                    b.HasOne("Treasury.App.Domain.Transaction", "Transaction")
+                        .WithMany("TransactionTags")
+                        .HasForeignKey("TransactionId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+
+                    b.Navigation("Tag");
+
+                    b.Navigation("Transaction");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.VisibilityRule", b =>
+                {
+                    b.HasOne("Treasury.App.Domain.Account", "Account")
+                        .WithMany("VisibilityRules")
+                        .HasForeignKey("AccountId")
+                        .OnDelete(DeleteBehavior.Cascade)
+                        .IsRequired();
+
+                    b.Navigation("Account");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Account", b =>
+                {
+                    b.Navigation("VisibilityRules");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Tag", b =>
+                {
+                    b.Navigation("TransactionTags");
+                });
+
+            modelBuilder.Entity("Treasury.App.Domain.Transaction", b =>
+                {
+                    b.Navigation("TransactionTags");
+                });
+#pragma warning restore 612, 618
+        }
+    }
+}
diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903134028_AddTransactionBalanceAfterTransaction.cs b/src/Treasury.App/Infrastructure/Migrations/20260903134028_AddTransactionBalanceAfterTransaction.cs
new file mode 100644
index 0000000..227c7c1
--- /dev/null
+++ b/src/Treasury.App/Infrastructure/Migrations/20260903134028_AddTransactionBalanceAfterTransaction.cs
@@ -0,0 +1,29 @@
+﻿using Microsoft.EntityFrameworkCore.Migrations;
+
+#nullable disable
+
+namespace Treasury.App.Infrastructure.Migrations
+{
+    /// <inheritdoc />
+    public partial class AddTransactionBalanceAfterTransaction : Migration
+    {
+        /// <inheritdoc />
+        protected override void Up(MigrationBuilder migrationBuilder)
+        {
+            migrationBuilder.AddColumn<decimal>(
+                name: "BalanceAfterTransaction",
+                table: "Transactions",
+                type: "numeric",
+                nullable: false,
+                defaultValue: 0m);
+        }
+
+        /// <inheritdoc />
+        protected override void Down(MigrationBuilder migrationBuilder)
+        {
+            migrationBuilder.DropColumn(
+                name: "BalanceAfterTransaction",
+                table: "Transactions");
+        }
+    }
+}
diff --git a/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs b/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
index 30ceabe..9340be2 100644
--- a/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
+++ b/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
@@ -1,11 +1,11 @@
-// <auto-generated />
+﻿// <auto-generated />
 using System;
 using Microsoft.EntityFrameworkCore;
 using Microsoft.EntityFrameworkCore.Infrastructure;
 using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
 using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
 using Treasury.App.Infrastructure.Data;
 
 #nullable disable
 
 namespace Treasury.App.Infrastructure.Migrations
@@ -523,20 +523,23 @@ namespace Treasury.App.Infrastructure.Migrations
                     b.Property<Guid>("Id")
                         .ValueGeneratedOnAdd()
                         .HasColumnType("uuid");
 
                     b.Property<Guid>("AccountId")
                         .HasColumnType("uuid");
 
                     b.Property<decimal>("Amount")
                         .HasColumnType("numeric");
 
+                    b.Property<decimal>("BalanceAfterTransaction")
+                        .HasColumnType("numeric");
+
                     b.Property<string>("Category")
                         .IsRequired()
                         .HasColumnType("text");
 
                     b.Property<DateTime>("CreatedAt")
                         .HasColumnType("timestamp with time zone");
 
                     b.Property<string>("Currency")
                         .IsRequired()
                         .HasColumnType("text");
@@ -571,21 +574,21 @@ namespace Treasury.App.Infrastructure.Migrations
                     b.Property<Guid>("TagId")
                         .HasColumnType("uuid");
 
                     b.HasKey("TransactionId", "TagId");
 
                     b.HasIndex("TagId");
 
                     b.ToTable("TransactionTags");
                 });
 
-                        modelBuilder.Entity("Treasury.App.Domain.Transfer", b =>
+            modelBuilder.Entity("Treasury.App.Domain.Transfer", b =>
                 {
                     b.Property<Guid>("Id")
                         .ValueGeneratedOnAdd()
                         .HasColumnType("uuid");
 
                     b.Property<decimal>("Amount")
                         .HasColumnType("numeric");
 
                     b.Property<DateTime>("CreatedAt")
                         .HasColumnType("timestamp with time zone");
diff --git a/src/Treasury.App/Pages/Transactions.razor b/src/Treasury.App/Pages/Transactions.razor
index 626e971..973da1c 100644
--- a/src/Treasury.App/Pages/Transactions.razor
+++ b/src/Treasury.App/Pages/Transactions.razor
@@ -63,36 +63,111 @@
                                      Label="@tag.Name" />
                     }
                 </MudStack>
             }
 
             <MudStack Row="true" Class="mt-4" Justify="Justify.FlexEnd">
                 <MudButton Variant="Variant.Filled" Color="Color.Tertiary" OnClick="CreateTransactionAsync">Add transaction</MudButton>
             </MudStack>
         </MudPaper>
 
+        @if (_editingTransaction is not null)
+        {
+            <MudPaper Class="pa-4 rounded-xl" Elevation="2">
+                <MudStack Spacing="3">
+                    <MudStack Spacing="1">
+                        <MudText Typo="Typo.subtitle1">Edit transaction</MudText>
+                        <MudText Typo="Typo.body2">
+                            @(_editingTransaction.IsLatest
+                                ? "This is the latest transaction for the account, so amount, date, and type can be edited."
+                                : "This is not the latest transaction, so only description, category, and tags can be edited.")
+                        </MudText>
+                    </MudStack>
+
+                    <MudGrid>
+                        <MudItem xs="12" md="4">
+                            <MudTextField Label="Description" @bind-Value="_editingTransaction.Description" Required="true" />
+                        </MudItem>
+                        <MudItem xs="12" md="3">
+                            <MudTextField Label="Category" @bind-Value="_editingTransaction.Category" />
+                        </MudItem>
+                        <MudItem xs="12" md="2">
+                            <MudNumericField Label="Amount"
+                                             @bind-Value="_editingTransaction.Amount"
+                                             Required="true"
+                                             Min="0.01m"
+                                             Disabled="@(!_editingTransaction.IsLatest)" />
+                        </MudItem>
+                        <MudItem xs="12" md="2">
+                            <MudSelect T="string"
+                                       Label="Type"
+                                       @bind-Value="_editingTransaction.Type"
+                                       Disabled="@(!_editingTransaction.IsLatest)">
+                                <MudSelectItem Value="@("income")">Income</MudSelectItem>
+                                <MudSelectItem Value="@("expense")">Expense</MudSelectItem>
+                                <MudSelectItem Value="@("transfer")">Transfer</MudSelectItem>
+                                <MudSelectItem Value="@("transfer-out")">Transfer out</MudSelectItem>
+                                <MudSelectItem Value="@("transfer-in")">Transfer in</MudSelectItem>
+                            </MudSelect>
+                        </MudItem>
+                        <MudItem xs="12" md="1">
+                            <MudDatePicker Label="Date"
+                                           @bind-Date="_editingTransaction.TransactionDate"
+                                           Disabled="@(!_editingTransaction.IsLatest)" />
+                        </MudItem>
+                    </MudGrid>
+
+                    <MudDivider />
+
+                    <MudText Typo="Typo.subtitle2">Tags</MudText>
+                    @if (_tags.Count == 0)
+                    {
+                        <MudText Typo="Typo.body2" Class="mt-2">No tags yet. Add one from the Tags page.</MudText>
+                    }
+                    else
+                    {
+                        <MudStack Row="true" Class="mt-2" Wrap="Wrap.Wrap">
+                            @foreach (var tag in _tags)
+                            {
+                                <MudCheckBox T="bool"
+                                             Value="@_editingSelectedTagIds.Contains(tag.Id)"
+                                             ValueChanged="@(value => ToggleEditingTag(tag.Id, value))"
+                                             Label="@tag.Name" />
+                            }
+                        </MudStack>
+                    }
+
+                    <MudStack Row="true" Justify="Justify.FlexEnd" Spacing="2">
+                        <MudButton Variant="Variant.Outlined" OnClick="CancelEditAsync">Cancel</MudButton>
+                        <MudButton Variant="Variant.Filled" Color="Color.Tertiary" OnClick="SaveTransactionEditAsync">Save changes</MudButton>
+                    </MudStack>
+                </MudStack>
+            </MudPaper>
+        }
+
         @if (_transactions.Count == 0)
         {
             <MudAlert Severity="Severity.Info">No transactions yet. Add your first household transaction to start tracking cash flow.</MudAlert>
         }
         else
         {
             <MudPaper Class="pa-4 rounded-xl" Elevation="2">
                 <MudTable T="Treasury.App.Domain.Transaction" Items="_transactions" Hover="true" Dense="true">
                     <HeaderContent>
                         <MudTh>Description</MudTh>
                         <MudTh>Category</MudTh>
                         <MudTh>Tags</MudTh>
                         <MudTh>Account</MudTh>
                         <MudTh>Type</MudTh>
                         <MudTh>Amount</MudTh>
                         <MudTh>Date</MudTh>
+                        <MudTh>Actions</MudTh>
                     </HeaderContent>
                     <RowTemplate>
                         <MudTd DataLabel="Description">@context.Description</MudTd>
                         <MudTd DataLabel="Category">@context.Category</MudTd>
                         <MudTd DataLabel="Tags">
                             @if (_transactionTags.TryGetValue(context.Id, out var tags) && tags.Count > 0)
                             {
                                 foreach (var tag in tags)
                                 {
                                     <MudChip T="string" Size="Size.Small" Style="@($"background:{tag.Color}; color:white;")">@tag.Name</MudChip>
@@ -100,58 +175,71 @@
                             }
                             else
                             {
                                 <MudText Typo="Typo.caption">No tags</MudText>
                             }
                         </MudTd>
                         <MudTd DataLabel="Account">@GetAccountName(context.AccountId)</MudTd>
                         <MudTd DataLabel="Type">@context.Type</MudTd>
                         <MudTd DataLabel="Amount">@context.Amount.ToString("N2") @context.Currency</MudTd>
                         <MudTd DataLabel="Date">@context.TransactionDate.ToLocalTime().ToString("yyyy-MM-dd")</MudTd>
+                        <MudTd DataLabel="Actions">
+                            <MudButton Size="Size.Small"
+                                       Variant="Variant.Outlined"
+                                       OnClick="@(() => BeginEdit(context))">
+                                Edit
+                            </MudButton>
+                        </MudTd>
                     </RowTemplate>
                 </MudTable>
             </MudPaper>
         }
     </MudStack>
 </MudContainer>
 
 @code {
     private readonly List<Treasury.App.Domain.Transaction> _transactions = new();
     // active accounts used for the picker
     private readonly List<Treasury.App.Domain.Account> _accounts = new();
     // full visible accounts (includes inactive) used for mapping historical transactions to names
     private readonly List<Treasury.App.Domain.Account> _visibleAccounts = new();
     private readonly List<Treasury.App.Domain.Tag> _tags = new();
     private readonly Dictionary<Guid, List<Treasury.App.Domain.Tag>> _transactionTags = new();
     private readonly HashSet<Guid> _selectedTagIds = new();
+    private readonly HashSet<Guid> _editingSelectedTagIds = new();
 
     private readonly NewTransactionForm _newTransaction = new();
+    private EditingTransactionForm? _editingTransaction;
 
     protected override async Task OnInitializedAsync()
     {
         await LoadAsync();
     }
 
     private async Task LoadAsync()
     {
         _accounts.Clear();
         _accounts.AddRange(await DbContext.Accounts.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync());
 
         // Load full visible account list (includes inactive) so historical transactions still show account names
         _visibleAccounts.Clear();
         _visibleAccounts.AddRange(await DbContext.Accounts.OrderBy(x => x.Name).ToListAsync());
 
         _tags.Clear();
         _tags.AddRange(await DbContext.Tags.OrderBy(x => x.Name).ToListAsync());
 
         _transactions.Clear();
-        _transactions.AddRange(await DbContext.Transactions.OrderByDescending(x => x.TransactionDate).ToListAsync());
+        _transactions.AddRange(await DbContext.Transactions
+            .OrderByDescending(x => x.TransactionDate)
+            .ThenByDescending(x => x.CreatedAt)
+            .ThenByDescending(x => x.Id)
+            .ToListAsync());
 
         _transactionTags.Clear();
         var tagLinks = await DbContext.TransactionTags
             .Include(x => x.Tag)
             .Where(x => _transactions.Select(t => t.Id).Contains(x.TransactionId))
             .ToListAsync();
 
         foreach (var group in tagLinks.GroupBy(x => x.TransactionId))
         {
             _transactionTags[group.Key] = group.Select(x => x.Tag).ToList();
@@ -201,87 +289,307 @@
             return;
         }
 
         // Prevent creating transactions against inactive accounts via the UI save path
         if (!account.IsActive)
         {
             Snackbar.Add("Selected account is inactive. Reactivate it before adding transactions.", Severity.Warning);
             return;
         }
 
-        var normalizedType = (_newTransaction.Type ?? "expense").Trim().ToLowerInvariant();
-        var delta = _newTransaction.Amount;
-        if (normalizedType == "expense")
-        {
-            delta = -Math.Abs(_newTransaction.Amount);
-        }
-        else if (normalizedType == "income")
-        {
-            delta = Math.Abs(_newTransaction.Amount);
-        }
-        else if (normalizedType == "transfer")
-        {
-            delta = _newTransaction.Amount;
-        }
+        var normalizedType = NormalizeType(_newTransaction.Type);
+        var delta = GetDelta(_newTransaction.Amount, normalizedType);
+        var utcNow = DateTime.UtcNow;
 
         var transaction = new Treasury.App.Domain.Transaction
         {
             HouseholdId = account.HouseholdId,
             AccountId = account.Id,
             Description = _newTransaction.Description.Trim(),
             Category = string.IsNullOrWhiteSpace(_newTransaction.Category) ? "General" : _newTransaction.Category.Trim(),
             Amount = _newTransaction.Amount,
             Currency = account.Currency,
             Type = normalizedType,
-            TransactionDate = _newTransaction.TransactionDate ?? DateTime.UtcNow,
-            CreatedAt = DateTime.UtcNow,
-            UpdatedAt = DateTime.UtcNow
+            TransactionDate = _newTransaction.TransactionDate ?? utcNow,
+            CreatedAt = utcNow,
+            UpdatedAt = utcNow
         };
 
         foreach (var tagId in _selectedTagIds)
         {
             var tag = _tags.FirstOrDefault(x => x.Id == tagId);
             if (tag is null)
             {
                 continue;
             }
 
             transaction.TransactionTags.Add(new Treasury.App.Domain.TransactionTag
             {
                 Transaction = transaction,
                 TagId = tag.Id,
                 Tag = tag
             });
         }
 
         DbContext.Transactions.Add(transaction);
         account.CurrentBalance += delta;
-        account.UpdatedAt = DateTime.UtcNow;
+        transaction.BalanceAfterTransaction = account.CurrentBalance;
+        account.UpdatedAt = utcNow;
         await DbContext.SaveChangesAsync();
 
         _newTransaction.Description = string.Empty;
         _newTransaction.Category = "General";
         _newTransaction.Amount = 0m;
         _newTransaction.Type = "expense";
         _newTransaction.TransactionDate = DateTime.UtcNow;
         _selectedTagIds.Clear();
 
         Snackbar.Add("Transaction saved.", Severity.Success);
         await LoadAsync();
     }
 
+    private void BeginEdit(Treasury.App.Domain.Transaction transaction)
+    {
+        _editingTransaction = new EditingTransactionForm
+        {
+            Id = transaction.Id,
+            AccountId = transaction.AccountId,
+            Description = transaction.Description,
+            Category = transaction.Category,
+            Amount = transaction.Amount,
+            Type = transaction.Type,
+            TransactionDate = transaction.TransactionDate,
+            IsLatest = IsLatestTransaction(transaction)
+        };
+
+        _editingSelectedTagIds.Clear();
+        if (_transactionTags.TryGetValue(transaction.Id, out var tags))
+        {
+            foreach (var tag in tags)
+            {
+                _editingSelectedTagIds.Add(tag.Id);
+            }
+        }
+    }
+
+    private Task CancelEditAsync()
+    {
+        _editingTransaction = null;
+        _editingSelectedTagIds.Clear();
+        return Task.CompletedTask;
+    }
+
+    private void ToggleEditingTag(Guid tagId, bool isSelected)
+    {
+        if (isSelected)
+        {
+            _editingSelectedTagIds.Add(tagId);
+            return;
+        }
+
+        _editingSelectedTagIds.Remove(tagId);
+    }
+
+    private async Task SaveTransactionEditAsync()
+    {
+        if (_editingTransaction is null)
+        {
+            return;
+        }
+
+        if (string.IsNullOrWhiteSpace(_editingTransaction.Description))
+        {
+            Snackbar.Add("Description is required.", Severity.Warning);
+            return;
+        }
+
+        if (_editingTransaction.IsLatest && _editingTransaction.Amount <= 0m)
+        {
+            Snackbar.Add("Amount must be greater than zero.", Severity.Warning);
+            return;
+        }
+
+        var transaction = await DbContext.Transactions
+            .Include(x => x.TransactionTags)
+            .SingleOrDefaultAsync(x => x.Id == _editingTransaction.Id);
+        if (transaction is null)
+        {
+            Snackbar.Add("Transaction could not be found.", Severity.Error);
+            return;
+        }
+
+        var account = await DbContext.Accounts.SingleOrDefaultAsync(x => x.Id == transaction.AccountId);
+        if (account is null)
+        {
+            Snackbar.Add("Account could not be found.", Severity.Error);
+            return;
+        }
+
+        var accountTransactions = await DbContext.Transactions
+            .Where(x => x.AccountId == account.Id)
+            .ToListAsync();
+
+        var originalSnapshots = accountTransactions
+            .Select(x => new TransactionBalanceSnapshot(x.Id, x.Amount, x.Type, x.TransactionDate, x.CreatedAt))
+            .ToList();
+
+        var latestTransactionId = originalSnapshots
+            .OrderByDescending(x => x.TransactionDate)
+            .ThenByDescending(x => x.CreatedAt)
+            .ThenByDescending(x => x.Id)
+            .Select(x => x.Id)
+            .FirstOrDefault();
+
+        var isLatest = latestTransactionId == transaction.Id;
+        var normalizedType = NormalizeType(string.IsNullOrWhiteSpace(_editingTransaction.Type) ? transaction.Type : _editingTransaction.Type);
+        var normalizedDate = _editingTransaction.TransactionDate ?? transaction.TransactionDate;
+
+        if (!isLatest)
+        {
+            if (_editingTransaction.Amount != transaction.Amount)
+            {
+                Snackbar.Add("Amount can only be changed on the latest transaction.", Severity.Warning);
+                return;
+            }
+
+            if (normalizedDate != transaction.TransactionDate)
+            {
+                Snackbar.Add("Transaction date can only be changed on the latest transaction.", Severity.Warning);
+                return;
+            }
+
+            if (!string.Equals(normalizedType, transaction.Type, StringComparison.OrdinalIgnoreCase))
+            {
+                Snackbar.Add("Type can only be changed on the latest transaction.", Severity.Warning);
+                return;
+            }
+        }
+
+        var validTagIds = await DbContext.Tags
+            .Where(x => x.HouseholdId == account.HouseholdId && _editingSelectedTagIds.Contains(x.Id))
+            .Select(x => x.Id)
+            .ToListAsync();
+
+        var utcNow = DateTime.UtcNow;
+        transaction.Description = _editingTransaction.Description.Trim();
+        transaction.Category = string.IsNullOrWhiteSpace(_editingTransaction.Category) ? "General" : _editingTransaction.Category.Trim();
+        transaction.UpdatedAt = utcNow;
+
+        dbContextUpdateTagLinks(transaction, validTagIds);
+
+        if (isLatest)
+        {
+            transaction.Amount = _editingTransaction.Amount;
+            transaction.Type = normalizedType;
+            transaction.TransactionDate = normalizedDate;
+
+            var updatedSnapshots = originalSnapshots
+                .Select(x => x.Id == transaction.Id
+                    ? x with
+                    {
+                        Amount = transaction.Amount,
+                        Type = transaction.Type,
+                        TransactionDate = transaction.TransactionDate
+                    }
+                    : x)
+                .ToList();
+
+            var initialBalance = account.CurrentBalance - originalSnapshots.Sum(x => GetDelta(x.Amount, x.Type));
+            var runningBalance = initialBalance;
+            var trackedById = accountTransactions.ToDictionary(x => x.Id);
+
+            foreach (var snapshot in updatedSnapshots
+                .OrderBy(x => x.TransactionDate)
+                .ThenBy(x => x.CreatedAt)
+                .ThenBy(x => x.Id))
+            {
+                runningBalance += GetDelta(snapshot.Amount, snapshot.Type);
+                var trackedTransaction = trackedById[snapshot.Id];
+                trackedTransaction.BalanceAfterTransaction = runningBalance;
+                trackedTransaction.UpdatedAt = utcNow;
+            }
+
+            account.CurrentBalance = runningBalance;
+            account.UpdatedAt = utcNow;
+        }
+
+        await DbContext.SaveChangesAsync();
+
+        Snackbar.Add("Transaction updated.", Severity.Success);
+        await CancelEditAsync();
+        await LoadAsync();
+    }
+
+    private void dbContextUpdateTagLinks(Treasury.App.Domain.Transaction transaction, List<Guid> validTagIds)
+    {
+        DbContext.TransactionTags.RemoveRange(transaction.TransactionTags);
+        transaction.TransactionTags.Clear();
+
+        foreach (var tagId in validTagIds)
+        {
+            transaction.TransactionTags.Add(new Treasury.App.Domain.TransactionTag
+            {
+                Transaction = transaction,
+                TagId = tagId
+            });
+        }
+    }
+
     private string GetAccountName(Guid accountId)
     {
         // Use the full visible accounts collection so historical transactions from inactive accounts still show a name
         return _visibleAccounts.FirstOrDefault(x => x.Id == accountId)?.Name ?? "Unknown account";
     }
 
+    private bool IsLatestTransaction(Treasury.App.Domain.Transaction transaction)
+    {
+        var latestTransaction = _transactions
+            .Where(x => x.AccountId == transaction.AccountId)
+            .OrderByDescending(x => x.TransactionDate)
+            .ThenByDescending(x => x.CreatedAt)
+            .ThenByDescending(x => x.Id)
+            .FirstOrDefault();
+
+        return latestTransaction?.Id == transaction.Id;
+    }
+
+    private static string NormalizeType(string type) =>
+        string.IsNullOrWhiteSpace(type) ? "expense" : type.Trim().ToLowerInvariant();
+
+    private static decimal GetDelta(decimal amount, string type)
+    {
+        var normalizedType = NormalizeType(type);
+        return normalizedType switch
+        {
+            "expense" => -Math.Abs(amount),
+            "income" => Math.Abs(amount),
+            "transfer" => Math.Abs(amount),
+            "transfer-in" => Math.Abs(amount),
+            "transfer-out" => -Math.Abs(amount),
+            _ => amount
+        };
+    }
+
     private sealed class NewTransactionForm
     {
         public string AccountId { get; set; } = string.Empty;
         public string Description { get; set; } = string.Empty;
         public string Category { get; set; } = "General";
         public decimal Amount { get; set; }
         public string Type { get; set; } = "expense";
         public DateTime? TransactionDate { get; set; } = DateTime.UtcNow;
     }
+
+    private sealed class EditingTransactionForm
+    {
+        public Guid Id { get; set; }
+        public Guid AccountId { get; set; }
+        public string Description { get; set; } = string.Empty;
+        public string Category { get; set; } = "General";
+        public decimal Amount { get; set; }
+        public string Type { get; set; } = "expense";
+        public DateTime? TransactionDate { get; set; } = DateTime.UtcNow;
+        public bool IsLatest { get; set; }
+    }
+
+    private sealed record TransactionBalanceSnapshot(Guid Id, decimal Amount, string Type, DateTime TransactionDate, DateTime CreatedAt);
 }
diff --git a/tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs b/tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs
new file mode 100644
index 0000000..69b2760
--- /dev/null
+++ b/tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs
@@ -0,0 +1,184 @@
+using System.Net;
+using System.Net.Http.Json;
+using System.Text.Json;
+using FluentAssertions;
+using Microsoft.AspNetCore.Mvc.Testing;
+using Microsoft.EntityFrameworkCore;
+using Microsoft.Extensions.DependencyInjection;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.IntegrationTests;
+
+public class TransactionEditingRulesTests
+{
+    [Fact]
+    public async Task Create_Transaction_Stores_BalanceAfterTransaction()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, "Balance tracking");
+
+        var response = await client.PostAsJsonAsync("/api/transactions", new
+        {
+            AccountId = accountId,
+            Description = "Salary deposit",
+            Category = "Income",
+            Amount = 1200m,
+            Currency = "PLN",
+            Type = "income",
+            TransactionDate = DateTime.UtcNow
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
+        var transactionId = payload.RootElement.GetProperty("id").GetGuid();
+
+        await using var scope = app.Services.CreateAsyncScope();
+        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
+
+        var transaction = await db.Transactions.SingleAsync(x => x.Id == transactionId);
+        var account = await db.Accounts.SingleAsync(x => x.Id == accountId);
+
+        transaction.BalanceAfterTransaction.Should().Be(account.CurrentBalance);
+    }
+
+    [Fact]
+    public async Task Edit_NonLatest_Rejects_Amount_Date_Type_Changes()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, "Historical edits");
+
+        var firstTransactionId = await CreateTransactionAsync(client, accountId, "First", 10m, "expense", DateTime.UtcNow.AddDays(-2));
+        _ = await CreateTransactionAsync(client, accountId, "Second", 20m, "expense", DateTime.UtcNow.AddDays(-1));
+
+        var response = await client.PutAsJsonAsync($"/api/transactions/{firstTransactionId}", new
+        {
+            Id = firstTransactionId,
+            Description = "First updated",
+            Category = "Updated",
+            Amount = 15m,
+            Type = "income",
+            TransactionDate = DateTime.UtcNow.AddDays(-3),
+            TagIds = Array.Empty<Guid>()
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
+        var body = await response.Content.ReadAsStringAsync();
+        body.Should().Contain("latest transaction");
+        body.Should().Contain("Amount can only be changed on the latest transaction.");
+        body.Should().Contain("Transaction date can only be changed on the latest transaction.");
+        body.Should().Contain("Type can only be changed on the latest transaction.");
+    }
+
+    [Fact]
+    public async Task Edit_Latest_Allows_Amount_And_Recomputes_Balance()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, "Latest edit");
+
+        _ = await CreateTransactionAsync(client, accountId, "Earlier", 10m, "expense", DateTime.UtcNow.AddDays(-2));
+        var latestTransactionId = await CreateTransactionAsync(client, accountId, "Latest", 5m, "expense", DateTime.UtcNow.AddDays(-1));
+
+        var response = await client.PutAsJsonAsync($"/api/transactions/{latestTransactionId}", new
+        {
+            Id = latestTransactionId,
+            Description = "Latest adjusted",
+            Category = "Updated",
+            Amount = 8m,
+            Type = "income",
+            TransactionDate = DateTime.UtcNow.AddDays(-3),
+            TagIds = Array.Empty<Guid>()
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        await using var scope = app.Services.CreateAsyncScope();
+        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
+
+        var account = await db.Accounts.SingleAsync(x => x.Id == accountId);
+        var editedTransaction = await db.Transactions.SingleAsync(x => x.Id == latestTransactionId);
+        var earlierTransaction = await db.Transactions.SingleAsync(x => x.Description == "Earlier" && x.AccountId == accountId);
+
+        account.CurrentBalance.Should().Be(-2m);
+        editedTransaction.Description.Should().Be("Latest adjusted");
+        editedTransaction.BalanceAfterTransaction.Should().Be(8m);
+        earlierTransaction.BalanceAfterTransaction.Should().Be(-2m);
+    }
+
+    private static HttpClient CreateAuthenticatedClient(TreasuryHostFactory app) =>
+        app.CreateClient(new WebApplicationFactoryClientOptions
+        {
+            AllowAutoRedirect = false,
+            HandleCookies = true
+        });
+
+    private static async Task RegisterAndSignInAsync(HttpClient client)
+    {
+        var email = $"owner-{Guid.NewGuid():N}@example.com";
+        const string password = "Password123!";
+
+        var registerResponse = await client.PostAsJsonAsync("/auth/register-submit", new
+        {
+            Email = email,
+            Password = password,
+            ConfirmPassword = password
+        });
+        registerResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
+
+        var loginResponse = await client.PostAsJsonAsync("/auth/login-submit", new
+        {
+            Email = email,
+            Password = password
+        });
+        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
+    }
+
+    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name)
+    {
+        var response = await client.PostAsJsonAsync("/api/accounts", new
+        {
+            Name = name,
+            Currency = "PLN",
+            AccountType = "cash-wallet"
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
+        return json.RootElement.GetProperty("id").GetGuid();
+    }
+
+    private static async Task<Guid> CreateTransactionAsync(
+        HttpClient client,
+        Guid accountId,
+        string description,
+        decimal amount,
+        string type,
+        DateTime transactionDate)
+    {
+        var response = await client.PostAsJsonAsync("/api/transactions", new
+        {
+            AccountId = accountId,
+            Description = description,
+            Category = "General",
+            Amount = amount,
+            Currency = "PLN",
+            Type = type,
+            TransactionDate = transactionDate
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
+        return json.RootElement.GetProperty("id").GetGuid();
+    }
+}
