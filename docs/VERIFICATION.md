# Doğrulama kaydı

Tarih: 20 Ekim 2026 (Europe/Istanbul). Ortam: macOS ARM64, .NET SDK 10.0.402.

- Solution build: başarılı, 0 uyarı, 0 hata.
- xUnit: 10 başarılı, 0 başarısız, 0 atlanan. PBKDF2 salt/yanlış şifre/eski hash/bozuk kayıtlar; dashboard JSON giriş sözleşmesi ve hata durumları; SQLite üzerinde eski bakiye güncellemesinin reddedilmesi.
- Yerel smoke: 21 kontrol başarılı. Health, yetkisiz erişimin reddi, hatalı şifre, başarılı API girişi, Swagger, müşteri oluşturma/listeleme, iki hesap açma, yatırma/çekme/transfer, yetersiz bakiye/negatif tutar reddi, decimal bakiyeler, işlem geçmişi/raporlar, dashboard session ve rapor sayfası.
- NuGet audit: 6 proje ve geçişli paketler tarandı; bu taramada bilinen açık bildirilmedi. Bu sonuç kod güvenlik incelemesinin yerine geçmez.
- git diff --check: başarılı.

Sınırlar: gerçek SQL Server bağlantısı/migration test edilmedi. PaymentApproval rol/kimlik/idempotency akışları tamamlanmış sayılmaz; ayrıntılar DEEPSEEK_HANDOFF.md içinde. SQLite yalnızca sentetik Development verisi içindir. DeepSeek modeliyle canlı görev çağrısı yapılmadı; mevcut Harness bağlantısı yalnızca arayüzde gözlendi.
