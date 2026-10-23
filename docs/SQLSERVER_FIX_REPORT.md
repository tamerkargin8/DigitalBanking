# SQL Server migration fixes — FK multiple cascade paths + missing Accounts.Currency

Date: 2026-10-23
Worktree base: `6b3a05b` (own worktree; main/UAT/GPT snapshots untouched)

Three real SQL Server 2022 runs exposed independent gaps:

| Run | Result | Root cause |
|---|---|---|
| 39984232994 | 0 passed / 8 failed, migration aborted | `FK_ReconciliationExceptions_ReconciliationRows_RowId` multiple cascade paths |
| 39988214854 | 1 passed / 7 failed / 0 skipped | one fixture username bug + `Invalid column name 'Currency'` |
| 39989641833 | 7 passed / 1 failed / 0 skipped | concurrent approval leaked `DbUpdateException` / SQL 2601 on `IX_JournalEntries_SourceKey` |

Both are addressed here. A real SQL Server rerun is still required; this Mac
cannot host SQL Server and local skips are **NOT RUN**, not passes.

## 1. Reconciliation FK multiple cascade paths (run 39984232994)

Trigger:

```text
Introducing FOREIGN KEY constraint
'FK_ReconciliationExceptions_ReconciliationRows_RowId' on table
'ReconciliationExceptions' may cause cycles or multiple cascade paths.
```

Relationships before the fix:

- `ReconciliationBatches -> ReconciliationRows` = CASCADE
- `ReconciliationBatches -> ReconciliationExceptions` = CASCADE
- `ReconciliationRows -> ReconciliationExceptions` = SET NULL

SQL Server rejects the third path. The fix is the smallest schema change:

- `ReconciliationExceptions.RowId -> ReconciliationRows.Id` is **NO ACTION**
- `BankDbContext.OnModelCreating` uses `DeleteBehavior.NoAction`
- the original migration `20261020212050_AddCustomerOwnershipTimelineReconciliation`
  was corrected (`ReferentialAction.NoAction`), not merely patched by a later
  migration
- all migration designers and `BankDbContextModelSnapshot` are aligned
- `Batch -> Rows` and `Batch -> Exceptions` stay CASCADE

Rendered fresh SQL Server DDL now contains:

```sql
CONSTRAINT [FK_ReconciliationExceptions_ReconciliationRows_RowId]
  FOREIGN KEY ([RowId]) REFERENCES [ReconciliationRows] ([Id])
```

with default NO ACTION; no `ON DELETE SET NULL`.

### Demo cleanup ordering

Because the row link is NO ACTION, dependents must be removed first.
`DemoScenarioService.ResetAsync` explicitly removes:

1. demo reconciliation exceptions
2. demo reconciliation rows
3. demo reconciliation batches

inside the existing reset transaction. Audit includes removed row/exception
counts. `DemoScenarioCleanupTests` covers the SQLite behavior; the SQL
integration reconciliation test covers the same order on SQL Server.

## 2. Missing `Accounts.Currency` (run 39988214854)

Trigger:

```text
Microsoft.Data.SqlClient.SqlException : Invalid column name 'Currency'.
```

`20260628212612_AddCurrencyColumnToAccounts` was an **empty migration** even
though its target model and the current model contain `Accounts.Currency`. The
initial `Accounts` DDL never created the column. Therefore fresh SQL Server
schema was inconsistent with the EF model:

```text
Accounts: Id, AccountNumber, Balance, CustomerId, CreatedDate, RowVersion
model:    ... + Currency (nvarchar(3), NOT NULL, default TRY)
```

`dotnet ef migrations has-pending-model-changes` reporting “no changes” only
means the model and snapshot agree; it does **not** prove the migration DDL
matches the model.

### Fix for a fresh migration chain

The original empty migration now adds the column:

```csharp
migrationBuilder.AddColumn<string>(
    name: "Currency", table: "Accounts", type: "nvarchar(3)",
    maxLength: 3, nullable: false, defaultValue: "TRY");
```

### Fix for an existing migration history

A conditional additive migration `20261023164046_RepairMissingAccountsCurrency`
adds the column only if it is absent:

```sql
IF COL_LENGTH(N'Accounts', N'Currency') IS NULL
    ALTER TABLE [Accounts]
        ADD [Currency] nvarchar(3) NOT NULL
        CONSTRAINT [DF_Accounts_Currency_LegacyRepair] DEFAULT N'TRY';
```

- No row, balance, approval or journal is deleted.
- No table is dropped or reset.
- Fresh databases get the column from the corrected original migration; the
  repair migration is a no-op.
- Databases that already recorded the old empty migration get the column from
  the repair migration.
- Both `Up` methods preserve existing rows. `Down` methods are intentionally
  no-op for this repair because dropping `Currency` would destroy operator data.

**Data assumption:** the old schema had no currency value. Existing account
balances are therefore defaulted to **TRY**. If an operator knows a legacy
account was USD/EUR/GBP, they must correct its `Currency` explicitly after a
backup; this project performs no FX conversion and makes no silent cross-currency
claim. This is documented here and in `docs/migrations/README.md`.

### Schema comparison

A model-versus-rendered-DDL column comparison was run over all 14 mapped tables.
All model columns are now present in the migration DDL; the only extra SQL
columns are the historical temporary approval-id columns. `Accounts.Currency`
is now present with the model’s `nvarchar(3)`, NOT NULL, default `TRY`.

## 3. Concurrent approval unique-journal conflict (run 39989641833)

Trigger:

```text
DbUpdateException
 ---> SqlException 2601:
 Cannot insert duplicate key row in object 'dbo.JournalEntries'
 with unique index 'IX_JournalEntries_SourceKey'. The duplicate key value is (approval:1).
```

Two approval attempts read `Pending` concurrently. One commits; the other reaches
the unique `SourceKey` guard. `PaymentApprovalService` rolled back correctly but
re-threw the raw `DbUpdateException`, so the test (correctly) did not classify it
as a controlled `ConcurrencyException`.

Fix in production `ApprovePaymentAsync` transaction:

- catch `DbUpdateException` only when the inner exception is:
  - SQL Server `SqlException` with number **2601 or 2627** and the message names
    `IX_JournalEntries_SourceKey` / `JournalEntries`, or
  - SQLite `SqliteException` error 19 with
    `UNIQUE constraint failed: JournalEntries.SourceKey`.
- rollback first, then throw `ConcurrencyException` (“başka bir incelemeci”).
- every other database exception is re-thrown unchanged; no generic swallowing.
- SQLite duplicate-SourceKey regression now asserts `ConcurrencyException` and
  the same balance/approval/journal/audit rollback.
- SQL concurrent-approval test rejects `DbUpdateException`, requires
  `BusinessRuleException` or `ConcurrencyException`, and checks exactly one
  journal, one approved audit row, one balance movement and Approved status.

## 4. Fixture username bug

`SqlServerFixture.AddActorAsync` used:

```csharp
Username = $"it-{role}-{Guid.NewGuid():N}"[..42]
```

`Owner` is shorter than 42 characters after the prefix and GUID, so the range
threw `ArgumentOutOfRangeException` before test setup. It now builds the string
first and truncates only when longer than 42:

```csharp
var username = $"it-{role}-{Guid.NewGuid():N}";
Username = username.Length <= 42 ? username : username[..42]
```

This is test-fixture code only; no production rule changed.

## 5. SQL integration suite (8 tests preserved)

`DigitalBanking.IntegrationTests/SqlServerIntegrationTests.cs` was strengthened
without increasing the 8-test count:

- migration test: journal/assignment migration and repair migration applied,
  `JournalEntries`/`JournalLines`, unique `SourceKey` index, assignment
  index/columns, `Accounts.Currency` non-null/default, reconciliation row FK
  `NO_ACTION`
- decimal test: every journal entry balanced, one currency per entry, 100,000.00
  transfer has two legs
- rollback test: duplicate journal `SourceKey` after balance/legacy writes;
  balance, approval, transaction and audit roll back
- concurrent approval test: exactly one journal for the payment
- reconciliation test: exception -> row -> batch cleanup on SQL Server

## 6. Verification in this environment

- Build: **0 warnings / 0 errors**
- Unit tests: **178 passed, 0 failed**
- SQL integration tests: **8 skipped — NOT RUN**; local skip is not PASS
- Stage 3/4 SQLite E2E: **104/104**
- Isolated smoke: **280/280**
- Stage 3/4 SQLite E2E includes demo reset and additive SQLite upgrade
- `dotnet ef migrations has-pending-model-changes`:
  **No changes have been made to the model since the last migration.**
- Rendered SQL Server script includes `ALTER TABLE [Accounts] ADD [Currency]`
  and the reconciliation FK without `ON DELETE SET NULL`
- Workflow guard and Python/Bash syntax checks: PASS
- `git diff --check`: clean

## 7. Existing installations / deployment boundary

- FK repair for a manually/legacy-created schema:
  `docs/sqlserver/2026-10-23-reconciliation-row-fk-noaction.sql`
  (conditional; no row deletion).
- Currency repair is the EF migration
  `20261023164046_RepairMissingAccountsCurrency`; run normal migrations.
- No SQL Server database was opened or changed here. GPT must review, commit/push,
  and rerun the EULA-approved SQL workflow; this report is not a SQL pass.
- No UAT/main/parent copy, no secret read/print, no git reset/clean/push/dispatch
  or EULA acceptance was performed here.

## 8. Files

- `DigitalBanking.Infrastructure/Persistence/BankDbContext.cs`
- `DigitalBanking.Infrastructure/Migrations/20261020212050_AddCustomerOwnershipTimelineReconciliation.cs`
- `DigitalBanking.Infrastructure/Migrations/20260628212612_AddCurrencyColumnToAccounts.cs`
- `DigitalBanking.Infrastructure/Migrations/20261023164046_RepairMissingAccountsCurrency.cs` (+ designer)
- `DigitalBanking.Infrastructure/Migrations/*.Designer.cs`, `BankDbContextModelSnapshot.cs`
- `DigitalBanking.Infrastructure/Services/DemoScenarioService.cs`
- `DigitalBanking.Infrastructure/Services/PaymentApprovalService.cs`
- `DigitalBanking.Tests/DemoScenarioCleanupTests.cs`
- `DigitalBanking.IntegrationTests/SqlServerFixture.cs`
- `DigitalBanking.IntegrationTests/SqlServerIntegrationTests.cs`
- `docs/sqlserver/2026-10-23-reconciliation-row-fk-noaction.sql`
- `docs/evidence/2026-10-23-sqlserver-fk-fix.txt`
- `docs/evidence/2026-10-23-sqlserver-currency-fix.txt`
- `docs/evidence/2026-10-23-sqlserver-concurrency-fix.txt`
- `docs/SQLSERVER_FIX_REPORT.md`
- `docs/SQLSERVER_VERIFICATION.md`, `docs/FINAL_GAPS.md`, `docs/PROGRESS.md`,
  `docs/INTEGRATION_HANDOFF.md`, `docs/migrations/README.md`
