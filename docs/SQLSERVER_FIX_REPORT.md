# SQL Server migration fix — reconciliation FK multiple cascade paths

Date: 2026-10-23
Trigger: GitHub Actions run **39984232994** (real SQL Server 2022), job
`Real SQL Server integration (8/8 required)`.

## Finding

All 8 SQL Server tests failed in fixture migration with:

```text
Microsoft.Data.SqlClient.SqlException:
Introducing FOREIGN KEY constraint
'FK_ReconciliationExceptions_ReconciliationRows_RowId' on table
'ReconciliationExceptions' may cause cycles or multiple cascade paths.
Specify ON DELETE NO ACTION or ON UPDATE NO ACTION, or modify other FOREIGN KEY
constraints.
```

The failure is at `SqlServerDatabaseFixture.InitializeAsync` before any test
body, so it is a schema/migration failure, not a test assertion failure. The
safe masked log was read read-only from
`/tmp/digitalbanking-sql39984232994-failed.log`; no credentials/tokens were
printed or transmitted.

## Root cause

The reconciliation model had three relevant relationships:

- `ReconciliationBatches -> ReconciliationRows` — CASCADE
- `ReconciliationBatches -> ReconciliationExceptions` — CASCADE
- `ReconciliationRows -> ReconciliationExceptions` — SET NULL

SQL Server rejects the third path because deleting a batch can reach
`ReconciliationExceptions` directly and via `ReconciliationRows`, producing
multiple cascade paths / a cycle. SQLite tolerates this, so local SQLite runs
never exposed it.

## Fix (smallest schema change)

`ReconciliationExceptions.RowId -> ReconciliationRows.Id` is now **NO ACTION**:

- `BankDbContext.OnModelCreating`: `DeleteBehavior.NoAction`
- original migration `20261020212050_AddCustomerOwnershipTimelineReconciliation`:
  `onDelete: ReferentialAction.NoAction` for that FK
- all migration designers + `BankDbContextModelSnapshot` updated
- fresh SQL Server DDL now renders the FK without `ON DELETE SET NULL`

The two batch relationships remain CASCADE. The row link is nullable and is not
used as a deletion path by the application; reconciliation rows/batches are
immutable after import.

## Existing installations

A fresh SQL Server could not have created this FK with SET NULL, because SQL
Server rejects it. If an operator has a manually/legacy-created schema where the
constraint exists with `SET_NULL`, run the idempotent manual script:

`docs/sqlserver/2026-10-23-reconciliation-row-fk-noaction.sql`

It drops/recreates only that constraint (no row deletion, no table drop, no
reset) and is a no-op on a corrected schema. No EF follow-up migration is needed
for the normal fresh chain; this is documented rather than silently changing a
database that cannot have been created by the failed migration.

## Demo cleanup ordering

Because row -> exceptions is NO ACTION, deletion must remove dependents first.
`DemoScenarioService.ResetAsync` now explicitly removes:

1. demo reconciliation exceptions,
2. demo reconciliation rows,
3. demo reconciliation batches,

inside the existing reset transaction, before other demo data. Audit details
include removed row/exception counts. The SQL integration reconciliation test
also deletes exceptions -> rows -> batch to verify the order on real SQL Server.

## Integration suite changes (still 8 tests)

`DigitalBanking.IntegrationTests/SqlServerIntegrationTests.cs` was strengthened
without increasing the test count:

- migration test now asserts the journal/assignment migration, tables,
  unique `SourceKey` index, assignment index/columns, and
  `FK_ReconciliationExceptions_ReconciliationRows_RowId` delete action =
  `NO_ACTION`;
- decimal test asserts every journal entry is balanced, one currency per entry,
  and the 100,000.00 transfer has two legs;
- rollback test now forces a duplicate journal `SourceKey` after balance/legacy
  writes and asserts balance, approval, transaction and audit rollback;
- concurrent approval test asserts exactly one journal for the payment;
- reconciliation test verifies exceptions -> rows -> batch cleanup on SQL Server.

## Verification in this environment

- Build: **0 warnings / 0 errors**
- Unit tests: **178 passed, 0 failed**
- SQL integration tests: **8 skipped — NOT RUN** (no SQL runtime on this Mac);
  local skip is **not** a pass.
- SQLite Stage 3/4 E2E: **104/104** (includes demo reset and additive upgrade)
- Isolated HTTP smoke: **280/280**, exit 0
- SQL Server DDL rendered and inspected: `FK ... REFERENCES ... ([Id])` with no
  `ON DELETE SET NULL`; no SQL Server apply was performed.
- `dotnet ef migrations has-pending-model-changes`:
  **No changes have been made to the model since the last migration.**
- `docs/sqlserver/2026-10-23-reconciliation-row-fk-noaction.sql` is read-only
  documentation until an operator applies it to a compatible database.

## Residual risk / boundary

- A real SQL Server apply of the fixed migration is still required; the GitHub
  run must be repeated by GPT after review.
- The foreign key is NO ACTION, so any future code that deletes reconciliation
  rows must delete/clear exceptions first. Current application code only deletes
  them together in demo cleanup, and rows/batches are immutable otherwise.
- No customer data, balance, approval, limit or journal semantics changed.
- No UAT database, parent/main copy or GPT snapshot was modified. No push,
  deployment, GitHub dispatch or EULA acceptance was performed here.

## Files

- `DigitalBanking.Infrastructure/Persistence/BankDbContext.cs`
- `DigitalBanking.Infrastructure/Migrations/20261020212050_AddCustomerOwnershipTimelineReconciliation.cs`
- `DigitalBanking.Infrastructure/Migrations/*.Designer.cs`, `BankDbContextModelSnapshot.cs`
- `DigitalBanking.Infrastructure/Services/DemoScenarioService.cs`
- `DigitalBanking.Tests/DemoScenarioCleanupTests.cs`
- `DigitalBanking.IntegrationTests/SqlServerIntegrationTests.cs`
- `docs/sqlserver/2026-10-23-reconciliation-row-fk-noaction.sql`
- `docs/evidence/2026-10-23-sqlserver-fk-fix.txt`
- `docs/SQLSERVER_FIX_REPORT.md`
- `docs/SQLSERVER_VERIFICATION.md`, `docs/FINAL_GAPS.md`, `docs/PROGRESS.md`,
  `docs/INTEGRATION_HANDOFF.md` (updated status/manifest)
