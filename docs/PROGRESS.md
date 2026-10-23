> **Current 2026-10-23:** Hosted CI passed; real SQL Server passed 8/8 with no skips for `862c59c`. Manual user UAT remains pending. See [final verification](FINAL_VERIFICATION_20261023.md). Earlier NOT RUN/failure statements below are historical records.

# PROGRESS

Güncelleme: **2026-10-22** (Europe/Istanbul).

## Durum: Stage 3–5 tamam (SQLite + sunum); SQL Server/UAT/GitHub açık

Kullanıcı bekleme kapısını kaldırdı. Owner/DelegatedApprover/Customer rol modeli,
tüm finansal taleplerin onay zorunluluğu, kişi/operasyon/para birimi limitleri,
mutabakat/istisna, timeline/dekont ve demo merkezi uygulandı. Yerel build/test/
smoke geçiyor. **Üretim veya SQL Server doğrulaması değildir.**

## Çalışma alanı

- Worktree: `.../.dsh-worktrees/dsh-mvh4pdzy-u7y7j4`
- Dal: `dsh/dsh-mvh4pdzy-u7y7j4`
- Son QA/entegrasyon commit: `35f247e` (integration/local-development fast-forward)
- Backend: `11684b2`; dashboard: `d2e90e3`; ilk smoke: `7340a1c`
- Önceki ownership/timeline/reconciliation: `cac7f00`
- Ana kopya / `integration/local-development`: **dokunulmadı**
- Demo: API `http://127.0.0.1:5180`, dashboard `http://127.0.0.1:5181`
- `.local/` git dışı; 5080/5081 süreçlerine dokunulmadı.

## Tamamlanan dilimler

1. **Owner/üyelik:** tek seferlik localhost setup, PendingActivation kayıt,
   aktivasyon, rol değişimi, son Owner koruması, disable'ın delegasyonları
   kapatması, public kayıtta Owner yasağı.
2. **Onay modeli:** transfer/deposit/withdrawal/account opening için istisnasız
   Pending; oluşturmada para hareketi yok; risk yalnız bilgi.
3. **Delegasyon:** operasyon/para birimi/limit (`MaxAmount` dahil), opsiyonel
   günlük limit ve expiry, anında iptal; her onayda DB kontrolü. 100.000,00
   onaylanır, 100.000,01 Owner'a kalır.
4. **Self-approval:** Owner dahil kendi talebini onaylama/reddetme engeli.
5. **Rezervasyon:** bekleyen giden talepler available balance'ı düşürür; iki
   talep overcommit edemez; iptal/red serbest bırakır; onayda yeniden kontrol.
6. **Sahiplik:** `Customer.UserId`, `/api/me/*`, IDOR 404, müşteri timeline/
   receipt, maskeleme, Europe/Istanbul, DEMO uyarısı.
7. **Mutabakat:** CSV preview/import, SHA-256+scope mükerrer, deterministik
   eşleşme sınıfları, pending ayrımı, formül-safe export, istisna akışı ve
   farklı onaycı; bakiye düzeltmesi yok.
8. **Demo merkezi:** Development-only, Owner-only, deterministic/idempotent seed,
   scoped reset.
9. **Dashboard:** Setup/Login/Register; Owner özet/onay/üyeler/yetkiler/
   müşteriler/hesaplar/talep/audit/mutabakat/demo/rapor; Delegated kuyruk;
   Customer hesaplar/transfer/talepler/dekont.
10. **Doğrudan hareket uçları:** 400 ile onay akışına yönlendirme.
11. **Hesap ürünleri / ilk hesap:** Vadesiz/Vadeli/Yatırım türleri, ad/tür
    kalıcılığı, Hesaplarım sekmeleri, hesapsız müşteriden açılış talebi,
    yalnız sahibin ad düzenlemesi, hesap detayından para yatır/çek talepleri;
    SQL Server additive migration ve SQLite idempotent upgrade script'i.
12. **Stage 3 — çift kayıtlı muhasebe (bu dilim):** append-only
    `JournalEntry`/`JournalLine`, tek para birimi, pozitif bacaklar, debit=credit;
    yatırma/çekme/transfer/hesap açılışı onay işlemiyle aynı DB transaction'ında;
    `SourceKey` unique ile replay engeli; onay hatasında bakiye/legacy kayıt/
    onay/timeline/audit tam geri alma; Owner-only gap tabanlı baseline preview/post
    (tam journal kapsamındaki hesap ve mevcut baseline atlanır; fark yönüne göre
    debit/credit; para birimi bazında toplamlar); salt okunur mizan/journal API'si
    ve `/Admin/Accounting` ekranı; additive SQL Server migration + SQLite upgrade
    script; 18 muhasebe testi + baseline/onay yarış testi.
