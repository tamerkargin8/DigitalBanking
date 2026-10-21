# Development completion report — Owner model, approvals, reconciliation and dashboard

- Tarih: **2026-10-20** (Europe/Istanbul)
- Ortam: macOS ARM64, .NET SDK **10.0.402** (runtime 10.0.13), EF Core **10.0.3**
- Dal: `dsh/dsh-mvh4pdzy-u7y7j4` (izole worktree)
- Backend: `11684b2` — Owner/delegation/approval request model
- Dashboard: `d2e90e3` — Owner, delegated approver and customer Razor Pages
- Önceki dilim: `cac7f00` — ownership, timeline, receipts, reconciliation backend
- QA/final düzeltmeleri (single-owner invariant, Setup/rate-limit, approval ownership/atomicity, demo auth): **commit bekliyor**; manifest `docs/INTEGRATION_HANDOFF.md`
- Ana kopya `../DigitalBanking` / `integration/local-development` **değiştirilmedi**
- Demo: API `http://127.0.0.1:5180`, dashboard `http://127.0.0.1:5181`

## Teslim kapsamı

Kullanıcı bekleme kapısını kaldırdı ve şu üç özelliği onayladı: gün sonu
mutabakat/istisna yönetimi, işlem timeline'ı + yazdırılabilir demo dekont ve
Development-only demo senaryo merkezi. Ardından rol modelini **Owner /
DelegatedApprover / Customer** olarak kesinleştirdi; tüm finansal taleplerin
onaya tabi olmasını, limitlerin kişi/operasyon/para birimi bazında verilmesini ve
Owner'ın kendi talebini dahi onaylayamamasını istedi. Bu rapor uygulanan son
durumu kaydeder.

## Tamamlananlar

### 1. Üyelik, Owner ve oturum

- Public kayıt yalnız `username`, `email`, `firstName`, `lastName`, `password`,
  `confirmPassword` alır; istemci rol/limit/sahiplik/bakiye gönderemez.
- Yeni kayıt **PendingActivation**; Owner aktive edene kadar login `403`.
- İlk Owner yalnız **localhost + Development + Owner yokken** `POST /api/setup/owner`
  ile oluşturulur. Owner oluşunca uç `409` ile kapanır. Public kayıt Owner yaratamaz.
- Son Owner devre dışı bırakılamaz, rolü değiştirilemez; rol endpoint'i yalnız
  Customer/DelegatedApprover atar, Owner atayamaz.
- Aktivasyon Customer profili oluşturur. Disable, delegasyonları kapatır.
- Login PBKDF2-SHA256 600k, legacy rehash, 5 hatalı deneme/5 dk kilit ve IP rate
  limit ile korunur. Dashboard oturumu HttpOnly/SameSite=Strict ve 30 dk idle.

### 2. Onay modeli — istisnasız maker-checker

- Transfer, deposit, withdrawal ve account opening **her zaman** `Pending`
  oluşur; oluşturma anında para hareket etmez.
- Düşük risk dahil hiçbir talep otomatik onaylanmaz. Risk seviyesi yalnız bilgi
  ve kuyruk filtresidir.
- Owner tüm talepleri onaylayabilir/reddedebilir; **kendi oluşturduğu talebi
  onaylayamaz/reddedemez**.
- DelegatedApprover yalnız aktif delegasyonunun kapsadığı operasyon, para birimi
  ve limit içinde onaylar. Limit **dahildir**: 100.000,00 TRY onaylanır,
  100.000,01 TRY Owner'a kalır. Opsiyonel günlük kümülatif limit, UTC günü ve
  aynı operasyon/para birimi için uygulanır.
- Yetki her onayda veritabanından okunur: rol, durum, `IsActive`, `ExpiresAt`,
  limit ve günlük kullanım. İptal/rol değişikliği eski JWT ile hemen etkisizdir.
- Onay anında hesap, sahiplik, para birimi, bakiye, amount scale ve güncel durum
  yeniden doğrulanır. `RowVersion` çift onayı engeller.
- Bekleyen giden transfer/withdrawal **available balance** rezervasyonu yapar;
  iki talep bakiyeyi aşamaz. İptal/red rezervasyonu serbest bırakır.
