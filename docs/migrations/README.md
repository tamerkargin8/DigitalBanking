# Migration notes — ownership, approvals, delegations and reconciliation

Provider: **SQL Server** (default). Development SQLite uses `EnsureCreated` and
does **not** apply these migrations.

> **Doğrulama durumu:** Bu migration'lar bu ortamda SQL Server'a **uygulanmadı**.
> SQL Server yok; `dotnet ef database update` çalıştırılmadı. Aşağıdaki adımlar
> operatör içindir ve çalıştırılmış gibi raporlanmamalıdır.

## İlgili migration'lar

1. `20261020203345_PaymentApprovalGuidApproversAndIdempotency`
   - PaymentApproval kullanıcı/işlem id'leri `uniqueidentifier`, `RowVersion`,
     `IdempotencyRecords`, audit indeksleri.
2. `20261020212050_AddCustomerOwnershipTimelineReconciliation`
   - `Customers.UserId` (+ filtered unique index), `IsDemo` alanları,
     `PaymentApprovals.CorrelationId`, `PaymentTimelineEvents`,
     `ReconciliationBatches/Rows/Exceptions`, string uzunlukları.
   - Legacy approval'lara `NEWID()` correlation atar.
3. `20261020213624_AddOwnerDelegationAndApprovalRequests`
   - User ad/e-posta/durum/aktivasyon alanları; `ApprovalDelegations`;
     `PaymentApprovals.RequestType`, `CreatedAccountId`, `RequestedCustomerId`,
     `RequestedInitialBalance`.
   - `Status` mevcut kullanıcılar için `2 (Active)` varsayılanı alır; yeni public
     kayıtlar uygulama tarafından açıkça `PendingActivation=1` yazılır.
   - `RequestType` mevcut satırlarda `1 (Transfer)` olur.
4. `20261021011500_EnforceSingleOwner`
   - `Users` üzerinde filtered unique index:
     `IX_Users_Role_OwnerUnique` (`[Role] = 3`).
   - Aynı anda iki Owner oluşturmayı DB seviyesinde engeller; ikinci setup
     isteği uygulama tarafından `409`'a çevrilir.
   - **Mevcut veride >1 Owner varsa migration bilinçli olarak durur** ve açık bir
     SQL hatası verir. Hiçbir satır silinmez/demote edilmez; operatör önce hangi
     hesabın Owner kalacağına karar vermelidir.
5. `20261021023000_AddAccountMetadata`
   - `Accounts.AccountName` (`nvarchar(100)`, default `Vadesiz Hesap`) ve
     `AccountType` (`int`, default `1=Checking`).
   - `PaymentApprovals.RequestedAccountName` / `RequestedAccountType` (nullable).
   - Additive; eski hesaplar **Vadesiz** kabul edilir, hiçbir satır silinmez.

6. `20261022191101_AddDoubleEntryJournalAndApprovalAssignment`
   - `JournalEntries` / `JournalLines` (append-only double-entry journal).
   - `SourceKey` unique index (`approval:<id>`, `baseline:<accountId>`,
     `reversal:<entryId>`); currency/lookup indexes; account FK restrict.
   - Additive; **hiçbir eski hesap için otomatik baseline/journal üretmez**.
     Baseline yalnız Owner'ın açık preview/post işlemiyle oluşur.
   - Aynı additive migration, Stage 4 operasyon ataması için
     `PaymentApprovals.AssignedToUserId/AssignedToUsername/AssignedAt` ve
     `(AssignedToUserId, Status)` indeksini de ekler. Atama onay yetkisi vermez.

Eski migration dosyaları değiştirilmedi.

## Birden fazla Owner bulunduysa (operatör uyarısı)

Eski bir yarış/hatalı kurulum nedeniyle `Role=3` kullanıcı sayısı 1'den fazlaysa:

1. Uygulamayı durdurun ve tam yedek alın.
2. Hangi hesabın Owner kalacağını **iş kararıyla** belirleyin. Diğer hesapları
   silmeyin; gerekirse `Customer`/`DelegatedApprover` rollerine açıkça çevirin
   veya `Disabled` yapın.
3. Aşağıdaki sorguyla doğrulayın; sonuç `1` olmadan migration'ı çalıştırmayın:
   ```sql
   SELECT Role, COUNT(*) FROM dbo.Users WHERE Role = 3 GROUP BY Role;
   ```
4. Migration'ı yeniden çalıştırın. `IX_Users_Role_OwnerUnique` yalnız tek Owner
   kaldığında oluşur.
5. Uygulama başlangıcında da `>1 Owner` durumu **silme yapmadan** Serilog
   warning olarak raporlanır.

SQLite Development'ta `EnsureCreated` mevcut dosyayı değiştirmez; bu nedenle eski
bir SQLite DB'de index yoksa dosya arşivlenip yeniden oluşturulmalıdır.

## Legacy rol eşlemesi — otomatik değil

