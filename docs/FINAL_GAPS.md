> **Current 2026-10-23:** Hosted CI passed; real SQL Server passed 8/8 with no skips for `862c59c`. Manual user UAT remains pending. See [final verification](FINAL_VERIFICATION_20261023.md). Earlier NOT RUN/failure statements below are historical records.

# Final gaps — completed vs verified vs pending

Date: 2026-10-22. This file deliberately separates “implemented” from “verified in
this environment”. A written workflow or migration is not a run.

## A. Implemented in this worktree

- Stage 1: SQL Server integration harness, 8 env-gated tests, runner/doc.
- Stage 2: GitHub Actions workflows, smoke/TRX/secret-preparation scripts,
  workflow guardrails, CI guide and security matrix.
- SQL Server migration repairs after real runs 39984232994/39988214854:
  reconciliation row FK is NO ACTION; `Accounts.Currency` is added by the
  corrected original migration plus a conditional repair migration for existing
  histories; demo cleanup ordering fixed; 8 SQL tests strengthened. Legacy
  balances default to TRY explicitly (no FX). Concurrent approval journal
  uniqueness now maps only SQL 2601/2627 + journal SourceKey (and SQLite
  equivalent) to ConcurrencyException. See `SQLSERVER_FIX_REPORT.md`.
- Stage 3: append-only double-entry journal, atomic approval integration,
  gap-based Owner baseline (legacy balance minus already journaled liability;
  fully journaled/existing-baseline accounts skipped; per-currency totals),
  accounting API/UI, additive migration + SQLite upgrade, accounting
  tests/report/evidence.
- Stage 4: operations control centre (age/SLA, overdue, filters, bounded
  pagination, safe assignment, role-scoped visibility, reconciliation gaps),
  assignment schema, tests/report/evidence.
- Stage 5: English README + Turkish summary, Mermaid architecture/sequences,
  security matrix expansion, screenshots, demo script, CV entry, this gap file,
  Stage 5 report.
- Reproducible Stage 3/4 HTTP E2E runner:
  `scripts/stage3_stage4_e2e.py` (temporary DB/credentials, ephemeral loopback
  ports, UAT/reserved-port refusal, additive upgrade checks).

## B. Verified by an executed command in this environment

| Check | Result | Evidence |
|---|---|---|
| Solution build | 0 warnings / 0 errors | command output |
| Unit tests | **178 passed, 0 failed** | `docs/evidence/2026-10-22-stage3-accounting.txt`, `...stage4-operations.txt` |
| SQL Server integration tests | **8 skipped — NOT RUN** | test output |
| Stage 3 + 4 reproducible E2E | **104/104**: Stage 3 **25/25**, Stage 4 **24/24**, financial 25/25, operations 12/12, CSV/demo 9/9, SQLite upgrade 9/9 | `scripts/stage3_stage4_e2e.py`; `docs/evidence/2026-10-22-stage3-stage4-e2e.txt` |
| Earlier ad-hoc Stage 3/4 E2E | 25/25 and 24/24 on isolated 5198/5199 | Stage 3/4 evidence (historical run) |
| Baseline regression | legacy+deposit, new account, debit/negative direction, multi-currency, repeat, race | `AccountingJournalTests`, `AccountingBaselineRaceTests`; 178 unit tests |
| Isolated smoke | **280/280, exit 0**, temp DB, ports 5196/5197 | smoke summary run |
| SQL Server DDL render | succeeded; **not applied** | migration script output |
| SQLite additive upgrade | dry-run + apply + idempotent re-run on synthetic DB | Stage 3/4 evidence |
| Static workflow/syntax checks | guardrails PASS; Bash/Python syntax OK | command output |
| Screenshots | real synthetic captures, 2× DPR | `docs/screenshots/` |
| UAT DB untouched | no command targeted UAT paths | worktree boundary |

## C. Implemented but NOT verified here (environment)

