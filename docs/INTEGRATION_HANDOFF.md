# Integration handoff — Stages 1–5

- Tarih: **2026-10-22**
- Worktree: `.dsh-worktrees/dsh-mvh4pdzy-u7y7j4`
- Branch: `dsh/dsh-mvh4pdzy-u7y7j4`
- HEAD: **`76ea501`** (account products/UI). Stages 1–5 are **uncommitted** in
  this worktree.
- Integration/local-development was already fast-forwarded by GPT to `76ea501`.
- UAT 5480/5481, UAT `.local`, 5280/5281, parent/main and GPT QA 5680/5681:
  **not touched**.
- Real SQL Server: **NOT RUN** (8 integration tests compiled/skipped). SQLite is
  not SQL Server proof.
- GitHub Actions: **NOT RUN**. No push/PR/tag/release/deploy was performed.
- Final push and CV publication are **GPT-controlled at project end**.

## Manifest (uncommitted)

### Stage 1 — SQL Server verification preparation

- `DigitalBanking.slnx`
- `DigitalBanking.IntegrationTests/` (`SqlServerFixture.cs`, `SqlServerIntegrationTests.cs`, `.csproj`)
- SQL fixes: `docs/SQLSERVER_FIX_REPORT.md`,
  `docs/sqlserver/2026-10-23-reconciliation-row-fk-noaction.sql`,
  `docs/evidence/2026-10-23-sqlserver-fk-fix.txt`,
  `docs/evidence/2026-10-23-sqlserver-currency-fix.txt`
- Currency repair migration: `20261023164046_RepairMissingAccountsCurrency`;
  original `20260628212612_AddCurrencyColumnToAccounts` now actually adds
  `Accounts.Currency` (legacy rows default TRY, documented, no FX).
- `scripts/run_sqlserver_integration.sh`
- `docs/PORTFOLIO_ROADMAP.md`, `docs/SQLSERVER_VERIFICATION.md`, `docs/STAGE_01_REPORT.md`
- `docs/evidence/2026-10-22-sqlserver-integration.txt`

### Stage 2 — GitHub Actions preparation

- `.github/workflows/ci.yml`, `.github/workflows/sqlserver-integration.yml`
- `scripts/ci_prepare_credentials.py`, `ci_run_smoke.sh`, `ci_summarize_trx.py`,
  `verify_sqlserver_trx.py`, `ci_check_workflow_guards.py`
- `docs/CI_GUIDE.md`, `docs/SECURITY_MATRIX.md`, `docs/STAGE_02_REPORT.md`,
  `docs/STAGE_02_TASK_NOTES.md`
- `docs/evidence/2026-10-22-ci-static-checks.txt`, `...-ci-wrapper-e2e.txt`

### Stage 3 — double-entry accounting

- Domain: `JournalEntry`, `JournalLine`, journal enums
- `DigitalBanking.Infrastructure/Services/JournalPoster.cs`, `AccountingService.cs`
- `DigitalBanking.API/Controllers/AccountingController.cs`
- `DigitalBanking.Dashboard/Pages/Admin/Accounting/`
- `DigitalBanking.Application/DTOs/Accounting/`, `IAccountingService.cs`,
  `AccountingLabels.cs`
- migration `20261022191101_AddDoubleEntryJournalAndApprovalAssignment`
- `scripts/upgrade_sqlite_journal.py`
- `DigitalBanking.Tests/AccountingJournalTests.cs`
- `DigitalBanking.Tests/AccountingBaselineRaceTests.cs`
- `DigitalBanking.Tests/DemoScenarioCleanupTests.cs`
- `scripts/stage3_stage4_e2e.py` (reproducible isolated HTTP E2E)
- `docs/STAGE_03_REPORT.md`, `docs/evidence/2026-10-22-stage3-accounting.txt`,
  `docs/evidence/2026-10-22-stage3-stage4-e2e.txt`,
  `docs/adr/0002-double-entry-journal.md`

### Stage 4 — operations control centre

- `DigitalBanking.Infrastructure/Services/OperationsService.cs`,
  `OperationsOptions.cs`, `SystemClock.cs`
- `DigitalBanking.Application/DTOs/Operations/`, `IOperationsService.cs`, `IClock.cs`
- `DigitalBanking.API/Controllers/OperationsController.cs`
- `DigitalBanking.Dashboard/Pages/Admin/Operations/`
- `PaymentApproval` assignment fields + `PaymentApprovalResponse`
- `BankDbContext` indexes and the combined additive migration (journal + assignment)
- `DigitalBanking.Tests/OperationsCenterTests.cs`
- `docs/STAGE_04_REPORT.md`, `docs/evidence/2026-10-22-stage4-operations.txt`

### Stage 5 — portfolio

- `README.md` (English + Turkish summary)
- `docs/ARCHITECTURE.md`, `docs/DEMO_SCRIPT.md`, `docs/CV_PROJECT_ENTRY.md`,
  `docs/FINAL_GAPS.md`, `docs/STAGE_05_REPORT.md`
- `docs/SECURITY_MATRIX.md` expanded
- `docs/screenshots/owner-accounting.png`, `owner-journal-detail.png`,
  `owner-operations.png`, `delegated-operations.png`, updated screenshot index
- `docs/evidence/2026-10-22-stage5-portfolio.txt`

### Shared changes

- `PaymentApprovalService` journal hooks
- `AccountingService` gap-based baseline + per-currency totals; `JournalPoster.AddBaseline` signed adjustment
- `DemoScenarioService` scoped demo-journal cleanup
- `BankDbContext` journal/assignment configuration + snapshot
- `InfrastructureServiceCollectionExtensions` DI
- `Program.cs`, `appsettings.json`
- `_Layout.cshtml` navigation; `DashboardUi.cs` labels
- `docs/PROGRESS.md`, `docs/TEST_REPORT.md` (historical note), `docs/PORTFOLIO_NOTES.md`,
  `docs/migrations/README.md`, `docs/INTEGRATION_HANDOFF.md`

