# Development completion report — backend, rules and tests

- Tarih: **2026-10-20** (Europe/Istanbul)
- Ortam: macOS ARM64, .NET SDK **10.0.402** (runtime 10.0.13), EF Core **10.0.3**
- Dal: `dsh/dsh-mvh4pdzy-u7y7j4` (izole worktree)
- Taban commit: `0ceead9`
- Backend commit'i: `2316572`
- Dashboard uyumluluk + smoke commit'i: `095a31b`
- Ana kopya `../DigitalBanking` / `integration/local-development` **değiştirilmedi**
- Demo: API `http://127.0.0.1:5180`, dashboard `http://127.0.0.1:5181`

## Teslim kapısı

Bu teslim **backend + bankacılık kuralları + güvenlik + test + rapor** aşamasıdır.
Kullanıcının güncel talimatıyla kapsamlı dashboard ekran/tasarım geliştirmesi
**başlatılmadı**. Mevcut dashboard yalnızca değişen API sözleşmeleriyle çalışır
kalması için zorunlu uyumluluk düzeltmeleri aldı. Dashboard ürün gereksinimleri
[`DASHBOARD_PRODUCT_BRIEF.md`](DASHBOARD_PRODUCT_BRIEF.md) içinde kayıtlıdır.

## Tamamlananlar

### 1. Kimlik, roller ve oturum

- `ActorContext` yalnız doğrulanmış JWT claim'lerinden üretiliyor
  (`NameIdentifier` = Guid, `Name`, `Role`).
- `PaymentApproval.RequestedByUserId`/`ReviewedByUserId` **int → Guid**; istemci
  gövdesinden kullanıcı kimliği kabul edilmiyor.
- Controller + service katmanında rol kontrolü: para hareketleri, onay, audit ve
  kullanıcı yönetimi Admin; standart `User` salt-okunur.
- Login: PBKDF2-SHA256 600k + legacy SHA-256 rehash, 8+ harf/rakam şifre
  politikası, kullanıcı adı başına 5 hatalı deneme/5 dk kilit, IP başına login
  rate limit. Şifre/token audit veya ekrana yazılmıyor.
- Dashboard session: HttpOnly/SameSite=Strict, 30 dk idle, çıkışta tam temizlik,
  401'de girişe yönlendirme.

### 2. Bankacılık kuralları

- **Maker-checker**: düşük risk (< 50.000) otomatik onay; orta/yüksek/kritik
  `Pending`; farklı Admin onayı/reddi; talep sahibi kendi talebini inceleyemez.
- **Doğrudan transfer**: yalnız Admin ve 50.000 altı; üstü onay akışına zorunlu.
- **Risk**: Low/Medium/High/Critical eşikleri gerçek workflow'a bağlı.
- **Para**: decimal, 2 hane; fazla küsurat reddi; TRY/USD/EUR/GBP; transfer ve
  onayda para birimi eşleşmesi; FX yok.
- **Doğrulamalar**: aynı hesap, yetersiz bakiye, olmayan hesap, negatif/sıfır
  tutar, aşırı tutar, geçersiz para birimi, geçersiz tarih aralığı.
- **Eşzamanlılık**: `PaymentApproval.RowVersion` ile çift onay engeli; hesap
  `RowVersion` korunuyor.
- **Idempotency**: kalıcı `IdempotencyRecords` tablosu + `Idempotency-Key` filtresi;
  tamamlanmış yanıt replay, farklı payload `409`, in-progress `409`.
- **Atomiklik**: bakiye + debit/credit ledger + onay durumu + audit aynı DB
  transaction'ında; hata/rollback'te kısmi işlem yok.
- **Hesap açılışı**: para birimi ve açılış bakiyesi işleniyor; açılış deposit
  ledger satırı ve audit yazılıyor.
- **Raporlar**: tarih-only `to` gün sonu dahil; 366 gün sınırı; para birimi
  kapsamlı top-accounts; statement/daily summary; sayfalama clamp.
- **Audit**: `/api/audit` Admin, tarih/aksiyon/kullanıcı filtresi + sayfalama;
  detaylarda şifre/token yok.

### 3. Şema / migration

- Yeni migration:
  `20261020203345_PaymentApprovalGuidApproversAndIdempotency`.
- `PaymentApprovals` int→Guid kullanıcı/işlem alanları, `RowVersion`,
  `IdempotencyRecords`, audit indeks/uzunlukları.
- Eski migration dosyaları **değiştirilmedi**.
- Gerçek veride int→Guid dönüşümü otomatik yapılamaz; eski değerler
  `LegacyPaymentApprovalIds` tablosuna kopyalanır, yeni alanlar ilk Admin ile
  doldurulur, `RelatedTransactionId` NULL yapılır. Operatör adımları:
  [`migrations/README.md`](migrations/README.md).
- **SQL Server'a uygulanmadı/doğrulanmadı.** SQLite Development `EnsureCreated`
  kullanır; eski `.local/digitalbanking.db` arşivlenip yeniden oluşturuldu.

### 4. VS Code SDK keşfi

- C# Dev Kit logu `dotnet --info` için `dotnet: command not found` ve
  `Could not find installed SDK` gösteriyordu; GUI uygulamaları shell PATH'ini
  okumuyor.
- `.vscode/settings.json`: `dotnet.defaultSolution`, `existingDotnetPath`,
  terminal env eklendi/güncellendi.
- `scripts/setup_macos_vscode_env.sh` launchd `DOTNET_ROOT`/`PATH` önerir;
  `scripts/verify_vscode_sdk.sh` yerel SDK/global.json/.vscode önkoşullarını
  denetler.
- Bu ajan oturumunda `launchctl setenv` **"Not privileged to set domain environment"**
  ile reddedildi. Kullanıcı normal terminalde script'i çalıştırıp VS Code'u
  yeniden başlatmalı. **GUI doğrulaması ajan tarafından yapılmadı.** Kanıt:
  [`evidence/2026-10-20-vscode-sdk.txt`](evidence/2026-10-20-vscode-sdk.txt).

## API sözleşmesi değişiklikleri (dikkat)

- `POST /api/paymentapprovals` gövdesinden `requestedByUserId` kaldırıldı;
  `POST /{id}/approve` gövdesiz; `POST /{id}/reject` yalnız `rejectionReason`.
- `PaymentApprovalResponse` kullanıcı kimlikleri Guid, `relatedTransactionId` Guid.
- `GET /api/customers`, `GET/POST /api/users` artık `ApiResponse<T>` zarfında.
- `POST /api/customers`/`users` yanıtı `data` altında.
- `GET /api/transactions/top-accounts` `currency` parametresi alır (varsayılan TRY).
- Statement/daily/top yanıtları para birimi ve transfer toplamları içerir.
- Yetkisiz para/onay/audit/users çağrıları `403`; tokensız `401`.
- Dashboard bu sözleşmelere uyarlandı; smoke testi silinmedi/gevşetilmedi,
  21 baseline senaryosu korunarak **68** kontrolle genişletildi.

## Doğrulama sonuçları

| Kontrol | Komut | Sonuç |
|---|---|---|
| Build | `bash scripts/dotnet.sh build DigitalBanking.slnx` | **0 uyarı, 0 hata** |
| Test | `bash scripts/dotnet.sh test DigitalBanking.slnx` | **95 geçti, 0 kaldı, 0 atlandı** |
| Smoke | `python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181` | **68 kontrol geçti** |
| NuGet audit | `dotnet list ... package --vulnerable --include-transitive` | 6 projede bilinen açık yok |
| Diff kontrol | `git diff --check` | temiz |

Smoke kapsamı: health, yetkisiz erişim, login/claim/rol, login throttle,
müşteri/hesap, decimal bakiye, para birimi, yatırma/çekme/transfer,
maker-checker, kendi talebini onaylama reddi, çift onay reddi, idempotent replay,
audit, rapor filtreleri, dashboard oturum/rol/çıkış.

Kanıt dosyaları:

- [`evidence/2026-10-20-build.txt`](evidence/2026-10-20-build.txt)
- [`evidence/2026-10-20-test.txt`](evidence/2026-10-20-test.txt)
- [`evidence/2026-10-20-smoke.txt`](evidence/2026-10-20-smoke.txt)
- [`evidence/2026-10-20-nuget-audit.txt`](evidence/2026-10-20-nuget-audit.txt)
- [`evidence/2026-10-20-vscode-sdk.txt`](evidence/2026-10-20-vscode-sdk.txt)

## Kalan işler ve riskler

1. **Kapsamlı dashboard** (sonraki aşama): Admin özet/müşteri/hesap/onay
   kuyruğu/audit/raporlar; müşteri self-servis bakiye/hareket/transfer talebi/
   durum. Tasarım: [`DASHBOARD_PRODUCT_BRIEF.md`](DASHBOARD_PRODUCT_BRIEF.md).
2. **User–Customer–Account sahipliği**: Müşteri self-servis için zorunlu.
   `Customer.UserId` + unique index, yeni migration, sunucu tarafı erişim
   kontrolleri, yeni testler. Şu an `User` salt-okunur; buton gizleyerek
   çözülmemeli.
3. **SQL Server doğrulaması**: bağlantı, migration, decimal, concurrency ve
   rollback testleri bu ortamda yapılmadı. Migration operatör adımları hazır ama
   uygulanmadı.
4. **Manuel UAT**: [`UAT_CHECKLIST.md`](UAT_CHECKLIST.md) kullanıcı tarafından
   doldurulacak; ajan manuel test yapmadı.
5. **Üretim sertleştirmesi**: secret manager, dağıtık login throttle, gözlemlenebilirlik,
   audit retention/immutability, WAF, uyumluluk sertifikasyonu yok; iddia
   edilmiyor.
6. **FX / saat dilimi**: kur çevrimi ve bankacılık gün sonu takvimi yok; tarihler UTC.
7. **VS Code GUI**: kullanıcı launchd adımını çalıştırıp IDE'yi yeniden başlatmalı;
   ajan GUI'yi doğrulamadı.

## Sonuç

Backend ve iş kuralları bu ortamda çalışıyor ve test edildi; demo 5180/5181
üzerinde çalışıyor. Ancak proje **production-ready değildir**, SQL Server
doğrulanmamıştır ve dashboard tamamlanmamıştır. Bu sınırlar README ve test
raporunda açıkça belirtilmiştir.
