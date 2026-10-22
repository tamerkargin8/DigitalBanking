# Remaining development task — Stages 3–5 (user-approved, no artificial stop)

Date: 2026-10-22. User removed the stage stop gates. Implement Step 3, then 4, then 5,
each with its own report/evidence/tests. Preserve Stage 1/2 files.

## Stage 3 — double-entry accounting — COMPLETED 2026-10-22 (see docs/STAGE_03_REPORT.md)
- Analyse current `LedgerOperations`, Account/Transaction/PaymentApproval, CSV/
  reconciliation/demo/backfill/migrations before changing.
- Add balanced journal (JournalEntry/JournalLine) per currency, 2dp, positive legs,
  debit=credit, account/type/currency/transaction/correlation links.
- Bank viewpoint: customer deposits = liabilities; deposit = cash/clearing asset
  debit + customer liability credit; withdrawal reverse; transfer = source
  liability debit + target liability credit (same currency).
- No fees/FX/interest/real GL claims. Zero opening => no zero journal; nonzero
  opening => balanced opening-balance offset with sign.
- Approval writes balance/legacy ledger/journal/approval/timeline/audit atomically;
  journal failure rolls all back. Pending/rejected/cancelled produce no money journal.
- Idempotency/source operation unique prevents duplicate journal on replay/double
  approval; rowversion/limits/revocation/self-approval preserved.
- Posted records append-only; no delete/update endpoints. Reversal if needed = new
  balanced entry linked to original; otherwise explicitly pending.
- Read-only Owner/delegated accounting screen/API: currency journal/drilldown and
  trial balance equality; customer cannot see another account (IDOR tests).
- Reject unbalanced/cross-currency/missing account. Additive migration; do not
  falsely backfill old history. Detect/preview + documented explicit opening/
  baseline strategy; preserve balances/history. Do not apply UAT DB migration;
  prepare/test on synthetic copy. Demo reset only own seed journals.
- Tests: deposit/withdraw/transfer balanced per currency, 0.01, 100k, rollback,
  concurrent/replay, ownership, pending no balance, legacy baseline. Keep 150/280;
  update CI count gates; SQL tests compile but NOT RUN.
- `docs/STAGE_03_REPORT.md` + evidence.

## Stage 4 — operations control centre — COMPLETED 2026-10-22 (see docs/STAGE_04_REPORT.md)
- Pending age/UTC clock → Istanbul display; configurable demo SLA; overdue count/
  duration; unresolved reconciliation differences; filters type/currency/status/age;
  bounded pagination.
- Assignment only Owner/scoped approver; only active eligible target; assignment
  does not grant approval/limit bypass; audit+concurrency/idempotency.
- Delegated sees only permitted queue/summary; Owner general; customer 403.
- Drilldown to existing approval/exception screens; live self-approval/delegation.
- No FX summing across currencies. No auto-delete/fix on journal/SLA mismatch.
- Turkish empty/loading/error/mobile/focus/safe CSV. Deterministic clock tests:
  SLA before/at/after, closed excluded, assignment unauthorized/inactive/stale role,
  filters/pagination/IDOR. `docs/STAGE_04_REPORT.md` + real screenshots/evidence.

## Stage 5 — portfolio — COMPLETED 2026-10-22 (see docs/STAGE_05_REPORT.md)
- English README + Turkish summary, problem → workflow → architecture → real
  evidence, features/role limits, .NET10 SQL vs SQLite setup; only implemented
  features. Mermaid architecture + approval/journal sequences, ADR links, security
  requirement→implementation→real-test evidence matrix (selected ASVS; no
  certification claim).
- Real synthetic masked screenshots for new Owner/approver/customer/accounting/
  operations. No aesthetic visual claim if no tool.
- 3-minute demo: new member→activation→account→deposit→delegation 100000 inclusive/
  over→transfer→journal balanced→CSV difference→audit. Video NOT RECORDED if not.
- `docs/CV_PROJECT_ENTRY.md` TR/EN bullets + 30s pitch + Q&A; no invented claims;
  honest AI contribution. Don't claim a CV file was edited unless it was.
- `docs/FINAL_GAPS.md` separates completed vs verified and environmental/manual UAT/
  SQL Server/EULA/GitHub run/final push/release/secrets. `docs/STAGE_05_REPORT.md`.

## Hard boundaries
Only own worktree and synthetic 5180/5181 or explicit isolated ports. Do not touch
UAT `outputs/DigitalBanking-UAT`, `.local` there, 5480/5481, main `DigitalBanking`,
parent dirty copy, GPT QA copies. No secrets, reset/clean/force, package/system/
cloud installs, push/PR/deploy. Commit index block -> manifest and continue.
SQL 8 skip ≠ passed. EULA/GitHub hosted run/real SQL Server remain pending.

## Continuation state
Context is high; use this file + PROGRESS/roadmap. Stages 3–5 are complete. SQL Server, GitHub Actions, UAT, EULA and final push remain open.
