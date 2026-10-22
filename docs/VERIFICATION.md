# Doğrulama kaydı

> **Historical scope:** this report reflects the 2026-10-20/21 slice. Current
> verification after Stages 3–5 is **177 xUnit**, **280 smoke**, **25/25
> accounting E2E** and **24/24 operations E2E**; SQL Server, hosted CI and UAT
> remain **NOT RUN**. See `FINAL_GAPS.md` and `STAGE_05_REPORT.md`.


Tarih: **20 Ekim 2026** (Europe/Istanbul). Ortam: macOS ARM64, .NET SDK **10.0.402**
(runtime 10.0.13), EF Core **10.0.3**.

- Dal: `dsh/dsh-mvh4pdzy-u7y7j4` (izole worktree)
- Backend commit: `11684b2` — Owner/delegation/approval model
- Dashboard commit: `d2e90e3` — üç rollü Razor Pages
- Önceki ownership/timeline/reconciliation backend: `cac7f00`
- QA/final düzeltmeleri: **commit bekliyor** (index erişimi sandbox tarafından engellendi; `docs/INTEGRATION_HANDOFF.md`)
- Ana kopya `../DigitalBanking` / `integration/local-development` değiştirilmedi.

## Çalıştırılan kontroller

| Kontrol | Sonuç |
|---|---|
| Solution build | başarılı, **0 uyarı, 0 hata** |
| xUnit | **150 başarılı, 0 başarısız, 0 atlanan** |
| Yerel smoke (temiz Development DB; eşzamanlı Owner setup, stale Setup POST, setup 429 dahil) | **280 kontrol başarılı** |
| NuGet audit (6 proje + transitif) | bilinen açık yok (mevcut kaynaklar) |
| `git diff --check` | temiz |
| VS Code SDK önkoşul script'i | yerel SDK/global.json/.vscode kontrolleri geçti; GUI launchd adımı kullanıcıda |

Komutlar:

```sh
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh build DigitalBanking.slnx \
  -p:UseSharedCompilation=false -m:1 -nr:false
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh test DigitalBanking.slnx \
  -p:UseSharedCompilation=false -m:1 -nr:false --no-build
python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181
```

Kanıtlar: [`evidence/2026-10-20-build.txt`](evidence/2026-10-20-build.txt),
[`evidence/2026-10-20-test.txt`](evidence/2026-10-20-test.txt),
[`evidence/2026-10-20-smoke.txt`](evidence/2026-10-20-smoke.txt),
[`evidence/2026-10-20-nuget-audit.txt`](evidence/2026-10-20-nuget-audit.txt),
[`evidence/2026-10-20-vscode-sdk.txt`](evidence/2026-10-20-vscode-sdk.txt).

Ayrıntı: [`DEVELOPMENT_COMPLETION_REPORT.md`](DEVELOPMENT_COMPLETION_REPORT.md),
[`TEST_REPORT.md`](TEST_REPORT.md).

## Doğrulanmayanlar ve sınırlar

- **SQL Server bağlantısı/migration'ı test edilmedi.** İki yeni migration
  üretildi ve operatör adımları belgelendi; `dotnet ef database update`
  çalıştırılmadı. SQLite testleri SQL Server davranışının kanıtı değildir.
- **Legacy rol eşlemesi yapılmadı.** `Admin=1` / `User=2` bilinçli geçersiz;
  operatör Owner/Customer/DelegatedApprover eşlemesi yapmadan login çalışmaz.
- **Manuel UAT yapılmadı.** [`UAT_CHECKLIST.md`](UAT_CHECKLIST.md) kullanıcı
  tarafından doldurulacak.
- **Screenshot:** 10 gerçek dashboard ekranı alındı (`docs/screenshots/`);
  sahte görsel yok. Manuel UAT yine yapılmadı.
- **VS Code GUI** kullanıcı launchd adımını çalıştırıp yeniden başlatmalı.
- **Production sertleştirmesi yok.** Secret manager, WAF, dağıtık throttle,
  yük/sızma/erişilebilirlik, audit retention/immutability ve uyumluluk iddiası
  yoktur.
- **AI katkısı** vardır; [`PORTFOLIO_NOTES.md`](PORTFOLIO_NOTES.md) içinde
  dürüstçe belirtilmiştir.

Bu sonuçlar "production-ready" veya "SQL Server doğrulandı" anlamına gelmez.
