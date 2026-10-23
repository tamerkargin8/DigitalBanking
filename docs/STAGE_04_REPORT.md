# Stage 4 report — operations control centre

Date: 2026-10-22 (Europe/Istanbul)
Status: **implemented and verified on synthetic SQLite/local demo; SQL Server and GitHub Actions not run**

## 1. Scope delivered

Stage 4 adds an operational view over the existing approval and reconciliation
flows. It does not move money, auto-approve, auto-close, or rewrite journals.

Implemented:

- Pending-approval age in minutes and a configurable demo SLA (default 30 min).
- Overdue counts, oldest pending age, unassigned count and “assigned to me”.
- Filters for status, request type, currency, age/SLA and assignment; bounded
  pagination (max 50).
- Safe assignment of a pending approval to an active eligible approver.
- Delegated visibility limited to the live permitted queue; Owner sees all.
- Reconciliation differences: unresolved batch-level differences and open
  exception counts, with drill-down to existing batch/exception screens.
- Turkish dashboard (`/Admin/Operations`), loading-free server rendering, empty
  and error states, mobile-safe responsive tables, Istanbul date display.
- Deterministic-clock tests and a live synthetic API E2E.

Out of scope and not claimed: automatic escalation, notifications/email,
regulatory SLA reporting, cross-currency totals, auto-fixing reconciliation
differences, or changing journal/approval decisions.

## 2. SLA and time model

- Age is calculated from UTC (`IClock.UtcNow`) minus `PaymentApproval.CreatedAt`.
- A pending approval is **overdue when age >= SLA minutes**; exactly 30 minutes is
  overdue with the default 30-minute demo SLA.
- Configuration: `Operations:PendingSlaMinutes` in `appsettings.json`
  (default `30`). Values are clamped to 1–10,080 minutes at runtime.
- The dashboard shows Istanbul time for timestamps (`Europe/Istanbul`); SLA/age
  durations themselves are timezone-neutral.
- Closed statuses are excluded from pending/overdue counts. If an Owner filters
  to a closed status, rows are shown but never marked overdue.

This is a demo SLA, not a contractual or regulatory SLA.

## 3. Role-scoped visibility

| Role | Behavior |
|---|---|
| Owner | All pending/closed requests, summary, assignment, reconciliation differences |
| DelegatedApprover | Only pending requests allowed by the live delegation (operation, currency, per-request limit, daily limit, expiry, active flag); never their own request; only pending status |
| Customer | `403` on every operations endpoint |

Visibility is recalculated from the database on every request through
`IApprovalAuthorizationService`, so revocation/demotion/expiry applies to an open
screen or old token.

For a delegated approver without `ReconciliationException` permission, the
reconciliation section is hidden and counted as zero; calling the reconciliation
endpoint returns `403`.

## 4. Assignment rules

`POST /api/operations/approvals/{id}/assignment` with
`{ "assignedToUserId": "<guid>" }` or `null` to clear.

- Only pending requests can be assigned.
- Owner may assign to any active Owner/DelegatedApprover who can actually approve
  the request (operation/currency/limit checked live), excluding the requester.
- DelegatedApprover may assign only to themselves and may clear only their own
  assignment.
- Inactive, Customer, missing, or ineligible targets are rejected.
- Assigning never grants approval rights or bypasses limits. The normal
  `ApprovePaymentAsync` check still runs; a direct/stale assignment to an
  ineligible approver does not let them approve.
- Repeating the same assignment is idempotent (`changed=false`, no duplicate
  audit row). A real change writes an `APPROVAL_ASSIGNED` audit record.
- The approval `RowVersion` token catches concurrent approval updates; assignment
  maps a concurrency failure to the existing `ConcurrencyException`.
- The UI posts the assignment with an idempotency key.

## 5. Reconciliation differences

Batch-level unresolved differences are imported batches with at least one of:
only-external, only-system, amount mismatch, currency mismatch, duplicate
reference. A batch is included when it has no exceptions, or at least one
exception that is not `Closed`/`Rejected`.

- “Open” filter: unresolved exception or no exception.
- “No exception” filter: batches with no exception rows.
- “All” filter: all imported batches with differences.
- Closed/rejected-only batches are treated as resolved and excluded.
- No amount is summed across currencies.

Drill-down links to `/Admin/Reconciliation/Details` and the existing exception
screen. The operations layer never changes a batch, exception, journal or balance.

## 6. API

| Method | Path | Notes |
|---|---|---|
| GET | `/api/operations/summary` | Pending/overdue/unassigned/assigned-to-me, oldest age, reconciliation counts |
| GET | `/api/operations/approvals?page=&pageSize=&status=&requestType=&currency=&age=&assignment=` | `age=overdue|within`, `assignment=unassigned|me`; page size max 50 |
| GET | `/api/operations/reconciliation?page=&pageSize=&currency=&exceptionFilter=` | `exceptionFilter=open|none|all` |
| POST | `/api/operations/approvals/{id}/assignment` | Owner/scoped delegate; `[Idempotent]` filter enabled |