13. **Stage 4 — operasyon kontrol merkezi (bu dilim):** UTC yaş → Istanbul
    gösterim, yapılandırılabilir demo SLA (30 dk, `age >= SLA` gecikmiş),
    geciken/en eski/atanmamış/bana atanan özetleri, tür/para birimi/durum/yaş/
    atama filtreleri, sınırlı sayfalama, canlı delegasyona göre kapsamlı
    görünürlük, güvenli atama (onay yetkisi vermez), çözülmemiş mutabakat
    farkları ve `/Admin/Operations` ekranı; 8 deterministik saat/atama testi.
14. **Stage 5 — portföy sunumu (bu dilim):** İngilizce README + Türkçe özet,
    Mermaid mimari/onay/journal/operasyon akışları, seçilmiş ASVS temalı güvenlik
    kanıt matrisi, gerçek sentetik ekran görüntüleri, 3 dakikalık demo senaryosu
    (video çekilmedi), TR/EN CV maddeleri ve 30 sn pitch, final gaps ve Stage 5
    raporu. Push/release/CV yayını yapılmadı; GPT kontrollü.
15. **Dashboard UX/QA (önceki dilim):** rol etiketleri Türkçe; Delegasyon limitinin
    dahil olduğu, operasyon/para birimi/süre ve “Süresi doldu” durumu görünür;
    geçmiş expiry sunucuda reddedilir; yerel datetime → UTC; müşteri ilk hesap
    boş durumu ve hesap açılışı CTA’sı; talep formu hesapsız müşteride
    `AccountOpening` varsayılanı; dar ekranda tablolar kart içinde kayar;
    form label/focus düzeltmeleri; PendingActivation giriş mesajı; İngilizce
    servis mesajları için güvenli Türkçe eşleme.

## Son doğrulanan sonuçlar

- Build: **0 uyarı, 0 hata**.
- xUnit: **178 geçti, 0 kaldı, 0 atlandı**; SQL Server integration **8 atlandı (koşulmadı)**.
- Smoke: **280 kontrol geçti** (temiz Development DB; Stage 3 öncesi kapsam).
- Yeni tekrarlanabilir E2E: **104/104** (`scripts/stage3_stage4_e2e.py`; Stage 3 **25/25**, Stage 4 **24/24**, finansal 25/25, operasyon 12/12, CSV/demo 9/9, SQLite upgrade 9/9).
- Önceki ad-hoc Stage 3 E2E: **25/25**, Stage 4 E2E: **24/24** (izole SQLite 5198/5199; tarihsel kanıt).
- Stage 5: README/mimari/güvenlik matrisi/demo script/CV/final gaps yazıldı; video **çekilmedi**, push **yapılmadı**.
- SQL Server migration script'i üretildi; **gerçek SQL Server'a uygulanmadı**.
- NuGet audit: bilinen açık yok.
- `git diff --check`: temiz.
- Kanıt: [`evidence/`](evidence/), [`TEST_REPORT.md`](TEST_REPORT.md).

## Açık sınırlar / sıradaki işler

- **SQL Server:** dört yeni migration yalnız üretildi; apply/decimal/concurrency
  doğrulanmadı. Legacy `Admin=1`/`User=2` rol eşlemesi operatör işidir.
  Hesap adı/türü additive migration'ı ve SQLite upgrade script'i UAT'e
  uygulanmadı.
  `20261021011500_EnforceSingleOwner` mevcut >1 Owner verisinde **silme yapmadan**
  durur; operatör kararı gerekir.
- **Manuel UAT:** [`UAT_CHECKLIST.md`](UAT_CHECKLIST.md) doldurulmadı.
- **Ekran görüntüsü:** gerçek dashboard'dan masaüstü + dar ekran (13 görsel)
  alındı (`docs/screenshots/`); UI değişince yenilenmeli.
- **Yeni üye/sunucu yaşam döngüsü:** yeni üye kayıt→aktivasyon→login akışı
  sentetik olarak doğrulandı. Foreground `run_local.py` SIGTERM’de çocukları
  kapatıyor; oturumdan bağımsız çalışma için `scripts/start_local_detached.py`
  eklendi. UAT servisi bu araçla durdurulmadı.
