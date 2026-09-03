# Review Package

Base: 0d3a73de0ac9491cfcbc9c31a723d58a939d918d
Head: d97d260905eb29c7b6fd6246d1eb427521508700

## Commits

d97d260 feat: add linked transfers workflow and transfers page

## Diff Stat

 .superpowers/sdd/phase2-task-2-report.md           |  22 +
 .../Components/Layout/MainLayout.razor             |   2 +
 src/Treasury.App/Domain/Transfer.cs                |   2 +
 .../Endpoints/Transfers/CreateTransferEndpoint.cs  |  28 +-
 ...3153000_AddTransferTransactionLinks.Designer.cs | 744 +++++++++++++++++++++
 .../20260903153000_AddTransferTransactionLinks.cs  |  40 ++
 .../Migrations/TreasuryDbContextModelSnapshot.cs   |  10 +-
 src/Treasury.App/Pages/Transfers.razor             |  92 +++
 src/Treasury.App/Pages/Transfers.razor.cs          | 267 ++++++++
 .../TransfersWorkflowTests.cs                      | 169 +++++
 10 files changed, 1370 insertions(+), 6 deletions(-)

## Full Diff (-U10)

diff --git a/.superpowers/sdd/phase2-task-2-report.md b/.superpowers/sdd/phase2-task-2-report.md
new file mode 100644
index 0000000..5d0e3f8
--- /dev/null
+++ b/.superpowers/sdd/phase2-task-2-report.md
@@ -0,0 +1,22 @@
+# Phase 2 Task 2 Report
+
+## What changed
+- Added `OutflowTransactionId` and `InflowTransactionId` to the transfer domain model.
+- Updated `POST /api/transfers` to persist the transfer and both linked transactions atomically, then write back the transaction IDs.
+- Added a dedicated `/transfers` page with a creation form and recent transfer history only, matching the approved option C scope.
+- Added transfers navigation entries in the top bar and drawer.
+- Added an EF Core migration for the new transfer link columns and updated the model snapshot.
+- Added `TransfersWorkflowTests` integration coverage for successful linked transfers, inactive-account rejection, and transfers page rendering.
+
+## Validation
+- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~TransfersWorkflowTests"`
+  - Result: passed, 3/3 tests.
+- `dotnet test tests\Treasury.IntegrationTests\Treasury.IntegrationTests.csproj -c Debug --filter "FullyQualifiedName~AccountsLifecycleTests"`
+  - Result: passed, 7/7 tests.
+
+## Notes
+- The page uses direct server-side database access for the form/history workflow, while the API endpoint remains the canonical write path for transfer creation behavior and validation.
+- No balance summary widgets were added, per the scope clarification.
+
+## Commit
+- 4a56c75
diff --git a/src/Treasury.App/Components/Layout/MainLayout.razor b/src/Treasury.App/Components/Layout/MainLayout.razor
index aff3575..a7b43c1 100644
--- a/src/Treasury.App/Components/Layout/MainLayout.razor
+++ b/src/Treasury.App/Components/Layout/MainLayout.razor
@@ -9,20 +9,21 @@
     <MudAppBar Elevation="1" Dense="true">
         <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit" OnClick="ToggleDrawer" />
         <MudText Typo="Typo.h6">Treasury</MudText>
         <MudSpacer />
 
         <AuthorizeView>
             <Authorized>
                 <MudButton Variant="Variant.Text" Href="/">Dashboard</MudButton>
                 <MudButton Variant="Variant.Text" Href="/accounts">Accounts</MudButton>
                 <MudButton Variant="Variant.Text" Href="/transactions">Transactions</MudButton>
