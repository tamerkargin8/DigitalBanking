# Stage 5 report — portfolio presentation

Date: 2026-10-22 (Europe/Istanbul)
Status: **documentation, diagrams, screenshots, demo script and CV source prepared; no push, release or video**

## 1. Deliverables

| Deliverable | Location | State |
|---|---|---|
| English README + Turkish summary | `README.md` | written |
| Architecture + Mermaid sequences | `docs/ARCHITECTURE.md`, `README.md` | written |
| Security evidence matrix (selected ASVS themes) | `docs/SECURITY_MATRIX.md` | expanded; not a certification |
| Reproducible Stage 3/4 HTTP E2E | `scripts/stage3_stage4_e2e.py` | 104/104; temp DB, no `.local`, UAT refused |
| Real synthetic screenshots | `docs/screenshots/` | 17 PNG captures; no secrets |
| 3-minute demo script | `docs/DEMO_SCRIPT.md` | prepared; **video NOT RECORDED** |
| CV bullets / pitches / Q&A | `docs/CV_PROJECT_ENTRY.md` | written; no external CV file edited |
| Completed vs verified vs pending gaps | `docs/FINAL_GAPS.md` | written |
| ADR links | `docs/adr/0001...`, `docs/adr/0002...` | present |
| Final push / release / CV publication | — | **NOT DONE**; GPT-controlled at project end |

## 2. README narrative

The README now follows problem → workflow → architecture → real evidence:

- **Problem:** maker-checker, scoped delegated limits, atomic accounting,
  operational backlog and truthful history are the hard parts, not CRUD.
- **Workflow:** every request is Pending; no money moves before approval; the
  Owner cannot self-approve; delegated limits are inclusive and live.
- **Accounting:** append-only balanced journal, one currency, 2dp positive legs,
  explicit Owner baseline, no silent backfill.
- **Operations:** SLA/age, overdue, filters, bounded pagination, safe assignment,
  delegation-scoped visibility, unresolved reconciliation differences.
- **Evidence:** 178 xUnit, 280 smoke, 104/104 accounting/operations E2E,
  real screenshots, and explicit non-claims.

The Turkish summary is near the top; the body is English.

## 3. Mermaid diagrams

- Component flow: browser → dashboard → API → services → EF → SQL Server/SQLite.
- Approval + journal sequence: Pending creation, live checks, one transaction,
  commit/rollback.
- Journal invariant flow: positive 2dp, one currency, debit = credit, unique
  `SourceKey`.
- Operations sequence: scoped summary and assignment without approval bypass.

Diagrams describe implemented behavior only. They do not imply a real bank,
general ledger, event bus or distributed transaction.

## 4. Screenshots

Real captures from synthetic local data, 1440px CSS width at 2× DPR:

| File | Content |
|---|---|
| `owner-accounting.png` | trial balance, journal list, gap-based Owner baseline panel |
| `owner-journal-detail.png` | balanced entry and two legs |
| `owner-operations.png` | SLA cards, overdue filter, assignment, reconciliation gaps |
| `delegated-operations.png` | delegated scoped operations view |
| existing Owner/approver/customer/account/receipt captures | role and account flows |

No password, token, `.local` content or UAT data is shown. No aesthetic or
visual-regression claim is made; page HTML was checked for expected content.

## 5. Demo script

`docs/DEMO_SCRIPT.md` covers the requested path:

new member → activation → first account → deposit → delegation 100,000.00
inclusive vs 100,000.01 over-limit → transfer → balanced journal → mismatch CSV →
operations gap → audit.

It includes a shorter path and a “claims to avoid” list. A 3-minute video was
**not recorded**; no video file or link is claimed.

## 6. CV package

`docs/CV_PROJECT_ENTRY.md` contains:

- one-line title;
- English and Turkish CV bullets;
- 30-second English and Turkish pitches;
- likely interview questions with honest answers;
- a “do not put on the CV” list.

AI-assisted implementation/review is disclosed. No external CV, LinkedIn or
portfolio file was modified, and final CV authority remains with the user/GPT.

## 7. Security evidence matrix

Expanded from CI-only controls to application controls:

- password hashing, JWT identity, function/object authorization, IDOR;
- maker-checker, scoped delegation, atomic posting, replay/idempotency;
- decimal/scale integrity, audit, masking, CSV handling, append-only journal,
  assignment safety, secrets/config.

The matrix labels selected ASVS themes and explicitly disclaims certification or
complete coverage. Real SQL Server execution remains a gap.

## 8. Verification for this stage

Stage 5 itself produced documentation and captures; the underlying code was
re-verified:

| Check | Result |
|---|---|
| Build | **0 warnings, 0 errors** |
| Full xUnit | **178 passed**, 8 SQL Server skipped |
| Reproducible Stage 3+4 E2E | **104/104** (Stage 3 25/25, Stage 4 24/24) |
| Isolated smoke | **280/280, exit 0** |
| SQL Server migration | DDL rendered; reconciliation FK + Accounts.Currency fixed after real runs 39984232994/39988214854; **not applied** |
| GitHub Actions | **NOT RUN** |
| Manual UAT / EULA / video | **NOT DONE / NOT ACCEPTED / NOT RECORDED** |

Evidence: `docs/evidence/2026-10-22-stage3-accounting.txt`,
`docs/evidence/2026-10-22-stage4-operations.txt`, plus command outputs captured in
this report and `docs/FINAL_GAPS.md`.

## 9. What is deliberately not claimed

- Production readiness, certification, ASVS/SOC2/PCI compliance or audit opinion.
- Real SQL Server apply/verification, hosted GitHub Actions success, manual UAT.
- Real bank/payment connectivity, real customer data, FX, fees or interest.
- A recorded video, a pushed commit/PR/release, or a modified external CV.
- Full accessibility, penetration test or load test.

## 10. Final publish boundary

The user approved the final GitHub push and CV description at project end. That
work is **GPT-controlled** after independent verification and a clean commit
manifest. This stage did not push, open a PR, tag a release, deploy or publish.
The worktree has uncommitted changes because the harness blocked git index writes;
see `docs/FINAL_GAPS.md` section D1.