- Owner'ın operasyonel talebi de aynı self-approval kontrolüne tabidir.
- Kuyruk filtreleri: `Pending`, request type, currency ve delegated görünümü;
  Owner tüm durumları görür. Müşteri yalnız kendi taleplerini ve red gerekçesini.

### 3. Limit/Yetki yönetimi

- `ApprovalDelegation`: kullanıcı + operasyon + para birimi + `MaxAmount`,
  opsiyonel `DailyLimit`, `ExpiresAt`, `IsActive`, `RowVersion`.
- Owner CRUD + anında deactivate. DelegatedApprover bu uçlara ve rol
  yönetimine erişemez.
- Reconciliation exception onayı operasyon iznine bağlıdır; parasal limit
  uygulanmaz (tutarsız kayıt operasyonel kapatılır, bakiye değişmez).

### 4. Sahiplik, timeline ve dekont

- `Customer.UserId` (+ SQL Server filtered unique index) ile müşteri–login
  sahipliği kuruldu. `/api/me/*` yalnız kendi verisini döner; IDOR `404`.
- `PaymentTimelineEvent` gerçek kalıcı olaylardan timeline üretir; legacy
  kayıtlar için kontrollü fallback vardır.
- Dekont yalnız tamamlanmış ödemede tamamlanmış veri döner; pending/rejected
  durumda `IsCompleted=false` ve açıklama. Hesap numaraları maskeli, saat
  Europe/Istanbul, açık **DEMO — GERÇEK BANKACILIK DEKONTU DEĞİLDİR** uyarısı.
- Tarayıcı print-to-PDF kullanılır; PDF kütüphanesi yok.

### 5. Gün sonu mutabakat ve istisnalar

- CSV: UTF-8 (BOM kabul), katı başlık, 2 MB / 5.000 satır sınırı, tırnaklı alan,
  2 hane decimal, TRY/USD/EUR/GBP, UTC tarih, Debit/Credit doğrulaması.
- Önizleme kalıcı batch üretir; import ayrı ve idempotent adımdır. Dosya SHA-256
  + scope anahtarı ile mükerrer yükleme algılanır.
- Deterministik referans eşleme; `Matched`, `OnlyExternal`, `OnlySystem`,
  `AmountMismatch`, `CurrencyMismatch`, `DuplicateReference`, `Invalid`.
- Bekleyen onaylar "only system"den ayrı sayılır. Import sonrası batch sonucu
  değişmez.
- İstisna: `Open → InReview → ResolutionProposed → Closed/Rejected`; yeniden
  açma. Öneriyi yapan kendi çözümünü onaylayamaz/reddedemez. Close **bakiye
  düzeltmez**; yalnız operasyonel kayıttır.
- Export formül enjeksiyonuna karşı `= + - @` hücrelerini `'` ile nötrler.

### 6. Development-only demo merkezi

- Owner-only, ayrıca service ve controller düzeyinde Development kontrolü;
  Development dışında `404`.
- Sentetik senaryolar: başarılı transfer, yetersiz bakiye, yüksek tutarlı
  maker-checker, self-approval engeli, mükerrer istek, red, eşleşen/eşleşmeyen
  mutabakat.
- Seed deterministik/idempotent; reset yalnız `IsDemo` işaretli ödeme, hareket,
  timeline ve batch kayıtlarını kaldırır, kullanıcı/hesap/UAT verisini korur.
- Örnek CSV indirme uçları dinamik ve sentetiktir.

### 7. Dashboard

- Türkçe, lacivert/nefes alan nötr tasarım, yerel Bootstrap, mobil/erişilebilir
  temel; boş/yükleniyor/hata/başarı durumları.
- Owner: özet, onay kuyruğu/detay/dekont, üyelik aktivasyon/rol, **Yetkiler &
  Limitler**, müşteri/hesap, operasyonel talep, audit, mutabakat liste/detay/
  istisna/örnek/export, demo merkezi, rapor.
- DelegatedApprover: yalnız uygun kuyruk ve kendi incelediği kayıtlar; yönetim
  sayfaları reddedilir.
