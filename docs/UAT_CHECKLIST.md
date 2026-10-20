# UAT checklist — Owner, delegated approval and customer flows

> **Manuel testtir.** Hiçbir madde ajan tarafından "yapıldı" işaretlenmemiştir.
> Sonuç sütununu kullanıcı doldurur. Yalnız sentetik veri kullanın; şifre/token'ı
> sohbete, ekran görüntüsüne veya commit'e koymayın.

## 0. Hazırlık

1. Temiz demo DB (isteğe bağlı): `.local/digitalbanking.db*` dosyalarını arşivleyin.
2. Build + çalıştırın:
   ```sh
   bash scripts/dotnet.sh build DigitalBanking.slnx
   python3 scripts/run_local.py --api-port 5180 --dashboard-port 5181
   ```
3. Dashboard'u açın (bu worktree'de `http://127.0.0.1:5181`; kendi UAT kopyanızda
   ilgili port, ör. `5481`). Owner yoksa `/Setup`'a yönlenmeli.
4. İlk Owner'ı **yalnız localhost** üzerinden oluşturun. Şifreyi `.local` dışında
   paylaşmayın; tekrar `/Setup` denemesinin reddedildiğini doğrulayın. İki
   eşzamanlı setup isteğinde yalnız biri `200`, diğeri `409` almalı; DB'de tek
   `Role=Owner` kalmalı.
5. Owner varken eski bir `/Setup` sekmesinden POST gönderin: form işlenmemeli,
   login sayfasında Türkçe "Owner oluşturuldu" bildirimi görünmeli.
6. Setup/register/login limitleri ayrıdır; limite takılırsanız `429` mesajı
   Türkçe olmalı ve `Retry-After` başlığı bulunmalı. Limitleri test için kaldırmayın.
7. Demo şifresini `.local/credentials.json` içinden yerel olarak okuyun; ekrana
   veya nota yazmayın. Gerçek Owner şifresini istemeyin/paylaşmayın.
8. SQL Server testi bu ortamda yapılmadı; SQLite sonucu SQL Server kanıtı değildir.

### Personas

| Persona | Kullanıcı | Rol / durum | Beklenen |
|---|---|---|---|
| Owner | yerel setup | Owner | Tüm talepler/audit/üyeler/yetkiler; kendi talebini onaylayamaz |
| 100k approver | `approver.100k` | DelegatedApprover | TRY transfer/deposit/withdrawal/account opening 100.000 dahil; exception |
| Düşük limitli approver | `approver.low` | DelegatedApprover | TRY transfer 10.000; üstü Owner |
| Müşteri A | `customer.a` | Customer (aktif) | TRY/USD/EUR hesapları; yalnız kendi verisi |
| Müşteri B | `customer.b` | Customer (aktif) | Alıcı; A'nın verisini göremez |
| Bekleyen üye | `applicant.pending` | PendingActivation | Login reddi; Owner aktivasyonu sonrası Customer |

---

## A. Owner / üyelik / güvenlik

