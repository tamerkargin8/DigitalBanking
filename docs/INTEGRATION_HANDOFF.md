# Integration handoff — source snapshot, manifest and safe merge plan

- Tarih: **2026-10-20/21**
- Kaynak worktree: `.dsh-worktrees/dsh-mvh4pdzy-u7y7j4`
- Kaynak branch: `dsh/dsh-mvh4pdzy-u7y7j4`
- Son commit: **`7340a1c`** (`Rewrite end-to-end smoke for Owner approval model`)
- Bu belgedeki QA/final düzeltmeleri **henüz commit edilmedi**; dosyalar korunuyor.
- Son doğrulanmış kaynak: **147 xUnit / 272 smoke**, 0 build warning/error.
- UAT kopyası: `outputs/DigitalBanking-UAT` (5480/5481) — **bu handoff kapsamı dışında; dokunulmadı.**
- Parent / ana entegrasyon: yazma yapılmadı; yalnız salt okunur teşhis.

## 1. Commit manifest

### Commit edilmiş kaynak

| Commit | İçerik |
|---|---|
| `cac7f00` | Customer ownership, timeline, receipts, reconciliation backend |
| `11684b2` | Owner / DelegatedApprover / Customer approval-request model |
| `d2e90e3` | Owner, delegated approver and customer dashboard |
| `7340a1c` | 272-check end-to-end smoke test |

### Commit bekleyen QA/final düzeltmeleri

Kod:
- `DigitalBanking.API/Controllers/SetupController.cs`
- `DigitalBanking.API/Program.cs`
- `DigitalBanking.Dashboard/Pages/Setup.cshtml.cs`
- `DigitalBanking.Infrastructure/Persistence/BankDbContext.cs`
- `DigitalBanking.Infrastructure/Persistence/DatabaseSeeder.cs`
- `DigitalBanking.Infrastructure/Services/UserIdentityService.cs`
- `DigitalBanking.Infrastructure/Services/PaymentApprovalService.cs`
- `DigitalBanking.Infrastructure/Services/DemoScenarioService.cs`
- `DigitalBanking.Infrastructure/Migrations/BankDbContextModelSnapshot.cs`
- `DigitalBanking.Infrastructure/Migrations/20261021011500_EnforceSingleOwner.cs`
- `DigitalBanking.Infrastructure/Migrations/20261021011500_EnforceSingleOwner.Designer.cs`

Test:
- `DigitalBanking.Tests/IdentityAndDelegationTests.cs`
- `DigitalBanking.Tests/PaymentApprovalServiceTests.cs`
- `DigitalBanking.Tests/SetupPageTests.cs`
- `scripts/smoke_test.py`

Dokümantasyon ve kanıt:
- `README.md`, `DEEPSEEK_START_HERE.md`
- `docs/PROGRESS.md`, `docs/TEST_REPORT.md`, `docs/DEVELOPMENT_COMPLETION_REPORT.md`, `docs/VERIFICATION.md`
- `docs/UAT_CHECKLIST.md`, `docs/DASHBOARD_PRODUCT_BRIEF.md`, `docs/PORTFOLIO_NOTES.md`
- `docs/DEEPSEEK_FINDINGS.md`, `docs/LOCAL_DEVELOPMENT.md`
- `docs/adr/0001-payment-and-role-policy.md`, `docs/migrations/README.md`
- `docs/RECONCILIATION_FORMAT.md`, `docs/samples/*.csv`
- `docs/screenshots/*` (7 gerçek ekran + README)
- `docs/evidence/2026-10-20-*.txt`
- `docs/INTEGRATION_HANDOFF.md` (bu dosya)

> `.local/` ve UAT `.local` veritabanı commit kapsamına **girmez**; git-ignored.

## 2. Önerilen yerel commit

Sandbox `.git/worktrees` index’ine yazmayı engelliyor. Bu oturumda `git add` denendi ve izin yükseltilmedi:

```text
fatal: Unable to create '.../.git/worktrees/dsh-mvh4pdzy-u7y7j4/index.lock':
Operation not permitted
add exit=128
```

Commit atılmadı; dosyalar korunuyor. Temiz bir yetkili kabukta veya GPT’nin dar kapsamlı araç ortamında:

```sh
git add \
  DigitalBanking.API/Controllers/SetupController.cs \
  DigitalBanking.API/Program.cs \
  DigitalBanking.Dashboard/Pages/Setup.cshtml.cs \
  DigitalBanking.Infrastructure/Persistence/BankDbContext.cs \
  DigitalBanking.Infrastructure/Persistence/DatabaseSeeder.cs \
  DigitalBanking.Infrastructure/Services/UserIdentityService.cs \
  DigitalBanking.Infrastructure/Services/PaymentApprovalService.cs \
  DigitalBanking.Infrastructure/Services/DemoScenarioService.cs \
  DigitalBanking.Infrastructure/Migrations/BankDbContextModelSnapshot.cs \
  DigitalBanking.Infrastructure/Migrations/20261021011500_EnforceSingleOwner.cs \
  DigitalBanking.Infrastructure/Migrations/20261021011500_EnforceSingleOwner.Designer.cs \
  DigitalBanking.Tests/IdentityAndDelegationTests.cs \
  DigitalBanking.Tests/PaymentApprovalServiceTests.cs \
  DigitalBanking.Tests/SetupPageTests.cs \
  scripts/smoke_test.py \
  README.md DEEPSEEK_START_HERE.md docs

git -c user.name=Codex -c user.email=codex@local.invalid commit -m \
  "Harden single-owner setup, approval ownership and QA evidence"
```

`docs/` eklemek screenshots/evidence dahil yukarıdaki belgeleri kapsar. `.local`, UAT kopyası ve parent worktree eklenmemelidir.

## 3. Otomatik merge / dirty parent teşhisi (salt okunur)

Gözlem:

- `git worktree list`:
  - `integration/local-development` = `f201e0e`, temiz.
  - `deepseek/dashboard-and-tests` = `565df74` (ayrı worktree), **110 dosya staged ve kirli**.
  - Kaynak branch = `7340a1c`, bu worktree.
- `git merge-base HEAD integration/local-development` = **`f201e0e`**.
- Kaynak branch integration’ın önünde; commit edilmiş 10 commit var.
- Kirli `deepseek` worktree’sinde `git ls-files -u` **boş** (gerçek unmerged conflict kaydı yok); fakat index’te 110 dosyalık staged ekleme/silme seti var.
- Bu nedenle “auto-merge conflict” uyarısı büyük olasılıkla **dirty/staged parent index** kaynaklı; içerik çakışması kanıtı yok.

Kurallar:
- Parent / `deepseek` / integration worktree’ye **reset, clean, checkout, merge, commit** yapılmayacak.
- Kirli parent “quarantine” kabul edilecek; içeriği incelenmeden kullanılmayacak.
- Entegrasyon yeni/temiz bir worktree’de yapılmalı.

## 4. Güvenli entegrasyon planı

1. Kaynakta commit’i tamamla; `git status` temiz ve `git log -1` beklenen commit olsun.
2. Temiz entegrasyon worktree’si oluştur (parent dirty worktree’yi kullanma):
   ```sh
   git worktree add /tmp/DigitalBanking-integration integration/local-development
   ```
3. Kaynak branch integration’ın descendant’ı olduğu için önce fast-forward dene:
   ```sh
   git -C /tmp/DigitalBanking-integration merge --ff-only dsh/dsh-mvh4pdzy-u7y7j4
   ```
4. Fast-forward olmazsa commit’leri sırayla cherry-pick et:
   ```sh
   git -C /tmp/DigitalBanking-integration cherry-pick \
     cac7f00 11684b2 d2e90e3 7340a1c <QA-commit>
   ```
5. Entegrasyon worktree’sinde doğrula:
   ```sh
   DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh build DigitalBanking.slnx \
     -p:UseSharedCompilation=false -m:1 -nr:false
   DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh test DigitalBanking.slnx \
     -p:UseSharedCompilation=false -m:1 -nr:false --no-build
   # Temiz sentetik DB ile:
   python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181
   ```
6. Beklenen: **147 test**, **272 smoke**, 0 build warning/error.
7. SQL Server migration’ı **apply etme**; yalnız model/migration incele. `20261021011500_EnforceSingleOwner` mevcut >1 Owner’da veri silmeden durur.
8. UAT kopyasını bu merge’den sonra ayrı, temiz bir DB ile hazırla; mevcut UAT `.local` verisini kopyalama/çalıştırma.
9. Push/PR/deploy yok.

## 5. Entegrasyon riskleri

| Risk | Not |
|---|---|
| Dirty parent index | Reset/clean yapma; temiz worktree kullan. |
| SQL Server uygulanmadı | Migration yalnız üretildi; decimal/concurrency kanıtı değil. |
| Mevcut >1 Owner | Migration durur; operatör kararı gerekir. |
| UAT `.local` | Kullanıcıya ait; kopyalama/seed/reset yok. |
| Rate-limit pencereleri | Smoke temiz DB ve taze süreç ister. |
| Screenshot/QA kanıtı | Kod commit’iyle birlikte commit edilmeli; UAT DB kanıtı değildir. |
