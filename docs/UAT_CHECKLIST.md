# UAT checklist — manual acceptance

> **Manuel testtir.** Bu dosyadaki hiçbir madde ajan tarafından "yapıldı" olarak
> işaretlenmemiştir. Sonuç sütunu kullanıcı tarafından doldurulur. Dashboard'un
> tamamı henüz yoktur; B bölümü sonraki dashboard aşaması içindir.

## 0. Hazırlık ve güvenlik

1. Yerel demo servislerini başlatın:
   ```sh
   bash scripts/dotnet.sh build DigitalBanking.slnx
   python3 scripts/run_local.py --api-port 5180 --dashboard-port 5181
   ```
2. Açın: dashboard `http://127.0.0.1:5181`, Swagger `http://127.0.0.1:5180/swagger`.
3. Demo şifresi ve JWT anahtarı `.local/credentials.json` içinde, yalnız kullanıcı
   tarafından okunabilir (`0600`) olarak tutulur. **Şifreyi/token'ı sohbete,
   ekran görüntüsüne, issue/PR'a veya commit'e koymayın.** Dosyayı paylaşmayın.
4. Yalnız sentetik veri kullanın. Gerçek müşteri/hesap/para/kurum bilgisi girmeyin.
5. Sonuçları bu dosyaya veya ayrı bir nota yazarken şifre/token yazmayın.
6. SQL Server testi bu ortamda yapılmadı; SQLite demo sonucu SQL Server kanıtı
   değildir.

### Demo personları

| Persona | Rol | Nasıl elde edilir | Beklenen erişim |
|---|---|---|---|
| Müşteri A | `User` | Sonraki aşamada sentetik `customer.a` seed'i; şimdilik `tamer` salt-okunur | Yalnız kendi profil/hesapları (sonraki aşama); şimdilik rapor okuma |
| Müşteri B | `User` | Sonraki aşamada sentetik `customer.b` seed'i | Yalnız kendi verisi; A'nın hesabını göremez |
| Admin maker | `Admin` | Seed `admin` | Tüm operasyon; kendi talebini onaylayamaz |
| Admin checker | ikinci `Admin` | Admin > Kullanıcılar > Kullanıcı oluştur (Admin rolü) | Onay kuyruğu; maker'dan farklı kişi |

> Mevcut backend'de `User` salt-okunurdur ve User–Customer sahipliği yoktur.
> Müşteri self-servis maddeleri **sonraki dashboard aşaması** içindir; bu aşamada
> "beklenen" olarak işaretlenen davranışlar backend/API düzeyindedir.

---

## A. Bu teslimde manuel doğrulanabilir backend + minimal dashboard

| ID | Adım | Beklenen sonuç | Sonuç |
|---|---|---|---|
| A1 | Dashboard'da admin ile giriş yapın | Panel açılır; sağ üstte kullanıcı adı ve `Admin` rozeti | ☐ |
| A2 | Çıkış yapın, sonra `/` adresini açın | Login sayfasına yönlenir; "Çıkış yapıldı" bilgisi | ☐ |
| A3 | Yanlış şifreyle giriş deneyin | Teknik ayrıntı/token göstermeyen "kullanıcı adı veya şifre hatalı" mesajı | ☐ |
| A4 | `tamer` (User) ile giriş yapın, `/Admin/Users` açın | Panele yönlenir; "Admin yetkisi gerekir" uyarısı; kullanıcı listesi görünmez | ☐ |
| A5 | Admin ile `/Reports/TopAccounts` açın; tarih aralığı/para birimi/adet değiştirin | Sonuçlar para birimi bazında; hatalı tarih/adet için anlaşılır mesaj; boş aralıkta "veri yok" | ☐ |
| A6 | Tarih aralığını ters girin (bitiş < başlangıç) | "Bitiş tarihi başlangıçtan önce olamaz" mesajı; tablo bozulmaz | ☐ |
| A7 | 400 günden uzun aralık girin | "366 günü aşamaz" mesajı | ☐ |
| A8 | Swagger'da tokensız `GET /api/accounts` çağırın | `401` | ☐ |
| A9 | User token'ıyla `POST /api/transactions/deposit` çağırın | `403` (yetki yok) | ☐ |
| A10 | User token'ıyla `GET /api/audit` çağırın | `403` | ☐ |
| A11 | Admin maker ile ikinci bir Admin kullanıcı oluşturun; onunla giriş yapın | İkinci Admin girişi başarılı; rol `Admin` | ☐ |
| A12 | Maker ile 75.000 TRY onay talebi oluşturun (`POST /api/paymentapprovals`) | `201`, durum `Pending`, risk `Medium`, para birimi `TRY` | ☐ |
| A13 | Maker kendi talebini onaylamayı denesin | `400`; "kendi talebini inceleyemez" mesajı; bakiye değişmez | ☐ |
| A14 | Checker ile aynı talebi onaylayın | `204`; bakiye bir kez hareket eder; audit `PAYMENT_APPROVED` | ☐ |
| A15 | Checker ile aynı talebi tekrar onaylayın | `400`; bakiye ikinci kez değişmez | ☐ |
| A16 | Maker ile düşük riskli (ör. 100 TRY) talep oluşturun | `201`, durum `AutoApproved`, bakiye hemen hareket eder | ☐ |
| A17 | Orta riskli yeni bir talep oluşturup checker ile reddedin (gerekçe yazın) | `204`, durum `Rejected`, gerekçe görünür, bakiye değişmez | ☐ |
| A18 | Aynı `Idempotency-Key` ile aynı deposit isteğini iki kez gönderin | İkinci yanıt yeniden oynatılır (`Idempotency-Replayed: true`), bakiye bir kez artar | ☐ |
| A19 | Aynı anahtarla farklı tutar gönderin | `409`; ikinci hareket yok | ☐ |
| A20 | Yetersiz bakiye ile withdraw/transfer deneyin | `400`, "Insufficient balance" benzeri anlaşılır hata, bakiye değişmez | ☐ |
| A21 | Aynı hesaba transfer deneyin | `400`, anlaşılır hata | ☐ |
| A22 | TRY hesabından USD hesabına transfer/onay deneyin | `400`, para birimi uyuşmazlığı; kur çevrimi yok | ☐ |
| A23 | 50.000 ve üzeri doğrudan transfer deneyin | `400`; onay akışına yönlendirme mesajı | ☐ |
| A24 | Audit ekranı/API'sini Admin ile açın; aksiyon/kullanıcı filtresi uygulayın | Kayıtlar gelir; şifre/token görünmez | ☐ |
| A25 | Aynı anda iki farklı checker ile aynı talebi onaylamayı deneyin (iki sekme) | Yalnız biri başarılı; diğeri `409`/`400`; bakiye bir kez hareket eder | ☐ |
| A26 | Oturum süresi bitişi (JWT 2 saat) | Sonraki API çağrısında oturum temizlenir, giriş sayfasına "oturum süresi doldu" ile yönlenir | ☐ (sonraki UI aşamasında daha kolay) |