| ID | Adım | Beklenen | Sonuç |
|---|---|---|---|
| A1 | Owner ile giriş | `/Admin/Overview`; Owner menüsü; başka rol menüsü yok | ☐ |
| A2 | `/Setup` adresini tekrar aç / setup iste | Kurulum kapalı; `409`/yönlendirme; yeni Owner oluşmaz | ☐ |
| A2b | Owner varken eski Setup formunu POST et | Form işlenmez; login sayfasında Türkçe "Owner oluşturuldu" bilgisi | ☐ |
| A3 | Public `/Register` ile yeni üye oluştur; role/limit alanı göndermeyi dene | `PendingActivation`; ekstra alan yok sayılır; login 403 | ☐ |
| A4 | Aynı kullanıcı adı/e-posta ile tekrar kayıt | `409`; ikinci hesap yok | ☐ |
| A5 | Geçersiz e-posta/şifre uyuşmazlığı/zayıf şifre | `400`; teknik ayrıntı yok | ☐ |
| A6 | Owner → Üyeler: bekleyen üyeyi aktive et | Durum Active; Customer profili oluşur; login çalışır | ☐ |
| A7 | Owner → Üyeler: bir üyeyi DelegatedApprover yap; sonra Customer'a düşür | Rol değişir; Customer'a düşünce delegasyonları kapanır | ☐ |
| A8 | Owner kendini disable/demote etmeyi dene; Owner rolü atamayı dene | `400`; son Owner korunur; Owner atanamaz | ☐ |
| A9 | Owner → Yetkiler & Limitler: 100.000 TRY transfer, 50.000 günlük deposit, geçmiş expiry dene | Kayıt oluşur; geçmiş expiry reddedilir; delegated yalnız kendi kapsamını görür | ☐ |
| A10 | DelegatedApprover ile `/Admin/Users`, `/Admin/Delegations`, rol endpoint'i | `403`/yönlendirme; veri sızmaz | ☐ |
| A11 | 5 hatalı login sonrası doğru şifre | `429`; Türkçe bekleme mesajı; token üretilmez | ☐ |
| A11b | Setup/register limitini ayrı dene | Login limitinden bağımsız; `429` + `Retry-After`, limit kaldırılmaz | ☐ |
| A12 | Audit sayfası | OWNER_SETUP, MEMBERSHIP_*, USER_ROLE_CHANGED, DELEGATION_*, APPROVAL_*; şifre/token yok | ☐ |

---

## B. 100.000 TRY sınırı ve onay akışı

| ID | Adım | Beklenen | Sonuç |
|---|---|---|---|
| B1 | Müşteri A ile giriş; TRY hesabından B'ye **99.999,99** transfer talebi | `Pending`; bakiye değişmez; approver kuyruğunda görünür | ☐ |
| B2 | `approver.100k` ile onayla | `Approved/Completed`; bakiye bir kez düşer; A/B hareketleri oluşur | ☐ |
| B3 | Aynı talebi tekrar onayla | `400`; bakiye değişmez | ☐ |
| B4 | **100.000,00** TRY talep oluştur; approver onayla | Onaylanır (limit dahil); bakiye bir kez | ☐ |
| B5 | **100.000,01** TRY talep oluştur | Pending; `approver.100k` kuyruğunda **yok**; approver onayı `400`; Owner reject edebilir | ☐ |
| B6 | Aynı 100.000,01 talebi Owner ile onaylamayı dene (aynı kişi değilse) | Owner yetkisiyle onaylanabilir; kendi talebiyse `400` | ☐ |
| B7 | USD hesabından USD talep; yalnız TRY delegasyonu olan approver onaylasın | `400`; Owner'a yönlendirme; kur çevrimi yok | ☐ |
| B8 | `approver.low` ile 10.000 ve 10.000,01 TRY | 10.000 onaylanır; 10.000,01 reddedilir/Owner'a kalır | ☐ |
| B9 | Owner kendi oluşturduğu talebi onaylamayı/reddetmeyi dene | `400`; başka reviewer gerekir; iptal edilebilir | ☐ |
| B10 | İki giden talep bakiyeyi aşacak şekilde oluştur | İkinci talep `400` (rezervasyon); ilk talep iptal edilince yer açılır | ☐ |
| B11 | Onay anında bakiyeyi yetersiz bırak (uygun senaryo/UAT) | `400`; ledger ve bakiye değişmez; talep Pending kalır | ☐ |
| B12 | Talep beklerken müşteri–hesap sahipliği değişsin (kontrollü test) | Onay `400`; bakiye/ledger değişmez; talep Pending kalır | ☐ |

---

## C. Müşteri A/B — sahiplik, timeline, dekont