- **FX/gün sonu takvimi:** yok; rapor UTC, dekont Europe/Istanbul.
- **Dağıtık throttle/secret manager/observability/audit retention:** yok.
- **VS Code GUI SDK:** kullanıcı launchd adımını çalıştırmalı.
- İsteğe bağlı sonraki geliştirme: manuel UAT, SQL Server integration test
  ortamı, dağıtık rate-limit store. Bunlar kullanıcı onayı olmadan başlatılmadı.

## Harness notu

- Worktree dosyaları macOS `hidden` bayrağıyla oluşturuldu; `appsettings.json`
  görünmez olduğunda `chflags -R nohidden .` uygulandı.
- Sandbox paylaşımlı Roslyn derleyicisini engelledi; doğrulama
  `DOTNET_CLI_USE_MSBUILD_SERVER=0` ve
  `-p:UseSharedCompilation=false -m:1 -nr:false` ile yapıldı.
- Commit'ler `.git/worktrees` erişimi için sandbox yükseltmesi gerektirdi;
  global git kimliği değiştirilmedi; commit başına
  `-c user.name=Codex -c user.email=codex@local.invalid` kullanıldı.

## Teslim belgeleri

- [`DEVELOPMENT_COMPLETION_REPORT.md`](DEVELOPMENT_COMPLETION_REPORT.md)
- [`TEST_REPORT.md`](TEST_REPORT.md)
- [`VERIFICATION.md`](VERIFICATION.md)
- [`UAT_CHECKLIST.md`](UAT_CHECKLIST.md)
- [`DASHBOARD_PRODUCT_BRIEF.md`](DASHBOARD_PRODUCT_BRIEF.md)
- [`PORTFOLIO_NOTES.md`](PORTFOLIO_NOTES.md)
- [`migrations/README.md`](migrations/README.md)
- [`RECONCILIATION_FORMAT.md`](RECONCILIATION_FORMAT.md)
- [`INTEGRATION_HANDOFF.md`](INTEGRATION_HANDOFF.md)
- [`adr/0001-payment-and-role-policy.md`](adr/0001-payment-and-role-policy.md)
- [`../README.md`](../README.md)

## 2026-10-21 — Yeni kullanıcı kapsamı (öncelik)

Önceki işler korunuyor; aşağıdaki kapsam sıradaki teslimdir:

- **Yeni üye akışı:** public register → Owner activation → dashboard login →
  `/Index` → `/Customer/Home` → ilk hesap talebi; yeni üyenin `Customer.UserId`
  bağlantısı, session cookie ve JWT role/status yönlendirmesi.
- **Hesap türleri:** `Vadesiz`, `Vadeli`, `Yatırım`; `Hesaplarım` içinde
  `Tümü / Vadesiz / Vadeli / Yatırım` sekmeleri, her sekmede boş durum ve
  görünür **Hesap Aç**.
- **Hesap meta verisi:** kullanıcı tarafından hesap adı + tür; ad yalnız sahibi
  tarafından sonradan düzenlenebilir (boşluk/uzunluk/XSS/IDOR kuralları).
- **İlk hesap:** mevcut hesap seçimine bağlı olmadan açılış talebi; Owner veya
  yetkili onayı sonrası hesabın listede kalıcı görünmesi.
- **Para yatır/çek:** her hesap detayından talep; tutar/açıklama/özet; bekleyen/
  onaylanan/reddedilen takip; bakiye yalnız onayda değişir; self-approval,
  idempotency ve rollback korunur.
- **Vadeli/Yatırım:** yalnız hesap türü/yönetim demosu; faiz, tahakkuk, alım-satım
  veya kur getirisi simüle edilmez. Kısa demo açıklaması gösterilir.
- **Migration:** SQL Server için additive migration; mevcut SQLite verisini
  koruyan idempotent yükseltme script'i. UAT DB'sine uygulanmaz.

## 2026-10-22 — Stage 1: SQL Server verification preparation — PREPARED / BLOCKED

- Roadmap recorded in [`PORTFOLIO_ROADMAP.md`](PORTFOLIO_ROADMAP.md); only Stage 1
  was worked on.
- Environment read-only check: macOS ARM64; Docker/Podman/Colima/sqlcmd absent.
  Microsoft supports SQL Server Linux containers only on x86-64 Linux hosts and
  does not support Rosetta/QEMU translation; no emulation was presented as valid.
