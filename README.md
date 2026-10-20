# DigitalBanking — banking operations portfolio demo

> **Delivery status (2026-10-20):** the .NET 10 API, banking rules, security controls and automated tests are implemented and verified locally. The existing Razor dashboard is intentionally kept minimal in this delivery; the full customer/admin dashboard is designed in [`docs/DASHBOARD_PRODUCT_BRIEF.md`](docs/DASHBOARD_PRODUCT_BRIEF.md) and will be built as the next stage. SQL Server migrations were generated but **not applied or tested**. This is an educational demo, not a production banking system.

A working, testable simulation of the operational controls behind retail payments: customers and accounts, deposits/withdrawals, maker-checker payment approvals, risk levels, audit, reports, decimal money and JWT-based identity.

## The 30-second version

DigitalBanking is a small banking back office built with ASP.NET Core 10 and EF Core. It demonstrates the parts of banking software that are usually invisible in CRUD demos: a **maker-checker approval workflow**, **risk-based routing**, **idempotent money movement**, **atomic audit**, **role/claim enforcement**, and **decimal/currency correctness**.

- **Stack:** C# / .NET 10, ASP.NET Core Web API, Razor Pages, EF Core 10, SQL Server (default) + SQLite (Development only), xUnit.
- **Verified locally:** build 0 warnings / 0 errors, **95 automated tests**, **71 end-to-end smoke checks**.
- **Try it:** `bash scripts/dotnet.sh build DigitalBanking.slnx` then `python3 scripts/run_local.py --api-port 5180 --dashboard-port 5181`.
- **API/Swagger:** <http://127.0.0.1:5180/swagger> · **Dashboard:** <http://127.0.0.1:5181>

## Business context

The project translates day-to-day banking operations into software: a customer asks for a payment, operations checks limits and balances, a second person approves high-risk items, and every movement leaves an audit trail. The domain model deliberately stays small so the controls are visible and testable.

It is a portfolio project, not a claim about any employer's systems. The author's background is in banking operations, payments, SWIFT operations, operational risk/controls and management information systems; this repository shows how those controls can be expressed in backend code.

## Demo personas and end-to-end scenario

The target demo (dashboard stage) uses synthetic personas only:

| Persona | Role | Purpose |
|---|---|---|
| Customer A | `User` linked to synthetic customer A | Own balance cards, statement, transfer request |
| Customer B | `User` linked to synthetic customer B | Recipient / second customer view |
| Admin maker | `Admin` | Creates operational payments and approval requests |
| Admin checker | second `Admin` | Reviews and approves/rejects maker requests |

End-to-end story: Admin maker creates a request from Customer A's TRY account to Customer B. Low-risk amounts are auto-approved; medium/high/critical amounts enter the pending queue. Admin checker opens the queue, sees risk/status badges, approves or rejects, and the customer sees the result and ledger entry. A customer can never approve their own request, spend another customer's money, or create money by "depositing" it.

**Current interim state:** the backend supports the full maker-checker flow for Admin users. `User` currently has no business-data access (deny-by-default) because the data model has no User–Customer ownership yet; it can only authenticate and read its own identity. The ownership extension, customer self-service endpoints and four-persona dashboard are specified but **not implemented**; see the product brief.

## Feature and role matrix

| Capability | Admin | User (current) | User (next dashboard phase, planned) |
|---|:---:|:---:|:---:|
| Sign in / JWT identity | ✅ | ✅ | ✅ |
| Read customers/accounts/reports | ✅ | — (yalnız kimlik) | ✅ (own data only) |
| Open customer / open account | ✅ | — | — |
| Deposit / withdraw (teller demo) | ✅ | — | — (operations only) |
| Direct transfer below 50,000 | ✅ | — | — |
| Create payment approval request | ✅ | — | ✅ (from own account) |
| Approve/reject payment request | ✅ (different Admin) | — | — (Admin checker) |
| View pending approval queue | ✅ | — | — |
| Audit log | ✅ | — | — |
| User management | ✅ | — | — |
| Own profile / balances / statement | — | — | ✅ |
| Transfer request status tracking | — | — | ✅ |

`✅` = implemented and tested in the API/backend; the current minimal dashboard exposes only login/logout, a small panel, an Admin-only top-account report and Admin user list. `—` = not permitted. Planned items are not implemented yet.

**Current User access:** a standard `User` can authenticate and read only its own identity (`GET /api/auth/me`). Customer/account/report endpoints are Admin-only until User–Customer ownership is implemented. This deny-by-default choice avoids leaking another customer’s synthetic data.

## Architecture and data flow

