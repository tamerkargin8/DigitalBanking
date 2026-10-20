# ADR-0001 — Payment and role policy

- Durum: Kabul edildi
- Tarih: 2026-10-20
- Bağlam: DigitalBanking demo, maker-checker akışı, iki rol, müşteri–kullanıcı sahipliği yok

## Bağlam

Modelde `User` ile `Customer`/`Account` arasında sahiplik ilişkisi yoktur. Var olan
iki rol `Admin` ve `User`'dır. Doğrudan transfer endpoint'i maker-checker akışını
atlayabildiği için yüksek tutarlı ödemelerin tek adımda ve tek kişi tarafından
uygulanması riski vardı. Risk seviyesi hesaplanıyor ancak yetki/workflow'a
bağlanmıyordu.

## Karar

1. **Roller**
   - `Admin`: operasyonel rol. Müşteri/hesap açma, para yatırma/çekme, doğrudan
     transfer, onay/red, kullanıcı ve audit görüntüleme yetkileri.
   - `User`: salt-okunur demo rolü. Müşteri/hesap/rapor okuma; para hareketi,
     onay, kullanıcı ve audit erişimi yok. Müşteri–kullanıcı sahipliği olmadığı
     için keyfi bir sahiplik varsayımı kurulmadı; en güvenli varsayılan seçildi.

2. **Doğrudan transfer**
   - Yalnız `Admin`.
   - Yalnız `amount < 50.000` (hesap para birimi cinsinden nominal). Bu tutar
     düşük risk sınırıdır.
   - `amount >= 50.000` doğrudan transfer **reddedilir** ve maker-checker
     (`POST /api/paymentapprovals`) akışına yönlendirilir.

3. **Ödeme onayı (maker-checker)**
   - Yalnız `Admin` talep oluşturabilir/inceleyebilir.
   - Düşük risk (`< 50.000`): sistem tarafından otomatik onaylanır (`AutoApproved`),
     ledger hareketi aynı transaction içinde uygulanır.
   - Orta/yüksek/kritik: `Pending` kalır; farklı bir `Admin` onaylar veya reddeder.
   - Talep sahibi kendi talebini onaylayamaz/reddedemez.
   - Onay/red sırasında hesap, para birimi, bakiye ve güncel durum yeniden
     doğrulanır. `RowVersion` ile eşzamanlı çift onay engellenir.

4. **Risk eşikleri**
   - `< 50.000` → Low (otomatik)
   - `50.000 – 249.999,99` → Medium (checker)
   - `250.000 – 999.999,99` → High (checker)
   - `>= 1.000.000` → Critical (checker)

5. **Para birimi ve tutar**
   - Tüm tutarlar `decimal`, 2 küsurat. Fazla küsurat reddedilir.
   - Desteklenen para birimleri: TRY, USD, EUR, GBP.
   - FX/kur çevrimi yok. Transfer ve onayda iki hesabın para birimi aynı olmalıdır.

6. **Tutarlılık**
   - Bakiye, debit/credit ledger satırları, onay durumu ve audit aynı DB
     transaction'ında commit/rollback edilir.
   - `Idempotency-Key` verilen isteklerde aynı anahtar+payload tekrarı para
     hareketini ikinci kez uygulamaz; tamamlanmış yanıt yeniden oynatılır.

## Sonuçlar

- Yüksek tutarlı ödeme tek kişinin doğrudan transferiyle atlanamaz.
- Standart kullanıcı demo ortamında para hareketi yapamaz; bu, eksik sahiplik
  modelinin bilinçli ve güvenli sonucudur.
- Ayrı bir “senior approver” rolü yoktur. Risk seviyesi iş akışını belirler,
  rol hiyerarşisini değil. README ve raporlarda daha fazlası iddia edilmez.
- Müşteri–kullanıcı sahipliği eklendiğinde satır düzeyi yetki ayrı bir ADR ile
  tasarlanmalıdır.

## Reddedilen alternatifler

- **Üçüncü bir Supervisor rolü**: iki rollü modeli gereksiz karmaşıklaştırır ve
  mevcut demo kullanıcılarıyla uyum için ek yönetim gerektirir.
- **Tüm transferleri onaya zorlamak**: düşük tutarlı operasyonel transferleri
  gereksiz yere yavaşlatır; otomatik onay politikası bunu dengeler.
- **Doğrudan transferi tamamen kaldırmak**: mevcut API sözleşmesini ve demo
  akışını gereksiz bozar.
