# DeepSeek çalışma alanı

Bu klasör DigitalBanking için ayrılmış bağımsız Git worktree'sidir. Ana kopya
`../DigitalBanking` (`integration/local-development`) olup değiştirilmemelidir.

## Güncel durum (2026-10-20/21)

- Owner / DelegatedApprover / Customer modeli, tüm finansal taleplerde onay,
  kişi/operasyon/para birimi limitleri, mutabakat/istisna, timeline/dekont ve
  demo merkezi uygulandı.
- Build: 0 uyarı/0 hata; xUnit: **147** geçti; smoke: **272** kontrol geçti.
- Üç rollü dashboard uygulandı; UAT ve gerçek ekran görüntüleri açık.
- Teslim raporları: `docs/DEVELOPMENT_COMPLETION_REPORT.md`,
  `docs/TEST_REPORT.md`, `docs/VERIFICATION.md`.
- Manuel kabul: `docs/UAT_CHECKLIST.md`; SQL Server **uygulanmadı/doğrulanmadı**.
- Sonraki işler kullanıcı onayına bağlı: gerçek screenshot + UAT, SQL Server
  integration ortamı, dağıtık rate-limit store.

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