## Current verification

```text
Build:                 0 warnings, 0 errors
Main xUnit:            178 passed, 0 failed
SQL integration:       0 passed, 8 skipped — NOT RUN
Reproducible E2E:      104/104 (scripts/stage3_stage4_e2e.py)
  Stage 3:             25/25
  Stage 4:             24/24
  Financial:           25/25
  Operations:          12/12
  CSV/demo:            9/9
  SQLite upgrade:      9/9
Isolated smoke:        280/280, exit 0 (temp DB, 5196/5197)
SQL Server DDL:        rendered, NOT applied (FK + Accounts.Currency fixed; runs 39984232994/39988214854 addressed)
SQL Server tests:      8 strengthened but still SKIPPED on this Mac — NOT RUN
GitHub Actions:        NOT RUN
Manual UAT:            NOT DONE
EULA:                  NOT ACCEPTED
Video:                 NOT RECORDED
```

## Exact test commands

Run from this worktree; all commands use disposable synthetic data.

```sh
# Build (harness flags; normal developer path may omit them)
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh build DigitalBanking.slnx \
  -p:UseSharedCompilation=false -m:1 -nr:false

# Unit + gated SQL integration (8 SQL tests skipped without env)
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh test DigitalBanking.slnx \
  -p:UseSharedCompilation=false -m:1 -nr:false --no-restore

# Reproducible Stage 3 + 4 HTTP E2E (builds, temp DB, ephemeral ports)
python3 scripts/stage3_stage4_e2e.py

# Isolated 280-check smoke (temporary DB + port pair).
# Run from a clean snapshot/checkout without .local; the wrapper intentionally
# refuses to overwrite an existing .local/credentials.json.
CI_API_PORT=5196 CI_DASHBOARD_PORT=5197 bash scripts/ci_run_smoke.sh

# SQL Server tests only when a supported disposable server + EULA exist
export DIGITALBANKING_SQLSERVER_CONNECTION='<disposable server connection>'
export DIGITALBANKING_SQLSERVER_ALLOW_DESTRUCTIVE=1
bash scripts/run_sqlserver_integration.sh

# Existing SQL schema only: repair an old SET NULL row FK (no data reset)
# docs/sqlserver/2026-10-23-reconciliation-row-fk-noaction.sql
```

The SQL Server fix for GitHub run 39984232994 changes
`FK_ReconciliationExceptions_ReconciliationRows_RowId` to NO ACTION in the
model, original migration, designers and snapshot. Fresh migrations no longer
create a second cascade path. The 8 SQL tests were strengthened (journal table/
index, assignment schema, balanced journal, rollback, cleanup ordering) but this
Mac still skips them; local skips are not PASS.

The E2E script refuses UAT-looking roots and reserved ports, never reads
`.local`, generates credentials in memory, prints only check names and counts,
and keeps temp logs only on failure. Evidence:
`docs/evidence/2026-10-22-stage3-stage4-e2e.txt`.

## Additive SQLite upgrade transfer order

Apply only to a **copy** of a synthetic Development database, never to UAT.

1. Stop the app/dashboard (no open writer).
2. Take a full backup (both scripts also create their own timestamped backup).
3. Dry-run the account metadata upgrade; apply only if it reports pending work:
   `python3 scripts/upgrade_sqlite_account_metadata.py <copy.db>`
   `python3 scripts/upgrade_sqlite_account_metadata.py <copy.db> --apply`
4. Dry-run the journal/assignment upgrade, then apply:
   `python3 scripts/upgrade_sqlite_journal.py <copy.db>`
   `python3 scripts/upgrade_sqlite_journal.py <copy.db> --apply`
   This adds `JournalEntries`/`JournalLines`, the unique `SourceKey` index,
   `PaymentApprovals.Assigned*` columns and the assignment index. It does not
   create baselines or modify balances.
   After the schema upgrade, the Owner baseline preview/post is separate and
   gap-based: it posts only `account balance - journal customer liability`, skips
   fully journaled accounts and never rewrites an existing baseline. Audit/UI
   totals are per currency; no mixed total is produced.
5. Verify row counts / balances before and after; run the upgrade script a second
   time and require `Already up to date` with no new backup.
6. Start the app on the upgraded copy, check health/login, then run the Owner
   baseline preview/post explicitly only if intended.
7. Re-run Stage 3/4 E2E and smoke on a fresh disposable DB. Do not copy the
   upgraded DB back into UAT; UAT transfer is GPT/user-owned.

Order matters only in that older databases need account metadata before the
journal/assignment script; both scripts are additive and idempotent.

## Commit/push boundary

This session did not create a commit: the harness blocked git index writes:

```text
fatal: Unable to create '.../.git/worktrees/dsh-mvh4pdzy-u7y7j4/index.lock':
Operation not permitted
```

A suggested review command (do not run blindly; check the final diff and keep the
unrelated worktree rules):

```sh
git status --short
git diff --check
# After GPT review, stage only the intended files and commit with a clean message.
```

No push, PR, tag, release, deployment or external CV edit was performed. GPT owns
the final integration/push and CV publication after independent verification.

## Demo instances used

- Main synthetic demo 5180/5181: restarted with the additive SQLite upgrade;
  health/CSS checked.
- Isolated Stage 3/4 demo 5198/5199 with `.local/stage3-e2e.db`: used for E2E and
  screenshots. This is not UAT and is disposable.
