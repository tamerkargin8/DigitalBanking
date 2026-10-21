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
12. **Dashboard UX/QA (bu dilim):** rol etiketleri Türkçe; Delegasyon limitinin
    dahil olduğu, operasyon/para birimi/süre ve “Süresi doldu” durumu görünür;
    geçmiş expiry sunucuda reddedilir; yerel datetime → UTC; müşteri ilk hesap
    boş durumu ve hesap açılışı CTA’sı; talep formu hesapsız müşteride
    `AccountOpening` varsayılanı; dar ekranda tablolar kart içinde kayar;
    form label/focus düzeltmeleri; PendingActivation giriş mesajı; İngilizce
    servis mesajları için güvenli Türkçe eşleme.

## Son doğrulanan sonuçlar

- Build: **0 uyarı, 0 hata**.
- xUnit: **150 geçti, 0 kaldı, 0 atlandı**.
- Smoke: **280 kontrol geçti** (temiz Development DB; eşzamanlı Owner setup, stale Setup POST, yeni üye aktivasyon/login, hesap metadata/ad/IDOR ve ilk hesap akışı dahil).
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
