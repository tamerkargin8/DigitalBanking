# DEEPSEEK FINDINGS

Bu dosya bulguları, uygulanan çözümleri ve bilinçli olarak ertelenen sınırları
kaydeder. Eski README/refactoring raporları tarihseldir.

## Çözülen bulgular

| # | Bulgu | Çözüm | Durum |
|---|-------|-------|-------|
| 1 | `PaymentApproval.RequestedByUserId`/`ReviewedByUserId` int, `User.Id` Guid; kimlik istemci gövdesinden geliyordu | Entity/DTO/servis/controller Guid'e taşındı; kimlik yalnız doğrulanmış JWT claim'inden (`ActorContext`) alınıyor | Çözüldü |
| 2 | `RelatedTransactionId` int, `Transaction.Id` Guid | Guid'e taşındı; onay/otomatik onay sırasında debit işlem id'si yazılıyor | Çözüldü |
| 3 | Eşzamanlı çift onay ve idempotency yok | `PaymentApproval.RowVersion`, `Idempotency-Key` filtresi + kalıcı `IdempotencyRecords` tablosu | Çözüldü |
| 4 | Audit, para hareketinden sonra ayrı `SaveChanges` ile yazılıyordu; kısmi işlem riski | Audit aynı DbContext/transaction içinde `IAuditService.Add` ile ekleniyor; tek commit | Çözüldü |
| 5 | Doğrudan transfer maker-checker'ı atlayabiliyordu | Admin + 50.000 altı doğrudan transfer; üstü yalnız onay akışı. ADR-0001 | Çözüldü |
| 6 | Risk seviyesi hesaplanıyor ama yetkiye bağlanmıyordu | Düşük risk otomatik onay; orta+ Admin checker; risk politikası tek yerde (`PaymentPolicy`) | Çözüldü |
| 7 | `AccountService` Currency/InitialBalance'ı yok sayıyordu | Para birimi ve açılış bakiyesi işleniyor; açılış deposit işlemi + audit yazılıyor | Çözüldü |
| 8 | Raporlarda `to` gece yarısı olduğunda gün sonu dışlanıyordu | `ReportDateRange` gün sonu dahil normalize ediyor; 366 gün sınırı | Çözüldü |
| 9 | Şifre alt sınırı 4 karakterdi; giriş denemesi sınırı yoktu | 8+ harf+rakam politikası; kullanıcı adı başına 5 hata/5 dk kilit; auth rate limit | Çözüldü |
| 10 | Dashboard `Error = ex.Message` ile teknik ayrıntı gösterebiliyordu | Hata/401/403/boş durumları için güvenli `ApiResult` katmanı | Devam ediyor (dashboard) |
| 11 | SQLite/TEXT ve decimal davranışı test edilmemişti | Decimal para testleri (0.01, 100.25, 10.10), 3 haneli küsurat reddi | Çözüldü (SQLite) |
| 12 | Audit görüntüleme yoktu | `/api/audit` (Admin, filtre + sayfalama) ve dashboard ekranı | API çözüldü |

## Bilinçli sınırlar / ertelenenler

- **SQL Server doğrulaması yok**: Bu ortamda SQL Server yok. Migration dosyası
  üretildi ve veri koruma adımları belgelendi; `dotnet ef database update`
  çalıştırılmadı. SQLite testleri SQL Server kanıtı değildir.
- **Eski int kullanıcı kimlikleri**: Gerçek veride int→Guid dönüşümü otomatik
  yapılamaz. Migration eski değerleri `LegacyPaymentApprovalIds` tablosuna
  kopyalar, yeni alanları ilk Admin ile doldurur; operatör uzlaştırması gerekir.
- **Müşteri–kullanıcı sahipliği yok**: Modelde User ile Customer/Account arasında
  ilişki yok. Bu nedenle standart `User` rolü yalnız kimlik doğrulaması yapar;
  müşteri/hesap/rapor verisi, para hareketleri ve onay işlemleri Admin'e kapalıdır
  (deny-by-default). Keyfi sahiplik varsayımı
  kurulmadı. Gelecekte `Customer.UserId` eklenirse satır düzeyi yetki tasarlanmalı.
- **Tek onay kademesi**: İki rol (Admin/User) korundu. Risk seviyesi iş akışını
  (otomatik vs maker-checker) belirler; ayrı bir "senior approver" rolü yoktur.
  Bu, uygulanan kuralla tutarlı olarak README/ADR'de açıkça yazılır.
