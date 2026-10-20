# Kullanıcının güncel talimatı ve DeepSeek ana görevi

Kullanıcı artık geliştirmeyi ağırlıklı olarak DeepSeek'in yapmasını, GPT'nin kapsamlı promptlarla yönlendirmesini ve az kullanılmasını istiyor. Önceki %5 kredi bekleme koşulu bu yeni talimatla değiştirilmiştir. Görevini şimdi başlat. Bu talimat, AGENTS.md ve DEEPSEEK_HANDOFF.md içindeki önceki yalnızca dashboard kapsamını genişletir; güvenli çalışma ve ayrı worktree kuralları sürer.

## Hedef

DigitalBanking'i çalışan, tutarlı, test edilmiş ve sunulabilir bir bankacılık portföy uygulamasına tamamla. Kullanıcıya her küçük uygulama kararı için soru sorma; mevcut yapı ve kanıtlara dayanarak ilerle. Gerçek bankaya bağlanma, gerçek müşteri verisi veya para kullanma. Ürünün educational/demo niteliğini koru.

## Önce durumu doğrula

Bu klasör /Users/tamerkargin/Documents/Codex/2026-10-20/gpt-imdi-x20/outputs/DigitalBanking-DeepSeek, dal deepseek/dashboard-and-tests. Ana kopya ../DigitalBanking, dal integration/local-development. Hazırlık commit'i f201e0e. Yalnızca kendi worktree'nde çalış. Ana dalı değiştirmen veya otomatik merge yapman istenmiyor. Branch ismi görevin yeni geniş kapsamını sınırlamaz.

Önce git status ve branch'i kontrol et; AGENTS.md, docs/LOCAL_DEVELOPMENT.md, docs/VERIFICATION.md, docs/DEEPSEEK_HANDOFF.md oku. EXECUTION_PROMPT.md dosyası GPT'nin bu yeni göreve özel eklediği dosyadır; beklenen başlangıç değişikliğidir. Tarihsel README/refactoring raporlarını güncel durum sanma.

Şimdi mevcut SDK ve baseline'ı doğrula:

```sh
bash scripts/dotnet.sh build DigitalBanking.slnx
bash scripts/dotnet.sh test DigitalBanking.slnx
python3 scripts/run_local.py --api-port 5180 --dashboard-port 5181
# Ayrı terminalden:
python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181
```

.NET SDK 10.0.402 ARM64 ~/Documents/Codex/toolchains/dotnet altında. Baseline: build 0 hata/0 uyarı; 10 xUnit testi; 21 smoke kontrolü. Bunları kendi kopyanda yeniden çalıştır. Ana kopyanın 5080/5081 süreçlerini durdurma. Her worktree'nin .local klasörü ayrı. Kimlik bilgilerini launcher/testler içinde kullanabilirsin ama şifre/token/JWT anahtarı/API anahtarı içeriğini ekrana basma, prompta koyma, commit veya upload etme. Shell çıktılarını token yazdırmadan incele.

## Sırayla uygula

### 0. VS Code SDK keşfi

SDK ve build/test komutları çalışıyor, ancak GUI'den başlatılmış VS Code C# Dev Kit mevcut PATH'te dotnet bulamadığını ve global.json gereksiniminin karşılanmadığını bildirdi. .vscode existingDotnetPath yalnızca extension runtime keşfini etkiliyor; terminal PATH ayarı host SDK keşfini düzeltmeyebilir. İlk aşamada resmi Microsoft ayarlarıyla proje SDK keşfini düzelt ve IDE'de solution'ın açıldığını doğrula. SDK'yı yeniden kurmadan önce kurulu ~/Documents/Codex/toolchains/dotnet yolunu değerlendir. Güvenlik kontrollerini kapatma; gerekli sistem değişikliğini kullanıcıya açıkla. Wrapper üzerinden baseline komutları kullanılabilir.

### 1. Temel uygulama ve dashboard

Mevcut giriş, çıkış, rapor ve kullanıcı yönetimi sözleşmelerini doğrula. Eksik loading/empty/error/session-expired durumlarını düzelt. Tarih ve limit filtrelerini doğrula. API'nin ApiResponse zarfını tutarlı oku. Bir sonraki aşamalarda müşteri/hesap listesi ve açılışı, işlem geçmişi, yatırma/çekme/transfer, ödeme onay kuyruğu ve audit görüntüleme için mevcut Razor Pages yapısında anlaşılır ekranlar oluştur. Yeni frontend framework'ü ekleme. Türkçe, tutarlı ve gösterime uygun bir arayüz kullan. Hata mesajlarında teknik yığın veya sır gösterme.

### 2. Kimlik, yetki ve bankacılık kuralları

PaymentApproval.RequestedByUserId ve ReviewedByUserId şu an int; User.Id Guid. Kullanıcı kimliklerini doğrulanmış JWT claim'inden alacak şekilde entity/DTO/service/controller zincirini düzelt. İstemcinin başka kullanıcı gibi kimlik göndermesine izin verme. SQL Server şema değişikliği gerekiyorsa yeni migration ekle; eski migration dosyalarını değiştirme. Mevcut verisi olan SQL Server'da otomatik uygulanması riskli bir dönüşümü sessizce yapma; dönüşüm/backfill/operatör adımlarını belgeleyip bu ortamda doğrulanamadığını belirt.

Maker-checker görev ayrımı, kendi talebini onaylayamama, tutar/para birimi/hesap doğrulamaları ve eşzamanlı çift onayı engelleyen durum kontrolü ekle. Tek transaction içinde bakiye, debit/credit, onay durumu ve gerekli audit tutarlılığını sağla. Başarısızlıkta kısmi işlem kalmasın. Tekrarlanan istekte para hareketinin iki kez uygulanmasını engelle. Doğrudan transferin onay akışını atlamaması için açık, test edilmiş rol/işlem politikası kur; gerekçeyi ADR veya kısa karar dosyasında açıkla.

Risk seviyelerine göre onay yetkilerini gerçek uygulanan kurallara bağla; rol modelini gereksiz karmaşıklaştırma. Müşteri/hesap erişim sınırlarını ve standart kullanıcı/Admin ayrımını açıkça tanımla. Kullanıcıların hesapla ilişkisi mevcut modelde eksikse keyfi erişim varsayımı kurmak yerine demo rollerinin kapsamını açıkla ve güvenli varsayılan seç. Yeni kullanıcı şifre politikasını, oturum yönetimini ve giriş denemesi sınırlandırmasını iyileştir; PBKDF2 ve legacy rehash desteğini koru.

### 3. Hesaplar, raporlama ve audit

AccountService'in Currency/InitialBalance davranışını tutarlı hale getir. Para decimal kalmalı; SQLite için double'a çevirme. Raporlarda tarih aralığı, gün sonu sınırı, sayfalama ve miktar toplamlarını test et. Negatif/0 tutar, yetersiz bakiye, aynı hesaba transfer, farklı para birimi, kayıp hesap ve bozuk filtre senaryolarını ele al. Audit verisinde şifre/token saklama. Audit ekranı yetkili role açık olsun.

### 4. Testler, belge ve teslim

Anlamlı unit ve integration testleri ekle: giriş/rol/claim, maker-checker, çift onay, idempotency, rollback, para hassasiyeti, dashboard sözleşmeleri. SQLite testlerinin SQL Server testlerinin yerine geçmediğini açık tut. SQL Server erişimi yoksa çalıştırmış gibi gösterme; gerekli komutları ve kalan sınırı belgeleyerek demo doğrulamasını tamamla.

README'yi gerçek kurulum, özellikler, demo başlatma, roller, testler ve bilinen kısıtlarla güncelle. Eski planlarda tamamlanan/tamamlanmayan ayrımını düzelt. docs/PROGRESS.md dosyasında yapılanlar, sonraki adım ve doğrulama sonuçlarını her aşama sonunda güncelle. docs/DEEPSEEK_FINDINGS.md içinde bulguları ve çözümleri kaydet.

## Çalışma biçimi ve bitirme ölçütü

Önce kısa uygulanabilir plan yaz, sonra işi gerçekten yap. Yalnızca inceleme raporu veya planla bitirme. Küçük tamamlanmış dilimler halinde yerel commit oluştur; GitHub'a push, PR, deploy veya yayın yapma. Kullanıcı kimliğini global git ayarında değiştirme. Dosyaları silerek, testleri gevşeterek, doğrulama veya security kontrollerini kapatarak yeşil sonuç alma. Var olan kullanıcı değişikliklerini koru.

Gereksiz Repository/UnitOfWork katmanları, toplu mimari değişim, toplu paket yükseltmesi veya unrelated kurulum yapma. Sırları ve özel dosyaları okumaya/taramaya çalışma. Geniş Full Access isteme; mümkün olduğunda yalnızca kendi çalışma klasörüne yazma yetkisiyle çalış. Dış servise yalnızca gerekli paket/doküman erişimi yap. API anahtarı veya hesap yetkisi gerekirse güvenli biçimde kullanıcıdan iste; sır istemini normal sohbete kopyalatma.

Başarı: build ve tüm testler başarılı; 21 baseline kontrolü korunmuş ve kritik ödeme akışları ek testlerle doğrulanmış; çalışan dashboard/API demo; yerel başlatma belgeleri; sır içermeyen değişiklikler; açık kalan production/SQL Server sınırları dürüstçe raporlanmış. Tamamlama mesajında dal/commit, değişiklikler, çalıştırılmış test sayıları, demo adresi ve kalan gerçek engelleri kısa yaz.

Kesilirsen baştan başlama: git durumu, docs/PROGRESS.md ve bu görev dosyasından devam et. Gerekmedikçe GPT'den ara karar isteme; kanıtlı, kapsam içi kararları kendin al.
