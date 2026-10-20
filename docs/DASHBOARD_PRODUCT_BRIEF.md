# Dashboard product brief — next phase

- Durum: **Tasarım / plan.** Bu aşama başlatılmadı; mevcut dashboard yalnızca
  giriş/çıkış, küçük panel, hesap sıralaması raporu ve Admin kullanıcı yönetimi
  içerir.
- Amaç: sıradan CRUD tablosundan fazla, banka uygulaması hissi veren, Türkçe,
  tutarlı ve keyifli bir demo deneyimi; kullanıcı ve Admin için ayrı akışlar.
- Kapsam dışı: gerçek para, gerçek banka/kurum entegrasyonu, canlı ödeme ağı,
  üretim güvenliği/uyumluluğu iddiası.

## 1. Mevcut durum ve kritik boşluk

Bugün backend'de:

- `Admin` operasyonel yetkilere sahiptir; maker-checker akışını kullanabilir.
- `User` şu an yalnız kimlik doğrulaması yapabilir; sahiplik olmadığı için müşteri/hesap/rapor verisine erişemez (deny-by-default).
- `User` ile `Customer`/`Account` arasında **sahiplik ilişkisi yoktur**.

Bu nedenle müşteri self-servis deneyimi yalnızca buton gizleyerek yapılamaz.
Önce sunucu tarafı sahiplik ve yetki modeli kurulmalı; aksi halde bir kullanıcı
başka müşterinin hesabını görebilir veya ondan transfer talep edebilir. Bu brief
o genişletmeyi ayrı bir aşama olarak tanımlar.

## 2. Demo personası (dört persona)

| Persona | Rol / bağ | Demo amacı |
|---|---|---|
| **Müşteri A** | `User`, sentetik müşteri A'ya bağlı | Kendi TRY/USD/EUR hesapları, bakiye kartları, son işlemler, alıcı seçerek transfer talebi, sonuç/durum takibi |
| **Müşteri B** | `User`, sentetik müşteri B'ye bağlı | Alıcı taraf; A'nın talebinin B hesabına ulaşması; B kendi verisini görür |
| **Admin maker** | `Admin` | Müşteri/hesap açma, teller yatırma/çekme, düşük risk doğrudan transfer, onay talebi oluşturma |
| **Admin checker** | ikinci `Admin` | Onay kuyruğunu görme, risk/durum rozetleriyle onay/red; kendi talebini inceleyemez |

Sentetik veri: iki müşteri, her birinde TRY/USD/EUR hesapları, farklı risk
seviyelerinde (Low/Medium/High/Critical) örnek ödemeler. Gerçek kişi/hesap/kurum
verisi kullanılmaz; hesap numaraları ekranda maskelenir (`TR** **** **** 1234`
benzeri, yalnız son 4 hane).

**Para yatırma/çekme:** Bunlar demo ortamında **teller/operasyon** işlemleridir ve
Admin'de kalmalıdır; müşterinin kendi kendine "para yatırma" yapması para arzı
yaratmak olur. Müşteri ekranında bakiye ve hareketler gösterilir; para girişi
gerekiyorsa Admin teller işlemi yapar. Bu kural UAT'te açıkça test edilir.

## 3. Hedef deneyim

### Admin

- **Operasyon özeti:** bekleyen onay sayısı (risk bazında), bugünkü işlem hacmi
  (para birimi bazında ayrı), son audit olayları, kısa uyarılar.
- **Müşteri/hesap yönetimi:** arama, müşteri detayı, hesap açma (para birimi +
  açılış bakiyesi), müşteriye ait hesaplar.
- **Ödeme/onay kuyruğu:** maker, tutar/para birimi, risk ve durum rozetleri,
  talep zamanı, onay/red aksiyonu; red gerekçesi zorunlu; kendi talebinde
  aksiyon kapalı ve nedeni açıklanır.
- **Raporlar:** tarih aralığı, para birimi, adet; gün sonu dahil; özet toplamlar
  para birimi bazında; boş/hatalı filtre mesajları.
- **Audit:** tarih/aksiyon/kullanıcı filtresi, sayfalama; şifre/token yok.

### Müşteri

- **Profil/özet:** ad-soyad, maskeli müşteri no, rol; yalnız kendi verisi.
- **Bakiye kartları:** her para birimi ayrı kart; **kur olmadan toplanmaz**.
  Örn. "TRY 12.500,00", "USD 2.400,00", "EUR 800,00" ayrı.
- **Son işlemler:** tarih, tür (yatırma/çekme/transfer giriş/çıkış), tutar,
  işlem sonrası bakiye, açıklama; sayfalama.