```text
Browser (Razor Pages)
   │  session cookie (JWT kept server-side in session; never rendered)
   ▼
Dashboard ── HTTP + Bearer JWT ──► ASP.NET Core API
                                     │
                                     ├─ ExceptionMiddleware → ApiResponse envelope
                                     ├─ JWT auth (Guid nameidentifier, role claim)
                                     ├─ Rate limiter + login-attempt throttle
                                     ├─ Idempotency-Key filter (money endpoints)
                                     ├─ Controllers (role policy from claims)
                                     └─ Services
                                          ├─ LedgerOperations (decimal balances + ledger rows)
                                          ├─ PaymentApprovalService (risk/maker-checker)
                                          ├─ AuditService (same transaction for money)
                                          └─ EF Core ──► SQL Server (default)
                                                          SQLite (Development demo)
```

Key request flow for a payment:

1. Controller builds an `ActorContext` **only** from validated JWT claims.
2. Service re-checks role, accounts, currency, balance, amount scale and current status.
3. Money and audit are written in one database transaction; `RowVersion` blocks concurrent double approval.
4. `Idempotency-Key` prevents a retried HTTP request from moving money twice.
5. The response is wrapped in `ApiResponse<T>`; the dashboard maps failures to safe Turkish messages.

## Technology choices and rationale

- **ASP.NET Core 10 / C#** — modern runtime, strong typing, good fit for API + Razor Pages in one solution.
- **EF Core 10** — explicit relational model, optimistic concurrency (`rowversion`), transactions, provider portability.
- **SQL Server as the default provider** — realistic banking storage and decimal semantics; migrations kept intact.
- **SQLite only for local Development** — disposable playground, no SQL Server proof; `EnsureCreated` is used, migrations are not applied.
- **Razor Pages** — no new frontend framework; server-rendered pages keep the demo small and accessible, while the API remains independently consumable.
- **JWT + role claims** — stateless API authentication; the dashboard stores the token only in server-side session.
- **xUnit + SQLite in-memory** — fast rule-level tests. They do not replace SQL Server integration tests.

## Money, identity and control decisions

- **Money is decimal.** All amounts use `decimal`, two decimal places; more precision is rejected instead of silently rounded. No `double`/`float` conversion "to make SQLite work".
- **Identity comes from the JWT.** `PaymentApproval.RequestedByUserId`/`ReviewedByUserId` are `Guid` values matching `User.Id`. Request bodies cannot choose a requester or reviewer. See ADR-0001.
- **Maker-checker.** Low-risk payments (`< 50,000` in the account currency) are auto-approved by system policy; medium/high/critical payments stay `Pending` for a **different** Admin. Self-approval and self-rejection are blocked.
- **Direct-transfer limit.** Direct transfers are Admin-only and must be below the approval threshold; larger payments must use the approval flow, so the maker-checker path cannot be bypassed.
- **Risk levels.** Low / Medium / High / Critical thresholds are explicit and mapped to the actual workflow, not just displayed.
- **Currency.** TRY/USD/EUR/GBP are supported; transfers and approvals require matching account currencies. No FX conversion is performed, and cross-currency totals are never summed.
- **Idempotency.** Money-moving endpoints accept `Idempotency-Key`; a completed key replays the original response, a different payload with the same key returns `409`, and an in-progress key returns `409` instead of applying money twice.
- **Atomic audit.** Balance, debit/credit ledger rows, approval status and audit record commit or roll back together. Audit details never contain passwords or tokens.
- **Login hardening.** PBKDF2-SHA256 (600k iterations) with legacy SHA-256 rehash-on-login, new-password policy (8+ chars, letter + digit), per-username failed-attempt lockout, and a per-IP login rate limit.
- **Reports.** Date-only `to` filters are inclusive of the whole day; ranges are capped at 366 days; top-account reports are scoped to one currency.

Full decision record: [`docs/adr/0001-payment-and-role-policy.md`](docs/adr/0001-payment-and-role-policy.md).

## Verified results

Environment: macOS ARM64, .NET SDK **10.0.402**, EF Core **10.0.3**, SQLite Development database. Date: **2026-10-20**.

| Check | Result |
|---|---|
| `bash scripts/dotnet.sh build DigitalBanking.slnx` | **0 warnings, 0 errors** |
| `bash scripts/dotnet.sh test DigitalBanking.slnx` | **95 passed, 0 failed, 0 skipped** |
| `python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181` | **71 checks passed** |
| NuGet vulnerable-package audit (6 projects, transitive) | no vulnerable packages reported by current sources |
| `git diff --check` | clean |

The smoke run covers health, unauthorized access, login/claims/roles, login throttling, customers, accounts, decimal balances, currency mismatch, deposit/withdraw/transfer, maker-checker, self-approval rejection, double-approval rejection, idempotent replay, audit, report filters and dashboard session/role flows. Evidence: [`docs/evidence/`](docs/evidence/).

