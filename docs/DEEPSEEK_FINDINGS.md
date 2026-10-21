# DEEPSEEK FINDINGS

Bu dosya uygulanan çözümleri, kullanıcı kararlarıyla değişen modeli ve bilinçli
sınırları kaydeder. Eski auto-approval/iki-rol anlatıları tarihseldir.

## Çözülen bulgular

| # | Bulgu | Çözüm | Durum |
|---|-------|-------|-------|
| 1 | Kullanıcı kimlikleri istemci gövdesinden geliyordu; int/Guid uyumsuzluğu | Guid claim tabanlı `ActorContext`; DTO/entity Guid; istemci requester/reviewer seçemez | Çözüldü |
| 2 | Düşük risk otomatik onayı bankacılık onay politikasını atlıyordu | **Tüm finansal talepler Pending**; risk yalnız bilgi; Owner/delegated onayı zorunlu | Çözüldü (politika tersine çevrildi) |
| 3 | Rol modeli Admin/User idi; limit ve müşteri sahipliği yoktu | Owner / DelegatedApprover / Customer; `Customer.UserId`; üyelik aktivasyonu; minimal kayıt | Çözüldü |
| 4 | Owner/Delegated limitleri ve self-approval kontrolü yoktu | Operasyon+para birimi+`MaxAmount` (dahil), opsiyonel günlük/expiry, DB'den canlı kontrol; Owner dahil self-approval engeli | Çözüldü |
| 5 | İki bekleyen talep bakiyeyi aşabiliyordu | Pending giden talepler available balance rezervasyonu; onayda yeniden kontrol | Çözüldü |
| 6 | Çift onay/idempotency | `RowVersion`, status kontrolü, kalıcı `IdempotencyRecords`, replay/409 | Çözüldü |
| 7 | Audit para hareketinden ayrı yazılabiliyordu | Ledger + timeline + status + audit aynı transaction | Çözüldü |
| 8 | Müşteri self-servis için sahiplik yoktu | `Customer.UserId`, server-side join, IDOR 404, yalnız kendi verisi | Çözüldü |
| 9 | Timeline/dekont yoktu | `PaymentTimelineEvent`; tamamlanmış işlemde maskeli, Europe/Istanbul, DEMO uyarılı receipt; pending'de completed veri yok | Çözüldü |
| 10 | Gün sonu mutabakat/istisna yoktu | CSV preview/import, SHA-256 mükerrer, eşleşme sınıfları, istisna maker-checker, formül-safe export, bakiye düzeltmesiz kapanış | Çözüldü |
| 11 | Demo verisi tekrar üretilemiyor/temizlenemiyordu | Development-only, Owner-only deterministic seed; `IsDemo` scoped reset | Çözüldü |
| 12 | Public kayıt rol/limit kabul ediyordu | Yalnız minimal alanlar; rol/limit/sahiplik yok sayılır; PendingActivation; Owner yalnız localhost setup | Çözüldü |
| 13 | Son Owner/rol yükseltme koruması yoktu | Owner disable/demote/atama engeli; setup tek seferlik; legacy roller geçersiz | Çözüldü |
| 14 | Doğrudan transfer/deposit onay akışını atlayabiliyordu | Doğrudan uçlar `400`; tüm hareketler approval request | Çözüldü |
| 15 | Dashboard eski API/rollere göreydi | Setup/Login/Register; Owner, delegated ve customer sayfaları; guard'lar; Türkçe UI; invariant decimal binding | Çözüldü |
| 16 | Smoke eski auto-approval modelini test ediyordu | 280 kontrollük yeni akış: Owner setup, üyelik, limit sınırı, revocation, stale JWT, mutabakat, demo, dashboard | Çözüldü |
| 17 | Eşzamanlı iki `/api/setup/owner` iki Owner oluşturabiliyordu | DB filtered unique index (`Role=3`), transaction, duplicate-key'i `409`'a eşleme; iki gerçek context ile regression testi | Çözüldü |
| 18 | Setup sayfası Owner oluştuktan sonra açık kalıyor; setup 429 İngilizce login mesajı veriyordu | GET/POST setup durumunu yeniden kontrol ediyor, stale POST login bildirimine gidiyor; setup ayrı rate-limit partition'ı, Türkçe 429 + `Retry-After` | Çözüldü |
| 19 | Onay anında müşteri–hesap sahipliği yeniden doğrulanmıyordu | Onay öncesi `Customer.UserId` ve kaynak hesabın `CustomerId` kontrolü; uyuşmazlıkta `400`, para hareketi yok | Çözüldü |
| 20 | Talep oluşturma iki ayrı `SaveChanges` ile audit/timeline’dan kopabiliyordu; reject/cancel yarışı 500 üretebiliyordu | Oluşturma tek transaction; reject/cancel `DbUpdateConcurrencyException` → `409` | Çözüldü |
| 21 | Demo seed/reset eski Owner JWT’siyle çağrılabiliyordu | Service, aktörün güncel DB rolünü/durumunu doğruluyor | Çözüldü |
| 22 | Yeni üye login “sistem atıyor” görünüyordu; UAT logunda uygulamalar kapanmıştı | Yeni üye akışı sentetik olarak yeniden üretildi; hata auth değil, foreground launcher’ın SIGTERM’de çocukları kapatması. `start_local_detached.py` eklendi; UAT’e dokunulmadı | Kök neden + güvenli launcher |
| 23 | Hesap türü/adı yoktu; Hesaplarım tek düz listeydi | `AccountType`, `AccountName`, DTO/API/DB/onay response ve dashboard; Vadesiz/Vadeli/Yatırım sekmeleri, kart/detay | Çözüldü |
| 24 | İlk hesap açılışı mevcut hesap seçimine bağlı görünüyordu; ad düzenleme yoktu | Hesapsız müşteri CTA + AccountOpening formu; ad/tür; sahip-only rename (XSS/IDOR), deposit/withdraw talepleri | Çözüldü |