Customers receive `403`; unauthenticated requests remain `401` from the existing
authorization pipeline.

## 7. Dashboard

`/Admin/Operations` (Owner and DelegatedApprover navigation) contains:

- summary cards for pending, overdue, unassigned/assigned-to-me and reconciliation;
- approval table with age/SLA badge, risk, assignment, filters and pagination;
- per-row assignment form with eligible users (delegated sees self);
- unresolved reconciliation table with difference mix and links.

Screenshots:

- `docs/screenshots/owner-operations.png` (2880×3130, 2× DPR)
- `docs/screenshots/delegated-operations.png` (2880×2826, 2× DPR)

Page HTML was checked for the summary, SLA, reconciliation and assignment text.
No aesthetic quality claim beyond the real captures is made.

## 8. Schema

`PaymentApprovals` gained nullable `AssignedToUserId`, `AssignedToUsername`,
`AssignedAt` and an `(AssignedToUserId, Status)` index. The already additive
combined migration
`20261022191101_AddDoubleEntryJournalAndApprovalAssignment` contains the journal
tables and these assignment columns.

SQLite Development existing databases are upgraded with
`scripts/upgrade_sqlite_journal.py` (dry-run, timestamped backup, UAT path
refusal). It now adds both journal tables and the assignment columns/index.

**No SQL Server database was migrated.** The SQL Server DDL was rendered and
inspected only.

## 9. Verification

| Check | Result |
|---|---|
| Solution build | **0 warnings, 0 errors** |
| Full xUnit | **178 passed, 0 failed**, 8 SQL Server integration tests **skipped** |
| Operations test class | 8 tests, all passing |
| Live API E2E (isolated SQLite, 5198/5199) | **24/24 checks passed** |
| Isolated HTTP smoke (5196/5197) | **280/280, exit 0** |
| SQL Server migration script render | succeeded; assignment columns + journal tables, not applied |
| Workflow/static checks | guardrails PASS; bash/python syntax OK |

`DigitalBanking.Tests/OperationsCenterTests.cs` covers:

1. SLA before/at/after 30 minutes, closed exclusion.
2. Delegated sees only live-permitted rows and never own requests.
3. Assignment authorization: customer, other assignee, inactive, ineligible,
   requester target.
4. Assignment idempotency and audit.
5. Assignment does not bypass the approval limit.
6. Stale role/disabled user rejected by live DB state.
7. Combined filters, bounded page size, pagination.
8. Reconciliation unresolved/resolved batches and delegated permission.

Live E2E additionally exercised real login, pending request creation, a
backdated row, Owner assignment, delegated scoping, invalid targets, CSV
preview/import with a mismatch, and combined filters.

Reproducible runner: `python3 scripts/stage3_stage4_e2e.py` — Stage 4 **24/24**
plus operations 12/12, CSV/demo 9/9 and upgrade 9/9; total **104/104**.

Evidence: [`evidence/2026-10-22-stage4-operations.txt`](evidence/2026-10-22-stage4-operations.txt),
[`evidence/2026-10-22-stage3-stage4-e2e.txt`](evidence/2026-10-22-stage3-stage4-e2e.txt).

## 10. Limitations / open boundaries

- **SQL Server still not verified**; the 8 env-gated tests are skipped. A rendered
  DDL script is not an applied-database proof.
- **GitHub Actions not run**. The main-test gate is now 178; smoke is 280.
- SLA is a configurable demo value, not a legal/regulatory SLA.
- The operations page uses server-rendered tables, not live polling; a manual
  refresh picks up new data.
- Reconciliation visibility is batch-level; per-row exception creation remains
  the existing manual flow.
- No notifications, escalation, or auto-assignment.
- Demo reset does not clear operational assignment/audit history (assignments are
  harmless metadata and audit is append-only).

## 11. Key files

- `DigitalBanking.Infrastructure/Services/OperationsService.cs`
- `DigitalBanking.Application/Abstractions/Services/IOperationsService.cs`, `IClock.cs`
- `DigitalBanking.Application/DTOs/Operations/OperationsDtos.cs`
- `DigitalBanking.API/Controllers/OperationsController.cs`
- `DigitalBanking.Dashboard/Pages/Admin/Operations/`
- `DigitalBanking.Infrastructure/Persistence/BankDbContext.cs`
- `DigitalBanking.Infrastructure/Migrations/20261022191101_AddDoubleEntryJournalAndApprovalAssignment.cs`
- `DigitalBanking.Tests/OperationsCenterTests.cs`
- `scripts/upgrade_sqlite_journal.py`
- `docs/STAGE_04_REPORT.md`