**Not verified:** SQL Server connection, applying the new migration to SQL Server, SQL Server decimal/concurrency behaviour, production hardening, browser UI automation/manual UAT. Do not treat the SQLite demo as SQL Server proof. Details: [`docs/TEST_REPORT.md`](docs/TEST_REPORT.md), [`docs/VERIFICATION.md`](docs/VERIFICATION.md).

## Quick start

Prerequisites: macOS/Linux with the bundled .NET 10 SDK at `~/Documents/Codex/toolchains/dotnet` (or `DOTNET_ROOT` pointing at a .NET 10 SDK), Python 3.

```sh
# 1) Build (the wrapper uses the bundled SDK if dotnet is not on PATH)
bash scripts/dotnet.sh build DigitalBanking.slnx

# 2) Run API + dashboard on loopback only
python3 scripts/run_local.py --api-port 5180 --dashboard-port 5181

# 3) In another terminal: synthetic end-to-end smoke (no real data)
python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181
```

- Dashboard: <http://127.0.0.1:5181>
- API/Swagger: <http://127.0.0.1:5180/swagger>
- Health: <http://127.0.0.1:5180/health>

The first run creates `.local/credentials.json` (mode `0600`) with a random demo password and JWT secret. **Never commit or share this file.** Read the password locally when signing in; do not paste it into chat, screenshots or issues. `.local/` is git-ignored.

The demo uses synthetic data only. It never contacts live banking/payment systems.

### Database reset

SQLite uses `EnsureCreated`, not migrations. After a schema change, archive/delete `.local/digitalbanking.db*` and restart; the next run creates the current schema and re-seeds `admin` and `tamer`.

### Tests

```sh
bash scripts/dotnet.sh test DigitalBanking.slnx
```

The harness used for this delivery needed single-node/no-shared-compilation flags because its sandbox blocks the shared compiler; the plain command above is the normal developer path.

## Current dashboard scope

The existing Razor Pages are deliberately minimal: login/logout, a small panel, top-account report filters and Admin user management. They have been migrated to the new API contracts with safe error/401/403 handling, but they are **not** the finished portfolio dashboard.

The next phase (documented, not started) adds:

- Admin operations overview, customer/account management, payment/approval queue with risk/status badges, audit and reports.
- Customer self-service: own profile/accounts, balance cards per currency, recent transactions, recipient selection, transfer request, result/receipt/status tracking.
- The ownership and server-side authorization model required for safe customer self-service (User–Customer–Account), plus schema/migration/tests.

See [`docs/DASHBOARD_PRODUCT_BRIEF.md`](docs/DASHBOARD_PRODUCT_BRIEF.md) for scope, four personas, UX/accessibility rules, security model, schema/migration impact, test plan and effort. Manual acceptance steps are in [`docs/UAT_CHECKLIST.md`](docs/UAT_CHECKLIST.md).

## Screenshots (reserved for the dashboard phase)

No screenshots are fabricated. Real screenshots will be added after the dashboard phase, showing:

| Screen | What it should demonstrate |
|---|---|
| Admin overview | pending approvals by risk, balances/totals per currency |
| Approval queue | maker/checker separation, risk and status badges |
| Customer home | own balance cards (TRY/USD/EUR, not summed), recent activity |
| Transfer wizard | account/recipient/amount summary before confirmation |
| Receipt/status | transfer result and pending/approved/rejected state |
| Audit | filterable operational trail without secrets |

## Project structure

```text
DigitalBanking.API            HTTP API, JWT auth, filters, middleware, Swagger
DigitalBanking.Application    service contracts, DTOs, validators, policies
DigitalBanking.Domain         entities and enums
DigitalBanking.Infrastructure EF Core, migrations, security, services
DigitalBanking.Dashboard      Razor Pages (minimal current UI)
DigitalBanking.Tests          xUnit rule and contract tests
docs/                         ADR, reports, evidence, UAT, product brief
scripts/                      dotnet wrapper, local runner, smoke test, VS Code SDK checks
```

## Selected API surface

| Endpoint | Access | Notes |
|---|---|---|
| `POST /api/auth/login` | anonymous | JWT + role; rate limited and throttled |
| `GET /api/customers`, `POST /api/customers` | read: authenticated; create: Admin | wrapped `ApiResponse<T>` |
| `GET /api/accounts`, `POST /api/accounts/open` | read: authenticated; open: Admin | currency + opening balance |
| `POST /api/transactions/deposit`, `/withdraw` | Admin | `Idempotency-Key` supported |
| `POST /api/accounts/transfer` | Admin | only below 50,000; `Idempotency-Key` |
| `POST /api/paymentapprovals` | Admin (current) | risk routing; `Idempotency-Key` |
| `GET /api/paymentapprovals/pending`, `GET /{id}` | Admin | approval queue/detail |
| `POST /api/paymentapprovals/{id}/approve`, `/reject` | Admin, different from requester | maker-checker |
| `GET /api/transactions/statement/{id}`, `/daily-summary/{id}`, `/top-accounts` | authenticated | inclusive end-of-day, currency-scoped top accounts |
| `GET /api/audit` | Admin | filter + page; no secrets |
| `GET /api/users`, `POST /api/users` | Admin | wrapped response |

