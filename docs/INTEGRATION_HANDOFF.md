# Integration handoff — account/UI slice

- Tarih: **2026-10-20/21**
- Kaynak worktree: `.dsh-worktrees/dsh-mvh4pdzy-u7y7j4`
- Kaynak branch: `dsh/dsh-mvh4pdzy-u7y7j4`
- **Son commit ve integration/local-development: `35f247e`** (Owner invariant + approval hardening + QA evidence)
- Bu belgedeki **dashboard/UI düzeltmeleri henüz commit edilmedi**; dosyalar korunuyor.
- Son doğrulanmış kaynak: **150 xUnit / 280 smoke**, 0 build warning/error.
- UAT: `outputs/DigitalBanking-UAT` (5480/5481) ayrı ve **dokunulmadı**; UAT `.local` kopyalanmadı.
- Parent kirli kopya, 5280/5281 ve GPT QA ortamı: **dokunulmadı**.
- SQL Server: migration/model üretildi, **uygulanmadı/doğrulanmadı**.

## 1. Commit edilmiş temel

| Commit | İçerik |
|---|---|
| `cac7f00` | Ownership, timeline, receipts, reconciliation backend |
| `11684b2` | Owner / DelegatedApprover / Customer approval model |
| `d2e90e3` | Üç rollü dashboard |
| `7340a1c` | 272-check smoke (historical) |
| `35f247e` | Single-owner invariant, setup/rate-limit, approval ownership/atomicity, QA evidence; integration fast-forward |

## 2. Commit bekleyen dashboard/UI dilimi

Kod/test:
- `DigitalBanking.Dashboard/Services/DashboardUi.cs`
- `DigitalBanking.Dashboard/Services/ApiClient.cs`
- `DigitalBanking.Dashboard/Services/ApiAuthService.cs`
- `DigitalBanking.Dashboard/Pages/Shared/_Layout.cshtml`
- `DigitalBanking.Dashboard/Pages/Login.cshtml`
- `DigitalBanking.Dashboard/Pages/Register.cshtml` / `Setup.cshtml` labels
- `DigitalBanking.Dashboard/Pages/Admin/Delegations/Index.cshtml(.cs)`
- `DigitalBanking.Dashboard/Pages/Admin/Users/Index.cshtml`
- `DigitalBanking.Dashboard/Pages/Admin/Approvals/Index.cshtml`, `Details.cshtml`
- `DigitalBanking.Dashboard/Pages/Admin/Requests/New.cshtml`
- `DigitalBanking.Dashboard/Pages/Admin/Reconciliation/Exceptions.cshtml`
- `DigitalBanking.Dashboard/Pages/Customer/Home.cshtml`
- `DigitalBanking.Dashboard/Pages/Customer/Accounts/Index.cshtml(.cs)`
- `DigitalBanking.Dashboard/Pages/Customer/Accounts/Details.cshtml(.cs)`
- `DigitalBanking.Dashboard/Pages/Customer/Requests.cshtml(.cs)`
- `DigitalBanking.Dashboard/Pages/Customer/Transfer.cshtml`
- `DigitalBanking.Dashboard/Pages/Customer/Payments/Index.cshtml`, `Details.cshtml`
- `DigitalBanking.Dashboard/wwwroot/css/site.css`
- `DigitalBanking.Domain/Enums/AccountType.cs`
- `DigitalBanking.Domain/Entities/Account.cs`
- `DigitalBanking.Domain/Entities/PaymentApproval.cs`
- `DigitalBanking.Application/DTOs/Account/*` (name/type/rename)
- `DigitalBanking.Application/DTOs/ApprovalRequests/*`
- `DigitalBanking.Application/DTOs/PaymentApprovals/PaymentApprovalResponse.cs`
- `DigitalBanking.Application/Mappings/MappingProfile.cs`
- `DigitalBanking.Application/Common/AccountMetadata.cs`
- `DigitalBanking.Infrastructure/Services/AccountService.cs`
- `DigitalBanking.Infrastructure/Services/CustomerPortalService.cs`
- `DigitalBanking.Infrastructure/Services/DelegationService.cs`
- `DigitalBanking.Infrastructure/Services/PaymentApprovalService.cs`
- `DigitalBanking.Infrastructure/Services/PaymentTimelineService.cs`
- `DigitalBanking.Infrastructure/Services/AccountService.cs` / `LedgerOperations.cs` / `TransactionService.cs`
- `DigitalBanking.Application/Common/Money.cs` / `ReportDateRange.cs`
- `DigitalBanking.Application/Validators/Auth/LoginRequestValidator.cs`
- `DigitalBanking.Application/Validators/PaymentApprovals/CreatePaymentApprovalRequestValidator.cs`
- `DigitalBanking.Tests/AccountServiceTests.cs`
- `DigitalBanking.Tests/DashboardLoginContractTests.cs`
- `DigitalBanking.Tests/IdentityAndDelegationTests.cs`
- `DigitalBanking.Tests/CustomerPortalTests.cs`
- `scripts/run_local.py` (isolated `--database` for smoke)
- `scripts/start_local_detached.py` (safe detached demo)
- `scripts/upgrade_sqlite_account_metadata.py` (additive SQLite upgrade)
- `scripts/smoke_test.py` (mevcut 280 kapsamı)

