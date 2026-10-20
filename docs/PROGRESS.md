# PROGRESS

Güncelleme: **2026-10-20** (Europe/Istanbul).

## Durum: backend + iş kuralları + test + rapor tamam; dashboard aşaması bekliyor

Kullanıcının güncel teslim sırası uygulandı: önce backend, bankacılık iş kuralları,
güvenlik ve testler; ardından completion/test raporları; **burada duruldu**.
Kapsamlı dashboard ekran/tasarım geliştirmesi başlatılmadı. Dashboard ürün
gereksinimleri [`DASHBOARD_PRODUCT_BRIEF.md`](DASHBOARD_PRODUCT_BRIEF.md),
manuel kabul adımları [`UAT_CHECKLIST.md`](UAT_CHECKLIST.md) içinde kayıtlı.

## Çalışma alanı

- Worktree: `/Users/tamerkargin/Documents/Codex/2026-10-20/gpt-imdi-x20/outputs/DigitalBanking-DeepSeek/.dsh-worktrees/dsh-mvh4pdzy-u7y7j4`
- Dal: `dsh/dsh-mvh4pdzy-u7y7j4` (taban `0ceead9`)
- Backend commit: `2316572`
- Dashboard uyumluluk + smoke commit: `095a31b`
- Dokümantasyon/portföy commit: `354fa0f`
- Deny-by-default güvenlik commit: `9f6b8ac`
- Ana kopya: `../DigitalBanking` / `integration/local-development` — **dokunulmadı**
- Demo: API `http://127.0.0.1:5180`, dashboard `http://127.0.0.1:5181`
- Smoke/veri: sentetik; `.local/` git dışı; ana kopyanın 5080/5081 süreçlerine
  dokunulmadı.

## Tamamlananlar

1. **Kimlik/yetki**: Guid `User.Id` ile uyumlu maker-checker kullanıcı kimlikleri;
   yalnız doğrulanmış JWT claim'inden `ActorContext`; Admin/User rol politikası;
   login throttle + rate limit + şifre politikası; session temizliği.
2. **Maker-checker ve risk**: düşük risk otomatik onay; orta/yüksek/kritik
   Pending; farklı Admin onayı; kendi talebini inceleme engeli; 50.000 doğrudan
   transfer sınırı; `RowVersion` ile çift onay engeli.
3. **Para/veri**: decimal, 2 hane, para birimi eşleşmesi, bakiye/hesap
   doğrulamaları, atomik ledger+audit, idempotency tablosu/filtresi.
4. **Rapor/audit**: gün sonu dahil tarih aralığı, 366 gün sınırı, para birimi
   kapsamlı top-accounts, audit API (Admin).
5. **Şema/migration**: yeni migration; eski migration'lar değiştirilmedi; int→Guid
   dönüşümü için legacy tablo + operatör adımları; SQL Server'da **uygulanmadı**.
6. **Dashboard uyumluluğu**: mevcut minimal Razor ekranları yeni sözleşmelere
   uyarlandı; güvenli 401/403/500 mesajları; kullanıcı adı/rol session'ı;
   çıkışta temizlik; Admin kullanıcı listesi zarfı; rapor filtreleri.
7. **Testler**: 95 xUnit + 71 smoke; baseline 10 test / 21 smoke korundu.
8. **VS Code SDK önkoşulu**: `.vscode` ayarları, `setup_macos_vscode_env.sh`,
   `verify_vscode_sdk.sh`; ajan oturumu launchd kısıtı nedeniyle GUI adımı kullanıcıda.

## Son doğrulanan sonuçlar

- Build: **0 uyarı, 0 hata**.
- xUnit: **95 geçti, 0 kaldı, 0 atlandı**.
- Smoke: **71 kontrol geçti**.
- NuGet audit: 6 projede bilinen açık yok (mevcut kaynaklar).
- `git diff --check`: temiz.
- Kanıt: [`evidence/`](evidence/), [`TEST_REPORT.md`](TEST_REPORT.md).

## Açık sınırlar

- SQL Server bağlantısı/migration'ı/gerçek concurrency testi yapılmadı; migration
  yalnız üretildi ve belgelendi.
- Manuel UAT yapılmadı; `UAT_CHECKLIST.md` kullanıcı tarafından doldurulacak.
- Kapsamlı dashboard yok; müşteri self-servis için User–Customer–Account sahipliği
  ve sunucu tarafı erişim kontrolleri gerekiyor.
