# Stage 1 report — real SQL Server verification

- Date: **2026-10-22**
- Worktree: `.dsh-worktrees/dsh-mvh4pdzy-u7y7j4`
- Branch: `dsh/dsh-mvh4pdzy-u7y7j4`
- Starting HEAD: **`76ea501`**
- Status: **partially completed — real SQL Server environment blocked; preparation
  and environment-gated tests completed.**

## Problem

The repository had SQLite-only automated evidence. The real SQL Server migration
path, decimal/rowversion behavior, transaction rollback and concurrency were not
verified. A macOS ARM64 machine does not provide a supported SQL Server Linux
container runtime: Microsoft supports SQL Server Linux containers only on Linux
hosts with Intel/AMD x86-64 CPUs, and explicitly does not test/support Rosetta 2,
Prism or QEMU translation.

## Applied in this stage

1. Read `AGENTS.md`, the current migration/provider/test structure and existing
   SQL Server migration strategy.
2. Read-only environment inspection: `docker`, `podman`, `colima` and `sqlcmd`
   are not installed. No Docker Desktop/EULA/system service/cloud account was
   installed or created; no credentials were requested.
3. Added a separate `DigitalBanking.IntegrationTests` project with 8 SQL
   Server-specific tests. The project is in the solution and compiles; tests have
   an explicit environment gate and are skipped when no supported server is
   configured.
4. Added a safe fixture:
   - requires `DIGITALBANKING_SQLSERVER_CONNECTION` and
     `DIGITALBANKING_SQLSERVER_ALLOW_DESTRUCTIVE=1`;
   - creates only a random `DigitalBanking_IT_<hex>` database;
   - applies EF migrations, not `EnsureCreated`;
   - drops only that random database, never an existing/UAT DB;
   - never prints a connection string or password.
5. Added `scripts/run_sqlserver_integration.sh` with a clear `NOT RUN` exit when
   the environment is absent.
6. Recorded the environment boundary, test inventory, risks and actual skip
   evidence in `docs/SQLSERVER_VERIFICATION.md` and
   `docs/evidence/2026-10-22-sqlserver-integration.txt`.
7. Kept the existing account/UI work from the previous approved scope intact; no
   broad refactor or package update was made.

## Real test evidence

| Check | Result |
|---|---|
| Build (all projects) | **0 warnings, 0 errors** |
| Main xUnit project | **150 passed, 0 failed, 0 skipped** |
| Smoke on isolated synthetic SQLite | **280 checks passed** (prior stage evidence) |
| SQL Server integration project | **0 passed, 8 skipped, 0 failed — NOT RUN** |
| SQL Server server version/migration apply | **not available** |

The integration test names and decisions are in
[`SQLSERVER_VERIFICATION.md`](SQLSERVER_VERIFICATION.md). They cover migration
apply/defaults/index, decimal boundaries, rollback, separate-connection
concurrency, idempotency replay/conflict, self-approval/live revocation/IDOR and
reconciliation replay/exception. These are **compiled, not passed**.

## Remaining risk / blocker

- No supported SQL Server host or CI service is available. macOS ARM64 emulation
  is not an accepted substitute.
- Legacy role conversion and multiple-Owner preflight must be exercised on an
  actual upgrade copy, not only an empty database.
- SQLite evidence is not SQL Server proof.

## Next action

Stage 2 (GitHub Actions) is **not started**. Once a supported, disposable
x86-64 SQL Server is available, run:

```sh
export DIGITALBANKING_SQLSERVER_CONNECTION='<disposable server connection>'
export DIGITALBANKING_SQLSERVER_ALLOW_DESTRUCTIVE=1
bash scripts/run_sqlserver_integration.sh
```

Record server version, applied migrations and pass/fail/skip counts without
secrets, then mark only the exercised SQL Server checks as verified.

See [`PORTFOLIO_ROADMAP.md`](PORTFOLIO_ROADMAP.md).