## Bilinçli sınırlar

- **SQL Server doğrulanmadı.** İki yeni migration üretildi/belgelendi; apply,
  decimal ve gerçek concurrency testi yapılmadı. SQLite Development atılabilir.
- **Legacy roller:** `Admin=1`, `User=2` bilinçli geçersiz; otomatik Owner/
  Customer dönüşümü yok. Operatör eşlemesi gerekir.
- **FX yok.** Limit/risk hesap para birimi cinsindendir; çapraz para birimi
  transfer/onay reddedilir.
- **Rapor saat dilimi UTC.** Dekont Europe/Istanbul; bankacılık gün sonu takvimi
  yok.
- **Login throttle bellek içi.** Dağıtık örneklerde paylaşımlı store gerekir.
- **Production sertleştirmesi yok.** Secret manager, WAF, dağıtık gözlemlenebilirlik,
  audit retention/immutability, sızma/yük/erişilebilirlik denetimi, uyumluluk
  sertifikası yok.
- **Manuel UAT yapılmadı.** Checklist kullanıcı içindir.
- **Screenshot:** gerçek dashboard'dan 13 ekran alındı; sahte görsel üretilmedi.
  Yine de manuel UAT ve güncel UI kontrolü gerekir.
- **VS Code GUI doğrulanmadı.** launchd/PATH adımı kullanıcıda.
- **Tarayıcı görsel otomasyonu yok.** Smoke HTTP ve dashboard guard düzeyinde;
  gerçek DOM/ekran görüntüsü testi değil.
- **Paket lisans/bakım değerlendirmesi** kapsam dışı; sürümler topluca
  yükseltilmedi.

## Portföy notu

README İngilizce 30 saniyelik anlatı + Türkçe özet, gerçek test sayıları ve dürüst
sınırlarla yazıldı. AI katkısı [`PORTFOLIO_NOTES.md`](PORTFOLIO_NOTES.md) içinde
açıklandı. Screenshot'lar gerçek dashboard'dan alınana kadar eklenmemeli; mevcut
sayfalar shot list olarak README'de belirtildi.
