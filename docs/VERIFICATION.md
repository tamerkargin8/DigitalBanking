# Doğrulama kaydı

Tarih: **20 Ekim 2026** (Europe/Istanbul). Ortam: macOS ARM64, .NET SDK **10.0.402**
(runtime 10.0.13), EF Core **10.0.3**.

- Dal: `dsh/dsh-mvh4pdzy-u7y7j4` (izole worktree)
- Backend commit: `2316572`
- Dashboard uyumluluk + smoke commit: `095a31b`
- Dokümantasyon/portföy commit: `354fa0f`
- Deny-by-default güvenlik commit: `9f6b8ac`
- Ana kopya `../DigitalBanking` / `integration/local-development` değiştirilmedi.

## Çalıştırılan kontroller

| Kontrol | Sonuç |
|---|---|
| Solution build (`bash scripts/dotnet.sh build DigitalBanking.slnx`) | başarılı, **0 uyarı, 0 hata** |
| xUnit (`bash scripts/dotnet.sh test DigitalBanking.slnx`) | **95 başarılı, 0 başarısız, 0 atlanan** |
| Yerel smoke (`python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181`) | **71 kontrol başarılı** |
| NuGet audit (`dotnet list ... package --vulnerable --include-transitive`) | 6 proje + geçişli paketlerde bilinen açık yok (mevcut kaynaklar) |
| `git diff --check` | temiz |
| VS Code SDK önkoşul script'i (`scripts/verify_vscode_sdk.sh`) | yerel SDK/global.json/.vscode kontrolleri geçti; GUI launchd eksik (uyarı) |

Smoke kapsamı: health, yetkisiz erişim, login/claim/rol, login throttle,
müşteri/hesap, decimal bakiye, para birimi, yatırma/çekme/transfer,
maker-checker, kendi talebini onaylama reddi, çift onay reddi, idempotent replay,
audit, rapor filtreleri, dashboard oturum/rol/çıkış.

Kanıtlar: [`evidence/2026-10-20-build.txt`](evidence/2026-10-20-build.txt),
[`evidence/2026-10-20-test.txt`](evidence/2026-10-20-test.txt),
[`evidence/2026-10-20-smoke.txt`](evidence/2026-10-20-smoke.txt),
[`evidence/2026-10-20-nuget-audit.txt`](evidence/2026-10-20-nuget-audit.txt),
[`evidence/2026-10-20-vscode-sdk.txt`](evidence/2026-10-20-vscode-sdk.txt).

Ayrıntılı raporlar: [`DEVELOPMENT_COMPLETION_REPORT.md`](DEVELOPMENT_COMPLETION_REPORT.md),
[`TEST_REPORT.md`](TEST_REPORT.md).

## Doğrulanmayanlar ve sınırlar

- **SQL Server bağlantısı/migration'ı test edilmedi.** Yeni migration üretildi ve
  operatör adımları belgelendi; `dotnet ef database update` çalıştırılmadı.
  SQLite testleri SQL Server davranışının kanıtı değildir.
- **Manuel UAT yapılmadı.** [`UAT_CHECKLIST.md`](UAT_CHECKLIST.md) kullanıcı
  tarafından doldurulacak.
- **Kapsamlı dashboard yok.** Mevcut Razor ekranları minimaldir; müşteri/admin
  dashboard'u [`DASHBOARD_PRODUCT_BRIEF.md`](DASHBOARD_PRODUCT_BRIEF.md) ile
  sonraki aşamaya bırakıldı.
- **User–Customer sahipliği yok.** Standart `User` şu an yalnız kimlik doğrular; müşteri
  self-servis güvenli biçimde açılamaz.
- **VS Code GUI doğrulanmadı.** Bu ajan oturumunda `launchctl setenv` reddedildi; kullanıcı
  `scripts/setup_macos_vscode_env.sh` çalıştırıp VS Code'u yeniden başlatmalı.
- **Production sertleştirmesi yok.** Secret manager, dağıtık throttle, yük/performans,
  sızma testi, erişilebilirlik denetimi, audit retention/immutability ve uyumluluk
  iddiası yoktur.
- **AI katkısı** vardır; [`PORTFOLIO_NOTES.md`](PORTFOLIO_NOTES.md) içinde dürüstçe
  belirtilmiştir.

Bu sonuçlar "production-ready" veya "SQL Server doğrulandı" anlamına gelmez.
