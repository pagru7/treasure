# Review Package

Base: e5746bee3bf63a861ef79719f8fc7d7d3cd1e2d0
Head: 97c373f3f7e1098bbead8e8e33b1edb8136ab618

## Commits

97c373f fix: enforce transaction edit rules
ae09ae7 feat: add transaction balance tracking and edit rules

## Diff Stat

 .superpowers/sdd/phase2-task-3-report.md           |  42 ++
 .../Transactions/TransactionEditingService.cs      | 272 ++++++++
 .../Transfers/TransferCreationService.cs           |   2 +
 .../Transactions/UpdateTransactionRequest.cs       |  12 +
 src/Treasury.App/Domain/Transaction.cs             |   1 +
 .../Accounts/GetAccountTransactionsEndpoint.cs     |   5 +-
 .../Transactions/CreateTransactionEndpoint.cs      |   9 +-
 .../Transactions/GetTransactionsEndpoint.cs        |   3 +
 .../Transactions/UpdateTransactionEndpoint.cs      |  75 +++
 .../Infrastructure/Data/Seed/InitialSeed.cs        |   3 +
 ...dTransactionBalanceAfterTransaction.Designer.cs | 747 +++++++++++++++++++++
 ...134028_AddTransactionBalanceAfterTransaction.cs |  29 +
 .../Migrations/TreasuryDbContextModelSnapshot.cs   |   7 +-
 src/Treasury.App/Pages/Transactions.razor          | 321 ++++++++-
 src/Treasury.App/Program.cs                        |   2 +
 src/Treasury.App/_Imports.razor                    |   2 +
 .../SharedReadOnlyUiPermissionTests.cs             |  22 +
 .../TransactionEditingRulesTests.cs                | 250 +++++++
 18 files changed, 1781 insertions(+), 23 deletions(-)

## Full Diff (-U10)

diff --git a/.superpowers/sdd/phase2-task-3-report.md b/.superpowers/sdd/phase2-task-3-report.md
new file mode 100644
index 0000000..86078e4
--- /dev/null
+++ b/.superpowers/sdd/phase2-task-3-report.md
@@ -0,0 +1,42 @@
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
+
+## Resolution update
+
+- Routed transaction edits through a shared `TransactionEditingService` so the UI and `PUT /api/transactions/{id:guid}` enforce the same permission checks.
+- Switched `UpdateTransactionRequest` to nullable/optional fields so omitted values are preserved instead of treated as edits.
+- Blocked edits for transfer-linked transactions and added coverage for shared-readonly permission checks, partial updates, and transfer-linked rejection.
diff --git a/src/Treasury.App/Application/Transactions/TransactionEditingService.cs b/src/Treasury.App/Application/Transactions/TransactionEditingService.cs
new file mode 100644
index 0000000..a739a95
--- /dev/null
+++ b/src/Treasury.App/Application/Transactions/TransactionEditingService.cs
@@ -0,0 +1,272 @@
+using Microsoft.AspNetCore.Identity;
+using Microsoft.EntityFrameworkCore;
+using Treasury.App.Contracts.Transactions;
+using Treasury.App.Domain;
+using Treasury.App.Infrastructure.Data;
+
+namespace Treasury.App.Application.Transactions;
+
+public enum TransactionEditStatus
+{
+    Success,
+    InvalidRequest,
+    NotFound,
+    Forbidden
+}
+
+public sealed record TransactionEditIssue(string Field, string Message);
+
+public sealed record TransactionEditOutcome(
+    Guid Id,
+    Guid AccountId,
+    string Description,
+    string Category,
+    decimal Amount,
+    string Currency,
+    string Type,
+    DateTime TransactionDate,
+    decimal BalanceAfterTransaction,
+    IReadOnlyList<Guid> TagIds);
+
+public sealed record TransactionEditResult(
+    TransactionEditStatus Status,
+    string? Message,
+    IReadOnlyList<TransactionEditIssue> Issues,
+    TransactionEditOutcome? Outcome)
+{
+    public bool Succeeded => Status == TransactionEditStatus.Success && Outcome is not null;
+
+    public static TransactionEditResult Success(TransactionEditOutcome outcome) =>
+        new(TransactionEditStatus.Success, null, [], outcome);
+
+    public static TransactionEditResult Failure(
+        TransactionEditStatus status,
+        string message,
+        params TransactionEditIssue[] issues) =>
+        new(status, message, issues, null);
+}
+
+public class TransactionEditingService(TreasuryDbContext db)
+{
+    public virtual async Task<TransactionEditResult> UpdateAsync(
+        ApplicationUser user,
+        UpdateTransactionRequest request,
+        CancellationToken ct)
+    {
+        if (request.Id == Guid.Empty)
+        {
+            return TransactionEditResult.Failure(
+                TransactionEditStatus.InvalidRequest,
+                "Please correct the highlighted fields.",
+                new TransactionEditIssue(nameof(UpdateTransactionRequest.Id), "A valid transaction is required."));
+        }
+
+        var transaction = await db.Transactions
+            .Include(x => x.TransactionTags)
+            .SingleOrDefaultAsync(x => x.Id == request.Id && x.HouseholdId == user.HouseholdId, ct);
+        if (transaction is null)
+        {
+            return TransactionEditResult.Failure(TransactionEditStatus.NotFound, "Transaction was not found.");
+        }
+
+        var account = await db.Accounts.SingleOrDefaultAsync(x => x.Id == transaction.AccountId && x.HouseholdId == user.HouseholdId, ct);
+        if (account is null)
+        {
+            return TransactionEditResult.Failure(TransactionEditStatus.NotFound, "Account was not found.");
+        }
+
+        if (account.OwnerUserId != user.Id)
+        {
+            return TransactionEditResult.Failure(
+                TransactionEditStatus.Forbidden,
+                "You do not have permission to edit this transaction.");
+        }
+
+        if (await IsTransferLinkedAsync(transaction, ct))
+        {
+            return TransactionEditResult.Failure(
+                TransactionEditStatus.InvalidRequest,
+                "Transfer-linked transactions cannot be edited. Edit the transfer instead.",
+                new TransactionEditIssue(nameof(UpdateTransactionRequest.Id), "Transfer-linked transactions cannot be edited. Edit the transfer instead."));
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
+        var hasDescription = request.Description is not null;
+        var hasCategory = request.Category is not null;
+        var hasAmount = request.Amount.HasValue;
+        var hasType = request.Type is not null;
+        var hasTransactionDate = request.TransactionDate.HasValue;
+        var hasTagIds = request.TagIds is not null;
+
+        var normalizedDescription = hasDescription ? request.Description!.Trim() : transaction.Description;
+        var normalizedCategory = hasCategory ? NormalizeCategory(request.Category!, transaction.Category) : transaction.Category;
+        var normalizedAmount = hasAmount ? request.Amount!.Value : transaction.Amount;
+        var normalizedType = hasType ? NormalizeType(request.Type!, transaction.Type) : transaction.Type;
+        var normalizedTransactionDate = hasTransactionDate ? request.TransactionDate!.Value : transaction.TransactionDate;
+
+        var issues = new List<TransactionEditIssue>();
+
+        if (hasDescription && string.IsNullOrWhiteSpace(normalizedDescription))
+        {
+            issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Description), "Description is required."));
+        }
+
+        if (hasAmount && normalizedAmount <= 0m)
+        {
+            issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Amount), "Amount must be greater than zero."));
+        }
+
+        var amountChanged = hasAmount && normalizedAmount != transaction.Amount;
+        var typeChanged = hasType && !string.Equals(normalizedType, transaction.Type, StringComparison.OrdinalIgnoreCase);
+        var dateChanged = hasTransactionDate && normalizedTransactionDate != transaction.TransactionDate;
+
+        if (!isLatest)
+        {
+            if (amountChanged)
+            {
+                issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Amount), "Amount can only be changed on the latest transaction."));
+            }
+
+            if (dateChanged)
+            {
+                issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.TransactionDate), "Transaction date can only be changed on the latest transaction."));
+            }
+
+            if (typeChanged)
+            {
+                issues.Add(new TransactionEditIssue(nameof(UpdateTransactionRequest.Type), "Type can only be changed on the latest transaction."));
+            }
+        }
+
+        if (issues.Count > 0)
+        {
+            return TransactionEditResult.Failure(
+                TransactionEditStatus.InvalidRequest,
+                "Please correct the highlighted fields.",
+                issues.ToArray());
+        }
+
+        transaction.Description = normalizedDescription;
+        transaction.Category = normalizedCategory;
+        transaction.Amount = normalizedAmount;
+        transaction.Type = normalizedType;
+        transaction.TransactionDate = normalizedTransactionDate;
+        transaction.UpdatedAt = DateTime.UtcNow;
+
+        if (hasTagIds)
+        {
+            var distinctTagIds = request.TagIds!.Distinct().ToArray();
+            var validTagIds = await db.Tags
+                .Where(x => x.HouseholdId == user.HouseholdId && distinctTagIds.Contains(x.Id))
+                .Select(x => x.Id)
+                .ToListAsync(ct);
+
+            db.TransactionTags.RemoveRange(transaction.TransactionTags);
+            transaction.TransactionTags.Clear();
+
+            foreach (var tagId in validTagIds)
+            {
+                transaction.TransactionTags.Add(new TransactionTag
+                {
+                    Transaction = transaction,
+                    TagId = tagId
+                });
+            }
+        }
+
+        if (isLatest && (amountChanged || typeChanged || dateChanged))
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
+        var tagIds = transaction.TransactionTags
+            .Select(x => x.TagId)
+            .ToList();
+
+        return TransactionEditResult.Success(new TransactionEditOutcome(
+            transaction.Id,
+            transaction.AccountId,
+            transaction.Description,
+            transaction.Category,
+            transaction.Amount,
+            transaction.Currency,
+            transaction.Type,
+            transaction.TransactionDate,
+            transaction.BalanceAfterTransaction,
+            tagIds));
+    }
+
+    private async Task<bool> IsTransferLinkedAsync(Transaction transaction, CancellationToken ct) =>
+        transaction.Type is "transfer-in" or "transfer-out"
+        || await db.Transfers.AnyAsync(
+            x => x.OutflowTransactionId == transaction.Id || x.InflowTransactionId == transaction.Id,
+            ct);
+
+    private static string NormalizeType(string? type, string fallback) =>
+        string.IsNullOrWhiteSpace(type) ? fallback : type.Trim().ToLowerInvariant();
+
+    private static string NormalizeCategory(string category, string fallback) =>
+        string.IsNullOrWhiteSpace(category) ? fallback : category.Trim();
+
+    private static decimal GetDelta(decimal amount, string type)
+    {
+        var normalizedType = NormalizeType(type, "expense");
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
index 0000000..3731079
--- /dev/null
+++ b/src/Treasury.App/Contracts/Transactions/UpdateTransactionRequest.cs
@@ -0,0 +1,12 @@
+namespace Treasury.App.Contracts.Transactions;
+
+public sealed class UpdateTransactionRequest
+{
+    public Guid Id { get; set; }
+    public string? Description { get; set; }
+    public string? Category { get; set; }
+    public decimal? Amount { get; set; }
+    public string? Type { get; set; }
+    public DateTime? TransactionDate { get; set; }
+    public List<Guid>? TagIds { get; set; }
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
index 0000000..57e7f4d
--- /dev/null
+++ b/src/Treasury.App/Endpoints/Transactions/UpdateTransactionEndpoint.cs
@@ -0,0 +1,75 @@
+using FastEndpoints;
+using Microsoft.AspNetCore.Identity;
+using Treasury.App.Application.Transactions;
+using Treasury.App.Contracts.Transactions;
+using Treasury.App.Domain;
+
+namespace Treasury.App.Endpoints.Transactions;
+
+public sealed class UpdateTransactionEndpoint(
+    TransactionEditingService transactionEditingService,
+    UserManager<ApplicationUser> userManager)
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
+        var result = await transactionEditingService.UpdateAsync(user, request, ct);
+        if (!result.Succeeded)
+        {
+            if (result.Status == TransactionEditStatus.NotFound)
+            {
+                await SendNotFoundAsync(ct);
+                return;
+            }
+
+            if (result.Status == TransactionEditStatus.Forbidden)
+            {
+                await SendForbiddenAsync(ct);
+                return;
+            }
+
+            if (result.Issues.Count > 0)
+            {
+                foreach (var issue in result.Issues)
+                {
+                    AddError(issue.Message);
+                }
+            }
+            else if (!string.IsNullOrWhiteSpace(result.Message))
+            {
+                AddError(result.Message);
+            }
+
+            await SendErrorsAsync(cancellation: ct);
+            return;
+        }
+
+        var outcome = result.Outcome!;
+        await SendAsync(new
+        {
+            outcome.Id,
+            outcome.AccountId,
+            outcome.Description,
+            outcome.Category,
+            outcome.Amount,
+            outcome.Currency,
+            outcome.Type,
+            outcome.TransactionDate,
+            outcome.BalanceAfterTransaction,
+            Tags = outcome.TagIds
+        }, StatusCodes.Status200OK, ct);
+    }
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
index 626e971..9028c32 100644
--- a/src/Treasury.App/Pages/Transactions.razor
+++ b/src/Treasury.App/Pages/Transactions.razor
@@ -1,13 +1,16 @@
 @page "/transactions"
 @attribute [Authorize]
 @inject TreasuryDbContext DbContext
+@inject AuthenticationStateProvider AuthenticationStateProvider
+@inject UserManager<ApplicationUser> UserManager
+@inject TransactionEditingService TransactionEditingService
 @inject ISnackbar Snackbar
 
 <MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="pa-4 pa-sm-6">
     <MudStack Spacing="3">
         <MudText Typo="Typo.h4">Transactions</MudText>
 
         <MudPaper Class="pa-4 rounded-xl" Elevation="2">
             <MudGrid>
                 <MudItem xs="12" md="3">
                     @if (_accounts.Count == 0)
@@ -63,36 +66,111 @@
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
@@ -100,95 +178,154 @@
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
+                            <MudStack Spacing="0">
+                                <MudButton Size="Size.Small"
+                                           Variant="Variant.Outlined"
+                                           Disabled="@(!CanEditTransaction(context))"
+                                           OnClick="@(() => BeginEdit(context))">
+                                    Edit
+                                </MudButton>
+                                @if (IsTransferLinked(context))
+                                {
+                                    <MudText Typo="Typo.caption" Color="Color.Warning">Transfer-linked</MudText>
+                                }
+                            </MudStack>
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
+    private readonly HashSet<Guid> _transferLinkedTransactionIds = new();
     private readonly HashSet<Guid> _selectedTagIds = new();
+    private readonly HashSet<Guid> _editingSelectedTagIds = new();
 
     private readonly NewTransactionForm _newTransaction = new();
+    private EditingTransactionForm? _editingTransaction;
+    private ApplicationUser? _currentUser;
 
     protected override async Task OnInitializedAsync()
     {
+        _currentUser = await GetCurrentUserAsync();
         await LoadAsync();
     }
 
     private async Task LoadAsync()
     {
         _accounts.Clear();
-        _accounts.AddRange(await DbContext.Accounts.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync());
+        if (_currentUser is not null)
+        {
+            _accounts.AddRange(await DbContext.Accounts
+                .Where(x => x.IsActive && x.OwnerUserId == _currentUser.Id)
+                .OrderBy(x => x.Name)
+                .ToListAsync());
+        }
 
         // Load full visible account list (includes inactive) so historical transactions still show account names
         _visibleAccounts.Clear();
         _visibleAccounts.AddRange(await DbContext.Accounts.OrderBy(x => x.Name).ToListAsync());
 
         _tags.Clear();
         _tags.AddRange(await DbContext.Tags.OrderBy(x => x.Name).ToListAsync());
 
+        _transferLinkedTransactionIds.Clear();
+        var transferLinks = await DbContext.Transfers
+            .Select(x => new { x.OutflowTransactionId, x.InflowTransactionId })
+            .ToListAsync();
+        foreach (var link in transferLinks)
+        {
+            if (link.OutflowTransactionId.HasValue)
+            {
+                _transferLinkedTransactionIds.Add(link.OutflowTransactionId.Value);
+            }
+
+            if (link.InflowTransactionId.HasValue)
+            {
+                _transferLinkedTransactionIds.Add(link.InflowTransactionId.Value);
+            }
+        }
+
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
         }
 
         if (_accounts.Count > 0 && string.IsNullOrWhiteSpace(_newTransaction.AccountId))
         {
             _newTransaction.AccountId = _accounts[0].Id.ToString();
         }
     }
 
