# ADR-0001 — Approval, role and delegation policy

- Durum: **Kabul edildi; önceki iki-rol/auto-approval kararını değiştirir**
- Tarih: 2026-10-20
- Bağlam: Üç rollü üyelik, tüm finansal taleplerde onay, kişi/operasyon/para
  birimi limitleri ve müşteri self-servisi

## Bağlam

İlk demo iki rolle (`Admin`, `User`) çalışıyor ve düşük riskli talepleri otomatik
onaylıyordu. Kullanıcı bankacılık operasyonlarına daha yakın bir model istedi:
Owner tüm talepleri görür; yetki belirli kişilere, belirli operasyon/para birimi
ve limitlerle devredilir; müşteri yalnız kendi talebini oluşturur. "Düşük risk"
gerekçesiyle onay atlamak bu modelde kabul edilemez.

## Karar

### 1. Roller

- **Owner / SuperAdmin:** Üyeleri aktive eder, Customer/DelegatedApprover rolü
  atar, yetki/limit tanımlar; tüm talepleri, audit'i, müşteri/hesap ve mutabakatı
  görür. İlk ve tek Owner yalnız localhost + Development setup ile oluşur; public
  kayıt Owner yaratamaz. Son Owner disable/demote edilemez.
- **DelegatedApprover:** Yalnız kendisine verilen operasyon + para birimi +
  `MaxAmount` (ve varsa `DailyLimit`/`ExpiresAt`) kapsamında onaylar. Üye/rol/
  yetki yönetemez; kendini veya başkasını yükseltemez.
- **Customer:** Public kayıt sonrası Owner aktivasyonuyla açılır. Yalnız kendi
  profil/hesap/hareket/talep/dekontunu görür.
- **PendingActivation:** Kayıt varsayılanı; login ve işlem yok.

Legacy `Admin=1` / `User=2` değerleri **geçersizdir** ve otomatik dönüştürülmez.
Operatör açık eşleme yapmalıdır (bkz. `docs/migrations/README.md`).

### 2. İstisnasız onay

- Transfer, deposit, withdrawal ve account opening **her zaman** `Pending`
  oluşur; oluşturma anında ledger/bakiye değişmez.
- Düşük risk **otomatik onay değildir**; yalnız bilgi/kuyruk filtresidir.
- Owner veya yetkili delegated approver onayı gerekir.
- Owner dahil hiç kimse **kendi talebini onaylayamaz/reddedemez**.
- Onay/red sırasında hesap, sahiplik, para birimi, bakiye, amount scale ve güncel
  durum yeniden doğrulanır.
- Red/iptal bakiye değiştirmez; bekleyen giden talep rezervasyonu serbest bırakır.

### 3. Delegasyon ve limit

- `ApprovalDelegation`: kullanıcı + operation + currency + `MaxAmount`; opsiyonel
  `DailyLimit`, `ExpiresAt`, `IsActive`, `RowVersion`.
- Limit **dahildir**: `100,000.00 TRY` onaylanır, `100,000.01 TRY` Owner'a kalır.
- Günlük limit aynı onaycının aynı operasyon/para birimindeki UTC günü
  onaylarının toplamıdır.
- Yetki her onayda DB'den okunur. Deactivate, expiry, rol değişikliği veya limit
  düşürme eski JWT/ekran ne gösterirse göstersin anında etkilidir.
- Reconciliation exception onayı operasyon iznine bağlıdır; parasal limit yoktur
  ve kapanış bakiye düzeltmez.

### 4. Para ve tutarlılık

- `decimal`, 2 hane; fazla küsurat reddedilir. TRY/USD/EUR/GBP; FX yok.
- Bekleyen giden transfer/withdrawal available balance rezervasyonu yapar; iki
  talep bakiyeyi overcommit edemez.
- Onay; bakiye + debit/credit ledger + status + timeline + audit'i tek
  transaction'da yazar.
- `RowVersion` eşzamanlı çift onayı engeller; `Idempotency-Key` tekrar isteği
  ikizlemez.

### 5. Üyelik

- Public kayıt yalnız kullanıcı adı, e-posta, ad-soyad, şifre+doğrulama alır.
  İstemci rol/limit/sahiplik/bakiye gönderemez; e-posta doğrulaması iddia edilmez.
- Kayıt `PendingActivation`; Owner aktivasyonu Customer profili oluşturur.
- Owner setup tek seferliktir; Owner oluşunca endpoint `409` ile kapanır.

## Sonuçlar

- Yetki, "hangi ekranı görüyor" değil **kişi + operasyon + para birimi + tutar**
  olarak modellenir.
- Owner'ın kendi talebi bile dört göz kuralına tabidir; başka yetkili gerekir.
- Müşteri self-servisi güvenli hale gelir; ancak müşteri adına hareket yalnız
  approval request ile olur.
- Daha fazla yönetim ekranı ve delegasyon verisi gerekir; bu kabul edilen
  karmaşıklıktır.
- SQL Server'da legacy rol eşlemesi ve migration operatör işidir.

## Reddedilen alternatifler

- **Düşük risk auto-approval:** Kullanıcı politikasına aykırı; limit/rol modelini
  anlamsızlaştırır. Kaldırıldı.
- **Sadece Owner onayı:** Ölçeklenmez; delegasyon iş ihtiyacıdır.
- **Delegasyonda yalnız global limit:** Operasyon/para birimi ayrımı olmadan
  yetki fazla geniş olur.
- **Owner'ın kendi talebini onaylaması:** Dört göz kuralını ihlal eder.
- **Public kayıtta rol seçimi:** Kalıcı yetki yükseltme riski.
- **Exception kapanışında bakiye düzeltmesi:** Mutabakat kaydı operasyonel
  kanıttır; para hareketi normal ledger akışından geçmelidir.
