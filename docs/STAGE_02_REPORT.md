# Stage 2 report — GitHub Actions CI preparation

- Date: **2026-10-22**
- Worktree: `.dsh-worktrees/dsh-mvh4pdzy-u7y7j4`
- Branch: `dsh/dsh-mvh4pdzy-u7y7j4`
- Starting HEAD: **`76ea501`**
- Stage 1 remains **prepared / environment-blocked**: real SQL Server is still
  not verified; 8 integration tests are compiled but skipped/not-run.
- Stage 2 status: **prepared, GitHub Actions NOT RUN.**

## Changed / added

- `.github/workflows/ci.yml`
- `.github/workflows/sqlserver-integration.yml`
- `scripts/ci_prepare_credentials.py`
- `scripts/ci_run_smoke.sh`
- `scripts/ci_summarize_trx.py`
- `scripts/verify_sqlserver_trx.py`
- `scripts/ci_check_workflow_guards.py`
- `scripts/run_sqlserver_integration.sh` (Stage 1, retained)
- `docs/CI_GUIDE.md`
- `docs/SECURITY_MATRIX.md`
- `docs/evidence/2026-10-22-ci-static-checks.txt`
- `docs/evidence/2026-10-22-ci-wrapper-e2e.txt`
- `docs/STAGE_02_REPORT.md`
- `docs/STAGE_02_TASK_NOTES.md`
- `docs/PORTFOLIO_ROADMAP.md`, `docs/PROGRESS.md`, `docs/INTEGRATION_HANDOFF.md`
- `README.md`

## Workflows

### `ci.yml` — push / pull_request

- `permissions: contents: read`; no write-all, no `pull_request_target`.
- `build-test`:
  - restore/build all projects;
  - main tests gated at **150**;
  - integration project runs with no connection env: **8 skipped**, reported as
    NOT RUN, not as SQL Server verification;
  - artifact: sanitized count summaries only.
- `smoke`:
  - isolated temp SQLite DB, loopback 5180/5181;
  - runtime-generated synthetic demo/Owner/JWT values;
  - bounded health wait and own-PID cleanup;
  - **280** checks; artifact: summary only.
- Both jobs have timeout and concurrency; no cache/credentials uploaded.

### `sqlserver-integration.yml` — manual only

- Trigger: `workflow_dispatch` only; **never** push/PR.
- Manual boolean input `accept_sqlserver_eula`; default false. The job sets
  `ACCEPT_EULA=Y` only after the operator manually dispatches it. Chat approval
  is not EULA acceptance.
- `ubuntu-24.04` x86-64 runner; pinned Microsoft image
  `mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04`; health check uses the
  documented `/opt/mssql-tools18/bin/sqlcmd` path and a shell-expanded env value.
  Static review only; no container run was possible here.
- Requires `sqlserver-integration` environment and secret
  `SQLSERVER_SA_PASSWORD`; no secret is in this repository.
- Fixture connection + `DIGITALBANKING_SQLSERVER_ALLOW_DESTRUCTIVE=1`; random
  `DigitalBanking_IT_<hex>` DB only; drop only that DB.
- `verify_sqlserver_trx.py` requires total=8, passed=8, skipped=0. A skip or
  connection failure cannot turn the job green.
- Artifact: sanitized `sqlserver-summary.txt` only.

## Official action versions used

Tags resolved from official repositories on 2026-10-22; workflows pin full
commit SHAs, not floating majors:

- `actions/checkout` v7.0.1 — `3d3c42e5aac5ba805825da76410c181273ba90b1`
- `actions/setup-dotnet` v6.0.0 — `a98b56852c35b8e3190ac28c8c2271da59106c68`
- `actions/upload-artifact` v7.0.2 — `cf430e030ddbb5b0abf93d22962f4752f3646cd9`

## What actually ran locally

| Check | Result |
|---|---|
| Ruby Psych YAML syntax on both workflows | parsed |
| `scripts/ci_check_workflow_guards.py` | PASS |
| `bash -n` changed shell scripts / Python compile | PASS |
| Solution build (all projects) | **0 warnings, 0 errors** |
| Main xUnit | **150 passed, 0 failed** |
| SQL Server integration (no env) | **0 passed, 8 skipped — NOT RUN** |
| SQLite HTTP smoke (isolated synthetic) | **280 passed** (prior Stage 1/account evidence; app unchanged by CI files) |
| `ci_run_smoke.sh` existing-credential guard | clean refusal, exit 1 |
| `ci_run_smoke.sh` full isolated snapshot (ports 5196/5197) | **exit 0, 280 checks, one summary artifact, ports free** |
| `ci_run_smoke.sh` occupied-port guard | refusal before `.local`, exit 1 |
| `run_sqlserver_integration.sh` without env | `NOT RUN`, exit 2 |
| TRX skip gate | synthetic 8-skip TRX failed; synthetic 8-pass TRX passed |

The CI smoke wrapper was verified end-to-end in a clean temporary source snapshot
with no developer `.local`: different loopback ports (5196/5197), temp DB, 280
checks, only the summary artifact, and clean port release. The main worktree's
`.local` and UAT environment were not touched. A local run against the existing
developer `.local` correctly refuses to overwrite it; the GitHub runner starts
from a fresh checkout.

## NOT RUN / blockers

- **GitHub Actions has not executed either workflow.** No push, PR, dispatch,
  publish, deploy or workflow run was performed.
- Real SQL Server is still **not verified**: no supported x86-64 host/CI service
  is available here; no EULA accepted on the user's behalf; no secret configured.
- Stage 1's 8 SQL tests remain **skipped/not-run**. Their passing TRX gate is
  prepared but unexercised against a real server.
- Actionlint/schema validator is not installed. YAML was parsed and static guards
  ran, but this is not equivalent to a GitHub-hosted run.

## GPT independent verification commands

```sh
# YAML syntax
ruby -e 'require "yaml"; YAML.load_file(".github/workflows/ci.yml"); YAML.load_file(".github/workflows/sqlserver-integration.yml")'
python3 scripts/ci_check_workflow_guards.py

# Syntactic/guard checks
bash -n scripts/ci_run_smoke.sh scripts/run_sqlserver_integration.sh
python3 -m py_compile scripts/ci_prepare_credentials.py scripts/ci_summarize_trx.py scripts/verify_sqlserver_trx.py
bash scripts/run_sqlserver_integration.sh; echo "expected exit=$?"

# Build and tests (SQL integration remains skipped)
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh build DigitalBanking.slnx \
  -p:UseSharedCompilation=false -m:1 -nr:false
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh test DigitalBanking.slnx \
  -p:UseSharedCompilation=false -m:1 -nr:false --no-build
```

To exercise the real SQL Server job, a human must accept the EULA, configure the
environment/secret and manually dispatch on a supported x86-64 runner. Do not
trigger it automatically.

## Security matrix

Initial requirement → implementation → evidence table:
[`SECURITY_MATRIX.md`](SECURITY_MATRIX.md). It covers least privilege, fork
secrets, pinned actions, credential isolation, deterministic SQL cleanup, skip
gates, EULA boundary, migration truthfulness, artifact minimization and
truthful reporting. It is not an ASVS certification claim.

## Next

Stage 3 (double-entry journal) must not start until the user approves it. The real
SQL Server job remains pending; a written workflow is not a CI run, and a green
SQLite smoke is not SQL Server proof.
