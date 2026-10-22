# CI guide — Stage 2 preparation

## What exists

- `.github/workflows/ci.yml` — push/pull-request CI.
  - `build-test`: restore/build, main **177-test** gate, then the integration
    project with no connection env. The 8 SQL Server tests are expected to skip
    and are reported separately; this job never claims SQL Server verification.
  - `smoke`: isolated SQLite HTTP smoke on loopback 5180/5181 using a temporary
    database and runtime-generated synthetic credentials; requires the existing
    **280** checks to pass.
- `.github/workflows/sqlserver-integration.yml` — **manual-only** SQL Server
  integration workflow. It is never triggered by push or pull request.
- `scripts/ci_run_smoke.sh` — bounded health wait, temp DB, own-PID cleanup.
  - `CI_API_PORT` / `CI_DASHBOARD_PORT` select distinct unprivileged loopback
    ports (1024–65535); defaults remain 5180/5181.
  - It refuses before creating credentials if either port is already occupied;
    readiness therefore cannot attach to another project. It never kills by
    process name, only the PID it started.
- `scripts/ci_prepare_credentials.py` — ephemeral random demo/Owner/JWT values;
  never prints them.
- `scripts/ci_summarize_trx.py` — secret-free count summary for main/integration
  skip results.
- `scripts/verify_sqlserver_trx.py` — hard gate: 8 tests must have run and passed;
  skipped tests fail the job.
- `scripts/run_sqlserver_integration.sh` — local/CI gate for a supplied SQL Server.

## Pinned actions (verified official tags, 2026-10-22)

Official release pages: [checkout v7.0.1](https://github.com/actions/checkout/releases/tag/v7.0.1),
[setup-dotnet v6.0.0](https://github.com/actions/setup-dotnet/releases/tag/v6.0.0),
[upload-artifact v7.0.2](https://github.com/actions/upload-artifact/releases/tag/v7.0.2).

| Action | Version | Commit SHA |
|---|---|---|
| `actions/checkout` | `v7.0.1` | `3d3c42e5aac5ba805825da76410c181273ba90b1` |
| `actions/setup-dotnet` | `v6.0.0` | `a98b56852c35b8e3190ac28c8c2271da59106c68` |
| `actions/upload-artifact` | `v7.0.2` | `cf430e030ddbb5b0abf93d22962f4752f3646cd9` |

No cache action is used. `setup-dotnet` reads `global.json` (SDK `10.0.402`) and
the explicit `dotnet-version: '10.0.402'`.

## Permissions / trigger safety

- Top-level `permissions: contents: read`; no `write-all`, no `pull_request_target`.
- Fork PRs do not receive repository secrets. The normal CI has no secret use.
- `sqlserver-integration.yml` is manual `workflow_dispatch` only and uses the
  `sqlserver-integration` environment (configure required reviewers if desired).
- No deployment, publish, push or workflow-dispatch execution is performed by
  this work.

## Normal CI job separation

1. **build-test** — main tests must be exactly 177; SQL Server integration tests
   run only to demonstrate that they skip (8 skipped expected). The job summary
   says **NOT RUN** for SQL Server.
2. **smoke** — builds, starts the app on loopback with a temp DB, waits for health,
   runs the 280-check smoke, and kills only the PID it started.
3. **sqlserver** — manual workflow only; not part of push/PR CI.

## Manual SQL Server workflow — prerequisites and EULA boundary

1. The operator must independently review and accept the Microsoft SQL Server
   EULA. Repository/chat approval is not itself EULA acceptance.
2. Configure the `sqlserver-integration` environment and repository/environment
   secret `SQLSERVER_SA_PASSWORD` with a strong disposable password. Do not put
   it in a file, issue, workflow YAML or chat. Use a generated password without
   semicolons or shell metacharacters, because the value is used in a SQL
   connection string and container health command.
3. Ensure the runner is a supported **x86-64 Linux** runner (`ubuntu-24.04` here)
   and the workflow runs on a disposable server. The image is pinned:
   `mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04`.
4. Manually dispatch the workflow with `accept_sqlserver_eula: true`. The job
   then sets `ACCEPT_EULA=Y` for that disposable service. This is a manual
   activation boundary, not automatic acceptance by push/PR.
5. The fixture creates only `DigitalBanking_IT_<hex>` through the supplied
   connection and drops only that DB. The connection must have `CREATE DATABASE`
   permission.
6. The TRX gate requires exactly 8 passed and 0 skipped. A missing connection,
   failed service or skip is a failure, not a green SQL result.

SQL Server 2022 CU27 Linux image tags are published by Microsoft at
`mcr.microsoft.com/mssql/server`; the pinned tag was checked against the MCR tag
list on 2026-10-22. EULA and container prerequisites:
[Microsoft SQL Server Linux container quickstart](https://learn.microsoft.com/en-us/sql/linux/quickstart-install-connect-docker?view=sql-server-ver16&preserve-view=true). Microsoft supports SQL Server Linux containers only on x86-64
Linux hosts; macOS ARM64 emulation is not a supported substitute.

## Artifacts

- `ci-test-summaries`: main and integration count summaries.
- `smoke-summary`: 280-check count line.
- `sqlserver-summary`: 8-test count line when the manual job runs.

Artifacts intentionally exclude `.local`, SQLite DBs, environment files, cookies,
tokens, TRX bodies and application logs. Logs may contain paths but are not
uploaded. Artifact names and paths contain no secrets.

## Verification actually performed

- YAML parsed with Ruby Psych; no tab characters; structure inspected.
- `bash -n` for changed shell scripts; `python3 -m py_compile` for Python scripts.
- `ci_run_smoke.sh` guard tested locally: existing `.local/credentials.json`
  causes a clean refusal (`exit 1`) rather than overwriting credentials.
- Full wrapper E2E in an isolated source snapshot with **no copied `.local`**:
  ports 5196/5197, temporary DB, **exit 0**, **280 checks**, only
  `artifacts/smoke-summary.txt`; ports free afterward; occupied-port run refused
  before credentials. Evidence:
  `docs/evidence/2026-10-22-ci-wrapper-e2e.txt`.
- `run_sqlserver_integration.sh` tested without env: `NOT RUN`, `exit 2`.
- `verify_sqlserver_trx.py` self-check with synthetic TRX:
  skipped run fails; fully passed run succeeds.
- Solution build passes; main tests 177; integration tests 8 skipped; smoke 280
  was verified on an isolated synthetic DB.

## NOT RUN

GitHub Actions has **not executed either workflow**; no push/PR/dispatch was
performed. The real SQL Server job is pending a supported host, EULA decision and
secret/environment configuration. Do not display a CI or SQL Server badge as
passing yet.