`UserRole` değerleri:

| Değer | Anlam | Durum |
|---:|---|---|
| 0 | None | geçersiz |
| 1 | Admin (eski demo) | **geçersiz**, login bloklu |
| 2 | User (eski demo) | **geçersiz**, login bloklu |
| 3 | Owner | geçerli |
| 4 | DelegatedApprover | geçerli |
| 5 | Customer | geçerli |

Migration hiçbir eski Admin/User'ı sessizce Owner/Customer yapmaz. Operatör
açıkça eşlemelidir. Örnek (gerçek kullanıcı adlarını ve kararı siz belirleyin;
tahmin etmeyin):

```sql
-- Backup sonrası, uygulama durdurulmuşken.
UPDATE dbo.Users SET Role = 3, Status = 2 WHERE Username = N'<owner-kararı>';
UPDATE dbo.Users SET Role = 5, Status = 2 WHERE Username = N'<müşteri-kararı>';
UPDATE dbo.Users SET Role = 4, Status = 2 WHERE Username = N'<delegated-kararı>';
```

- `Status=2` Active demektir; `1` PendingActivation, `3` Disabled.
- Customer rolü için `Customers.UserId` eşlemesi de gerekir; boş kalan müşteri
  `/api/me/*` kullanamaz (güvenli varsayılan).
- Owner için tercih edilen yol, temiz kurulumda **localhost `/Setup`** ile tek
  Owner oluşturmaktır. Mevcut veride Owner yoksa Setup açıktır; varsa `409`.
- Son Owner koruması uygulama düzeyindedir; SQL ile son Owner'ı disable etmek
  operasyonel olarak yapılmamalıdır.

## Customer.UserId backfill

`Customer.UserId` nullable'dır; eski müşteri kayıtları boş kalır. Operatör
eşlemesi olmadan müşteri self-servis açılmaz. Eşleme örneği:

```sql
UPDATE c
SET c.UserId = u.Id
FROM dbo.Customers c
JOIN dbo.Users u ON u.Username = N'<müşteri-kullanıcı-adı>'
WHERE c.Id = '<müşteri-id>';
```

- Filtered unique index aynı kullanıcının iki müşteriye bağlanmasını engeller.
- İki müşteri aynı login'e bağlanamaz.
- Eşleme tahminle yapılmamalı; iş kaydı/operasyon onayı gerekir.

## Approval request alanları

- `RequestType=1 Transfer`, `2 Deposit`, `3 Withdrawal`, `4 AccountOpening`.
  Migration eski satırları Transfer kabul eder.
- `FromAccountId`/`ToAccountId` account opening'de `Guid.Empty` olabilir.
- `RequestedCustomerId`, `RequestedInitialBalance` account opening için kullanılır;
  `CreatedAccountId` onaydan sonra dolar.
- `CorrelationId` legacy satırlarda `NEWID()` ile doldurulur; ledger eşleşmesi
  gerekirse operasyonel olarak doğrulanmalıdır.

## Operatör adımları

1. Tam DB + log yedeği alın; uygulamayı durdurun.
2. Legacy rol/müşteri eşleme kararlarını yazılı hale getirin.
3. Migration'ları uygulayın (bu ortamda çalıştırılmadı):
   ```sh
   dotnet ef database update --project DigitalBanking.Infrastructure \
     --startup-project DigitalBanking.API
   ```
4. Rolleri ve `Customers.UserId` eşlemesini yukarıdaki gibi açıkça yapın.
5. İlk Owner'ı localhost `/Setup` ile oluşturun (veya mevcut Owner'ı doğrulayın).
6. Owner olarak delegasyonları yeniden tanımlayın; eski auto-approval beklentisi
   taşıyan istemciler güncellenmelidir.
7. Smoke/health, login, onay kuyruğu, müşteri `/api/me/*`, timeline, mutabakat ve
   audit'i doğrulayın.
8. Geri alma gerekirse `Down` yalnız şema değişikliklerini geri alır; iş verisi
   kararları (rol/eşleme) ayrıca yönetilmelidir.

## SQLite (Development)

- `DatabaseSeeder` SQLite'ta `EnsureCreatedAsync()` kullanır. Yeni şema için
  `.local/digitalbanking.db*` arşivlenip yeniden oluşturulabilir.
- Mevcut bir SQLite demo DB'sini silmeden journal tablolarını eklemek için:
  ```sh
  python3 scripts/upgrade_sqlite_journal.py .local/digitalbanking.db          # dry-run
  python3 scripts/upgrade_sqlite_journal.py .local/digitalbanking.db --apply  # yedek + DDL
  ```
  Script UAT görünümlü yolları reddeder; baseline üretmez.
- SQLite verisi atılabilir demo verisidir; SQL Server davranışının kanıtı değildir.
- Owner seed edilmez; temiz DB'de `/Setup` bir kez kullanılır.