+                <MudButton Variant="Variant.Text" Href="/transfers">Transfers</MudButton>
                 <MudButton Variant="Variant.Text" Href="/budgets">Budgets</MudButton>
                 <MudButton Variant="Variant.Text" Href="/bills">Bills</MudButton>
                 <MudButton Variant="Variant.Text" Href="/tags">Tags</MudButton>
                 <MudButton Variant="Variant.Text" Href="/valuations">Valuations</MudButton>
                 <MudButton Variant="Variant.Text" Href="/shared">Shared</MudButton>
                 <MudButton Variant="Variant.Text" Href="/rates">Rates</MudButton>
                 <form method="post" action="/auth/logout-submit" style="display:inline; margin-left: 0.5rem;">
                     <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Text" Color="Color.Inherit">Logout</MudButton>
                 </form>
             </Authorized>
@@ -33,20 +34,21 @@
         </AuthorizeView>
     </MudAppBar>
 
     <MudDrawer @bind-Open="_drawerOpen" Variant="DrawerVariant.Temporary" ClipMode="DrawerClipMode.Always">
         <MudNavMenu>
             <AuthorizeView>
                 <Authorized>
                     <MudNavLink Href="/">Dashboard</MudNavLink>
                     <MudNavLink Href="/accounts">Accounts</MudNavLink>
                     <MudNavLink Href="/transactions">Transactions</MudNavLink>
+                    <MudNavLink Href="/transfers">Transfers</MudNavLink>
                     <MudNavLink Href="/budgets">Budgets</MudNavLink>
                     <MudNavLink Href="/bills">Bills</MudNavLink>
                     <MudNavLink Href="/tags">Tags</MudNavLink>
                     <MudNavLink Href="/valuations">Valuations</MudNavLink>
                     <MudNavLink Href="/shared">Shared</MudNavLink>
                     <MudNavLink Href="/rates">Rates</MudNavLink>
                     <form method="post" action="/auth/logout-submit">
                         <MudButton ButtonType="ButtonType.Submit" Variant="Variant.Text" FullWidth="true">Logout</MudButton>
                     </form>
                 </Authorized>
diff --git a/src/Treasury.App/Domain/Transfer.cs b/src/Treasury.App/Domain/Transfer.cs
index f49a6cd..7859741 100644
--- a/src/Treasury.App/Domain/Transfer.cs
+++ b/src/Treasury.App/Domain/Transfer.cs
@@ -1,14 +1,16 @@
 namespace Treasury.App.Domain;
 
 public sealed class Transfer
 {
     public Guid Id { get; set; } = Guid.NewGuid();
     public Guid HouseholdId { get; set; }
     public Guid FromAccountId { get; set; }
     public Guid ToAccountId { get; set; }
+    public Guid OutflowTransactionId { get; set; }
+    public Guid InflowTransactionId { get; set; }
     public decimal Amount { get; set; }
     public string Currency { get; set; } = "PLN";
     public string Description { get; set; } = string.Empty;
     public DateTime TransferDate { get; set; } = DateTime.UtcNow;
     public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 }
diff --git a/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs b/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs
index dca5eff..65ec060 100644
--- a/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs
+++ b/src/Treasury.App/Endpoints/Transfers/CreateTransferEndpoint.cs
@@ -119,26 +119,46 @@ public sealed class CreateTransferEndpoint(TreasuryDbContext db, UserManager<App
             TransactionDate = transferDate,
             CreatedAt = DateTime.UtcNow,
             UpdatedAt = DateTime.UtcNow
         };
 
         fromAccount.CurrentBalance -= Math.Abs(request.Amount);
         toAccount.CurrentBalance += Math.Abs(request.Amount);
         fromAccount.UpdatedAt = DateTime.UtcNow;
         toAccount.UpdatedAt = DateTime.UtcNow;
 
-        db.Transfers.Add(transfer);
-        db.Transactions.Add(outflow);
-        db.Transactions.Add(inflow);
-        await db.SaveChangesAsync(ct);
+        async Task PersistAsync()
+        {
+            db.Transfers.Add(transfer);
+            db.Transactions.Add(outflow);
+            db.Transactions.Add(inflow);
+            await db.SaveChangesAsync(ct);
+
+            transfer.OutflowTransactionId = outflow.Id;
+            transfer.InflowTransactionId = inflow.Id;
+            await db.SaveChangesAsync(ct);
+        }
+
+        if (db.Database.IsRelational())
+        {
+            await using var transaction = await db.Database.BeginTransactionAsync(ct);
+            await PersistAsync();
+            await transaction.CommitAsync(ct);
+        }
+        else
+        {
+            await PersistAsync();
+        }
 
         await SendAsync(new
         {
             transfer.Id,
             transfer.FromAccountId,
             transfer.ToAccountId,
+            transfer.OutflowTransactionId,
+            transfer.InflowTransactionId,
             transfer.Amount,
             transfer.Currency,
             transfer.TransferDate
         }, StatusCodes.Status201Created, ct);
     }
 }
diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903153000_AddTransferTransactionLinks.Designer.cs b/src/Treasury.App/Infrastructure/Migrations/20260903153000_AddTransferTransactionLinks.Designer.cs
new file mode 100644
index 0000000..aff0184
--- /dev/null
+++ b/src/Treasury.App/Infrastructure/Migrations/20260903153000_AddTransferTransactionLinks.Designer.cs
@@ -0,0 +1,744 @@
+// <auto-generated />
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
+    [Migration("20260903153000_AddTransferTransactionLinks")]
+    partial class AddTransferTransactionLinks
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
+                        modelBuilder.Entity("Treasury.App.Domain.Transfer", b =>
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
+                    b.Property<Guid>("InflowTransactionId")
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid>("OutflowTransactionId")
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
diff --git a/src/Treasury.App/Infrastructure/Migrations/20260903153000_AddTransferTransactionLinks.cs b/src/Treasury.App/Infrastructure/Migrations/20260903153000_AddTransferTransactionLinks.cs
new file mode 100644
index 0000000..2c8177b
--- /dev/null
+++ b/src/Treasury.App/Infrastructure/Migrations/20260903153000_AddTransferTransactionLinks.cs
@@ -0,0 +1,40 @@
+﻿using Microsoft.EntityFrameworkCore.Migrations;
+
+#nullable disable
+
+namespace Treasury.App.Infrastructure.Migrations
+{
+    /// <inheritdoc />
+    public partial class AddTransferTransactionLinks : Migration
+    {
+        /// <inheritdoc />
+        protected override void Up(MigrationBuilder migrationBuilder)
+        {
+            migrationBuilder.AddColumn<Guid>(
+                name: "InflowTransactionId",
+                table: "Transfers",
+                type: "uuid",
+                nullable: false,
+                defaultValue: Guid.Empty);
+
+            migrationBuilder.AddColumn<Guid>(
+                name: "OutflowTransactionId",
+                table: "Transfers",
+                type: "uuid",
+                nullable: false,
+                defaultValue: Guid.Empty);
+        }
+
+        /// <inheritdoc />
+        protected override void Down(MigrationBuilder migrationBuilder)
+        {
+            migrationBuilder.DropColumn(
+                name: "InflowTransactionId",
+                table: "Transfers");
+
+            migrationBuilder.DropColumn(
+                name: "OutflowTransactionId",
+                table: "Transfers");
+        }
+    }
+}
diff --git a/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs b/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
index a9b083a..d5db124 100644
--- a/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
+++ b/src/Treasury.App/Infrastructure/Migrations/TreasuryDbContextModelSnapshot.cs
@@ -1,11 +1,11 @@
-﻿// <auto-generated />
+// <auto-generated />
 using System;
 using Microsoft.EntityFrameworkCore;
 using Microsoft.EntityFrameworkCore.Infrastructure;
 using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
 using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
 using Treasury.App.Infrastructure.Data;
 
 #nullable disable
 
 namespace Treasury.App.Infrastructure.Migrations
@@ -571,21 +571,21 @@ namespace Treasury.App.Infrastructure.Migrations
                     b.Property<Guid>("TagId")
                         .HasColumnType("uuid");
 
                     b.HasKey("TransactionId", "TagId");
 
                     b.HasIndex("TagId");
 
                     b.ToTable("TransactionTags");
                 });
 