| ID | Adım | Beklenen | Sonuç |
|---|---|---|---|
| C1 | Müşteri A ana sayfa | TRY/USD/EUR kartları **ayrı**; kurla toplanmamış; yükleniyor/boş durum | ☐ |
| C2 | A'nın hesap detayı ve hareketleri | Yalnız A hesapları; tarih/tür/tutar/son bakiye; sayfalama | ☐ |
| C3 | URL'de B'nin hesap id'sini dene | `404`; B'nin bakiyesi/hareketi yok | ☐ |
| C4 | B ile giriş; A'nın ödeme/timeline id'sini dene | `404`; A verisi yok | ☐ |
| C5 | A ile transfer sihirbazı: özet ekranı | Hesap/alıcı/tutar/para birimi net; geri dönülebilir; masraf yok notu | ☐ |
| C6 | Formu çift tıkla / tekrar gönder | Tek talep; idempotent davranış; para ikizlenmez | ☐ |
| C7 | Pending talep timeline | Created/Pending; tamamlanmış dekont verisi yok | ☐ |
| C8 | Onay sonrası timeline/dekont | Created/Pending/Approved/Completed; maskeli hesap; Europe/Istanbul; **DEMO** uyarısı; print önizleme | ☐ |
| C9 | Reddedilen talep | Gerekçe müşteriye görünür; tamamlanmış dekont yok | ☐ |
| C10 | Müşteri kendi talebini iptal etsin | `Cancelled`; rezervasyon serbest; bakiye değişmez | ☐ |
| C11 | Müşteri deposit/withdraw/account opening talebi | Hepsi Pending; oluşturmada bakiye değişmez; onay sonrası uygulanır | ☐ |
| C12 | Doğrudan `/api/accounts/transfer` vb. denemesi | `400` / yetki; onay akışı atlanamaz | ☐ |

---

## D. Mutabakat ve istisna

| ID | Adım | Beklenen | Sonuç |
|---|---|---|---|
| D1 | Owner → Mutabakat → örnek CSV indir; preview | Satırlar/validasyon; import ayrı adım; dosya kalıcı | ☐ |
| D2 | Aynı dosyayı tekrar yükle | SHA-256 replay; ikinci batch/işlem yok | ☐ |
| D3 | Matched import | Eşleşen satırlar; bakiye değişmez | ☐ |
| D4 | Mismatch import | OnlyExternal/OnlySystem/AmountMismatch/Duplicate sınıfları; pending onaylar ayrı | ☐ |
| D5 | OnlyExternal satırdan istisna aç | `Open` | ☐ |
| D6 | Review → Propose; aynı kişi approve etsin | `400`; durum `ResolutionProposed` kalır | ☐ |
| D7 | Farklı yetkili (Owner veya exception delegasyonu olan approver) approve | `Closed`; **bakiye değişmez**; audit | ☐ |
| D8 | Reopen | `Open`; çözüm alanları temiz | ☐ |
| D9 | Export'u indir; `=2+2` hücreli dosyayı import/export et | Formül `'` ile nötrlenir; çalıştırılmaz | ☐ |
| D10 | Boş/.txt/bozuk başlıklı CSV | `400`/invalid; sessizce kabul edilmez | ☐ |

---

## E. Demo merkezi ve dashboard

| ID | Adım | Beklenen | Sonuç |
|---|---|---|---|
| E1 | Owner → Demo Merkezi; seed | Senaryo kartları; idempotent ikinci seed "zaten" | ☐ |
| E2 | Customer ile `/api/demo/*` / demo sayfası | `403`/yönlendirme | ☐ |
| E3 | Demo reset | Yalnız demo kayıtları silinir; UAT kullanıcı/hesap kalır; bakiye tutarlı | ☐ |
| E4 | Owner menüleri: özet/onay/üyeler/yetkiler/müşteriler/hesaplar/talep/audit/mutabakat/rapor | Hepsi açılır; hata/boş durumlar anlaşılır | ☐ |
| E5 | DelegatedApprover menüsü | Yalnız kuyruk/incelenenler; yönetim yok | ☐ |
| E6 | Customer menüsü | Hesap/transfer/talepler; admin yok | ☐ |
| E7 | Çıkış ve korumalı URL | Login'e dönüş; oturum temiz | ☐ |
| E8 | API'yi durdurup işlem yap | Güvenli "API'ye ulaşılamıyor"; stack trace/token yok | ☐ |
| E9 | Mobil genişlik + klavye + %200 zoom | Tek kolon, görünür focus, okunaklı; tüm aksiyonlar Tab ile | ☐ |
| E10 | Ekran görüntüsü alınacaksa | Gerçek ekran, sentetik veri, şifre/token yok | ☐ |

---

## Sonuç özeti (kullanıcı doldurur)

- Tarih:
- Ortam / commit:
- Geçen:
- Kalan:
- Notlar:
