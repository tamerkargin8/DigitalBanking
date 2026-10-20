# PROGRESS

Güncelleme: **2026-10-20/21** (Europe/Istanbul).

## Durum: üç rollü backend + dashboard + mutabakat tamam; UAT/SQL Server açık

Kullanıcı bekleme kapısını kaldırdı. Owner/DelegatedApprover/Customer rol modeli,
tüm finansal taleplerin onay zorunluluğu, kişi/operasyon/para birimi limitleri,
mutabakat/istisna, timeline/dekont ve demo merkezi uygulandı. Yerel build/test/
smoke geçiyor. **Üretim veya SQL Server doğrulaması değildir.**

## Çalışma alanı

- Worktree: `.../.dsh-worktrees/dsh-mvh4pdzy-u7y7j4`
- Dal: `dsh/dsh-mvh4pdzy-u7y7j4`
- Backend commit: `11684b2`
- Dashboard commit: `d2e90e3`
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

## Son doğrulanan sonuçlar

- Build: **0 uyarı, 0 hata**.
- xUnit: **147 geçti, 0 kaldı, 0 atlandı**.
- Smoke: **272 kontrol geçti** (temiz Development DB, eşzamanlı Owner setup ve stale Setup POST dahil).
- NuGet audit: bilinen açık yok.
- `git diff --check`: temiz.
- Kanıt: [`evidence/`](evidence/), [`TEST_REPORT.md`](TEST_REPORT.md).

## Açık sınırlar / sıradaki işler

- **SQL Server:** üç yeni migration yalnız üretildi; apply/decimal/concurrency
  doğrulanmadı. Legacy `Admin=1`/`User=2` rol eşlemesi operatör işidir.
  `20261021011500_EnforceSingleOwner` mevcut >1 Owner verisinde **silme yapmadan**
  durur; operatör kararı gerekir.
- **Manuel UAT:** [`UAT_CHECKLIST.md`](UAT_CHECKLIST.md) doldurulmadı.
- **Ekran görüntüsü:** gerçek dashboard'dan 7 ekran alındı (`docs/screenshots/`);
  UI değişince yenilenmeli.
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