- Customer: ana sayfa para birimi bazlı kartlar, hesaplar, transfer sihirbazı
  (ön özet), talepler, ödeme/timeline, dekont.
- Rol yönlendirmeleri ve guard'lar sunucu tarafında; buton gizleme güvenlik
  sınırı değildir.

### 8. Doğrudan hareket uçları

`POST /api/accounts/transfer`, `/api/accounts/open`,
`/api/transactions/deposit`, `/api/transactions/withdraw` bilinçli olarak `400`
döner ve onay talebi yolunu gösterir. Onay akışı atlanamaz.

## Doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Build | **0 uyarı, 0 hata** |
| xUnit | **150 geçti, 0 başarısız, 0 atlandı** |
| Smoke | **280 kontrol geçti** |
| NuGet audit | bilinen açık yok (mevcut kaynaklar) |
| `git diff --check` | temiz |

Test envanteri ve smoke kapsamı: [`TEST_REPORT.md`](TEST_REPORT.md).
Kanıt: [`evidence/`](evidence/).

## Şema / migration

- `20261020212050_AddCustomerOwnershipTimelineReconciliation`: `Customer.UserId`,
  filtered unique index, timeline, reconciliation tabloları, `CorrelationId`,
  `IsDemo`, string uzunlukları.
- `20261020213624_AddOwnerDelegationAndApprovalRequests`: User ad/e-posta/
  durum/aktivasyon alanları, `ApprovalDelegations`, `RequestType`,
  `CreatedAccountId`, `RequestedCustomerId`, `RequestedInitialBalance`.
- `20261021011500_EnforceSingleOwner`: `Users` üzerinde `[Role] = 3` filtered
  unique index. İki eşzamanlı setup'tan yalnız biri commit eder; kaybeden `409`
  alır. Mevcut DB'de >1 Owner varsa migration **veri silmeden** açık hata verir;
  operatör önce hangi hesabın Owner kalacağına karar verir. Başlangıçta da
  Serilog warning üretilir.
- `20261021023000_AddAccountMetadata`: `AccountName` / `AccountType` ve
  `PaymentApprovals.RequestedAccountName` / `RequestedAccountType`; additive,
  eski hesaplar Vadesiz varsayılır. SQLite için ayrı idempotent upgrade script'i
  (UAT'e uygulanmadı) mevcuttur.
- Eski `Admin=1` / `User=2` rolleri **bilinçli olarak geçersizdir**; otomatik
  Owner/Customer dönüşümü yapılmaz. Operatör eşlemesi gerekir.
- SQL Server migration'ları **uygulanmadı/doğrulanmadı**. SQLite Development
  `EnsureCreated` kullanır ve atılabilir. Ayrıntı:
  [`migrations/README.md`](migrations/README.md).

## Bilinen sınırlar

1. SQL Server bağlantısı/migration/gerçek SQL Server concurrency testi yapılmadı;
   eşzamanlı Owner yarışı ayrı SQLite dosyaları ve iki context ile test edildi.
2. Manuel UAT yapılmadı; [`UAT_CHECKLIST.md`](UAT_CHECKLIST.md) kullanıcı içindir.
3. Gerçek tarayıcı ekran görüntüleri `docs/screenshots/` altında alındı;
sahte/editleme yok. Manuel UAT yine yapılmadı.
4. FX yok; rapor tarihleri UTC; bankacılık gün sonu takvimi yok.
5. Login throttle bellek içi; dağıtık ortam için paylaşımlı store gerekir.
6. Üretim secret manager, WAF, observability, retention/immutability, sızma/
   yük/erişilebilirlik denetimi ve uyumluluk iddiası yoktur.
7. VS Code GUI SDK adımı kullanıcı tarafından doğrulanmalıdır.

## Sonuç

Backend, iş kuralları, güvenlik kontrolleri, mutabakat/timeline ve üç rollü
dashboard bu ortamda çalışır ve test edilmiştir. Proje **production-ready
değildir** ve SQL Server doğrulanmamıştır; bu sınırlar README ve test raporunda
açıkça belirtilmiştir.
