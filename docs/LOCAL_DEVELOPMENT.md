# Yerel geliştirme

Proje .NET 10 kullanır. Bu Mac için SDK: `~/Documents/Codex/toolchains/dotnet`
(10.0.402, ARM64). `scripts/dotnet.sh` sistemde `dotnet` varsa onu, yoksa bu
SDK'yı kullanır.

## Hızlı komutlar

```sh
bash scripts/dotnet.sh build DigitalBanking.slnx
bash scripts/dotnet.sh test DigitalBanking.slnx
python3 scripts/run_local.py --api-port 5180 --dashboard-port 5181
# Ayrı terminalden, sentetik veriyle:
python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181
```

- Dashboard: <http://127.0.0.1:5181>
- API/Swagger: <http://127.0.0.1:5180/swagger>
- Health: <http://127.0.0.1:5180/health>

Ana kopya 5080/5081 kullanır; bu worktree 5180/5181 kullanmalıdır. Aynı anda iki
örnek çalıştırmayın; `.local` klasörü her worktree'de ayrıdır.

## Kimlik bilgileri, Owner ve veri

- İlk açılışta rastgele demo şifresi ve JWT anahtarı `.local/credentials.json`
  içine yalnızca kullanıcı tarafından okunabilir (`0600`) olarak yazılır.
  **Paylaşmayın, commit etmeyin, sohbete/ekran görüntüsüne koymayın.**
- Temiz veritabanında **Owner yoktur**. Dashboard `/Setup`'a yönlendirir; ilk
  Owner yalnız localhost + Development'ta oluşturulur. Bu uç Owner oluştuktan
  sonra kapanır. Public `/Register` Owner yaratamaz.
- Sentetik demo kullanıcıları: `customer.a`, `customer.b`, `approver.100k`,
  `approver.low`, `applicant.pending`. Şifreleri `.local/credentials.json`
  içindeki `password` değeridir; yerel olarak okuyun. `applicant.pending`
  Owner aktivasyonuna kadar giriş yapamaz.
- SQLite veritabanı `.local/digitalbanking.db` içindedir; yalnız Development
  içindir. Gerçek müşteri/para verisi kullanmayın.
- SQL Server varsayılan sağlayıcıdır. SQLite `EnsureCreated` kullanır, SQL Server
  migration'larını uygulamaz; SQLite kontrolleri SQL Server kanıtı değildir.
- Smoke testi Owner girişini `.local/credentials.json` içindeki
  `owner_username`/`owner_password` veya `SMOKE_OWNER_USERNAME`/
  `SMOKE_OWNER_PASSWORD` ortam değişkenlerinden okur. Temiz DB'de Owner'ı kendisi
  oluşturabilir; bu değerleri commit etmeyin.
- Şema değiştiğinde eski `.local/digitalbanking.db*` dosyasını arşivleyip silin;
  sonraki açılış yeni şemayı ve sentetik demo kişileri oluşturur, Owner'ı seed
  etmez.

## Roller (mevcut)

- **Owner:** üyeleri aktive eder, Customer/DelegatedApprover rolü ve
  operasyon/para birimi limitleri atar; tüm talepleri, audit'i, müşteri/hesabı,
  mutabakatı, demo merkezini görür. Kendi talebini onaylayamaz/reddedemez.
- **DelegatedApprover:** yalnız aktif delegasyon kapsamında onaylar; üye/rol/
  yetki yönetemez.
- **Customer:** yalnız kendi profil/hesap/hareket/talep/dekontunu görür; tüm
  finansal talepleri Pending oluşturur.
- **PendingActivation:** aktivasyon öncesi login ve işlem yok.

## Mevcut dashboard kapsamı

Setup/Login/Register; Owner özet/onay/üyeler/yetkiler/müşteriler/hesaplar/
talep/audit/mutabakat/demo/rapor; Delegated kuyruk; Customer bakiye/hesap/
transfer/talepler/dekont ekranları uygulanmıştır. Gerçek screenshot'lar
`docs/screenshots/` altındadır; UAT kullanıcı adımıdır.

## VS Code / C# Dev Kit SDK keşfi

GUI'den (Finder/Dock) başlatılan VS Code, shell PATH'ini okumaz. C# Dev Kit
logunda `dotnet --info` için `dotnet: command not found` ve
`Could not find installed SDK` görülmüştü. `.vscode/settings.json` içindeki
`existingDotnetPath` yalnızca extension host'un kendi .NET runtime'ını seçer;
proje SDK keşfi için `dotnet` PATH'te olmalıdır.

1. Normal terminalde (ajan oturumu dışında) çalıştırın:
   ```sh
   bash scripts/setup_macos_vscode_env.sh
   ```
   Bu, `launchd` üzerinden `DOTNET_ROOT` ve `PATH` yayınlar. Bu delivery'nin
   ajan oturumunda `launchctl setenv` "Not privileged to set domain environment"
   ile reddedildi; bu nedenle adım kullanıcı tarafından yapılmalıdır.
2. VS Code'u tamamen kapatıp (Cmd+Q) yeniden açın; `DigitalBanking.slnx`
   otomatik açılmalıdır (`dotnet.defaultSolution`).
3. Önkoşulları denetleyin:
   ```sh
   bash scripts/verify_vscode_sdk.sh
   ```
   Yerel SDK/global.json/.vscode kontrolleri geçmeli; GUI launchd uyarıları
   kalmamalı. Bu script çalışan IDE penceresini denetleyemez; GUI doğrulaması
   kullanıcı tarafından yapılmalıdır.
4. Alternatif: VS Code'u SDK PATH'te olan bir terminalden başlatın.

Güvenlik kontrollerini kapatmayın; SDK'yı yeniden kurmadan mevcut
`~/Documents/Codex/toolchains/dotnet` yolunu kullanın.

## Harness notu

Bu delivery harness'ında worktree dosyaları macOS `hidden` bayrağıyla
oluşturulduğu için `PhysicalFileProvider` `appsettings.json`'ı göremedi ve
dosyalar `chflags -R nohidden .` ile düzeltildi. Sandbox paylaşımlı Roslyn
derleyicisini engellediği için doğrulama komutları `-p:UseSharedCompilation=false
-m:1 -nr:false` ve `DOTNET_CLI_MSBUILD_SERVER=0` ile çalıştırıldı. Normal
terminalde yukarıdaki sade komutlar yeterlidir.

## Raporlar

- [`DEVELOPMENT_COMPLETION_REPORT.md`](DEVELOPMENT_COMPLETION_REPORT.md)
- [`TEST_REPORT.md`](TEST_REPORT.md)
- [`VERIFICATION.md`](VERIFICATION.md)
- [`UAT_CHECKLIST.md`](UAT_CHECKLIST.md)
- [`DASHBOARD_PRODUCT_BRIEF.md`](DASHBOARD_PRODUCT_BRIEF.md)
