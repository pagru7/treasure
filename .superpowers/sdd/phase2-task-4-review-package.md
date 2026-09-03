# Review Package

Base: 74328d0755f6e8768cfbf224f2a3a23ee7c573c1
Head: ed7599fddd91739f3442e1556f7eecbdddad36ae

## Commits

ed7599f test/docs: finalize phase2 lifecycle transfer transaction coverage

## Diff Stat

 README.md | 8 ++++++++
 1 file changed, 8 insertions(+)

## Full Diff (-U10)

diff --git a/README.md b/README.md
index f5ff5ac..9b3a78c 100644
--- a/README.md
+++ b/README.md
@@ -47,10 +47,18 @@ API equivalents:
 3. For coins: fill asset name, quantity, unit value, then save.
 
 API equivalents:
 - `POST /api/valuations/bullion`
 - `POST /api/valuations/coin`
 
 ## Notes
 
 - In Docker production mode, PostgreSQL is used and migrations are applied automatically on startup.
 - DataProtection keys are persisted in a Docker volume to keep auth/antiforgery tokens stable across restarts.
+
+## Behavior and UI notes
+
+- Inactive accounts are hidden by default and blocked from creating new transfers or transactions.
+- Transfers page (UI): `/transfers` — use this page to create and view transfer records between accounts.
+- Transaction editing rule: only the latest transaction on an account may change amount, date, or type; older transactions are immutable except for tagging/notes.
+
+Operator guidance above reflects Phase 2 lifecycle and is enforced by acceptance/domain rules and integration tests.
