# Stage 2 task notes (working file)

- Date: 2026-10-22
- Branch/worktree: `dsh/dsh-mvh4pdzy-u7y7j4` / `.dsh-worktrees/dsh-mvh4pdzy-u7y7j4`
- Starting HEAD: `76ea501`; Stage 1 changes remain uncommitted and must be preserved.
- User-approved roadmap order is in `docs/PORTFOLIO_ROADMAP.md`.
- Stage 1: prepared / real SQL Server environment blocked; 8 integration tests
  compile and are skipped/not-run. This remains open; never call it SQL Server verified.
- Stage 2: prepared / NOT RUN on GitHub.

## Scope to implement now

1. Record progress/roadmap status before changes.
2. Safe GitHub Actions:
   - Job 1: build; main unit tests (150) and explicit SQL Server integration skip
     count separated; no green SQL Server claim.
   - Job 2: synthetic SQLite HTTP smoke: isolated temp DB, loopback ports,
     runtime-generated synthetic JWT/demo/Owner secrets, bounded health readiness,
     correct exit codes, kill only this job's PIDs, sanitized artifacts only.
   - Job 3: real SQL Server integration opt-in/service path on supported x86-64
     Linux; pinned Microsoft image; health wait; random preface DB; gate that a
     requested SQL Server run cannot pass if the 8 tests skip.
   - Minimum `contents: read`, timeout, concurrency, no secrets for fork PR,
     no `pull_request_target`, no write token, no cache credentials, no deploy.
3. Verify official GitHub Actions versions from current official documentation;
   avoid guessing.
4. Validate YAML syntax, shell/Python syntax, temp-config missing guards, build,
   150 tests + 8 SQL Server skips separately, 280 smoke locally.
5. Add `docs/CI_GUIDE.md`, `docs/STAGE_02_REPORT.md`, initial
   requirement→implementation→evidence security matrix.
6. Do not touch UAT 5480/5481, UAT `.local`, main/parent/GPT QA copies.
7. If commit remains index-permission blocked, report exact command/file list and
   leave files preserved for GPT.

## Latest verification

- CI smoke wrapper was run end-to-end in an isolated source snapshot without
  copying developer `.local`, on ports 5190/5191 with a temp DB: exit 0, 280
  checks, one summary artifact, ports free after cleanup.
- Occupied-port refusal and existing-`.local` refusal were tested before start.
- GitHub Actions and real SQL Server remain NOT RUN.

## Hard boundaries

- No push/PR/`workflow_dispatch`/publish/deploy.
- Workflow written ≠ workflow executed; report **NOT RUN** in GitHub Actions.
- Do not accept Docker/SQL Server EULA on the user's behalf; document the manual
  activation boundary.
- No cloud/paid host/account; no system service installation; no permission bypass.
- No secrets, connection strings, DB/cookies/tokens in artifacts.