### C1. Real SQL Server

- No supported SQL Server runtime on this macOS ARM64 host.
- Migration chain now renders with NO ACTION on
  `FK_ReconciliationExceptions_ReconciliationRows_RowId`; the prior fresh-create
  failure in real run 39984232994 is addressed in model, original migration,
  designers and snapshot. Run 39988214854 also exposed missing
  `Accounts.Currency`; the empty original migration and an additive repair
  migration now cover fresh and existing histories. It was **not applied** from
  this worktree.
- SQL Server decimal/concurrency/transaction behavior, migration ordering and
  rowversion semantics are **not proven**.
- 8 SQL Server tests remain **skipped**, not passed.
- See `docs/SQLSERVER_VERIFICATION.md`.

### C2. GitHub Actions / hosted CI

- `ci.yml` and `sqlserver-integration.yml` exist and pass static guard checks.
- They were **NOT RUN**: no push, PR, dispatch or hosted runner execution.
- No green badge, passing CI claim or artifact upload claim.

### C3. Manual UAT

- The separate `outputs/DigitalBanking-UAT` environment, its `.local`, and ports
  5480/5481 are user-owned and were not modified.
- `UAT_CHECKLIST.md` is not completed. No manual browser UAT is claimed.

### C4. Licensing / EULA

- SQL Server EULA acceptance is a human/licensing decision and was **not
  accepted** by this work.
- No paid/cloud service, real bank connection or production secret was used.

### C5. Video and visual/accessibility review

- The 3-minute demo script is prepared; **video NOT RECORDED**.
- Screenshots are real captures, not mockups. No automated visual-regression,
  screen-reader or full WCAG audit was performed.
- No penetration test, load test or formal threat-model sign-off.

## D. Pending user / GPT-controlled actions

### D1. Final push / PR / release

- Final GitHub push, PR, tag/release and CV publishing are **user-approved but
  GPT-controlled at project end**; they were **not done** in this stage.
- This worktree has uncommitted changes because the harness blocked git index
  writes (`index.lock: Operation not permitted`). A commit manifest is maintained
  in `docs/INTEGRATION_HANDOFF.md`; GPT must verify it before any push.
- No deployment, package publish or self-hosted runner registration occurred.

### D2. Secrets

- `.local/credentials.json`, JWT secret and demo passwords are local-only and
  git-ignored. They were never printed, committed or screenshotted.
- Real production secrets / secret manager / key rotation were not configured.
- Any future environment must supply secrets out-of-band; no secret belongs in
  the repo.

### D3. SQL Server verification run

To close C1:

1. Provide a supported x86-64 Linux SQL Server (or an operator-managed instance).
2. Accept the EULA explicitly.
3. Set `DIGITALBANKING_SQLSERVER_CONNECTION` and
   `DIGITALBANKING_SQLSERVER_ALLOW_DESTRUCTIVE=1` only for a disposable random
   database.
4. Run `scripts/run_sqlserver_integration.sh` and require **8/8 passed, 0
   skipped**.
5. Apply the migration to a synthetic copy and verify the journal/assignment
   schema manually. Use `scripts/stage3_stage4_e2e.py` for the reproducible
   local surface; it is not SQL Server proof.

### D4. Hosted CI run

Push/PR to GitHub, dispatch the workflow, and require both jobs to pass. The SQL
Server workflow remains manual/opt-in until a server exists.

### D5. UAT

Perform the real Owner/customer UAT on 5480/5481 using `UAT_CHECKLIST.md`; record
the result separately. Do not import UAT `.local` or data into this worktree.

## E. Deliberate non-claims

- Not production-ready, certified, audited or ASVS-compliant.
- No real bank/payment connectivity, real customer data, FX, fees, interest or
  regulatory general ledger.
- No SQL Server verification, GitHub Actions success, UAT completion or video.
- 8 skipped SQL tests are **not** 8 passed tests.