- Standart `User` şu an yalnız kimlik doğrulayabilir; sahiplik olmadığı için tüm müşteri/hesap/rapor okumaları Admin'e kapalıdır.
- FX, yerel saat dilimi/bankacılık gün sonu takvimi, dağıtık throttle, production
  secret/observability/retention yok.
- VS Code GUI doğrulanmadı; kullanıcı launchd adımını çalıştırıp IDE'yi yeniden
  başlatmalı.

## Sıradaki adım (başlatılmadı)

Dashboard aşaması: [`DASHBOARD_PRODUCT_BRIEF.md`](DASHBOARD_PRODUCT_BRIEF.md).
İlk iş `Customer.UserId` sahipliği + migration + testler; ardından Admin özet/
müşteri/hesap/onay kuyruğu/audit/rapor ekranları, müşteri bakiye/hareket/transfer
talebi/durum ekranları, erişilebilirlik ve UAT. Ürün brief'inde kaba efor ve
kabul ölçütleri var.

## Teslim belgeleri

- [`DEVELOPMENT_COMPLETION_REPORT.md`](DEVELOPMENT_COMPLETION_REPORT.md)
- [`TEST_REPORT.md`](TEST_REPORT.md)
- [`VERIFICATION.md`](VERIFICATION.md)
- [`UAT_CHECKLIST.md`](UAT_CHECKLIST.md)
- [`DASHBOARD_PRODUCT_BRIEF.md`](DASHBOARD_PRODUCT_BRIEF.md)
- [`PORTFOLIO_NOTES.md`](PORTFOLIO_NOTES.md)
- [`migrations/README.md`](migrations/README.md)
- [`adr/0001-payment-and-role-policy.md`](adr/0001-payment-and-role-policy.md)
- [`../README.md`](../README.md)

## Harness notu (devam eden oturum için)

- Worktree dosyaları macOS `hidden` bayrağıyla oluşturuldu; `PhysicalFileProvider`
  `appsettings.json`'ı göremediği için `chflags -R nohidden .` uygulandı.
- Sandbox paylaşımlı Roslyn derleyicisini engelledi; doğrulama
  `DOTNET_CLI_USE_MSBUILD_SERVER=0` ve `-p:UseSharedCompilation=false -m:1 -nr:false`
  ile yapıldı. Normal terminalde sade komutlar yeterlidir.
- Commit'ler worktree dışındaki `.git` index'i nedeniyle sandbox yükseltmesi
  gerektirdi; global git kimliği değiştirilmedi, commit başına `-c user.name=Codex
  -c user.email=codex@local.invalid` kullanıldı.

## 2026-10-21 — Yeni geliştirme aşaması (dilim A/B/C backend)

Kullanıcı üç yeni özelliği onayladı; bekleme kapısı kalktı. Devam noktası
`a327f48`; bu worktree temizdi, parent dal aynı ağacı merge etmişti.

- **Dilim A — sahiplik:** `Customer.UserId` (+ SQL Server filtered unique index),
  `ICustomerAccessService`, `/api/me/*` (profil, hesaplar, hareketler, alıcılar,
  ödemeler, transfer talebi), IDOR için 404, Admin için 403. `User` artık yalnız
  kendi verisine erişir.
- **Dilim B — timeline/dekont:** `PaymentTimelineEvent`, `CorrelationId`,
  gerçek olaylardan timeline; tamamlanmış işlem için yazdırılabilir demo dekont
  (tamamlanmamışta durum özeti), maskeli hesaplar, Europe/Istanbul.
- **Dilim C — mutabakat backend:** CSV parser (boyut/satır/başlık/UTF-8/tarih/
  decimal/para birimi), preview+import, deterministik referans eşleme,
  eşleşen/yalnız dış/yalnız sistem/tutar/para birimi/mükerrer, pending ayrımı,
  batch kalıcılığı, formül enjeksiyonuna dayanıklı export, istisna iş akışı
  (Open→InReview→Proposed→Closed/Rejected, reopen), farklı Admin onayı, RowVersion,
  audit. Bakiye düzeltmesi yok.
- Testler: **128 xUnit** (95 baseline korunarak). Migration:
  `20261020212050_AddCustomerOwnershipTimelineReconciliation` (SQL Server'da
  uygulanmadı).
