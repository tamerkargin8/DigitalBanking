# Test report — 2026-10-20

- Ortam: macOS ARM64, .NET SDK **10.0.402** (runtime 10.0.13), EF Core **10.0.3**
- Dal: `dsh/dsh-mvh4pdzy-u7y7j4`
- Backend commit: `2316572`; dashboard uyumluluk + smoke commit: `095a31b`
- Test veritabanı: SQLite in-memory (Development/lab); **SQL Server değil**
- Demo: API `127.0.0.1:5180`, dashboard `127.0.0.1:5181`

## Özet

| Kontrol | Sonuç |
|---|---|
| Solution build | **0 uyarı, 0 hata** |
| xUnit | **95 geçti, 0 başarısız, 0 atlandı** |
| End-to-end smoke | **68 kontrol geçti** |
| NuGet vulnerable-package audit | 6 projede bilinen açık yok (mevcut kaynaklar) |
| `git diff --check` | temiz |

**Önceki baseline korundu:** 10 xUnit testi ve 21 smoke senaryosu silinmedi veya
gevşetilmedi; aynı/artan kapsamla 95 test ve 68 smoke kontrolüne genişletildi.

## Çalıştırılan komutlar

```sh
# Normal geliştirici komutları
bash scripts/dotnet.sh build DigitalBanking.slnx
bash scripts/dotnet.sh test DigitalBanking.slnx
python3 scripts/run_local.py --api-port 5180 --dashboard-port 5181
python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181
```

Bu delivery harness'ında sandbox paylaşımlı Roslyn derleyicisini engellediği için
fiilen şu biçim kullanıldı (sonuçlar aynı):

```sh
bash scripts/dotnet.sh build DigitalBanking.slnx -p:UseSharedCompilation=false -m:1 -nr:false
bash scripts/dotnet.sh test DigitalBanking.slnx -p:UseSharedCompilation=false -m:1 -nr:false --no-build
```

Kanıt: [`evidence/2026-10-20-build.txt`](evidence/2026-10-20-build.txt),
[`evidence/2026-10-20-test.txt`](evidence/2026-10-20-test.txt),
[`evidence/2026-10-20-smoke.txt`](evidence/2026-10-20-smoke.txt).

## Test envanteri (95 test)

| Dosya | Test sayısı | Kapsam |
|---|---:|---|
| `MoneyAndPolicyTests` | 10 attribute / theory genişlemesi | Decimal ölçek/pozitiflik, para birimi normalizasyonu, gün sonu tarih aralığı, 366 gün sınırı, risk eşikleri, şifre politikası |
| `AccountServiceTests` | 5 attribute | Para birimi + açılış bakiyesi, açılış deposit ledger'ı, audit, Admin zorunluluğu, geçersiz para birimi, negatif bakiye, olmayan müşteri |
| `TransactionServiceTests` | 8 attribute | Deposit/withdraw decimal, rol reddi, transfer tipleri/bakiyeleri, 50.000 doğrudan transfer sınırı, aynı hesap/yetersiz/olmayan hesap/para birimi/ölçek, DB hatasında rollback, gün sonu statement, para birimi kapsamlı top-accounts, sayfalama |
| `PaymentApprovalServiceTests` | 8 attribute | Düşük risk otomatik onay, orta risk Pending, checker onayı, ikinci onayın parayı ikizlememesi, self-approval/self-reject reddi, red gerekçesi, geçersiz talep senaryoları, standart kullanıcı reddi, `RowVersion` stale onay |
| `AuthAndClaimsTests` | 7 attribute | JWT Guid/rol/issuer/audience claim'leri, hatalı giriş throttle + audit'te şifre yokluğu, başarılı girişte sayaç sıfırlama, legacy SHA-256 rehash, claim'den ActorContext, eksik/geçersiz claim fail-closed, eksik issuer hatası |
| `IdempotencyTests` | 6 attribute | Aynı anahtar+payload replay, farklı payload `409`, in-progress `409`, başlıksız istek, unique index, hata sonrası rezervasyon serbest bırakma |
| `DashboardLoginContractTests` | 10 attribute | JSON login sözleşmesi, güvenli hata/429/erişilemez API, `ApiResponse` zarfı, raw legacy dizi, 401/403/500 güvenli eşleme, business mesajı, Idempotency-Key iletimi |
| `ReportFilterTests` | 4 attribute | Varsayılan 30 gün, geçerli tarih/limit, limit clamp, hatalı tarih/aralık/limit mesajları |
| `LocalConcurrencyTests` | 1 attribute | Eski bakiye güncellemesinin `RowVersion` ile reddi (baseline) |
| `PasswordHasherTests` | 3 attribute | PBKDF2 salt/yanlış şifre, legacy doğrulama/rehash, bozuk kayıtlar (baseline) |

