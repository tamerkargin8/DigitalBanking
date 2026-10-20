# PROGRESS

Güncelleme: 2026-10-20 (Europe/Istanbul).

## Çalışma alanı

- Worktree: `/Users/tamerkargin/Documents/Codex/2026-10-20/gpt-imdi-x20/outputs/DigitalBanking-DeepSeek/.dsh-worktrees/dsh-mvh4pdzy-u7y7j4`
- Dal: `dsh/dsh-mvh4pdzy-u7y7j4` (0ceead9 tabanlı, ana kopya `../DigitalBanking` değiştirilmedi)
- Ana kopya: `../DigitalBanking` / `integration/local-development` (dokunulmadı)
- Portlar: API 5180, dashboard 5181. Ana kopyanın 5080/5081 süreçlerine dokunulmadı.

Harness notu: worktree dosyaları macOS `hidden` bayrağıyla oluşturulmuştu; bu,
`PhysicalFileProvider`'ın `appsettings.json` dosyasını görmemesine ve API'nin
JWT issuer/audience ayarlarını okumamasına yol açtı. `chflags -R nohidden .` ile
düzeltildi (commit dışı, yerel ortam). Harness sandbox'ı paylaşımlı Roslyn
derleyicisini engellediği için doğrulama komutları `-p:UseSharedCompilation=false
-m:1 -nr:false` ve `DOTNET_CLI_USE_MSBUILD_SERVER=0` ile çalıştırıldı. Normal
terminalde `bash scripts/dotnet.sh build DigitalBanking.slnx` yeterlidir.

## Doğrulanan baseline (değişiklik öncesi)

- Build: başarılı, 0 uyarı, 0 hata.
- xUnit: 10 başarılı.
- Smoke: 21 kontrol başarılı.
- Not: baseline smoke ilk çalıştırmada JWT audience hatası verdi; kök neden
  yukarıdaki hidden dosya bayrağıydı. Düzeltmeden sonra 21/21 geçti.

## Tamamlanan dilimler

1. **Domain/şema**: `PaymentApproval.RequestedByUserId`/`ReviewedByUserId` Guid,
   `RelatedTransactionId` Guid, `PaymentApproval.RowVersion`; `IdempotencyRecords`
   tablosu. Yeni SQL Server migration'ı veri korumalı (legacy tablo + backfill)
   olarak elle yazıldı; eski migration dosyaları değiştirilmedi.
2. **Kimlik/yetki**: Tüm kullanıcı kimlikleri JWT claim'inden (`ActorContext`).
   İstemci gövdesinden kullanıcı kimliği kabul edilmiyor. Login throttle (bellek
   içi), şifre politikası (8+, harf+rakam), auth rate limit eklendi.
3. **Para/politika**: Decimal korunuyor; 2 haneden fazla küsurat reddediliyor.
   Doğrudan transfer yalnız Admin ve 50.000 altı; üstü maker-checker akışına
   yönlendiriliyor. Düşük risk otomatik onay; orta/yüksek/kritik Pending.
   Maker-checker (kendi talebini inceleyememe), çift onay engeli (RowVersion),
   para birimi/hesap/bakiye doğrulamaları, atomik audit eklendi.
4. **Audit ve raporlar**: Audit yazımı para hareketiyle aynı transaction'da.
   Audit API'si (Admin) eklendi. Rapor tarih aralığı gün sonu dahil, 366 gün
   sınırı, para birimi kapsamlı top-accounts.
5. **Testler**: 80 xUnit testi başarılı (10 baseline + 70 yeni). SQLite üzerinde
   çalışır; SQL Server davranışı kanıtlanmış sayılmaz.

## Sıradaki adım

- Dashboard ekranlarını (müşteri/hesap/işlem/onay kuyruğu/audit/raporlar) yeni
  API sözleşmelerine göre yeniden yazmak; oturum/401/403/boş/hata durumlarını
  Türkçe ve sırsız göstermek.
- Smoke testini genişletmek ve çalıştırmak.
- README/VERIFICATION/ADR ve VS Code SDK keşif düzeltmesini tamamlamak.

## Son doğrulanan sonuçlar

- `bash scripts/dotnet.sh build DigitalBanking.slnx`: başarılı, 0/0.
- `bash scripts/dotnet.sh test DigitalBanking.slnx`: 80/80 başarılı.
- Smoke: dashboard yenilemesinden sonra çalıştırılacak.

## Güncel teslim sırası (2026-10-20, kullanıcı talimatı)

Kullanıcı sırayı değiştirdi: önce backend + iş kuralları + güvenlik + testler,
ardından `docs/DEVELOPMENT_COMPLETION_REPORT.md` ve `docs/TEST_REPORT.md`,
sonra kısa teslim ve **DUR**. Kapsamlı dashboard ekran/tasarım geliştirmesi
sonraki aşamaya bırakıldı. Bu aşamada yalnızca mevcut dashboard'u bozmayacak
zorunlu API sözleşmesi uyumluluk düzeltmeleri yapılabilir. `docs/UAT_CHECKLIST.md`
manuel kabul testi için hazırlanacak; manuel test yapılmış gibi raporlanmayacak.