**Not:** A12–A25 adımları mevcut minimal dashboard'da hazır ekran olarak yoktur;
Swagger/HTTP ile yapılabilir. Onay kuyruğu ve müşteri ekranları B bölümündedir.

---

## B. Sonraki dashboard aşaması — dört persona, uçtan uca

> Bu bölüm **henüz uygulanmadı**. Dashboard tamamlandığında manuel kabul için
> kullanılacaktır. Beklenen davranışlar ürün brief'ine göredir.

### B1. Müşteri A — giriş, bakiye, hareketler

| ID | Adım | Beklenen sonuç | Sonuç |
|---|---|---|---|
| B1.1 | Müşteri A ile giriş yapın | Yalnız kendi adı/maskeli müşteri no'su; Admin menüsü yok | ☐ |
| B1.2 | Ana sayfayı açın | TRY/USD/EUR bakiye kartları **ayrı**; kurla toplanmamış; yükleniyor durumu görünür | ☐ |
| B1.3 | Son işlemleri açın | Yalnız A'nın hesapları; tarih/tür/tutar/sonraki bakiye; sayfalama | ☐ |
| B1.4 | URL'de B'nin hesap id'sini deneyin | `403/404`; B'nin bakiyesi/hareketi görünmez | ☐ |
| B1.5 | Mobil genişlik + klavye ile gezinin | Tek kolon, okunaklı kontrast, görünür focus, tüm aksiyonlar Tab ile | ☐ |

### B2. Müşteri A — transfer talebi

| ID | Adım | Beklenen sonuç | Sonuç |
|---|---|---|---|
| B2.1 | "Transfer talebi" sihirbazını açın | Yalnız A'nın hesapları ve kayıtlı/demo alıcılar seçilebilir; başka müşterilerin tüm hesapları listelenmez | ☐ |
| B2.2 | 75.000 TRY, alıcı B, açıklama girin; özet ekranına geçin | Hesap/alıcı/tutar/para birimi net; "masraf yok (demo)" notu; geri dönülebilir | ☐ |
| B2.3 | Onaylayıp gönderin | Sonuç `Pending`; makbuz/talep numarası; buton disable; `Idempotency-Key` ile tek hareket | ☐ |
| B2.4 | Aynı formu çift tıklayın/tekrar gönderin | Tek talep oluşur; ikinci istek aynı sonucu döndürür veya `409`; para ikizlenmez | ☐ |
| B2.5 | Düşük riskli 100 TRY talep gönderin | `AutoApproved`; bakiye hemen düşer; makbuzda durum net | ☐ |
| B2.6 | Yetersiz bakiye ile gönderin | `400`; anlaşılır hata; bakiye değişmez | ☐ |
| B2.7 | Aynı hesaba transfer deneyin | `400`; anlaşılır hata | ☐ |
| B2.8 | Farklı para birimli alıcı seçin | `400`; kur çevrimi yok uyarısı | ☐ |
| B2.9 | Talep durumunu açın | `Pending/Approved/Rejected` ve varsa gerekçe; yalnız A'nın talepleri | ☐ |