- Added `DigitalBanking.IntegrationTests` with 8 environment-gated SQL Server
  tests (migration apply/defaults/index, decimal boundaries, rollback, separate
  connection concurrency, idempotency, self/live authority, IDOR/rename,
  reconciliation replay/exception). They compile but are **skipped/not run**
  without a disposable SQL Server.
- Added `scripts/run_sqlserver_integration.sh`; it refuses to run without both
  the connection env and explicit destructive allow flag, and prints `NOT RUN`
  otherwise. Random `DigitalBanking_IT_<hex>` DB is created/dropped only by the
  fixture; existing/UAT DBs are never touched.
- Build 0 warnings/0 errors; main xUnit **150 passed**; smoke **280 passed**;
  integration **8 skipped, 0 passed** — SQL Server remains unverified.
- Reports: [`SQLSERVER_VERIFICATION.md`](SQLSERVER_VERIFICATION.md),
  [`STAGE_01_REPORT.md`](STAGE_01_REPORT.md),
  [`evidence/2026-10-22-sqlserver-integration.txt`](evidence/2026-10-22-sqlserver-integration.txt).
- **Stage 1 status: prepared / environment-blocked**; real SQL Server still not
  verified.
- **Stage 2 status at Stage 1 snapshot: not started.** The later Stage 2 entry
  records preparation; GitHub Actions is **NOT RUN**.

## 2026-10-22 — Stage 2: GitHub Actions CI preparation

- Added `.github/workflows/ci.yml` (push/PR): pinned checkout v7.0.1,
  setup-dotnet v6.0.0, upload-artifact v7.0.2; build, main 150-test gate, explicit
  8 SQL Server skips, and separate isolated SQLite smoke job.
- Added `sqlserver-integration.yml` manual-only workflow: supported x86-64
  `ubuntu-24.04`, pinned `mcr.microsoft.com/mssql/server:2022-CU27-ubuntu-22.04`,
  health check, EULA manual input, environment/secret prerequisite, random
  `DigitalBanking_IT_<hex>` DB, TRX 8/8 no-skip gate.
- Added safe CI scripts: ephemeral credentials, isolated smoke with own-PID
  cleanup, TRX summaries, SQL skip gate, workflow guardrails.
- Verified: YAML parse, workflow guardrails PASS, Bash/Python syntax, solution
  build 0 warnings/0 errors, main **150 passed**, SQL integration **8 skipped —
  NOT RUN**, prior isolated SQLite smoke **280 passed**. The CI smoke wrapper was
  not run end-to-end against the existing developer `.local`; its guard refuses
  to overwrite.
- **GitHub Actions itself has NOT run**; no push/PR/dispatch/deploy.
- Reports: [`CI_GUIDE.md`](CI_GUIDE.md), [`STAGE_02_REPORT.md`](STAGE_02_REPORT.md),
  [`SECURITY_MATRIX.md`](SECURITY_MATRIX.md).
- Stage 3 was not started at that point; it is completed later in this file.

## 2026-10-22 — Stages 3–5 execution started

- User removed stage stop gates and approved Step 3 → 4 → 5 sequentially.
- Task/continuation record: [`REMAINING_DEVELOPMENT_TASK.md`](REMAINING_DEVELOPMENT_TASK.md).
- Stage 1 real SQL Server, Stage 2 GitHub Actions/hosted run and EULA remain
  pending/environment-blocked; they are not blocking development.
- Stage 3 completed on synthetic SQLite/local: balanced journal, atomic approval
  integration, Owner baseline, read-only accounting view, additive migration,
  178 unit tests and 25/25 live E2E checks; the baseline was later corrected to
  gap-based, with new regression/race tests. SQL Server application and GitHub
  Actions remain NOT RUN. Report: [`STAGE_03_REPORT.md`](STAGE_03_REPORT.md).
- Stage 4 completed on synthetic SQLite/local: operations centre, SLA/age,
  assignment, reconciliation gaps, 178 unit tests and 24/24 live E2E checks.
  The same 25/25 + 24/24 surface is now reproducible with
  `scripts/stage3_stage4_e2e.py` (**104/104**, financial 25/25, CSV/demo 9/9,
  upgrade 9/9). Report: [`STAGE_04_REPORT.md`](STAGE_04_REPORT.md). SQL Server
  and GitHub Actions remain NOT RUN.
