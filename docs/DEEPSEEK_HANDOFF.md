> **Tarihsel devir notu.** Güncel durum, tamamlananlar ve sınırlar için `docs/PROGRESS.md`, `docs/DEVELOPMENT_COMPLETION_REPORT.md`, `docs/TEST_REPORT.md` ve `docs/DASHBOARD_PRODUCT_BRIEF.md` kullanılmalıdır. Bu dosyadaki ilk dashboard kapsamı sonraki aşamaya taşınmıştır.

# DigitalBanking — GPT'den DeepSeek'e kapsamlı devir

Bu dosya bir görev devri hazırlığıdır. Kullanıcı GPT'nin çalışmayı sürdürmesini, kredisi %5 altına indiğinde kapsamlı prompt verilmesini istedi. Oturum kredi yüzdesine erişemiyor; bir eşik gözlemlenmeden otomatik görev başlatıldığı veya kredinin takip edildiği iddia edilmemeli. Bu hazırlık tek başına DeepSeek'in görevi devraldığı anlamına gelmez.

## Kaynak ve çalışma düzeni

- Kaynak: https://github.com/tamerkargin8/DigitalBanking ; incelenen kaynak commit: 71d08541c31843ccb4c51ed39760a17d9d80d26f (master).
- Ana çalışma klasörü: aynı üst klasördeki DigitalBanking. Dal: integration/local-development. Entegrasyonu GPT yönetir.
- Senin klasörün: DigitalBanking-DeepSeek. Dal: deepseek/dashboard-and-tests. Yalnızca kendi klasöründe değişiklik yap.
- Kullanıcı hedefi: bankacılık deneyimini gösteren, çalışan ve test edilmiş bir portföy projesi. Gerçek banka entegrasyonu veya gerçek para işlemi yok.
- VS Code'da Codex ve DeepSeek Harness eklentileri zaten kurulu. Microsoft C# Dev Kit bu hazırlıkta kuruldu. Ayrı DeepSeek Harness masaüstü uygulaması da kurulu; mevcut başka proje oturumlarını değiştirme.
- Kullanıcının varsayılan Codex sağlayıcısını veya global ayarlarını değiştirmedik. Mevcut DeepSeek bağlantısının anahtarını kopyalamadık/göstermedik. Bağlantının varlığı canlı model çağrısının başarıyla doğrulandığı anlamına gelmez.

## GPT'nin yaptığı değişiklikler

1. Microsoft'un resmi ARM64 .NET 10.0.402 SDK'sı ~/Documents/Codex/toolchains/dotnet içine kuruldu. global.json ve scripts/dotnet.sh eklendi.
2. SQL Server korunarak yalnızca Development için SQLite desteği eklendi. Yerel SQLite EnsureCreated kullanır; mevcut SQL Server migration'larına dokunulmadı. SQLite'ta Account.RowVersion uygulama tarafından üretilir ve iyimser eşzamanlılık kontrolü sürer.
3. scripts/run_local.py API + dashboard'u yalnızca loopback üzerinde başlatır. Her worktree ayrı .local veritabanı/kimlik bilgileri kullanır. Ana kopya portları 5080/5081; senin portların 5180/5181 olmalı.
4. JWT üretimi Jwt:Key, doğrulama Jwt:SecretKey okuyordu. Artık ikisi Jwt:SecretKey kullanır. Kaynak ayarlarındaki iki sabit anahtar kaldırıldı; rastgele anahtar ortamdan geçirilir.
5. Seed işlemi yalnızca Development'ta çalışır; rastgele demo şifresi ortamdan gelir. Production başlangıcı otomatik migration/demo kullanıcı oluşturmaz.
6. Dashboard giriş isteği query string yerine JSON body gönderir; ApiResponse<LoginResponse>.data.token okur. Yanlış şifre ve API erişim hatası kullanıcıya kontrollü döner. Session için eksik bellek önbelleği kaydı eklendi.
7. TopAccounts ekranı API'nin data zarfını ve take parametresini kullanır. CustomersController'daki yinelenen HttpGet kaldırıldı (Swagger sözleşmesi).
8. Yeni şifreler 600.000 iterasyon PBKDF2-SHA256 + rastgele salt ile saklanır. Eski SHA-256 kayıtları başarılı girişte yükseltilir. Karşılaştırma sabit zamanlıdır.
9. SQLitePCLRaw.lib.e_sqlite3 3.53.3 ve System.Security.Cryptography.Pkcs 9.0.21 ile gözlenen paket güvenlik uyarıları giderildi. Diğer paketler topluca yükseltilmedi.
10. .vscode görevleri/eklenti önerileri, AGENTS.md, yerel geliştirme rehberi, 21 kontrollü smoke testi ve 10 otomatik test eklendi.

## Doğrulama ve komutlar

Önce kendi git durumunu doğrula. Eski README ve refactoring raporları tarihsel; dosyalar ve gerçekten çalıştırdığın komutlar asıl kaynak.