- **FX yok**: Risk eşikleri hesap para birimi cinsinden nominal değerlerdir; kur
  çevrimi yapılmaz. Farklı para birimli transfer/onay reddedilir.
- **Login throttle bellek içi**: Çok örnekli dağıtımda paylaşımlı store gerekir.
- **Rapor saat dilimi**: Tarihler UTC kabul edilir; yerel saat dilimi dönüşümü yok.
- **Audit saklama/arşiv**: Retention, değiştirilemezlik ve dış log aktarımı yok.
- **Paket lisansları**: AutoMapper/FluentValidation lisans ve bakım değerlendirmesi
  kapsam dışı bırakıldı (paket sürümleri değiştirilmedi).
- **VS Code GUI doğrulaması**: SDK keşfi için launchd/PATH düzeltmesi yapıldı;
  çalışan VS Code penceresinin yeniden başlatılmadan doğrulanması mümkün değil.

## Dashboard uyumluluğu ve sonraki aşama bulguları

| # | Bulgu | Çözüm / plan | Durum |
|---|-------|--------------|-------|
| 13 | Mevcut dashboard eski API sözleşmelerini kullanıyordu (raw users/customers, `int` requester, `Error=ex.Message`) | Dashboard `ApiClient`/`ApiResult` katmanına taşındı; zarf/raw yanıt, 401/403/500 güvenli mesaj, kullanıcı/rol session'ı, çıkış temizliği, rapor filtreleri | Çözüldü (minimal uyumluluk) |
| 14 | Standart `User` yalnız kimlik doğrulayabiliyor; müşteri self-servis için User–Customer–Account sahipliği yok | Keyfi sahiplik varsayılmadı; tüm müşteri/hesap/rapor okumaları Admin'e kapatıldı (deny-by-default). `Customer.UserId` + unique index, sunucu tarafı erişim, yeni endpoint/testler sonraki aşama olarak `DASHBOARD_PRODUCT_BRIEF.md` içinde tasarlandı | Açık / sonraki aşama |
| 15 | Dashboard ürün hedefi (4 persona, lacivert/nötr tasarım, erişilebilirlik, mobil, makbuz/durum, para birimi bazlı kartlar) yok | Ürün brief'i, UAT checklist ve kaba efor kaydedildi; kapsamlı ekran geliştirmesi başlatılmadı | Planlandı |
| 16 | README tarihsel/abartılı özellikler içeriyordu | README İngilizce ana anlatı + Türkçe özet olarak yeniden yazıldı; yalnız uygulanan özellikler, gerçek test sayıları, ekran görüntüsü yerleri ve sınırlar | Çözüldü |
| 17 | VS Code GUI `dotnet --info` bulamıyor (`launchd` PATH boş) | `.vscode` ayarları + `scripts/setup_macos_vscode_env.sh` + `scripts/verify_vscode_sdk.sh`; ajan oturumunda `launchctl setenv` reddedildi, kullanıcı normal terminalde çalıştırıp VS Code'u yeniden başlatmalı | Kısmi (kullanıcı adımı) |
| 18 | SQL Server int→Guid migration'ı gerçek veride otomatik yapılamaz | Legacy tablo + backfill + operatör adımları; migration bu ortamda uygulanmadı | Belgelenmiş sınır |
| 19 | Portföy/CV anlatısı ve AI beyanı eksikti | `PORTFOLIO_NOTES.md` kanıta dayalı 3 madde + dürüst AI katkısı; abartılı unvan/ölçüm yok | Çözüldü |
| 20 | Manuel kabul kriterleri yoktu | `UAT_CHECKLIST.md`: 2 rol, 4 persona, mevcut backend + sonraki dashboard senaryoları; ajan testi yapılmış gibi işaretlenmedi | Hazır |

## Ürün hedefi notu

Kullanıcı projeyi GitHub'da yayınlayıp CV'sine ekleyecek. README ilk 30 saniyede
problemi, kontrol kararlarını ve kanıtlanmış sonuçları anlatacak şekilde
İngilizce; Türkçe özet ve rehber bağlantılarıyla desteklendi. Dashboard tamamlanana
kadar "tamamlanmış dashboard" izlenimi verilmemeli; ekran görüntüleri gerçek
ekranlardan alınmalı. AI katkısı `PORTFOLIO_NOTES.md` içinde açıkça belirtildi.
