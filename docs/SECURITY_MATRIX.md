# Security requirement → implementation → evidence matrix

Scope: implemented application controls (Stages 1–5) and CI/integration work.
This is an engineering evidence table, not a certification, audit opinion or full
OWASP ASVS compliance claim. Selected controls only; gaps and the SQL Server/
hosted-CI non-verification remain explicit.

| # | Requirement | Implementation | Evidence / status |
|---|---|---|---|
| S1 | CI least privilege | Both workflows declare `permissions: contents: read`; no write tokens, no deploy/publish | `.github/workflows/ci.yml`, `sqlserver-integration.yml`; static review |
| S2 | Fork PRs must not get repository secrets | Normal CI uses no secrets; no `pull_request_target`; SQL Server workflow is manual-only | Workflow triggers reviewed; SQL job requires manual dispatch |
| S3 | Supply-chain pinning | checkout `v7.0.1`, setup-dotnet `v6.0.0`, upload-artifact `v7.0.2` pinned to full commit SHAs; no cache action | Workflow comments/SHAs; tag SHAs resolved from official tags |
| S4 | No CI credentials in artifacts/logs | Ephemeral `.local` credentials generated at runtime and never echoed; only count summaries uploaded | `scripts/ci_prepare_credentials.py`, `ci_run_smoke.sh`; artifacts path excludes `.local` |
| S5 | Synthetic smoke isolation | Temp SQLite DB, loopback 5180/5181, bounded health wait, unique PID cleanup only | `scripts/ci_run_smoke.sh`; local guard and syntax checks |
| S6 | Destructive SQL tests are scoped | Env + `ALLOW_DESTRUCTIVE` gate; only `DigitalBanking_IT_<hex>` DB created/dropped; UAT paths denied | `SqlServerFixture`, `run_sqlserver_integration.sh`; static review; real run pending |
| S7 | SQL run cannot pass with skips | TRX checker requires total=8, passed=8, skipped=0; `continue-on-error` absent | `scripts/verify_sqlserver_trx.py`; checker test evidence |
| S8 | SQL EULA is a human decision | Manual `workflow_dispatch` with explicit `accept_sqlserver_eula`; no push/PR trigger; EULA not implied by chat approval | `sqlserver-integration.yml`; NOT RUN |
| S9 | Migration truthfulness | Integration fixture uses EF `MigrateAsync`, not `EnsureCreated`; SQLite is not called SQL Server proof | `SqlServerFixture`, `docs/SQLSERVER_VERIFICATION.md` |
| S10 | No production/live banking access | CI uses synthetic data/loopback; SQL Server disposable service; no cloud/paid/live connection | Workflow/docs; no deployment step |
| S11 | Artifact minimization | Upload only secret-free count summaries, not DB, `.local`, cookies, env or tokens | Workflow artifact paths |
| S12 | Secret redaction in runner output | Connection strings are env-only; fixture never prints them; GitHub masks secret values | Fixture/runner review; real run pending |
| S13 | No foreign-service readiness / port collision | `CI_API_PORT`/`CI_DASHBOARD_PORT` validated and preflight-occupied refusal before credentials; only own runner PID stopped | `ci_run_smoke.sh`, wrapper E2E evidence; occupied-port test exit 1 |

| S14 | Password storage (ASVS V2.1, selected) | PBKDF2-SHA256 600k iterations, per-user salt, rehash on login; passwords never logged | `PasswordHasher`, auth tests; local synthetic verification |
| S15 | Authentication/session identity (V3, selected) | JWT with Guid `NameIdentifier`, role claim, expiry; dashboard stores the token server-side and never renders it | `AuthAndClaimsTests`, dashboard login contract tests |
| S16 | Function-level authorization (V4.1) | Controller role policies plus service-level live DB re-read of user status/role; Customer gets `403` on accounting/operations | Accounting/operations tests; live E2E 403 checks |
| S17 | Object-level authorization / IDOR (V4.1) | `Customer.UserId` ownership joins; foreign account/payment returns `404`; delegated queue filtered by live delegation | Customer portal tests, smoke IDOR checks, Stage 4 delegated scoping test |
| S18 | Maker-checker / separation of duties (V4.2) | Every financial request Pending; Owner/delegated approver required; self-approval blocked even for Owner | Approval tests, smoke self-approval block, Stage 3/4 tests |
| S19 | Scoped delegated authority (V4.2/V11.1) | Live delegation row: operation, currency, inclusive max, optional daily total/expiry/active; re-read per decision | Delegation tests, 100,000.00 vs 100,000.01 smoke checks |
| S20 | Atomic financial posting (V11.1) | Balance + legacy transaction + journal + approval + timeline + audit in one DB transaction; `RowVersion` on approvals | Accounting rollback test; approval concurrency tests |
| S21 | Duplicate/replay protection (V11.1) | Unique `SourceKey` on journal; `Idempotency-Key` store on money/approval/assignment endpoints; status transition guards | Duplicate `SourceKey` test, replay test, idempotency tests |
| S22 | Monetary input integrity (V5.1) | `decimal` only, 2dp exact scale, positive legs, one currency per journal entry, max guard rail; no float/FX | Money/policy tests, journal invariant tests |
| S23 | Audit trail (V7.1, selected) | Audit rows for request creation/approval/rejection/cancellation, baseline, assignment and reconciliation actions; append-only history by convention | Audit tests, Stage 3/4 audit assertions |
| S24 | Sensitive data exposure (V8.3, selected) | Account numbers masked in customer/receipt views; no token/password/`.local` content in HTML, logs, artifacts or screenshots | Masking tests, screenshot review and secret-free capture process |
| S25 | CSV/file handling (V12, selected) | `.csv` extension/size/row limits, UTF-8, formula-injection prefixing (`= + - @`), synthetic-only import, no balance mutation | `CsvStatementParserTests`, smoke formula-safe export check |
| S26 | Accounting integrity/immutability (V11.1) | Append-only journal; no update/delete endpoint; baseline is explicit Owner action; trial balance exposes gaps instead of auto-backfill | Stage 3 accounting tests and surface-inspection test |
| S27 | Operational assignment safety (V4/V11) | Only pending, only active eligible target, delegated self-only, no approval-right bypass, idempotent audit; Customer `403` | Stage 4 operations tests and live E2E |
| S28 | Configuration/secrets (V14, selected) | Connection/JWT/demo secrets via env/local ignored `.local`; no secrets in CI artifacts; EULA not auto-accepted | CI scripts/docs, `FINAL_GAPS.md`; hosted run pending |

## Open gaps

- Real SQL Server execution, EULA acceptance, host and secret configuration are
  pending; the workflow is **NOT RUN**. Application controls were exercised on
  synthetic SQLite, not a production database.
- The matrix is not a complete ASVS mapping; only selected V2/V3/V4/V5/V7/V8/
  V11/V12/V14 themes are represented. No certification or audit opinion.
- No actionlint/action schema validator is installed; YAML syntax and structural
  checks were run only.
- No ASVS certification or complete control coverage is claimed; this table is
  an initial draft for later expansion.
- Workflow pinning must be kept current through deliberate review; no automated
  update bot is configured.