### B3. Müşteri B — alıcı tarafı

| ID | Adım | Beklenen sonuç | Sonuç |
|---|---|---|---|
| B3.1 | Müşteri B ile giriş yapın | Yalnız B'nin verisi; A'nın bakiyesi/hesabı görünmez | ☐ |
| B3.2 | Onaylanan transfer sonrası B'nin hareketlerini açın | `TransferIn` görünür; bakiye doğru; para birimi aynı | ☐ |
| B3.3 | B, A'nın talep id'sini URL'de denesin | Erişim yok; bilgi sızdırmayan `403/404` | ☐ |

### B4. Admin maker

| ID | Adım | Beklenen sonuç | Sonuç |
|---|---|---|---|
| B4.1 | Admin maker ile giriş; operasyon özeti | Bekleyen onaylar risk bazında; para birimi bazında hacim; loading/empty/error | ☐ |
| B4.2 | Müşteri açın, TRY/USD/EUR hesap açın (açılış bakiyeli) | Hesap oluşur; açılış deposit ledger'da; audit | ☐ |
| B4.3 | Teller yatırma/çekme yapın | Makbuz; bakiye ve audit tutarlı; yetersiz bakiyede hata | ☐ |
| B4.4 | Düşük risk doğrudan transfer yapın | Onay akışı gerekmez; makbuz | ☐ |
| B4.5 | 75.000 talep oluşturun | `Pending`; kendi talebinde onay butonu kapalı ve nedeni açık | ☐ |
| B4.6 | Müşteri listesinde arama yapın | Sonuçlar filtrelenir; boş durumda mesaj | ☐ |

### B5. Admin checker

| ID | Adım | Beklenen sonuç | Sonuç |
|---|---|---|---|
| B5.1 | Checker ile giriş; onay kuyruğu | Bekleyen talepler; risk ve durum rozetleri (renk + metin) | ☐ |
| B5.2 | Maker'ın talebini reddedin; gerekçe boş bırakın | Red engellenir; "gerekçe zorunlu" | ☐ |
| B5.3 | Gerekçeyle reddedin | Durum `Rejected`; bakiye değişmez; audit | ☐ |
| B5.4 | Başka bir talebi onaylayın | `Approved`; bakiye bir kez; makbuz; audit | ☐ |
| B5.5 | Aynı talebi ikinci kez onaylayın (çift tıklama) | İkinci işlem engellenir; bakiye ikizlenmez | ☐ |
| B5.6 | Aynı talebi iki checker sekmesinde aynı anda onaylayın | Yalnız biri başarılı; diğeri güvenli hata; bakiye bir kez | ☐ |
| B5.7 | Rapor filtreleri (tarih/para birimi/adet) | Gün sonu dahil; para birimleri ayrı; hatalı filtre anlaşılır | ☐ |
| B5.8 | Audit filtreleri | Kayıtlar gelir; şifre/token yok; sayfalama | ☐ |
| B5.9 | User ile Admin endpoint'ini deneyin | `403`; dashboard'da Admin menüsü görünmez | ☐ |

### B6. Oturum / hata / erişilebilirlik

| ID | Adım | Beklenen sonuç | Sonuç |
|---|---|---|---|
| B6.1 | Oturumu sonlandırın; korumalı sayfayı açın | Login'e yönlenir | ☐ |
| B6.2 | JWT süresi dolana kadar bekleyin (2 saat) veya oturum cookie'sini geçersiz kılın | Sonraki işlemde "oturum süresi doldu"; session temizlenir; token ekranda yok | ☐ |
| B6.3 | API'yi durdurun; dashboard'da işlem yapın | "API'ye ulaşılamıyor" gibi güvenli mesaj; stack trace yok | ☐ |
| B6.4 | Boş liste/boş rapor | "Veri yok" mesajı; boş tablo/0 sonuç karışıklığı yok | ☐ |
| B6.5 | Hatalı form girişleri | Alan bazlı/özet mesaj; teknik ayrıntı yok | ☐ |
| B6.6 | Klavye-only + yüksek kontrast + %200 zoom | Tüm akış kullanılabilir; odak kaybolmaz; metin okunaklı | ☐ |
| B6.7 | Ekran görüntüsü alınacaksa | Gerçek ekran; sentetik veri; şifre/token görünmüyor | ☐ |

---

## Sonuç özeti (kullanıcı doldurur)

- Tarih:
- Ortam / commit:
- Geçen:
- Kalan:
- Notlar:
