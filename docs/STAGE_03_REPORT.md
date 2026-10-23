# Stage 3 report — double-entry accounting

Date: 2026-10-22 (Europe/Istanbul)
Status: **implemented and verified on synthetic SQLite/local demo; SQL Server not run**

## 1. Scope delivered

Stage 3 adds an append-only double-entry journal next to the existing
customer-facing `Transaction` ledger. The legacy balance/statement path is kept,
so existing customers and screens continue to work, while every approved money
movement now also produces a balanced journal entry.

Implemented:

- `JournalEntry` / `JournalLine` domain model and EF configuration.
- Balanced journal factory (`JournalPoster`) with strict invariants.
- Atomic integration into `PaymentApprovalService.ApprovePaymentAsync`.
- Read-only accounting API for trial balance, journal list and drilldown.
- Turkish accounting dashboard (`/Admin/Accounting`) with filters, bounded
  pagination and entry detail.
- Explicit Owner-only, gap-based baseline preview/post for pre-journal accounts
  (already journaled balances are skipped; no duplicate liability).
- Demo-reset cleanup scoped to demo-owned journals.
- Additive SQL Server migration and additive SQLite upgrade script.
- Unit/integration-style SQLite tests and a 25-check live API E2E.

Not in scope, and not claimed: fees, FX conversion, interest, real general-ledger
integration, automatic correction/deletion, regulatory reporting, or automatic
backfill of historical accounts.

## 2. Accounting model

One currency per entry; every leg positive and 2dp; debit total must equal credit
total. Customer deposits are bank liabilities.

| Operation | Debit | Credit |
|---|---|---|
| Deposit | `CashClearing` | `CustomerLiability` (account) |
| Withdrawal | `CustomerLiability` (account) | `CashClearing` |
| Same-currency transfer | source `CustomerLiability` | target `CustomerLiability` |
| Nonzero account opening | `OpeningBalanceOffset` | `CustomerLiability` (account) |
| Baseline gap > 0 | `OpeningBalanceOffset` | `CustomerLiability` (account) |
| Baseline gap < 0 | `CustomerLiability` (account) | `OpeningBalanceOffset` |

Zero opening produces **no** journal. Cross-currency transfers, negative/zero
legs, unbalanced entries, over-precision values and customer-liability legs
without an account are rejected before persistence.

`JournalEntry.SourceKey` is unique:

- `approval:<approvalId>` — deposits, withdrawals, transfers, account openings
- `baseline:<accountId>` — Owner baseline
- `reversal:<journalEntryId>` — reserved for a future explicit reversal

This makes replay/double approval fail at the database boundary, in addition to
the existing approval-status check.

## 3. Atomicity and rollback

`PaymentApprovalService.ApprovePaymentAsync` still owns one database transaction.
Inside it, the existing balance mutation and `Transaction` row are now joined by
the journal entry, approval state, timeline events and audit record. If the
journal insert fails, the entire transaction rolls back.

Verified by a forced duplicate-`SourceKey` failure: balance, legacy transaction,
approval status and audit timeline all remained unchanged.

Pending, rejected and cancelled requests create no journal and do not move money.

## 4. Baseline strategy (no silent backfill, no duplicate liability)

Accounts created before this feature have balances but no journal. Startup and
migration **do not** fabricate history. The trial balance exposes the difference
between journal customer liabilities and current account balances.

The baseline is a **one-time gap adjustment**, not “post the full current
balance”:

```
journalLiability = sum(CustomerLiability credit - debit) for the account
gap              = account.Balance - journalLiability
```

- `gap > 0`: debit `OpeningBalanceOffset` / credit `CustomerLiability`.
- `gap < 0`: debit `CustomerLiability` / credit `OpeningBalanceOffset`.
- `gap == 0`: fully journaled; skip. This includes new accounts opened with a
  funded journal and accounts whose deposits/withdrawals are already journaled.
- An account with an existing `baseline:<accountId>` is skipped forever; an old
  baseline is never rewritten.
- A legacy account that receives a post-migration deposit is eligible only for
  the remaining legacy gap. The test suite covers exactly this regression.
- The snapshot reads account balance and net journal liability together; posting
  uses a serializable transaction and the unique source key to prevent duplicate
  baselines under concurrent approval/attempts.

Owner-only flow:

1. `GET /api/accounting/baseline/preview` returns per-account balance, journal
   liability, signed gap, direction and eligibility.
2. `POST /api/accounting/baseline/post` creates a balanced adjustment only for
   eligible accounts. Account balances are never modified.
3. Re-running preview/post is idempotent per account.

Totals are **per currency** (`CurrencyTotals` with increase/decrease/net). There
is no mixed-currency grand total and no FX conversion. The audit record contains
the same per-currency breakdown.

## 5. API and dashboard

API (JWT; `Owner` or `DelegatedApprover`):

| Method | Path | Notes |
|---|---|---|
| GET | `/api/accounting/trial-balance?currency=TRY` | Per-currency debit/credit totals; balanced flag; journal-vs-account liability difference |
| GET | `/api/accounting/journal-entries?page=&pageSize=&currency=&accountId=&sourceOperation=` | Bounded page size (max 100) |
| GET | `/api/accounting/journal-entries/{id}` | Entry + debit/credit legs + account/transaction links |
| GET | `/api/accounting/baseline/preview` | Owner only |
| POST | `/api/accounting/baseline/post` | Owner only, idempotent |

There is **no PUT/PATCH/DELETE** accounting endpoint. The test suite asserts this
surface with reflection and checks a real `PUT` returns `405`.

Dashboard:

- `/Admin/Accounting` — trial balance per currency, journal list, filters, page
  navigation, baseline panel for Owner.
- `/Admin/Accounting/Details?id=...` — entry header and legs.
- Navigation links added for Owner and DelegatedApprover; Customer sees no link
  and the API returns `403`.

## 6. Migrations and upgrade path

- SQL Server: `DigitalBanking.Infrastructure/Migrations/20261022191101_AddDoubleEntryJournalAndApprovalAssignment.cs`
  and its designer/snapshot. Additive: creates `JournalEntries`, `JournalLines`,
  unique `SourceKey` index and lookup indexes. Generated SQL Server script was
  inspected (`CREATE TABLE` ×2, unique index present); **it was not applied to a
  real SQL Server**.
- SQLite Development: existing databases are upgraded additively with
  `scripts/upgrade_sqlite_journal.py` (dry-run by default, timestamped backup,
  refuses UAT-looking paths). Fresh databases get the tables through
  `EnsureCreated`.
- No UAT database was touched.

## 7. Verification

All commands were run in this worktree with the pinned .NET 10.0.402 SDK.

| Check | Result |
|---|---|
| `dotnet build DigitalBanking.slnx` | **0 warnings, 0 errors** |
| `dotnet test DigitalBanking.slnx` | **178 passed, 0 failed**, 8 SQL Server integration tests **skipped** |
| Accounting test class | 18 tests, all passing |
| Baseline/approval race test | 1 test, passing (file SQLite, serializable baseline) |
| Demo cleanup ordering test | 1 test, passing (exceptions -> rows -> batch) |
| Live API E2E (isolated SQLite, ports 5198/5199) | **25/25 checks passed** |
| Isolated HTTP smoke (ports 5196/5197, temp DB) | **280/280 checks passed**, exit 0, ports released |
| SQL Server migration script generation | succeeded (DDL rendered, not applied) |
| Workflow guard/static checks | `PASS: workflow guardrails`; bash/python syntax OK |

Reproducible runner: `python3 scripts/stage3_stage4_e2e.py` (temporary DB,
ephemeral loopback ports, no `.local`, UAT/reserved-port refusal). Its Stage 3
section reports **25/25** and additionally covers financial rollback, replay,
decimal/currency and baseline access.

Evidence: [`evidence/2026-10-22-stage3-accounting.txt`](evidence/2026-10-22-stage3-accounting.txt),
[`evidence/2026-10-22-stage3-stage4-e2e.txt`](evidence/2026-10-22-stage3-stage4-e2e.txt).

### New accounting tests

`DigitalBanking.Tests/AccountingJournalTests.cs` covers:

1. Approved deposit posts two balanced legs and moves balance once.
2. Withdrawal + 0.01 + 100,000-scale transfer remain balanced; transfer has two
   customer-liability legs and reconciles.
3. Pending request creates no journal and no balance change.
4. Nonzero account opening uses opening offset; zero opening creates none.
5. Journal failure rolls back balance, legacy row, approval and audit.
6. Baseline preview/post is explicit, idempotent and never changes balances.
7. Customer is denied accounting reads; delegated may read but not baseline.
8. Listing is bounded/filterable; missing entry returns 404.
9. API surface has no update/delete of posted entries.
10. Replay approval is rejected without duplicate journal.
11. Unbalanced, cross-currency, zero and over-precision legs are rejected.
12. Database unique index rejects duplicate `SourceKey`.
13. Legacy balance + post-migration deposit: baseline covers only the legacy gap.
14. New account + deposit: fully journaled, baseline creates nothing.
15. Newly opened funded account + deposit: no baseline duplicate.
16. Baseline gap can exceed the current balance after a post-migration debit.
17. Negative gap: debit customer liability / credit opening offset.
18. Per-currency preview/post totals and audit; no mixed-currency total.
19. Baseline/approval race with separate contexts: one gap only, final liability
    equals the account balance.

## 8. Screenshots

Real synthetic captures from the isolated demo (1440px width, 2× DPR):

- `docs/screenshots/owner-accounting.png`
- `docs/screenshots/owner-journal-detail.png`

The page HTML was also checked for the trial-balance, “Dengeli” and
“Hesap bakiyeleri journal ile uyumlu” content. No visual-design quality claim is
made beyond these real captures.

## 9. Limitations and open boundaries

- **SQL Server is still not verified.** The 8 env-gated SQL Server tests remain
  skipped; a migration compiling/rendering SQL is not an applied-database proof.
- GitHub Actions is **NOT RUN**. The CI unit-test gate was updated from 150 to
  162 at Stage 3 and later to 178 at Stage 4; the smoke gate remains 280.
- The accounting UI is operational/read-only and Turkish; it is not a full
  finance back-office.
- There is no automatic reversal endpoint yet. A reversal entry type and source
  key convention are prepared, but no UI/API exposes it in this stage.
- Demo reset removes demo journals only. Baseline entries for preserved accounts
  intentionally remain.
- The local 5180/5181 database was upgraded additively, but this document does not
  claim its historical synthetic data is a clean baseline.

## 10. Key files

- `DigitalBanking.Domain/Entities/JournalEntry.cs`, `JournalLine.cs`
- `DigitalBanking.Domain/Enums/JournalSourceOperation.cs`, `JournalAccountType.cs`, `JournalSide.cs`
- `DigitalBanking.Infrastructure/Services/JournalPoster.cs`
- `DigitalBanking.Infrastructure/Services/AccountingService.cs`
- `DigitalBanking.Infrastructure/Services/PaymentApprovalService.cs`
- `DigitalBanking.Infrastructure/Persistence/BankDbContext.cs`
- `DigitalBanking.Infrastructure/Migrations/20261022191101_AddDoubleEntryJournalAndApprovalAssignment.cs`
- `DigitalBanking.API/Controllers/AccountingController.cs`
- `DigitalBanking.Dashboard/Pages/Admin/Accounting/`
- `DigitalBanking.Tests/AccountingJournalTests.cs`
- `scripts/upgrade_sqlite_journal.py`
- `docs/adr/0002-double-entry-journal.md`
