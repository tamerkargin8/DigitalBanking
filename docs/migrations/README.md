# Migration notes — PaymentApproval Guid ids and idempotency

Migration: `20261020203345_PaymentApprovalGuidApproversAndIdempotency`
Provider: **SQL Server** (the Development SQLite database uses `EnsureCreated` and
does not apply migrations).

> **Doğrulama durumu:** Bu migration bu ortamda SQL Server'a **uygulanmadı**.
> SQL Server yok; `dotnet ef database update` çalıştırılmadı. Aşağıdaki adımlar
> operatör içindir, çalıştırılmış gibi raporlanmamalıdır.

## Ne değişiyor?

- `PaymentApprovals.RequestedByUserId`: `int` → `uniqueidentifier` (User.Id)
- `PaymentApprovals.ReviewedByUserId`: `int?` → `uniqueidentifier?`
- `PaymentApprovals.RelatedTransactionId`: `int?` → `uniqueidentifier?` (Transaction.Id)
- `PaymentApprovals.RowVersion`: `rowversion` (eşzamanlı çift onay koruması)
- `IdempotencyRecords`: yeni tablo + unique `(Endpoint, Key)` indeksi
- `AuditLogs` metin alanlarına 100 karakter sınırı ve `CreatedDate` indeksi

## Veri dönüşümü neden otomatik değil?

Eski `int` kullanıcı kimlikleri hiçbir zaman `Users.Id` (Guid) ile
ilişkilendirilmemişti. Hangi eski int'in hangi Guid kullanıcıya karşılık geldiği
veriden güvenilir biçimde çıkarılamaz. Bu nedenle migration:

1. Eski değerleri **`LegacyPaymentApprovalIds`** tablosuna kopyalar
   (`ApprovalId`, `OldRequestedByUserId`, `OldReviewedByUserId`,
   `OldRelatedTransactionId`, `MigratedAt`). Veri sessizce atılmaz.
2. Yeni `RequestedByUserId` alanını **en erken oluşturulmuş Admin** kullanıcı ile
   doldurur; hiç Admin yoksa boş Guid (`00000000-...`) yazar.
3. `ReviewedByUserId` eski değeri boş değilse aynı Admin ile doldurur.
4. `RelatedTransactionId` **NULL** yapılır; eski int işlem kimlikleri Guid işlem
   kimliklerine eşlenemez.

## Operatör adımları

1. **Yedek alın** (tam DB + log yedeği) ve uygulamayı durdurun.
2. Migration'ı uygulamadan önce eski int→Guid kullanıcı eşlemesini biliyorsanız
   bir eşleme tablosu hazırlayın. Örnek:
   ```sql
   CREATE TABLE dbo.LegacyUserIdMap (OldUserId int PRIMARY KEY, NewUserId uniqueidentifier NOT NULL);
   -- Operasyonel kayıtlardan doldurun; tahmin etmeyin.
   ```
3. Migration'ı uygulayın (bu ortamda çalıştırılmadı):
   ```sh
   dotnet ef database update --project DigitalBanking.Infrastructure --startup-project DigitalBanking.API
   ```
4. Uygulama sonrası `LegacyPaymentApprovalIds` ile eşlemeyi karşılaştırın:
   ```sql
   SELECT l.ApprovalId, l.OldRequestedByUserId, p.RequestedByUserId
   FROM dbo.LegacyPaymentApprovalIds l
   JOIN dbo.PaymentApprovals p ON p.Id = l.ApprovalId;
   ```
   Eşleme tablonuz varsa düzeltin:
   ```sql
   UPDATE p SET p.RequestedByUserId = m.NewUserId
   FROM dbo.PaymentApprovals p
   JOIN dbo.LegacyPaymentApprovalIds l ON l.ApprovalId = p.Id
   JOIN dbo.LegacyUserIdMap m ON m.OldUserId = l.OldRequestedByUserId;
   ```
5. `RelatedTransactionId` gerekiyorsa eski int işlem kimliklerini operasyonel
   kayıtlardan elle eşleyin; otomatik eşleme yoktur.
6. Uygulamayı açıp onay kuyruğunu, audit kayıtlarını ve örnek bakiyeleri kontrol
   edin. Migration geri alınacaksa `Down` yalnız legacy tablo üzerinden int
   değerleri geri getirir; migration sonrası oluşan Guid kayıtlar için 0/NULL
   kullanır.

## SQLite (Development)

- `DatabaseSeeder` SQLite'ta `EnsureCreatedAsync()` kullanır. Yeni şema için eski
  `.local/digitalbanking.db` dosyası arşivlenip yeniden oluşturulmalıdır.
- SQLite verisi atılabilir demo verisidir; SQL Server davranışının kanıtı değildir.