+    private async Task<ApplicationUser?> GetCurrentUserAsync()
+    {
+        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
+        return await UserManager.GetUserAsync(authState.User);
+    }
+
     private void ToggleTag(Guid tagId, bool isSelected)
     {
         if (isSelected)
         {
             _selectedTagIds.Add(tagId);
             return;
         }
 
         _selectedTagIds.Remove(tagId);
     }
 
     private async Task CreateTransactionAsync()
     {
         if (_accounts.Count == 0)
         {
             Snackbar.Add("Create an account before adding transactions.", Severity.Warning);
             return;
         }
 
+        var currentUser = _currentUser ?? await GetCurrentUserAsync();
+        if (currentUser is null)
+        {
+            Snackbar.Add("You need to sign in again before adding transactions.", Severity.Error);
+            return;
+        }
+
         if (string.IsNullOrWhiteSpace(_newTransaction.Description) || string.IsNullOrWhiteSpace(_newTransaction.AccountId) || _newTransaction.Amount <= 0m)
         {
             Snackbar.Add("Description, account and a positive amount are required.", Severity.Warning);
             return;
         }
 
         if (!Guid.TryParse(_newTransaction.AccountId, out var accountId))
         {
             Snackbar.Add("Please select a valid account.", Severity.Warning);
             return;
@@ -201,87 +338,235 @@
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
+        if (account.OwnerUserId != currentUser.Id)
         {
-            delta = -Math.Abs(_newTransaction.Amount);
-        }
-        else if (normalizedType == "income")
-        {
-            delta = Math.Abs(_newTransaction.Amount);
-        }
-        else if (normalizedType == "transfer")
-        {
-            delta = _newTransaction.Amount;
+            Snackbar.Add("You can only add transactions to your own accounts.", Severity.Warning);
+            return;
         }
 
+        var normalizedType = NormalizeType(_newTransaction.Type);
+        var delta = GetDelta(_newTransaction.Amount, normalizedType);
+        var utcNow = DateTime.UtcNow;
+
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
+        if (!CanEditTransaction(transaction))
+        {
+            Snackbar.Add("Transfer-linked transactions and transactions from shared accounts cannot be edited here.", Severity.Warning);
+            return;
+        }
+
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
+        var currentUser = _currentUser ?? await GetCurrentUserAsync();
+        if (currentUser is null)
+        {
+            Snackbar.Add("You need to sign in again before saving changes.", Severity.Error);
+            return;
+        }
+
+        var request = new UpdateTransactionRequest
+        {
+            Id = _editingTransaction.Id,
+            Description = _editingTransaction.Description,
+            Category = _editingTransaction.Category,
+            Amount = _editingTransaction.IsLatest ? _editingTransaction.Amount : null,
+            Type = _editingTransaction.IsLatest ? _editingTransaction.Type : null,
+            TransactionDate = _editingTransaction.IsLatest ? _editingTransaction.TransactionDate : null,
+            TagIds = _editingSelectedTagIds.ToList()
+        };
+
+        var result = await TransactionEditingService.UpdateAsync(currentUser, request, CancellationToken.None);
+        if (!result.Succeeded)
+        {
+            var message = result.Issues.Count > 0
+                ? string.Join(" ", result.Issues.Select(x => x.Message).Distinct())
+                : result.Message ?? "Unable to update transaction.";
+            var severity = result.Status == TransactionEditStatus.Forbidden || result.Status == TransactionEditStatus.NotFound
+                ? Severity.Error
+                : Severity.Warning;
+            Snackbar.Add(message, severity);
+            return;
+        }
+
+        Snackbar.Add("Transaction updated.", Severity.Success);
+        await CancelEditAsync();
+        await LoadAsync();
+    }
+
     private string GetAccountName(Guid accountId)
     {
         // Use the full visible accounts collection so historical transactions from inactive accounts still show a name
         return _visibleAccounts.FirstOrDefault(x => x.Id == accountId)?.Name ?? "Unknown account";
     }
 
+    private bool CanEditTransaction(Treasury.App.Domain.Transaction transaction)
+    {
+        if (_currentUser is null)
+        {
+            return false;
+        }
+
+        if (IsTransferLinked(transaction))
+        {
+            return false;
+        }
+
+        var account = _visibleAccounts.FirstOrDefault(x => x.Id == transaction.AccountId);
+        return account is not null && account.OwnerUserId == _currentUser.Id;
+    }
+
+    private bool IsTransferLinked(Treasury.App.Domain.Transaction transaction) =>
+        _transferLinkedTransactionIds.Contains(transaction.Id)
+        || transaction.Type is "transfer-in" or "transfer-out";
+
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
 }