- **Transfer talebi sihirbazı:** kendi hesabını seç → alıcı seç (kayıtlı
  beneficiary veya demo alıcı listesi) → tutar/para birimi → **özet/onay ekranı**
  → gönder. Sonuç: `Pending` / `AutoApproved` / `Approved` / `Rejected` ve makbuz.
- **Durum takibi:** kendi taleplerinin listesi ve detayı; checker bilgisi
  gösterilecekse yalnız maskeli/güvenli biçimde.

### Ortak UX ilkeleri

- Türkçe, tutarlı terminoloji; teknik exception/stack/token asla ekranda yok.
- **Lacivert/nefes alan nötr** palet (ör. `#0B1F3A`, `#F4F6F8`, beyaz kart),
  kontrollü vurgu (mavi/teal); kırmızı yalnız hata/red, amber risk; gerçek banka
  markası taklit edilmez.
- Klavye erişimi: tüm form/aksiyonlar Tab ile; görünür focus; `aria-label`;
  renk tek başına anlam taşımaz (rozet + metin).
- Okunabilir kontrast (WCAG AA hedefi), 16px taban, mobil uyum (tek kolon,
  kartlar; tablolar yatay kaydırma veya kart görünümü).
- Her ekran için **loading / empty / error / success** durumları; iskelet veya
  "Yükleniyor…" göstergesi; hata tekrar dene; başarıda net geri bildirim.
- İşlem öncesi **özet/onay** (hesap, alıcı, tutar, para birimi, masraf yok notu);
  işlem sonrası **makbuz/durum** ekranı; çift gönderimi engellemek için buton
  disable + `Idempotency-Key`.
- Hesap numaraları maskeli; toplamlar para birimi bazında; tarih/saat açık.
- Erişilebilir tablo başlıkları, sıralama, sayfalama; boş durumda yönlendirici
  metin.

## 4. Sahiplik ve yetki modeli (öneri)

### Şema

- `Customer.UserId` (`uniqueidentifier?`) ekle; unique index (bir login bir
  müşteri profiline bağlanır). Alternatif `User.CustomerId` de olur; tek yön
  seçilmeli. Öneri: **`Customer.UserId`**, çünkü müşteri domain profilidir ve
  hesaplar zaten `Customer`'a bağlıdır.
- Admin kullanıcılarında `Customer.UserId` boş kalır.
- Sentetik demo seed'i Customer A/B'yi sırasıyla `customer.a`/`customer.b`
  kullanıcılarına bağlar; şifreler `.local/credentials.json` deseniyle rastgele
  üretilir, commit edilmez.
- Yeni migration yalnız ekleme + index; eski migration değiştirilmez. SQL Server
  backfill operatör adımıdır; bu ortamda uygulanmaz. `NULL` kalan müşteriler
  self-servis kullanamaz (güvenli varsayılan).

### Sunucu tarafı yetki

- `ActorContext`'e müşteri kimliği eklenir veya `ICustomerAccessResolver` ile
  doğrulanmış kullanıcıdan çözülür.
- Yeni self-service endpoint'leri:
  - `GET /api/me/profile` — kendi müşteri özeti (başkasınınki yok).
  - `GET /api/me/accounts` — yalnız `Customer.UserId == actor.UserId` hesapları.
  - `GET /api/me/accounts/{id}/transactions` — sahiplik doğrulaması; başkasının
    hesabı 404/403 (bilgi sızdırmayan yanıt).
  - `POST /api/me/transfers` — yalnız kendi hesabından; alıcı hesap seçimi;
    tutar/para birimi/bakiye; `PaymentApproval` oluşturur; düşük risk auto,
    orta+ Admin checker.
  - `GET /api/me/transfers` / `GET /api/me/transfers/{id}` — yalnız kendi
    talepleri; durum/makbuz.
  - Alıcı listesi için `Beneficiary` tablosu veya demo "alıcı hesap arama"
    endpoint'i; başka müşterilerin tüm hesaplarını listelemek yok.
- Yetki **controller + service** katmanında; dashboard'da buton gizleme yalnız
  UX'dir, güvenlik değildir.
- Admin onay/red endpoint'leri mevcut haliyle Admin'e kapalı kalır; müşteri
  kendi talebini onaylayamaz.
- Idempotency: müşteri transfer talebi ve Admin onay/red için zorunlu/önerilen
  `Idempotency-Key`; çift tıklama ve ağ retry para hareketini ikizlemez.
- Oturum: dashboard session + JWT; 401'de oturum temizlenir, girişe yönlendirme
  ve "oturum süresi doldu" mesajı; CSRF antiforgery tüm formlarda.

### Migration / test etkisi

- Yeni migration: `Customer.UserId` + unique index. SQL Server'da mevcut
  müşteriler NULL kalır; operatör eşlemesi dokümante edilir. SQLite demo
  `EnsureCreated` ile yeniden oluşturulur.
- Testler: sahiplik (A, B'nin hesabını göremez/transfer edemez), Admin tümünü
  görür, self-approval engeli, yetersiz bakiye, aynı hesap, para birimi uyuşmazlığı,
  idempotent çift tıklama, red, oturum bitişi, rapor filtreleri.
- Mevcut 95 test + 71 smoke korunur; yeni testler eklenir. SQLite testleri SQL
  Server kanıtı sayılmaz.

## 5. Ekran envanteri (öneri)

| Ekran | Rol | Not |
|---|---|---|
| Giriş / çıkış | tümü | Türkçe, rate-limit ve throttle mesajı, oturum süresi |
| Admin özet | Admin | risk bazında bekleyen, para birimi bazında hacim |
| Müşteriler / müşteri detayı | Admin | arama, hesaplar, maskeli no |
| Hesap açma / hesap detayı | Admin | para birimi, açılış bakiyesi, ledger |
| İşlemler (yatır/çek) | Admin | teller; makbuz |
| Ödeme/onay kuyruğu | Admin | maker-checker, risk/durum rozeti, red gerekçesi |
| Raporlar | Admin | tarih/para birimi/adet, gün sonu dahil |
| Audit | Admin | filtre + sayfalama |
| Kullanıcı yönetimi | Admin | rol/şifre politikası |
| Müşteri özeti | User | kendi bakiye kartları (ayrı para birimleri) |
| Hesap detayı / hareketler | User | yalnız kendi hesabı |
| Transfer sihirbazı | User | özet/onay → talep → makbuz/durum |
| Talep durumu | User | Pending/AutoApproved/Approved/Rejected |

## 6. Kabul ölçütleri

- Dört persona ile uçtan uca senaryo çalışır; A, B'nin verisini göremez; Admin
  checker farklı kişidir; self-approval engellenir.
- Düşük risk otomatik, orta+ Pending; onay/red sonrası bakiye ve audit tutarlı.
- Çift tıklama/retry tek hareket üretir; yetersiz bakiye/aynı hesap/para birimi
  hataları anlaşılır ve sırsız gösterilir.
- Bakiye kartları para birimi bazında; kur çevrimi/toplama yok.
- Boş/yükleniyor/hata/success durumları; klavye ve mobil; makbuz/durum ekranı.
- Testler: mevcut 95 + yeni sahiplik/UX sözleşme testleri; smoke genişletilir.
- README gerçek ekran görüntüleriyle güncellenir (sahte görsel yok).
- SQL Server migration uygulanmadı/doğrulanmadı ifadesi korunur.

## 7. Kaba efor (taahhüt değil)

| İş | Kaba efor |
|---|---|
| Sahiplik şeması + migration + seed personas | 1–2 gün |
| Self-service API + yetki + testler | 2–3 gün |
| Admin dashboard ekranları | 3–4 gün |
| Müşteri dashboard + transfer sihirbazı + erişilebilirlik | 3–4 gün |
| Uçtan uca test, UAT, README/ekran görüntüleri | 2–3 gün |

Toplam kaba 11–16 geliştirici günü; tek kişi/AI-destekli tempoda değişebilir.
SQL Server doğrulaması ayrıca ortam gerektirir.

## 8. Riskler ve açık kararlar

- **Sahiplik olmadan self-servis yasak.** Bu brief'in 4. bölümü uygulanmadan
  müşteri transfer ekranı açılmamalı.
- **Mevcut `User` rolü değişecek.** Geriye uyum: yeni self-service endpoint'ler;
  mevcut Admin endpoint'leri korunur. `User` şu an veri erişimine sahip değildir;
  yeni erişim yalnız kendi müşteri/hesap verisiyle açılır.
- **Alıcı keşfi** müşteri numarası/hesap numarası enumerasyonuna açık olmamalı;
  kayıtlı beneficiary veya maskeli arama tercih edilmeli.
- **Para birimi karışımı** raporlarda toplanmamalı; her toplam para birimi bazında.
- **Demo deposit** müşteriye verilmemeli; teller işlemi Admin'de kalmalı.
- **SQL Server** olmadan migration/backfill doğrulanmış sayılmaz.
- **Ekran görüntüleri** ancak gerçek dashboard hazır olduğunda eklenir.
