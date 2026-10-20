# Portfolio / CV notes

Bu notlar yalnızca bu depoda gerçekten uygulanan ve doğrulanan işlere dayanır.
Ölçüm, unvan, sertifika veya kurumsal uygunluk iddiası eklenmemelidir.

## CV için 2–3 kısa madde

1. **Maker-checker ödeme onayı ve risk yönlendirmesi (ASP.NET Core 10 / EF Core 10).**
   JWT claim'lerinden türetilen Guid kullanıcı kimliği, 50.000 eşikli doğrudan
   transfer sınırı, düşük riskte otomatik onay, orta/yüksek/kritik riskte farklı
   Admin onayı, kendi talebini onaylama engeli, `rowversion` ile eşzamanlı çift
   onay koruması ve `Idempotency-Key` ile tekrar istek engeli. Kanıt: ADR-0001,
   `PaymentApprovalService`, `IdempotencyFilter`, 95 xUnit testi ve 71 smoke kontrolü.

2. **Decimal para ve atomik audit disiplini.** Bakiye/ledger/onay/audit aynı
   transaction'da; 2 haneden fazla küsurat reddediliyor; TRY/USD/EUR/GBP için
   para birimi doğrulaması ve farklı para birimli transferin reddi; gün sonu dahil
   tarih filtreleri ve para birimi kapsamlı raporlama. Kanıt: `LedgerOperations`,
   `Money`, `ReportDateRange`, `AuditService`, testler.

3. **Güvenli JWT kimliği ve operasyonel giriş sertleştirmesi.** `int` kullanıcı
   kimliklerinin Guid'e taşınması, istemci gövdesinden kimlik kabul edilmemesi,
   600k iterasyon PBKDF2 + legacy SHA-256 rehash, 8+ harf/rakam şifre politikası,
   kullanıcı adı başına hatalı giriş kilidi ve IP bazlı login rate limit. Kanıt:
   migration + `AuthService` + `InMemoryLoginAttemptLimiter` + testler.

İsterseniz dördüncü madde olarak: **"SQL Server migration'ı üretildi, ancak bu
ortamda uygulanmadı; SQLite testleri SQL Server kanıtı değildir"** dürüst sınır
notu olarak eklenebilir. Bunu başarı maddesi gibi yazmayın.

## Proje anlatımı (kısa)

> Bankacılık operasyonlarındaki maker-checker kontrolünü, risk eşiklerini,
> idempotent para hareketini ve audit izini .NET 10 ile uygulayan bir portföy
> demosu. Backend ve iş kuralları test edildi; müşteri/admin dashboard deneyimi
> ayrı bir aşama olarak tasarlandı.

## AI katkısı (dürüst beyan)

- Bu teslim, DeepSeek tabanlı bir kodlama ajanı (DSH harness) ile AI-destekli
  olarak geliştirildi; mimari/ürün kararları ve kapsam kullanıcı talimatlarıyla
  yönlendirildi, çıktılar testler ve yerel demo ile doğrulandı.
- AI, üretim ortamı erişimi olmadan çalıştı; SQL Server, gerçek banka sistemi,
  gerçek müşteri/para verisi veya canlı ödeme ağı kullanılmadı.
- Testlerin ve smoke kontrollerinin çalıştırıldığı tarih/ortam raporlarda açıktır.
  Gelecekteki bir mülakatta "şu kadar kullanıcı/performans" gibi ölçümler
  uydurulmamalıdır; bu depoda böyle bir ölçüm yoktur.
- Dashboard'un tamamlanmış gibi sunulmaması gerekir; mevcut Razor ekranları
  sınırlıdır ve ürün brief'i sonraki aşamayı tanımlar.

## Mülakatta anlatılabilecek kararlar

- Neden maker-checker? Operasyonel risk ve görevler ayrılığı; yüksek tutarlı
  ödemenin tek kişi tarafından uygulanmasını engellemek.
- Neden düşük risk otomatik? Düşük tutarlı operasyonu gereksiz yavaşlatmamak;
  risk seviyesi iş akışını belirliyor, rol hiyerarşisini değil.
- Neden `RowVersion` + status kontrolü? İki checker aynı anda onaylarsa para iki
  kez hareket etmemeli.
- Neden idempotency tablosu? Ağ hatasında istemci aynı isteği tekrar gönderirse
  para ikinci kez uygulanmamalı; tamamlanmış yanıt yeniden oynatılmalı.
- Neden audit aynı transaction? Para hareketi ile kanıtı ayrışmamalı.
- Neden müşteri self-servis henüz yok? Modelde User–Customer–Account sahipliği
  yok; buton gizlemek güvenlik değildir. Önce sunucu tarafı sahiplik ve migration
  tasarlanmalı (DASHBOARD_PRODUCT_BRIEF).

## Yayın öncesi kontrol

- [ ] README'deki ekran görüntüsü yerleri gerçek dashboard tamamlandıktan sonra
      gerçek görüntülerle dolduruldu; sahte görsel yok.
- [ ] `.local/credentials.json`, JWT anahtarı, token veya gerçek veri commit'te yok.
- [ ] Test/smoke sayıları son çalıştırmayla güncel.
- [ ] SQL Server/migration doğrulanmadı ifadesi korunuyor.
- [ ] Kapsam dışı üretim iddiası yok.
