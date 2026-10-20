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

## Kimlik bilgileri ve veri

- İlk açılışta rastgele demo şifresi ve JWT anahtarı `.local/credentials.json`
  içine yalnızca kullanıcı tarafından okunabilir (`0600`) olarak yazılır.
  **Paylaşmayın, commit etmeyin, sohbete/ekran görüntüsüne koymayın.**
- `admin` ve `tamer` aynı yerel demo şifresini kullanır. Giriş ekranında
  `admin` + dosyadaki `password` değerini kullanın.
- SQLite veritabanı `.local/digitalbanking.db` içindedir; yalnız Development
  içindir. Gerçek müşteri/para verisi kullanmayın.
- SQL Server varsayılan sağlayıcıdır; mevcut migration dosyaları değiştirilmez.
  SQLite `EnsureCreated` kullanır, SQL Server migration'larını uygulamaz.
  SQLite kontrolleri SQL Server doğrulamasının yerine geçmez.
- Şema değiştiğinde eski `.local/digitalbanking.db*` dosyasını arşivleyip silin;
  sonraki açılış yeni şemayı oluşturur ve demo kullanıcıları seed eder.

## Roller (mevcut)

- `Admin`: müşteri/hesap açma, yatırma/çekme, 50.000 altı doğrudan transfer,
  maker-checker onay/red, kullanıcılar, audit.
- `User` (`tamer`): yalnız kimlik doğrulaması; sahiplik olmadığı için
  müşteri/hesap/rapor verisi ve para hareketi yok. Müşteri self-servis için User–Customer sahipliği henüz yok;
  sonraki aşama: [`DASHBOARD_PRODUCT_BRIEF.md`](DASHBOARD_PRODUCT_BRIEF.md).

## Mevcut dashboard kapsamı

Giriş/çıkış, küçük panel, hesap sıralaması raporu ve Admin kullanıcı yönetimi
vardır. Kapsamlı müşteri/admin ekranları sonraki aşamadadır; bu teslimde yalnızca
API sözleşmesi değişikliklerine uyumluluk düzeltmeleri yapıldı.

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
