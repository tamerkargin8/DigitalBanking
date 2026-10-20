# DeepSeek çalışma alanı

Bu klasör DigitalBanking için ayrılmış bağımsız Git worktree'sidir. Ana kopya
`../DigitalBanking` (`integration/local-development`) olup değiştirilmemelidir.

## Güncel durum (2026-10-20)

- Backend, bankacılık kuralları, güvenlik ve testler tamamlandı.
- Build: 0 uyarı/0 hata; xUnit: 95 geçti; smoke: 68 kontrol geçti.
- Mevcut dashboard minimaldir; kapsamlı dashboard aşaması **başlatılmadı**.
- Teslim raporları: `docs/DEVELOPMENT_COMPLETION_REPORT.md`,
  `docs/TEST_REPORT.md`.
- Sonraki dashboard hedefi: `docs/DASHBOARD_PRODUCT_BRIEF.md`.
- Manuel kabul: `docs/UAT_CHECKLIST.md`.

## Devam ederken

Önce `docs/PROGRESS.md`, `docs/DEEPSEEK_FINDINGS.md` ve git durumunu oku. Yalnızca
bu worktree'de çalış; ana kopyayı değiştirme, sırları gösterme, push/deploy yapma.
API 5180, dashboard 5181 portlarını kullan. Bir kontrolü çalıştırmadan başarılı
deme. SQL Server doğrulanmadı; SQLite yalnızca Development oyun alanıdır.

```sh
bash scripts/dotnet.sh build DigitalBanking.slnx
bash scripts/dotnet.sh test DigitalBanking.slnx
python3 scripts/run_local.py --api-port 5180 --dashboard-port 5181
python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181
```