- Stage 5 completed as documentation/portfolio deliverable. Final GitHub push,
  release and CV publication remain **GPT-controlled** and were **not done**.
  SQL Server, GitHub Actions, UAT and EULA remain unverified/open.

## 2026-10-23 — SQL Server FK migration fix (real run 39984232994)

- Gerçek SQL Server 2022 run'ında 8/8 test fixture migration'da fail etti:
  `FK_ReconciliationExceptions_ReconciliationRows_RowId` multiple cascade paths.
- Model, ilk reconciliation migration'ı, tüm designer'lar ve snapshot
  `ReconciliationRows -> ReconciliationExceptions` FK'si **NO ACTION** olacak
  şekilde hizalandı. Batch -> Rows ve Batch -> Exceptions CASCADE kalıyor.
- Demo reset artık exception -> row -> batch sırasıyla siliyor; SQLite cleanup
  regression'ı eklendi. SQL integration testleri journal tablo/index, assignment
  şema, balanced journal, rollback, concurrency ve cleanup sırasını doğrulayacak
  şekilde güçlendirildi; test sayısı **8** kaldı.
- Yerel: **178 unit passed**, 8 SQL skipped (PASS sayılmaz), **104/104 E2E**,
  build 0/0. SQL Server DDL render edildi, uygulanmadı.
- Rapor: [`SQLSERVER_FIX_REPORT.md`](SQLSERVER_FIX_REPORT.md).
  Mevcut kurulum için koşullu FK onarım script'i:
  [`sqlserver/2026-10-23-reconciliation-row-fk-noaction.sql`](sqlserver/2026-10-23-reconciliation-row-fk-noaction.sql).
- UAT/ana/parent/GPT snapshotlarına dokunulmadı; push/deploy/gh dispatch/EULA
  yok. GPT fix'i inceleyip commit/push ve EULA onaylı run'ı tekrar çalıştıracak.

### 2026-10-23 ikinci SQL run — Accounts.Currency

- Run 39988214854: migration geçti; 1 passed / 7 failed / 0 skipped.
- İki bulgu: `SqlServerFixture.AddActorAsync` kısa rolde 42 karakter substring
  hatası; `Accounts.Currency` migration DDL'de yok.
- `AddCurrencyColumnToAccounts` boş migration'ı düzeltildi; eski migration
  geçmişi için `RepairMissingAccountsCurrency` koşullu additive migration eklendi.
- Eski bakiyeler açıkça TRY varsayılır (FX yok); non-TRY legacy hesap operatör
  düzeltmesi gerektirir. Test fixture username'i bounded üretir.
- 8 SQL testi migration/table/index/FK/currency/journal/rollback/cleanup
  assertion'larıyla güçlendirildi; sayı 8.
- Yerel: 178 unit, 8 SQL skipped (NOT RUN), 104/104 E2E, 280/280 smoke.
- Rapor: `SQLSERVER_FIX_REPORT.md`; kanıt:
  `evidence/2026-10-23-sqlserver-currency-fix.txt`.


### 2026-10-23 üçüncü SQL run — kontrollü journal çakışması

- Run 39989641833: 7 passed / 1 failed / 0 skipped. Kalan hata concurrent approval
  sırasında raw `DbUpdateException`/SQL 2601 `IX_JournalEntries_SourceKey`.
- `PaymentApprovalService` yalnız SQL 2601/2627 + journal SourceKey (ve SQLite
  karşılığı) unique çakışmasını rollback sonrası `ConcurrencyException`'a çevirir;
  diğer DB hataları aynen fırlatılır.
- SQLite duplicate rollback testi controlled conflict + balance/approval/journal/
  audit assertion'larıyla güncellendi; SQL concurrent testi DbUpdateException'ı
  reddeder, tek journal/bakiye/approval/audit doğrular. 8 SQL testi korunuyor.
- Yerel: 178 unit, 8 SQL skipped (NOT RUN), 104/104 E2E, 280/280 smoke.

## 2026-10-23 — Dashboard visual refinement

Owner/customer shell, account cards, quick actions, queue hierarchy, responsive navigation and consistent forms/tables refined. Scope: presentation only. Independent178unit/280smoke passed; desktop1440/mobile390 visual snapshots and menu behavior reviewed. Final104 E2E and hosted CI recorded separately. Manual user UAT remains last and pending. See DASHBOARD_REFINEMENT_REPORT.md.
