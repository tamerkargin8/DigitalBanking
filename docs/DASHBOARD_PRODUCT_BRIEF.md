# Dashboard product brief and delivered scope

- Durum: **Owner / DelegatedApprover / Customer dashboard'u uygulandı**; UAT ve
  gerçek ekran görüntüleri `docs/screenshots/` altında alındı.
- Amaç: CRUD tablosundan fazla, banka operasyonu hissi veren; Türkçe, erişilebilir,
  mobil uyumlu ve güvenli bir demo.
- Kapsam dışı: gerçek para, gerçek banka/ödeme entegrasyonu, üretim güvenliği/
  uyumluluk iddiası.

## 1. Deneyim ilkeleri

- Lacivert/nefes alan nötr palet; kırmızı yalnız hata/red, amber risk; gerçek
  banka markası taklidi yok.
- Türkçe tutarlı terminoloji; exception/stack/token ekranda yok.
- Para birimi bazlı kartlar; **kurla toplama yok**; dekontta maskeli hesap.
- Her ekranda boş/yükleniyor/hata/başarı durumu; işlem öncesi özet, sonrasında
  durum/dekont.
- Klavye erişimi, görünür focus, renk+metin rozetleri, WCAG AA hedefi, mobil tek
  kolon.
- Buton gizleme güvenlik değildir; API ve service katmanı ayrıca kontrol eder.

## 2. Roller ve ekranlar

### Owner / SuperAdmin

| Ekran | İşlev |
|---|---|
| Overview | Bekleyen/onaylanan/reddedilen talepler, para birimi bazlı özet, son audit |
| Onay Kuyruğu | Pending/approved/rejected/cancelled; request type/currency filtre; detay/dekont |
| Üyeler | PendingActivation/aktif/pasif; aktivasyon, Customer/DelegatedApprover rolü, disable |
| Yetkiler & Limitler | Kullanıcı + operasyon + para birimi + `MaxAmount` + opsiyonel `DailyLimit`/expiry; deactivate |
| Müşteriler / Hesaplar | Arama, müşteri-hesap ilişkisi, bakiye/hareket |
| Operasyonel Talep | Owner adına transfer/deposit/withdrawal/account opening; **kendi talebini onaylayamaz** |
| Audit | Filtre + sayfalama; şifre/token yok |
| Mutabakat | Batch listesi/detay, örnek CSV, export, istisna yönetimi |
| Demo Merkezi | Development-only seed/reset/senaryo kartları |
| Rapor | Tarih/para birimi/adet; gün sonu dahil |

### DelegatedApprover