```sh
git status --short
git branch --show-current
bash scripts/dotnet.sh build DigitalBanking.slnx
bash scripts/dotnet.sh test DigitalBanking.slnx
python3 scripts/run_local.py --api-port 5180 --dashboard-port 5181
# Ayrı terminal:
python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181
```

Hazırlık sırasında build 0 uyarı/0 hata; 10 test başarılı; 21 yerel smoke kontrolü başarılı oldu. Son doğrulama sonuçları docs/VERIFICATION.md içinde tutulur. Bunlar SQL Server veya production doğrulaması değildir. SQLite para değerleri decimal kalır; double'a dönüştürme.

Smoke testi .local kimlik bilgilerini kendi içinde tüketir; şifre/token ekrana basmaz. Bu dosyaları doğrudan okuma veya modele gönderme. Testler sentetik müşteri ve hesap ekler; canlı banka verisi kullanma.

## İlk görev: dashboard sözleşmeleri ve kullanıcı deneyimi

Önce AGENTS.md ve LOCAL_DEVELOPMENT.md oku; sonra kısa ve somut bir plan çıkar. Ana kopyayı değiştirme. Aşağıdaki sırada çalış:

1. Mevcut 10 test ve smoke akışını kendi kopyanda yeniden doğrula.
2. Dashboard rapor/API sözleşmelerini incele. Admin kullanıcı listesi/oluşturma, giriş/çıkış ve TopAccounts ekranlarında hatalı endpoint, zarf veya parametre kullanımı varsa kapsam içinde düzelt.
3. API'ye erişilemediğinde, oturum bittiğinde ve liste boş olduğunda düzgün mesajlar göster. Teknik exception veya token göstermeme kuralını koru.
4. TopAccounts filtrelerine geçerli tarih aralığı, güvenilir sınırlar ve hatalı tarih için anlaşılır mesaj ekle. Parametreleri güvenli oluştur; para hesabında decimal kullan.
5. Anlamlı sözleşme testleri ekle: başarı yanıtı, boş data, hatalı şifre, API hatası, tarih sınırları, take sınırı. Mevcut davranışı kopyalayan anlamsız testler ekleme.
6. Testleri çalıştır; değişen dosyaları, sonuçları ve kısıtları docs/DEEPSEEK_FINDINGS.md içinde açıkla. Küçük, incelenebilir yerel commit'ler oluşturabilirsin. GPT'nin entegrasyon dalına merge/push yapma.

Kabul ölçütü: mevcut build/test/smoke çalışır; yeni ekran davranışları test edilir; bankacılık iş kuralları değişmez; sırlar commit'e girmez; tamamlandı iddiası çalıştırılmış kontrole dayanır.

## Sonraki kritik işler — şimdilik bulgu olarak kaydet

Bu maddeler ilk DeepSeek görevinin kapsamını genişletmez. Kullanıcı veya GPT somut görev verdiğinde ele alınacak:

- PaymentApproval RequestedByUserId/ReviewedByUserId int, User.Id Guid. Kimlikler istemci body'den geliyor. Kullanıcı kimliği doğrulanmış claim'den türetilmeli; DTO/entity/migration uyumu tasarlanmalı.
- PaymentApproval üzerinde eşzamanlı iki onayın/idempotency'nin ve audit atomikliğinin test edilmesi gerekiyor. Account.RowVersion tek başına tüm onay durum yarışlarını kapsamaz.
- Risk seviyeleri hesaplanıp açıklanıyor, ama seviyeye göre yetki uygulaması yeterli değil.
- Doğrudan transfer endpoint'i maker-checker akışını atlayabiliyor; iş kuralı ve rol sınırları netleştirilmeli.
- AccountService InitialBalance/Currency alanlarını tutarlı işlemiyor. İstenilen para birimi ve hesap açılış davranışı ayrı görev olmalı.
- Müşteri/hesap sahipliğine göre erişim ve standart kullanıcının yetkileri geniş kapsamlı incelenmeli.
- UsersController şifre alt sınırı hâlâ 4 karakter; tüm giriş/üye yönetimi politikaları ve rate limiting gözden geçirilmeli.
- SQL Server üzerinde gerçek migration, decimal raporlar, transaction rollback ve concurrency testleri henüz yapılmadı.
- AutoMapper lisans koşulları ve FluentValidation.AspNetCore paketinin bakım durumu sürüm bazında değerlendirilmeli.
- README eski planları içeriyor; uygulanan ve planlanan özellikler ayrıştırılmalı.

Mimariyi topluca yeniden yazma, Repository/UnitOfWork gibi yeni katmanlar ekleme, paketleri topluca yükseltme, SQL Server migration'larını değiştirme. Kapsam dışı sorunları kanıt ve dosya konumuyla raporla. Büyük işi tek seferde bitirmeye çalışmak yerine her doğrulanmış dilimden sonra durumu kaydet; kesilirse yeniden başlarken git durumu ve bu dosyalardan devam et.
