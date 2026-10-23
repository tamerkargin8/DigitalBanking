# DigitalBanking portfolio roadmap — 22 October

This roadmap records the user-approved order. **Stage 1 is prepared but
environment-blocked (no supported SQL Server); Stage 2 CI preparation is
prepared but NOT RUN on GitHub. Stages 3–5 are implemented and documented; Stage 3/4 are verified on synthetic
SQLite, and Stage 5 produced the portfolio package. SQL Server, hosted CI, UAT
and the final push remain open.** Do not mark a stage complete without its own evidence.

0. **Manual UAT (user-owned).** The user performs the real Owner/customer UAT in
   the separate 5480/5481 environment. This file does not mark it complete.
1. **Real SQL Server integration verification — PREPARED / ENVIRONMENT BLOCKED.**
   Real migration apply on an empty, isolated SQL Server database; decimal/
   concurrency/transaction/idempotency/ownership/reconciliation checks;
   environment-gated runner and documentation. No supported runtime is available
   on this macOS ARM64 host; 8 tests compile and are skipped/not-run. SQLite is
   never called SQL Server proof. Real verification remains open.
2. **GitHub Actions build/test — PREPARED / NOT RUN.** CI workflow for restore,
   build, unit tests and isolated SQLite HTTP smoke; SQL Server integration job
   only on a supported x86-64 Linux service, explicitly manual/opt-in and without
   cloud/paid account. A written workflow is not a GitHub run; report NOT RUN
   until executed.
3. **Double-entry accounting / balanced journal — COMPLETED (SQLite/local
   verification).** Append-only balanced journal, atomic approval integration,
   Owner baseline, read-only accounting UI/API, additive migration and tests.
   Real SQL Server application remains open; see `docs/STAGE_03_REPORT.md`.
4. **Operations control centre — COMPLETED (SQLite/local verification).**
   Pending age/SLA, overdue approvals, unresolved reconciliation differences,
   safe assignment and role-scoped read-only drill-down. Real SQL Server
   application remains open; see `docs/STAGE_04_REPORT.md`.
5. **Portfolio presentation — COMPLETED (documentation).** English README +
   Turkish summary, Mermaid diagrams, real synthetic screenshots, 3-minute demo
   script (video **NOT RECORDED**), CV source and final gaps. No push/release.
6. **Horizontal security evidence — COMPLETED (selected controls).**
   Requirement → implementation → evidence matrix with selected ASVS themes;
   no certification or full-compliance claim.

## Stage 1 acceptance boundary

- No cloud/paid service, no real bank connection, no live data.
- Only loopback/local or explicitly project-test infrastructure.
- A missing SQL Server runtime is a documented blocker, not a passing result.
- Destructive integration tests may only use a randomly prefixed database created
  by that test run, with an allowlist guard; never an existing/UAT database.
- Existing test/smoke coverage must stay green; current gate is 178 xUnit + 280 smoke.

## Final publish and CV deliverable (user-approved, GPT-controlled)

- The user has explicitly approved the **final GitHub push and CV description**
  at the end of the project. This is not permission for an early push/deploy, and
  this stage does not push, publish, deploy or open a PR.
- The final push is controlled by GPT after independent verification, final CI/
  UAT evidence review and a clean commit manifest. A green local/SQLite run is
  not a substitute for the real SQL Server and GitHub Actions run.
- Final CV package: English README/30-second narrative, accurate CV bullets, real
  screenshots, architecture diagram, three-minute demo script, and a video only
  if actually recorded. If no video is recorded, label it "prepared, not recorded".
- No green build badge or CI claim until the workflow actually runs. UAT 5480/5481
  and UAT `.local` remain user-owned and are never used for CI/test data.
- SQL Server EULA is a separate human/licensing decision; approval of the
  portfolio/push does not accept the EULA.