- Yalnız **uygun** pending kuyruk (operasyon + para birimi + limit + günlük limit
  + expiry DB'den kontrol edilir).
- Kendi incelediği kayıtlar; red gerekçesi.
- Üye/rol/yetki yönetimi yok; linkler görünmez ve URL guard'ı vardır.
- Reconciliation exception onayı operasyon izniyle yapılabilir; parasal limit yok.

### Customer

| Ekran | İşlev |
|---|---|
| Ana Sayfa | Kendi TRY/USD/EUR bakiye kartları (ayrı), son hareketler |
| Hesaplar | Tümü/Vadesiz/Vadeli/Yatırım sekmeleri; ad/tür/para birimi/bakiye/maskeli no; Hesap Aç; detay/hareket; IDOR 404 |
| Transfer | Kendi hesabı → alıcı → tutar/para birimi → ön özet → talep; `Idempotency-Key` |
| Hesap Aç / Para Yatır / Para Çek | İlk hesap seçimden bağımsız; tür/ad/para birimi; tutar/açıklama özeti; Pending/onay/red takibi |
| Talepler | Kendi transfer/deposit/withdrawal/account opening talepleri; durum/gerekçe |
| Ödemeler / Dekont | Timeline; tamamlanmışta maskeli, Europe/Istanbul, DEMO uyarılı print-to-PDF |

## 2b. Yeni kullanıcı kapsamı — hesap türleri ve ilk hesap

- `Hesaplarım` sekmeleri: **Tümü, Vadesiz, Vadeli, Yatırım**. Her sekmede boş
  durum ve görünür **Hesap Aç** düğmesi; hesap kartında ad, tür, para birimi,
  bakiye, maskeli numara.
- Hesap açma formu: tür, kullanıcı adı, para birimi. Mevcut hesap seçimi
  gerektirmez; ilk hesap sıfır bakiyeyle açılır ve onay sonrası listelenir.
- Hesap adı sonradan yalnız sahibi tarafından düzenlenebilir; boşluk, uzunluk,
  XSS ve IDOR kontrolleri sunucuda uygulanır.
- Her hesap detayında **Para Yatır** ve **Para Çek** talepleri; tutar/açıklama
  özeti, Pending/Approved/Rejected takibi. Bakiye yalnız yetkili onayında değişir.
- Vadeli/Yatırım bir hesap türü ve yönetim demosudur; faiz/tahakkuk/alım-satım/
  kur getirisi hesaplanmaz, bu sınır ekranda açıkça yazılır.
- Eski hesaplar varsayılan **Vadesiz** kabul edilir; additive SQL Server
  migration ve SQLite için veri kaybettirmeyen operatör upgrade script'i.

## 3. Kritik akışlar

1. **Kayıt → aktivasyon:** public kayıt Pending; Owner aktive eder; rol atar.
2. **Talep → onay:** her finansal talep Pending; para oluşturmada hareket etmez;
   yetkili onayıyla tek transactionda ledger + timeline + audit.
3. **Limit:** 100.000,00 TRY dahil; 100.000,01 Owner'a. Günlük limit opsiyonel.
4. **İptal/red:** bakiye değişmez; rezervasyon serbest; gerekçe görünür.
5. **Yetki iptali:** deactivate/rol düşürme eski JWT'yi anında etkisiz kılar.
6. **Mutabakat:** preview → import → sınıflandırma → istisna; bakiye düzeltmesi yok.
7. **Demo reset:** yalnız `IsDemo`; UAT verisi korunur.

## 4. Güvenlik ve oturum

- Dashboard JWT'yi server-side session'da tutar; ekrana basmaz.
- HttpOnly/SameSite=Strict cookie, 30 dk idle, logout'ta temizlik.
- Tüm formlarda antiforgery; API'de 401 oturumu kapatır.
- API 403/404 mesajları bilgi sızdırmaz; müşteri IDOR'da 404.
- Rol guard'ları `SecurePageModel` içinde; API/service kontrolleri asıl sınırdır.

## 5. Erişilebilirlik / UX kabulü

- Tüm form/aksiyonlar Tab ile; görünür focus; `aria-label`/tablo başlıkları.
- Renk tek başına anlam taşımaz; rozet + metin.
- Mobilde tek kolon, tablolarda yatay kaydırma/kart.
- Boş liste, API hatası, oturum bitişi ve başarı geri bildirimi.
- Tutarlar `tr-TR` biçiminde; input model binding invariant (nokta ondalık)
  dashboard tarafında sabitlendi.

## 6. Açık kalanlar

- **Manuel UAT:** [`UAT_CHECKLIST.md`](UAT_CHECKLIST.md) doldurulmadı.
- **Gerçek ekran görüntüsü:** alındı (`docs/screenshots/`, 13 ekran, sentetik veri).
  UAT sırasında güncel UI ile yenilenmeli; sahte görsel yok.
- **SQL Server:** migration apply/gerçek concurrency doğrulanmadı.
- **Tarayıcı otomasyonu:** smoke HTTP + dashboard guard düzeyinde; görsel/
  erişilebilirlik otomasyonu yok.
- **Screenshot shot list:** Setup, Owner overview, approval queue/detail,
  Yetkiler & Limitler, Customer home, transfer summary, receipt, reconciliation
  detail, demo center. Sentetik veri ve gizli bilgi yok.