> Not: attribute sayıları `[Theory]` satırlarıyla çoğaldığı için toplam 95'tir.

## Smoke kapsamı (68 kontrol)

Tek komutla, sentetik veriyle, gerçek API + dashboard üzerinden:

1. `/health`, tokensız `401`, hatalı login `400`.
2. Admin login; token claim'lerinde **Guid nameidentifier** ve `Admin` rolü.
3. Swagger, müşteri oluşturma/listeleme, iki hesap açma.
4. Deposit/withdraw/transfer; yetersiz bakiye ve negatif tutar reddi;
   bakiye **70.10 / 20.05** decimal doğrulaması.
5. Tarih aralıklı daily summary/top accounts.
6. `tamer` (User) login; rol `User`; deposit/withdraw/transfer/approval/audit/users
   çağrılarında **403**.
7. İkinci Admin oluşturma/login.
8. 100.000 deposit + 75.000 maker-checker talebi: `Pending`, `Medium`, `TRY`,
   Guid requester.
9. Maker'ın kendi talebini onaylaması **400**; checker onayı **204**; bakiyeler
   **25.070,10 / 75.020,05**; ikinci onay **400**, bakiye değişmez.
10. Düşük risk 100 → `AutoApproved`.
11. Red senaryosu: orta risk talep → checker reddi **204**, bakiyeler değişmez.
12. Idempotent deposit: aynı anahtar+payload ikinci kez → `Idempotency-Replayed`,
    bakiye bir kez artar; farklı payload → **409**.
13. USD hesap açma (açılış bakiyesi 10.05); TRY→USD transfer/onay **400**.
14. Audit: `PAYMENT_APPROVED`, `PAYMENT_REJECTED`, `PAYMENT_AUTO_APPROVED`,
    `DEPOSIT_SUCCESS`; şifre/token yok.
15. Geçersiz token **401**; 5 hatalı login sonrası **429**.
16. Dashboard: admin login, kullanıcılar sayfası, rapor sayfası hatasız; çıkış;
    User rolünde Admin sayfası "Admin yetkisi"; oturumsuz rapor → Login.

## Doğrulanmayan / çalıştırılmayan kontroller

- **SQL Server**: bağlantı, migration uygulama, decimal, transaction rollback ve
  gerçek eşzamanlılık testleri yapılmadı. Migration yalnız üretildi/belgelendi.
- **Gerçek migration/backfill**: `LegacyPaymentApprovalIds` operatör adımları
  çalıştırılmadı.
- **Tarayıcı otomasyonu / manuel UAT**: yapılmadı. `UAT_CHECKLIST.md` kullanıcı
  tarafından doldurulmalı.
- **Yük/performans, güvenlik sızma testi, erişilebilirlik denetimi**: yapılmadı.
- **VS Code GUI SDK keşfi**: launchd adımı ajan oturumunda uygulanamadı; GUI
  doğrulanmadı.
- **Dashboard tamamı**: kapsamlı ekranlar henüz yok; mevcut minimal dashboard
  uyumluluk testleri ve smoke ile sınırlı.

## Kanıt dosyaları

- [`evidence/2026-10-20-build.txt`](evidence/2026-10-20-build.txt)
- [`evidence/2026-10-20-test.txt`](evidence/2026-10-20-test.txt)
- [`evidence/2026-10-20-smoke.txt`](evidence/2026-10-20-smoke.txt)
- [`evidence/2026-10-20-nuget-audit.txt`](evidence/2026-10-20-nuget-audit.txt)
- [`evidence/2026-10-20-vscode-sdk.txt`](evidence/2026-10-20-vscode-sdk.txt)

## Sonuç

Bu ortamda build, 95 xUnit testi ve 68 smoke kontrolü başarılıdır. Bu sonuçlar
**SQL Server, production veya tam dashboard** için kanıt değildir. Kalan işler
`DEVELOPMENT_COMPLETION_REPORT.md` ve `DASHBOARD_PRODUCT_BRIEF.md` içinde
listelenmiştir.