diff --git a/src/Treasury.App/Program.cs b/src/Treasury.App/Program.cs
index 711f102..d9ac5ac 100644
--- a/src/Treasury.App/Program.cs
+++ b/src/Treasury.App/Program.cs
@@ -1,19 +1,20 @@
 using System.Text.Json;
 using FastEndpoints;
 using Microsoft.AspNetCore.Components.Authorization;
 using Microsoft.AspNetCore.Identity;
 using Microsoft.EntityFrameworkCore;
 using MudBlazor.Services;
 using Treasury.App.Application.Dashboard;
 using Treasury.App.Application.Accounts;
 using Treasury.App.Application.Transfers;
+using Treasury.App.Application.Transactions;
 using Treasury.App.Components;
 using Treasury.App.Contracts.Accounts;
 using Treasury.App.Contracts.Bills;
 using Treasury.App.Contracts.Budgets;
 using Treasury.App.Contracts.Rates;
 using Treasury.App.Contracts.Tags;
 using Treasury.App.Contracts.Transactions;
 using Treasury.App.Contracts.Valuations;
 using Treasury.App.Application.Valuations;
 using Treasury.App.Domain;
@@ -88,20 +89,21 @@ builder.Services.AddAuthorization(options =>
     options.AddPolicy(Policies.SharedReadOnly, policy => policy.RequireAuthenticatedUser());
 });
 
 builder.Services.AddHealthChecks();
 builder.Services.AddRazorComponents().AddInteractiveServerComponents();
 builder.Services.AddCascadingAuthenticationState();
 builder.Services.AddFastEndpoints();
 builder.Services.AddMudServices();
 builder.Services.AddScoped<AccountSharingService>();
 builder.Services.AddScoped<TransferCreationService>();
