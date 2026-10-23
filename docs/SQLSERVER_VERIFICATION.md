# SQL Server verification — Stage 1 (2026-10-22)

## Status

**NOT VERIFIED on real SQL Server.** No supported SQL Server runtime is available
in this environment. The integration tests are written and compile, but were
**skipped** because the environment gate is not configured. Their skip is not a
pass and must not be reported as SQL Server evidence.

**2026-10-23 update:** a real SQL Server 2022 GitHub run (39984232994) started
successfully but all 8 tests failed in fixture migration because
`FK_ReconciliationExceptions_ReconciliationRows_RowId` created multiple cascade
paths. The model/original migration/designers/snapshot now use NO ACTION for that
FK, demo cleanup ordering was fixed, and the 8 SQL tests were strengthened. This
fix has **not** been applied to SQL Server here; rerun is required. See
[`SQLSERVER_FIX_REPORT.md`](SQLSERVER_FIX_REPORT.md).

A **second** real run (39988214854) passed migration but exposed a missing
`Accounts.Currency` column and a fixture username substring bug. The original
empty `AddCurrencyColumnToAccounts` migration was corrected and an additive
`RepairMissingAccountsCurrency` migration was added for existing histories.
Legacy balances default to TRY explicitly (no FX). The 8 SQL tests were
strengthened; local SQL remains **skipped/not run**.

- Worktree: `.dsh-worktrees/dsh-mvh4pdzy-u7y7j4`
- Branch: `dsh/dsh-mvh4pdzy-u7y7j4`
- Starting HEAD: **`76ea501`** plus uncommitted Stage 1 changes
- OS: macOS **arm64**, macOS 26.7.1
- Available runtime check: `docker`, `podman`, `colima`, `sqlcmd` **not installed**
- No Docker Desktop, system service, cloud account, paid service or real DB was
  installed/created. No UAT/local database was touched.

### Official platform constraint

Microsoft's SQL Server Linux container documentation states that container images
are supported only on Linux hosts running **Intel/AMD x86-64** CPUs. Emulation or
translation environments (Rosetta 2, Prism, QEMU) are **not tested or supported**.
Therefore SQL Server Linux containers on this macOS ARM64 machine would not be a
supported verification target even if an emulator were available.

Source: <https://learn.microsoft.com/en-us/sql/linux/containers/deploy?view=sql-server-ver17>

## What was added

- `DigitalBanking.IntegrationTests/` xUnit project, added to `DigitalBanking.slnx`.
- `SqlServerFact` / environment gate:
  - `DIGITALBANKING_SQLSERVER_CONNECTION` must point at a disposable SQL Server.
  - `DIGITALBANKING_SQLSERVER_ALLOW_DESTRUCTIVE=1` is required before the fixture
    can create/drop anything.
- Fixture creates only `DigitalBanking_IT_<32-lowercase-hex>` on the supplied
  server, applies **EF migrations** (not `EnsureCreated`), and drops only that
  randomly prefixed database. Existing databases are never selected/dropped.
- `scripts/run_sqlserver_integration.sh` runs the gated tests and prints a clear
  `NOT RUN` exit code when the environment is absent. It never prints a
  connection string/password.
- `docs/evidence/2026-10-22-sqlserver-integration.txt` records the actual environment
  and skip result.

## Integration test inventory (8, currently skipped)

| Test | Decision verified |
|---|---|
| `MigrationsApplyOnAnEmptyDatabaseAndAccountMetadataDefaultsExist` | Real `MigrateAsync` applies all five recent migrations; `LegacyPaymentApprovalIds`, Owner filtered unique index, `AccountName/Type` defaults, requested-account columns exist; legacy Admin/User values are not silently remapped; an account inserted with omitted metadata receives Vadesiz/Checking defaults |
| `DecimalBoundariesInclusiveLimitAndRepeatedCentMovementsAreExact` | 99,999.99, 100,000.00 inclusive, 100,000.01 Owner-only; exact decimal ledger after repeated 0.01 deposits |
| `ApprovalFailureRollsBackBalanceApprovalLedgerAndAudit` | Missing destination at approval leaves approval Pending, source balance unchanged, no ledger row and no approval audit |
| `ConcurrentApprovalsOnSeparateConnectionsMoveMoneyOnce` | two SQL connections/contexts and SQL Server `rowversion`: exactly one approval, one debit/credit pair |
| `IdempotencyFilterReplaysCompletedRequestAndRejectsDifferentPayload` | durable completed replay and different-payload 409 against SQL Server |
| `SelfApprovalRevocationAndStaleRoleAreRejectedByLiveState` | Owner self-approval; revoked delegation; stale JWT/role re-read from DB |
| `CustomerIdorAndOwnershipRenameRulesAreEnforced` | IDOR 404; owner-only rename; whitespace trim; blank/HTML rejected |
| `ReconciliationReplayAndDifferentReviewerExceptionFlowKeepBalancesUnchanged` | CSV import/replay; different reviewer closes exception; balance unchanged |

Migration list exercised by the first test: all migrations in
`DigitalBanking.Infrastructure/Migrations/`, ending with
`20261021023000_AddAccountMetadata`.

## Commands

```sh
# Build + unit tests without SQL Server
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh build DigitalBanking.slnx \
  -p:UseSharedCompilation=false -m:1 -nr:false
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh test DigitalBanking.slnx \
  -p:UseSharedCompilation=false -m:1 -nr:false --no-build

# Real SQL Server only (never against UAT or an existing/shared database)
export DIGITALBANKING_SQLSERVER_CONNECTION='<server connection with CREATE DATABASE>'
export DIGITALBANKING_SQLSERVER_ALLOW_DESTRUCTIVE=1
bash scripts/run_sqlserver_integration.sh
```

The value of `DIGITALBANKING_SQLSERVER_CONNECTION` must not be pasted into a
report, screenshot or log.

## Current evidence

- Build: **0 warnings / 0 errors**
- Main xUnit: **150 passed, 0 failed, 0 skipped**
- Smoke: **280 passed** on an isolated synthetic SQLite DB
- SQL Server integration: **0 passed, 8 skipped, 0 failed — NOT RUN**
- NuGet audit: unchanged; no package/version changes for Stage 1

## Remaining risks / next action

1. Need a supported x86-64 SQL Server host/CI service. Do not use Rosetta/QEMU as
   supported proof.
2. Run the gated tests against an empty disposable database and save server
   version, applied migration list and pass/fail/skip counts without secrets.
3. Verify legacy role/mapping operator procedure and >1 Owner preflight on an
   actual upgrade copy, not just an empty DB.
4. SQLite test/smoke results remain SQLite-only evidence; they do not imply SQL
   Server decimal, rowversion or transaction behavior.

## Relationship to the roadmap

This is Stage 1 only. Stage 2 (GitHub Actions), journal accounting, operations
control centre, English README/demo video and ASVS matrix are planned, not
implemented here. See [`PORTFOLIO_ROADMAP.md`](PORTFOLIO_ROADMAP.md).