## Known limitations

- **SQL Server is not verified.** The new migration is written and documented, but never applied to SQL Server here. SQLite tests are not SQL Server evidence. Production migration/backfill requires an operator; see [`docs/migrations/README.md`](docs/migrations/README.md).
- **No User–Customer ownership yet.** The current `User` role can authenticate but has no business-data access (deny-by-default); the planned customer self-service experience is specified in the product brief and requires a schema/migration/authorization change. Do not treat the current dashboard as customer-ready.
- **No FX conversion.** Risk thresholds use nominal account-currency amounts; multi-currency totals are not converted or summed.
- **Login throttle is in-memory.** Multiple API instances would need a shared store.
- **UTC report dates.** No local-time/zone conversion or banking cut-off calendar.
- **No production hardening.** Demo credentials are local-only; no secret manager, WAF, full observability, retention/immutability policy or compliance certification is claimed.
- **VS Code GUI SDK discovery.** The C# Dev Kit log showed `dotnet` missing from the GUI PATH. Prerequisite scripts/settings are provided; the GUI change needs a user-run launchd step and a VS Code restart, and was not verified by the agent.
- **AI contribution.** The implementation and documentation were produced in an AI-assisted workflow (DeepSeek-based coding agent) with human review; see [`docs/PORTFOLIO_NOTES.md`](docs/PORTFOLIO_NOTES.md). Do not read this repository as an unattended production system.

## Documentation

- [Development completion report](docs/DEVELOPMENT_COMPLETION_REPORT.md)
- [Test report](docs/TEST_REPORT.md)
- [Verification](docs/VERIFICATION.md)
- [Progress](docs/PROGRESS.md)
- [Findings and limits](docs/DEEPSEEK_FINDINGS.md)
- [Payment/role policy ADR](docs/adr/0001-payment-and-role-policy.md)
- [Migration operator notes](docs/migrations/README.md)
- [Dashboard product brief (next phase)](docs/DASHBOARD_PRODUCT_BRIEF.md)
- [Manual UAT checklist](docs/UAT_CHECKLIST.md)
- [Portfolio/CV notes](docs/PORTFOLIO_NOTES.md)
- [Local development](docs/LOCAL_DEVELOPMENT.md)

## Türkçe özet

DigitalBanking, bankacılık operasyonlarındaki **maker-checker onay akışını**, risk
seviyelerini, audit izini ve decimal para kurallarını gösteren bir .NET 10 demo
uygulamasıdır. Backend ve iş kuralları tamamlanmış; **95 otomatik test** ve
**71 uçtan uca smoke kontrolü** başarılıdır. Mevcut Razor dashboard'u bu teslimde
bilinçli olarak minimaldir; müşteri ve admin için tam deneyim
[`docs/DASHBOARD_PRODUCT_BRIEF.md`](docs/DASHBOARD_PRODUCT_BRIEF.md) içinde
tasarlanmıştır ve sonraki aşamada geliştirilecektir.

Hızlı başlangıç, roller ve sınırlar için yukarıdaki bölümler; günlük geliştirme
için [`docs/LOCAL_DEVELOPMENT.md`](docs/LOCAL_DEVELOPMENT.md); manuel kabul
adımları için [`docs/UAT_CHECKLIST.md`](docs/UAT_CHECKLIST.md) kullanılabilir.

**Önemli sınırlar:** SQL Server bağlantısı/migration'ı bu ortamda test edilmedi;
SQLite yalnızca Development oyun alanıdır. Müşteri–kullanıcı sahipliği henüz yok;
standart `User` şu an yalnız kimlik doğrulaması yapabilir; müşteri/hesap/rapor verisi okuyamaz. Gerçek banka/para/kurum verisi kullanılmaz.
Bu bir eğitim/portföy projesidir, üretim sistemi değildir.

## Author

**Tamer Kargın** — Banking Operations & Payments Professional

- 13 years of banking operations experience
- Operations Coordinator at VakıfBank
- MSc in Management Information Systems
- [LinkedIn](https://www.linkedin.com/in/tamer-kargin-2a9564214/) · [GitHub](https://github.com/tamerkargin8)

## Disclaimer

This project is intended for educational and portfolio purposes only. It does not
represent any internal system, software or confidential process of any financial
institution. It must not be used to process real money or real customer data.