+builder.Services.AddScoped<TransactionEditingService>();
 
 var app = builder.Build();
 
 using (var scope = app.Services.CreateScope())
 {
     var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
 
     if (db.Database.IsRelational())
     {
         await db.Database.MigrateAsync();
diff --git a/src/Treasury.App/_Imports.razor b/src/Treasury.App/_Imports.razor
index 43b2c3b..48deda9 100644
--- a/src/Treasury.App/_Imports.razor
+++ b/src/Treasury.App/_Imports.razor
@@ -7,14 +7,16 @@
 @using Microsoft.AspNetCore.Components.Routing
 @using Microsoft.AspNetCore.Components.Web
 @using Microsoft.AspNetCore.Components.Web.Virtualization
 @using static Microsoft.AspNetCore.Components.Web.RenderMode
 @using Microsoft.AspNetCore.Identity
 @using Microsoft.AspNetCore.WebUtilities
 @using Microsoft.EntityFrameworkCore
 @using Microsoft.JSInterop
 @using MudBlazor
 @using Treasury.App
+@using Treasury.App.Application.Transactions
 @using Treasury.App.Application.Dashboard
+@using Treasury.App.Contracts.Transactions
 @using Treasury.App.Domain
 @using Treasury.App.Infrastructure.Data
 @using Treasury.App.Theme
diff --git a/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs b/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
index f3e206c..577eeb6 100644
--- a/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
+++ b/tests/Treasury.IntegrationTests/SharedReadOnlyUiPermissionTests.cs
@@ -100,20 +100,42 @@ public class SharedReadOnlyUiPermissionTests
             Description = "Attempt by shared user",
             Category = "General",
             Amount = 10m,
             Currency = "PLN",
             Type = "expense",
             TransactionDate = DateTime.UtcNow
         });
 
         postTransaction.StatusCode.Should().Be(HttpStatusCode.Forbidden);
 