Doküman/kanıt:
- `README.md`, `docs/PROGRESS.md`, `docs/TEST_REPORT.md`,
  `docs/DEVELOPMENT_COMPLETION_REPORT.md`, `docs/VERIFICATION.md`,
  `docs/UAT_CHECKLIST.md`, `docs/DASHBOARD_PRODUCT_BRIEF.md`,
  `docs/DEEPSEEK_FINDINGS.md`, `docs/INTEGRATION_HANDOFF.md`
- `docs/evidence/2026-10-20-{build,test,smoke,nuget-audit}.txt`
- `docs/screenshots/*` (13 gerçek masaüstü/dar ekran görseli + README)

`.local/`, UAT `.local` ve başka worktree/DB commit kapsamına girmez.

## 3. Önerilen yerel commit

Sandbox `.git/worktrees` index'ine yazmayı engelliyor. Final hesap/UI değişiklikleri için `git add` denendi ve geniş izin istenmedi:

```text
fatal: Unable to create '.../.git/worktrees/dsh-mvh4pdzy-u7y7j4/index.lock':
Operation not permitted
add_exit=128
```

Commit atılmadı; dosyalar korunuyor. Temiz yetkili kabuk/GPT dar kapsamlı araç:

```sh
git add \
  DigitalBanking.API DigitalBanking.Application DigitalBanking.Domain \
  DigitalBanking.Infrastructure DigitalBanking.Dashboard DigitalBanking.Tests \
  scripts/run_local.py scripts/smoke_test.py scripts/start_local_detached.py \
  scripts/upgrade_sqlite_account_metadata.py README.md DEEPSEEK_START_HERE.md docs
git -c user.name=Codex -c user.email=codex@local.invalid commit -m \
  "Add account products, first-account flow and dashboard UX hardening"
```

Commit öncesi/sonrası doğrula:
```sh
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh build DigitalBanking.slnx \
  -p:UseSharedCompilation=false -m:1 -nr:false
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh test DigitalBanking.slnx \
  -p:UseSharedCompilation=false -m:1 -nr:false --no-build
```
Smoke yalnız ayrı sentetik DB ile:
```sh
python3 scripts/run_local.py --api-port 5180 --dashboard-port 5181 \
  --database "$PWD/.local/qa-smoke-final.db"
python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181
```
Beklenen: **150 test**, **280 smoke**, 0 build warning/error.

## 4. Entegrasyon notu

- Integration `local-development` artık `35f247e`; eski dirty parent worktree kullanılmadı.
- Bu UI dilimi commit edildikten sonra temiz integration worktree'ye fast-forward
  dene; olmazsa yalnız bu commit'i cherry-pick et. Parent kirli kopyada
  reset/clean/merge yapma.
- UAT kopyasını ayrı DB ile hazırla; UAT `.local` ve kullanıcı hesabını kopyalama.
- SQL Server migration'larını uygulama; >1 Owner verisinde migration veri silmeden durur.
- Push/PR/deploy yok.
