# Kullanıcı kabul testi — kısa rehber

Adres: http://127.0.0.1:5481
Durum: **Kullanıcının manuel testi bekleniyor.** Otomatik rol testleri bu onayın yerine geçmez.

Mevcut Owner ve kayıtlarınızı kullanın. Demo sıfırlama/seed çalıştırmayın. Şifre veya hesap bilgilerini rapora/sohbete eklemeyin. Kullanıcı adları ve parolalar sizin kontrolünüzdedir; örnek persona adlarının bu UAT ortamında bulunduğu varsayılmaz.

| Adım | Yapılacak | Beklenen | Kullanıcı sonucu |
|---|---|---|---|
| 1 | Owner olarak giriş; üyeler, onaylar, muhasebe ve operasyon ekranlarını aç | Ekranlar hatasız; para birimleri ayrı gösterilir | Bekliyor |
| 2 | Ayrı tarayıcı oturumunda yeni sentetik müşteri oluştur, Owner ile aktive et | Aktivasyondan önce giriş kapalı; sonra müşteri ekranı açılır | Bekliyor |
| 3 | Müşteriyle boş hesap ekranından vadesiz/vadeli/yatırım türü ve isim seçerek talep oluştur | Talep onaya düşer; onaysız hesap/bakiye değişmez | Bekliyor |
| 4 | Müşteriden yatırma/çekme/transfer talep et; Owner ile onayla veya reddet | Yalnız onaylanan hareket bakiyeye yansır; durum/dekont güncellenir | Bekliyor |
| 5 | Owner ile ayrı sentetik yetkiliye 100000 TRY kapsamı tanımla | 100000 dahil uygun talep görünür; 100000.01 yetki dışıdır; kendi talebi onaylanamaz | Bekliyor |
| 6 | Yetkiyi iptal et; yetkilinin açık oturumundan tekrar dene | Eski oturum yetkiyi geri kazandırmaz | Bekliyor |
| 7 | Owner muhasebe kaydını ve operasyon atama/SLA ekranını incele | Borç/alacak dengeli; atama limitleri değiştirmez; para birimleri ayrı | Bekliyor |
| 8 | Çıkış yap, geri tuşu ve korumalı adresi dene | Yetkili veri erişimi için yeniden giriş gerekir | Bekliyor |

Eski bakiye muhasebe açılışı ayrı bir Owner işlemidir: önce önizlemeyi inceleyin; otomatik baseline gönderilmedi. Vadeli/yatırım türleri demo hesap sınıflarıdır; gerçek faiz veya menkul kıymet alım satımı yapılmaz.

Hata bildirimi: adım numarası, rol, beklenen ve görülen sonuç. Görsel paylaşırsanız ad, hesap no, bakiye ve gizli bilgileri maskeleyin. Ayrıntılı kapsam: UAT_CHECKLIST.md.

## Yeni görünümün son kontrolü

Müşteride Hesap Özeti → hesap kartları ve Hızlı İşlemler; kurucu yöneticide Genel Bakış → Onay Kuyruğu/Operasyon Merkezi bağlantıları kullanılır. Mobilde üstteki menü düğmesiyle gezinme açılır, Escape ile kapanır. Bakiye ve hesapları para birimi bazında ayrı değerlendirin. Bu tasarım kontrolünü kullanıcı olarak henüz tamamlamadınız; sonuç alanları bekliyor.