+        var ownerTransactionResponse = await ownerClient.PostAsJsonAsync("/api/transactions", new
+        {
+            AccountId = accountId,
+            Description = "Owner transaction",
+            Category = "General",
+            Amount = 15m,
+            Currency = "PLN",
+            Type = "expense",
+            TransactionDate = DateTime.UtcNow
+        });
+        ownerTransactionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
+        using var transactionJson = JsonDocument.Parse(await ownerTransactionResponse.Content.ReadAsStringAsync());
+        var transactionId = transactionJson.RootElement.GetProperty("id").GetGuid();
+
+        var editTransaction = await sharedClient.PutAsJsonAsync($"/api/transactions/{transactionId}", new
+        {
+            Id = transactionId,
+            Description = "Shared user edit attempt"
+        });
+
+        editTransaction.StatusCode.Should().Be(HttpStatusCode.Forbidden);
+
         // Additional guard: shared users must not be able to perform owner-only mutations such as balance correction
         var postBalanceCorrection = await sharedClient.PostAsJsonAsync($"/api/accounts/{accountId}/balance-correction", new
         {
             Amount = 100.00m,
             Reason = "Malicious correction by shared user"
         });
 
         // Expect that the mutation is forbidden for shared users
         postBalanceCorrection.StatusCode.Should().Be(HttpStatusCode.Forbidden);
     }
diff --git a/tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs b/tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs
new file mode 100644
index 0000000..8a421c6
--- /dev/null
+++ b/tests/Treasury.IntegrationTests/TransactionEditingRulesTests.cs
@@ -0,0 +1,250 @@
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
+    [Fact]
+    public async Task Edit_Partial_Request_Leaves_Omitted_Fields_Unchanged()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var accountId = await CreateAccountAsync(client, "Partial update");
+        var firstTransactionDate = DateTime.UtcNow.AddDays(-2);
+
+        var firstTransactionId = await CreateTransactionAsync(client, accountId, "First", 10m, "expense", firstTransactionDate);
+        _ = await CreateTransactionAsync(client, accountId, "Second", 20m, "income", DateTime.UtcNow.AddDays(-1));
+
+        var response = await client.PutAsJsonAsync($"/api/transactions/{firstTransactionId}", new
+        {
+            Id = firstTransactionId,
+            Description = "First renamed"
+        });
+
+        response.StatusCode.Should().Be(HttpStatusCode.OK);
+
+        await using var scope = app.Services.CreateAsyncScope();
+        var db = scope.ServiceProvider.GetRequiredService<TreasuryDbContext>();
+
+        var transaction = await db.Transactions.SingleAsync(x => x.Id == firstTransactionId);
+        transaction.Description.Should().Be("First renamed");
+        transaction.Amount.Should().Be(10m);
+        transaction.Type.Should().Be("expense");
+        transaction.TransactionDate.Date.Should().Be(firstTransactionDate.Date);
+    }
+
+    [Fact]
+    public async Task Edit_TransferLinked_Rejects_Changes()
+    {
+        await using var app = new TreasuryHostFactory();
+        var client = CreateAuthenticatedClient(app);
+        await RegisterAndSignInAsync(client);
+
+        var fromAccountId = await CreateAccountAsync(client, "Transfer from");
+        var toAccountId = await CreateAccountAsync(client, "Transfer to");
+
+        var transferResponse = await client.PostAsJsonAsync("/api/transfers", new
+        {
+            FromAccountId = fromAccountId,
+            ToAccountId = toAccountId,
+            Amount = 25m,
+            Currency = "PLN",
+            Description = "Blocked transfer",
+            TransferDate = DateTime.UtcNow
+        });
+
+        transferResponse.StatusCode.Should().Be(HttpStatusCode.Created);
+        using var transferJson = JsonDocument.Parse(await transferResponse.Content.ReadAsStringAsync());
+        var outflowTransactionId = transferJson.RootElement.GetProperty("outflowTransactionId").GetGuid();
+
+        var updateResponse = await client.PutAsJsonAsync($"/api/transactions/{outflowTransactionId}", new
+        {
+            Id = outflowTransactionId,
+            Description = "Should fail"
+        });
+
+        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
+        var body = await updateResponse.Content.ReadAsStringAsync();
+        body.Should().Contain("Transfer-linked transactions cannot be edited");
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