-            modelBuilder.Entity("Treasury.App.Domain.Transfer", b =>
+                        modelBuilder.Entity("Treasury.App.Domain.Transfer", b =>
                 {
                     b.Property<Guid>("Id")
                         .ValueGeneratedOnAdd()
                         .HasColumnType("uuid");
 
                     b.Property<decimal>("Amount")
                         .HasColumnType("numeric");
 
                     b.Property<DateTime>("CreatedAt")
                         .HasColumnType("timestamp with time zone");
@@ -597,20 +597,26 @@ namespace Treasury.App.Infrastructure.Migrations
                     b.Property<string>("Description")
                         .IsRequired()
                         .HasColumnType("text");
 
                     b.Property<Guid>("FromAccountId")
                         .HasColumnType("uuid");
 
                     b.Property<Guid>("HouseholdId")
                         .HasColumnType("uuid");
 
+                    b.Property<Guid>("InflowTransactionId")
+                        .HasColumnType("uuid");
+
+                    b.Property<Guid>("OutflowTransactionId")
+                        .HasColumnType("uuid");
+
                     b.Property<Guid>("ToAccountId")
                         .HasColumnType("uuid");
 
                     b.Property<DateTime>("TransferDate")
                         .HasColumnType("timestamp with time zone");
 
                     b.HasKey("Id");
 
                     b.ToTable("Transfers");
                 });
diff --git a/src/Treasury.App/Pages/Transfers.razor b/src/Treasury.App/Pages/Transfers.razor
new file mode 100644
index 0000000..f80df8b
--- /dev/null
+++ b/src/Treasury.App/Pages/Transfers.razor
@@ -0,0 +1,92 @@
+@page "/transfers"
+@attribute [Authorize]
+
+<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="pa-4 pa-sm-6">
+    <MudStack Spacing="3">
+        <MudText Typo="Typo.h4">Transfers</MudText>
+
+        @if (!string.IsNullOrWhiteSpace(_loadError))
+        {
+            <MudAlert Severity="Severity.Error">@_loadError</MudAlert>
+        }
+
+        <MudPaper Class="pa-4 rounded-xl" Elevation="2">
+            <MudGrid>
+                <MudItem xs="12" md="3">
+                    @if (_accounts.Count == 0)
+                    {
+                        <MudText Typo="Typo.body2" Class="mt-2">No active accounts available. Create or reactivate one of your accounts first.</MudText>
+                    }
+                    else
+                    {
+                        <MudSelect T="string" Label="From account" @bind-Value="_newTransfer.FromAccountId">
+                            @foreach (var account in _accounts)
+                            {
+                                <MudSelectItem Value="@account.Id.ToString()">@account.Name (@account.Currency)</MudSelectItem>
+                            }
+                        </MudSelect>
+                    }
+                </MudItem>
+                <MudItem xs="12" md="3">
+                    @if (_accounts.Count == 0)
+                    {
+                        <MudText Typo="Typo.body2" Class="mt-2">Transferring between active accounts is only available to account owners.</MudText>
+                    }
+                    else
+                    {
+                        <MudSelect T="string" Label="To account" @bind-Value="_newTransfer.ToAccountId">
+                            @foreach (var account in _accounts)
+                            {
+                                <MudSelectItem Value="@account.Id.ToString()">@account.Name (@account.Currency)</MudSelectItem>
+                            }
+                        </MudSelect>
+                    }
+                </MudItem>
+                <MudItem xs="12" md="2">
+                    <MudNumericField Label="Amount" @bind-Value="_newTransfer.Amount" Min="0.01m" />
+                </MudItem>
+                <MudItem xs="12" md="2">
+                    <MudDatePicker Label="Date" @bind-Date="_newTransfer.TransferDate" />
+                </MudItem>
+                <MudItem xs="12" md="2">
+                    <MudTextField Label="Description" @bind-Value="_newTransfer.Description" />
+                </MudItem>
+            </MudGrid>
+
+            <MudStack Row="true" Class="mt-4" Justify="Justify.FlexEnd">
+                <MudButton Variant="Variant.Filled" Color="Color.Tertiary" OnClick="CreateTransferAsync">Save transfer</MudButton>
+            </MudStack>
+        </MudPaper>
+
+        <MudPaper Class="pa-4 rounded-xl" Elevation="2">
+            <MudStack Row="true" Justify="Justify.SpaceBetween" AlignItems="AlignItems.Center" Class="mb-3">
+                <MudText Typo="Typo.h6">Recent history</MudText>
+                <MudText Typo="Typo.body2" Color="Color.Secondary">Latest 10 transfers in your household</MudText>
+            </MudStack>
+
+            @if (_transfers.Count == 0)
+            {
+                <MudAlert Severity="Severity.Info">No transfers yet. Create your first transfer to see it listed here.</MudAlert>
+            }
+            else
+            {
+                <MudTable T="TransferHistoryRow" Items="_transfers" Hover="true" Dense="true">
+                    <HeaderContent>
+                        <MudTh>Date</MudTh>
+                        <MudTh>From</MudTh>
+                        <MudTh>To</MudTh>
+                        <MudTh>Description</MudTh>
+                        <MudTh>Amount</MudTh>
+                    </HeaderContent>
+                    <RowTemplate>
+                        <MudTd DataLabel="Date">@context.TransferDate.ToLocalTime().ToString("yyyy-MM-dd")</MudTd>
+                        <MudTd DataLabel="From">@context.FromAccountName</MudTd>
+                        <MudTd DataLabel="To">@context.ToAccountName</MudTd>
+                        <MudTd DataLabel="Description">@context.Description</MudTd>
+                        <MudTd DataLabel="Amount">@context.Amount.ToString("N2") @context.Currency</MudTd>
+                    </RowTemplate>
+                </MudTable>
+            }
+        </MudPaper>
+    </MudStack>
+</MudContainer>
diff --git a/src/Treasury.App/Pages/Transfers.razor.cs b/src/Treasury.App/Pages/Transfers.razor.cs
new file mode 100644
index 0000000..0e59bd6
--- /dev/null
+++ b/src/Treasury.App/Pages/Transfers.razor.cs
@@ -0,0 +1,267 @@
+using Microsoft.AspNetCore.Components;
+using Microsoft.AspNetCore.Components.Authorization;
+using Microsoft.AspNetCore.Identity;
+using Microsoft.EntityFrameworkCore;
+using MudBlazor;
+using Treasury.App.Domain;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.App.Pages;
+
+public partial class Transfers
+{
+    [Inject] public TreasuryDbContext DbContext { get; set; } = default!;
+    [Inject] public ISnackbar Snackbar { get; set; } = default!;
+    [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
+    [Inject] public UserManager<ApplicationUser> UserManager { get; set; } = default!;
+
+    private ApplicationUser? _currentUser;
+    private readonly List<AccountChoice> _accounts = new();
+    private readonly List<TransferHistoryRow> _transfers = new();
+    private readonly NewTransferForm _newTransfer = new();
+    private readonly Dictionary<Guid, string> _visibleAccountNames = new();
+    private string? _loadError;
+
+    protected override async Task OnInitializedAsync()
+    {
+        await LoadAsync();
+    }
+
+    private async Task LoadAsync()
+    {
+        _loadError = null;
+
+        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
+        _currentUser = await UserManager.GetUserAsync(authState.User);
+        if (_currentUser is null)
+        {
+            _accounts.Clear();
+            _transfers.Clear();
+            _visibleAccountNames.Clear();
+            return;
+        }
+
+        try
+        {
+            var householdAccounts = await DbContext.Accounts
+                .Where(x => x.HouseholdId == _currentUser.HouseholdId)
+                .OrderBy(x => x.Name)
+                .ToListAsync();
+
+            _visibleAccountNames.Clear();
+            foreach (var account in householdAccounts)
+            {
+                _visibleAccountNames[account.Id] = account.Name;
+            }
+
+            _accounts.Clear();
+            _accounts.AddRange(householdAccounts
+                .Where(x => x.OwnerUserId == _currentUser.Id && x.IsActive)
+                .Select(x => new AccountChoice
+                {
+                    Id = x.Id,
+                    Name = x.Name,
+                    Currency = x.Currency
+                }));
+
+            _transfers.Clear();
+            var transfers = await DbContext.Transfers
+                .Where(x => x.HouseholdId == _currentUser.HouseholdId)
+                .OrderByDescending(x => x.TransferDate)
+                .ThenByDescending(x => x.CreatedAt)
+                .Take(10)
+                .ToListAsync();
+
+            _transfers.AddRange(transfers.Select(x => new TransferHistoryRow
+            {
+                Id = x.Id,
+                FromAccountName = ResolveAccountName(x.FromAccountId),
+                ToAccountName = ResolveAccountName(x.ToAccountId),
+                Description = x.Description,
+                Amount = x.Amount,
+                Currency = x.Currency,
+                TransferDate = x.TransferDate
+            }));
+
+            if (_accounts.Count > 0)
+            {
+                if (string.IsNullOrWhiteSpace(_newTransfer.FromAccountId) || !_accounts.Any(x => x.Id.ToString() == _newTransfer.FromAccountId))
+                {
+                    _newTransfer.FromAccountId = _accounts[0].Id.ToString();
+                }
+
+                if (string.IsNullOrWhiteSpace(_newTransfer.ToAccountId) || !_accounts.Any(x => x.Id.ToString() == _newTransfer.ToAccountId))
+                {
+                    _newTransfer.ToAccountId = _accounts[Math.Min(1, _accounts.Count - 1)].Id.ToString();
+                }
+            }
+        }
+        catch (Exception ex)
+        {
+            _accounts.Clear();
+            _transfers.Clear();
+            _visibleAccountNames.Clear();
+            _loadError = "Unable to load transfers.";
+            Snackbar.Add($"Unable to load transfers: {ex.Message}", Severity.Error);
+        }
+    }
+
+    private async Task CreateTransferAsync()
+    {
+        if (_currentUser is null)
+        {
+            Snackbar.Add("Sign in first.", Severity.Warning);
+            return;
+        }
+
+        if (_accounts.Count == 0)
+        {
+            Snackbar.Add("Create an active account before transferring funds.", Severity.Warning);
+            return;
+        }
+
+        if (!Guid.TryParse(_newTransfer.FromAccountId, out var fromAccountId) ||
+            !Guid.TryParse(_newTransfer.ToAccountId, out var toAccountId) ||
+            fromAccountId == Guid.Empty ||
+            toAccountId == Guid.Empty ||
+            fromAccountId == toAccountId ||
+            _newTransfer.Amount <= 0m)
+        {
+            Snackbar.Add("Choose two different active accounts and a positive amount.", Severity.Warning);
+            return;
+        }
+
+        var fromAccount = await DbContext.Accounts.SingleOrDefaultAsync(x =>
+            x.Id == fromAccountId &&
+            x.HouseholdId == _currentUser.HouseholdId &&
+            x.OwnerUserId == _currentUser.Id, CancellationToken.None);
+
+        var toAccount = await DbContext.Accounts.SingleOrDefaultAsync(x =>
+            x.Id == toAccountId &&
+            x.HouseholdId == _currentUser.HouseholdId &&
+            x.OwnerUserId == _currentUser.Id, CancellationToken.None);
+
+        if (fromAccount is null || toAccount is null)
+        {
+            Snackbar.Add("Transfer accounts were not found.", Severity.Error);
+            return;
+        }
+
+        if (!fromAccount.IsActive || !toAccount.IsActive)
+        {
+            Snackbar.Add("Transfers are allowed only between active accounts.", Severity.Warning);
+            return;
+        }
+
+        var transferDate = _newTransfer.TransferDate ?? DateTime.UtcNow;
+        var description = string.IsNullOrWhiteSpace(_newTransfer.Description)
+            ? "Account transfer"
+            : _newTransfer.Description.Trim();
+        var currency = fromAccount.Currency;
+
+        var transfer = new Transfer
+        {
+            HouseholdId = _currentUser.HouseholdId,
+            FromAccountId = fromAccount.Id,
+            ToAccountId = toAccount.Id,
+            Amount = _newTransfer.Amount,
+            Currency = currency,
+            Description = description,
+            TransferDate = transferDate,
+            CreatedAt = DateTime.UtcNow
+        };
+
+        var outflow = new Transaction
+        {
+            HouseholdId = _currentUser.HouseholdId,
+            AccountId = fromAccount.Id,
+            Description = $"{description} -> {toAccount.Name}",
+            Category = "Transfer",
+            Amount = _newTransfer.Amount,
+            Currency = currency,
+            Type = "transfer-out",
+            TransactionDate = transferDate,
+            CreatedAt = DateTime.UtcNow,
+            UpdatedAt = DateTime.UtcNow
+        };
+
+        var inflow = new Transaction
+        {
+            HouseholdId = _currentUser.HouseholdId,
+            AccountId = toAccount.Id,
+            Description = $"{description} <- {fromAccount.Name}",
+            Category = "Transfer",
+            Amount = _newTransfer.Amount,
+            Currency = currency,
+            Type = "transfer-in",
+            TransactionDate = transferDate,
+            CreatedAt = DateTime.UtcNow,
+            UpdatedAt = DateTime.UtcNow
+        };
+
+        fromAccount.CurrentBalance -= Math.Abs(_newTransfer.Amount);
+        toAccount.CurrentBalance += Math.Abs(_newTransfer.Amount);
+        fromAccount.UpdatedAt = DateTime.UtcNow;
+        toAccount.UpdatedAt = DateTime.UtcNow;
+
+        async Task PersistAsync()
+        {
+            DbContext.Transfers.Add(transfer);
+            DbContext.Transactions.Add(outflow);
+            DbContext.Transactions.Add(inflow);
+            await DbContext.SaveChangesAsync();
+
+            transfer.OutflowTransactionId = outflow.Id;
+            transfer.InflowTransactionId = inflow.Id;
+            await DbContext.SaveChangesAsync();
+        }
+
+        if (DbContext.Database.IsRelational())
+        {
+            await using var transaction = await DbContext.Database.BeginTransactionAsync();
+            await PersistAsync();
+            await transaction.CommitAsync();
+        }
+        else
+        {
+            await PersistAsync();
+        }
+
+        _newTransfer.Description = string.Empty;
+        _newTransfer.Amount = 0m;
+        _newTransfer.TransferDate = DateTime.UtcNow;
+
+        Snackbar.Add("Transfer saved.", Severity.Success);
+        await LoadAsync();
+    }
+
+    private string ResolveAccountName(Guid accountId) =>
+        _visibleAccountNames.TryGetValue(accountId, out var name) ? name : "Unknown account";
+
+    private sealed class AccountChoice
+    {
+        public Guid Id { get; set; }
+        public string Name { get; set; } = string.Empty;
+        public string Currency { get; set; } = string.Empty;
+    }
+
+    private sealed class NewTransferForm
+    {
+        public string FromAccountId { get; set; } = string.Empty;
+        public string ToAccountId { get; set; } = string.Empty;
+        public decimal Amount { get; set; }
+        public DateTime? TransferDate { get; set; } = DateTime.UtcNow;
+        public string Description { get; set; } = string.Empty;
+    }
+
+    private sealed class TransferHistoryRow
+    {
+        public Guid Id { get; set; }
+        public string FromAccountName { get; set; } = string.Empty;
+        public string ToAccountName { get; set; } = string.Empty;
+        public string Description { get; set; } = string.Empty;
+        public decimal Amount { get; set; }
+        public string Currency { get; set; } = string.Empty;
+        public DateTime TransferDate { get; set; }
+    }
+}
diff --git a/tests/Treasury.IntegrationTests/TransfersWorkflowTests.cs b/tests/Treasury.IntegrationTests/TransfersWorkflowTests.cs
new file mode 100644
index 0000000..8e9dff8
--- /dev/null
+++ b/tests/Treasury.IntegrationTests/TransfersWorkflowTests.cs
@@ -0,0 +1,169 @@
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
+public class TransfersWorkflowTests
+{
+    [Fact]
+    public async Task Transfer_Creates_Transfer_And_Two_Linked_Transactions()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var fromAccountId = await CreateAccountAsync(client, "From account");
+        var toAccountId = await CreateAccountAsync(client, "To account");
+
+        var response = await client.PostAsJsonAsync("/api/transfers", new
+        {
+            FromAccountId = fromAccountId,
+            ToAccountId = toAccountId,
+            Amount = 25.50m,
+            Currency = "PLN",
+            Description = "Rent split",
+            TransferDate = DateTime.UtcNow
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
+        var transferId = payload.RootElement.GetProperty("id").GetGuid();
+        var outflowTransactionId = payload.RootElement.GetProperty("outflowTransactionId").GetGuid();
+        var inflowTransactionId = payload.RootElement.GetProperty("inflowTransactionId").GetGuid();
+
+        await using var scope = app.Services.CreateAsyncScope();
+        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
+
+        var transfer = await db.Transfers.SingleAsync(x => x.Id == transferId);
+        transfer.OutflowTransactionId.Should().Be(outflowTransactionId);
+        transfer.InflowTransactionId.Should().Be(inflowTransactionId);
+        transfer.FromAccountId.Should().Be(fromAccountId);
+        transfer.ToAccountId.Should().Be(toAccountId);
+
+        var outflow = await db.Transactions.SingleAsync(x => x.Id == outflowTransactionId);
+        var inflow = await db.Transactions.SingleAsync(x => x.Id == inflowTransactionId);
+
+        outflow.AccountId.Should().Be(fromAccountId);
+        outflow.Type.Should().Be("transfer-out");
+        inflow.AccountId.Should().Be(toAccountId);
+        inflow.Type.Should().Be("transfer-in");
+
+        var fromAccount = await db.Accounts.SingleAsync(x => x.Id == fromAccountId);
+        var toAccount = await db.Accounts.SingleAsync(x => x.Id == toAccountId);
+
+        fromAccount.CurrentBalance.Should().Be(-25.50m);
+        toAccount.CurrentBalance.Should().Be(25.50m);
+    }
+
+    [Fact]
+    public async Task Transfer_Rejects_Inactive_Accounts()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var fromAccountId = await CreateAccountAsync(client, "From account");
+        var toAccountId = await CreateAccountAsync(client, "To account");
+
+        var deactivateResponse = await client.PutAsJsonAsync($"/api/accounts/{fromAccountId}/active-state", new
+        {
+            IsActive = false
+        });
+        deactivateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        var response = await client.PostAsJsonAsync("/api/transfers", new
+        {
+            FromAccountId = fromAccountId,
+            ToAccountId = toAccountId,
+            Amount = 10m,
+            Currency = "PLN",
+            Description = "Blocked transfer",
+            TransferDate = DateTime.UtcNow
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
+        var body = await response.Content.ReadAsStringAsync();
+        body.Should().Contain("inactive");
+    }
+
+    [Fact]
+    public async Task TransfersPage_Shows_Recent_History()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var fromAccountId = await CreateAccountAsync(client, "History from");
+        var toAccountId = await CreateAccountAsync(client, "History to");
+
+        var response = await client.PostAsJsonAsync("/api/transfers", new
+        {
+            FromAccountId = fromAccountId,
+            ToAccountId = toAccountId,
+            Amount = 12.34m,
+            Currency = "PLN",
+            Description = "History transfer",
+            TransferDate = DateTime.UtcNow
+        });
+        response.StatusCode.Should().Be(HttpStatusCode.Created);
+
+        var pageResponse = await client.GetAsync("/transfers");
+        pageResponse.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        var html = await pageResponse.Content.ReadAsStringAsync();
+        html.Should().Contain("Transfers");
+        html.Should().Contain("History transfer");
+        html.Should().Contain("History from");
+        html.Should().Contain("History to");
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
+}
