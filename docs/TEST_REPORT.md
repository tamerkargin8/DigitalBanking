# Test report — Owner model, approvals, reconciliation and dashboard

- Tarih: **2026-10-20** (Europe/Istanbul)
- Ortam: macOS ARM64, .NET SDK **10.0.402** (runtime 10.0.13), EF Core **10.0.3**
- Dal: `dsh/dsh-mvh4pdzy-u7y7j4`; backend `11684b2`, dashboard `d2e90e3`
- Test veritabanı: SQLite in-memory / disposable SQLite Development; **SQL Server değil**
- Demo: API `127.0.0.1:5180`, dashboard `127.0.0.1:5181`

## Özet

| Kontrol | Sonuç |
|---|---|
| Solution build | **0 uyarı, 0 hata** |
| xUnit | **150 geçti, 0 başarısız, 0 atlandı** |
| End-to-end smoke | **280 kontrol geçti** |
| NuGet vulnerable package audit | 6 projede bilinen açık yok (mevcut kaynaklar) |
| `git diff --check` | temiz |

Önceki teslim 95 xUnit + 71 smoke idi. Auto-approval kaldırıldığı, roller
Owner/DelegatedApprover/Customer olarak değiştiği ve tüm finansal talepler
onaya bağlandığı için **eski auto-approval beklentileri bilinçli olarak
kaldırıldı**; test sayısı 95 → 150'ye, smoke 71 → 280'e genişletildi. Kaldırılan
tek davranış düşük risk otomatik onayıdır; yerine "low risk dahil Pending"
testi geldi. Diğer güvenlik/para/idempotency kapsamı korunmuştur.

## Çalıştırılan komutlar

```sh
# Normal geliştirici yolu
bash scripts/dotnet.sh build DigitalBanking.slnx
bash scripts/dotnet.sh test DigitalBanking.slnx
python3 scripts/run_local.py --api-port 5180 --dashboard-port 5181
python3 scripts/smoke_test.py --api-port 5180 --dashboard-port 5181
```

Harness sandbox'ı paylaşımlı Roslyn derleyicisini engellediği için fiilen:

```sh
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh build DigitalBanking.slnx \
  -p:UseSharedCompilation=false -m:1 -nr:false
DOTNET_CLI_USE_MSBUILD_SERVER=0 bash scripts/dotnet.sh test DigitalBanking.slnx \
  -p:UseSharedCompilation=false -m:1 -nr:false --no-build
```

Kanıt: [`evidence/2026-10-20-build.txt`](evidence/2026-10-20-build.txt),
[`evidence/2026-10-20-test.txt`](evidence/2026-10-20-test.txt),
[`evidence/2026-10-20-smoke.txt`](evidence/2026-10-20-smoke.txt).

## Test envanteri (150)

| Dosya | Test | Kapsam |
|---|---:|---|
| `AccountServiceTests` | 6 | Para birimi/açılış bakiyesi, ledger+audit, Owner zorunluluğu, geçersiz para birimi, negatif bakiye, sıfır bakiye |
| `AuthAndClaimsTests` | 7 | Guid/rol/issuer/audience claim, throttle+audit, sayaç reset, legacy rehash, ActorContext, fail-closed, eksik issuer |
| `CsvStatementParserTests` | 17 | Geçerli/BOM/CRLF, katı başlık, UTF-8, boyut/satır, geçersiz GUID/tutar/para birimi/tarih/boş alan, unclosed quote |
| `CustomerPortalTests` | 12 | Sahiplik, IDOR 404, maskeli beneficiary, transfer Pending, başka hesaptan transfer reddi, timeline/dekont, red, deposit/account opening Pending, **ilk hesap türü/adı**, **ad değişimi/XSS/IDOR**, para yatır/çek, profil yok/aktif değil |
| `DashboardLoginContractTests` | 12 | JSON login, güvenli 400/401/429/erişilemez API, **PendingActivation 403 mesajı**, zarf/raw dizi, decimal, 401/403/500, Idempotency-Key |
| `IdempotencyTests` | 6 | Replay, farklı payload 409, in-progress 409, header yok, unique index, hata sonrası rezervasyon |
| `IdentityAndDelegationTests` | 7 | Tek Owner setup, **iki gerçek context ile eşzamanlı Owner yarışı**, kayıt validasyonu, Pending activation, Owner koruması, limit/para birimi/expiry/revocation, delegated rol yasağı |
| `LocalConcurrencyTests` | 1 | Stale `RowVersion` reddi |
| `MoneyAndPolicyTests` | 26 | Decimal/ölçek, para birimi, gün sonu, 366 gün, risk eşikleri, şifre politikası |
| `PasswordHasherTests` | 5 | PBKDF2 salt/yanlış şifre, legacy rehash, bozuk kayıt fail-closed |
| `PaymentApprovalServiceTests` | 16 | Low-risk Pending, 100.000 dahil sınır, currency/operation, Owner self-approval, revocation/expiry, stale JWT, pending/customer onay yasağı, çift onay, rezervasyon, onayda yetersiz bakiye, deposit/account opening, reject/cancel, günlük limit, **onayda sahiplik değişimi**, **approve/reject yarışı** |
| `SetupPageTests` | 5 | Setup tamamlanmışken GET/POST, yarışı kaybeden form, Türkçe 429, localhost/Development kapalı mesajı |
| `ReconciliationServiceTests` | 9 | Preview/import idempotency, mismatch sınıfları, invalid batch, satır hataları, formül export, istisna farklı onay, reopen, yetki, eşzamanlı onay |
| `ReportFilterTests` | 8 | Varsayılan 30 gün, geçerli filtre, limit clamp, hatalı tarih/aralık/limit |
| `TransactionServiceTests` | 13 | Decimal deposit/withdraw, rol reddi, transfer tipleri, doğrudan limit, aynı/eksik/yetersiz/kur/ölçek, rollback, statement/rapor, sayfalama |

Toplam **150**.

## Smoke kapsamı (280 kontrol)

Tek komut, gerçek API + dashboard, sentetik veri:

1. Health, tokensız 401, hatalı login; **aynı boş DB'ye iki eşzamanlı Owner
   setup** → tam olarak `200 + 409` (iki Owner yok), tekrar kurulumun kapalı
   olması; tamamlanmış Setup GET/POST'un login bildirimine yönlenmesi; setup
   rate-limit 429'unun Türkçe mesaj + `Retry-After: 600` döndürmesi.
2. Owner claim'leri; Owner-only users/customers/accounts/audit/delegations/demo;
   Swagger; geçersiz token 401.
3. Public kayıt → `PendingActivation`; login 403; **role=Owner injection ignore**;
   duplicate 409; invalid e-posta/şifre 400.
4. Owner aktivasyonu; Customer/DelegatedApprover rol değişimi; Owner disable/demote
   400; delegated'ın users/delegations/rol/operasyonel talep denemeleri 403.
5. Delegasyon oluşturma; **100.000,00 TRY dahil**, **99.999,99 onay**,
   **100.000,01 approver 400 + kuyrukta yok**; diğer para birimi 400; günlük
   50.000 limit; rezervasyonlu overcommit 400; iptal rezervasyonu serbest bırakır.
6. Yeni üye kayıt → PendingActivation → Owner aktivasyonu → dashboard login →
   `/Index` → `/Customer/Home`; hesapsız müşteride ilk hesap CTA’sı. Hesap açılışı
   tür/ad/para birimi ile Pending → Owner onayı → hesap; 300.000 deposit ve 100
   withdrawal → Pending → onay → bakiye; oluşturmada para hareketi yok.
7. IDOR: Customer B/A hesabı ve ödeme 404; A başka müşterinin ödemesini göremez.
8. Low-risk 1 TRY → Pending; explicit no-auto-approval kontrolü.
9. **Owner kendi talebini onaylayamaz/reddedemez**; iptal eder. Customer red
   gerekçesini kendi detayında görür.
10. Delegasyon iptali sonrası eski token 400; rol düşürme sonrası eski token 403
    ve talep Pending kalır.
11. Doğrudan transfer/deposit/withdraw/open `400`; bakiye değişmez.
12. Timeline Created/Pending/Approved/Completed; receipt tamamlanmışta maskeli ve
    Europe/Istanbul + DEMO; tamamlanmamışta `IsCompleted=false`; başka müşteri 404.
13. Demo seed idempotent; catalog; Customer demo 403; matched CSV preview/import,
    SHA-256 replay; mismatch sınıfları; **istisna Open→InReview→Proposed→Closed**,
    proposer self-approval 400, farklı approver kapatır, **bakiye değişmez**,
    reopen; formül-safe export; invalid/empty/`.txt` CSV reddi.
14. Dashboard: anonymous→Login; Owner 10 sayfa; approver queue + yönetim reddi;
    Customer 5 sayfa + admin reddi; Owner customer sayfası reddi; logout.
15. Audit aksiyonları (OWNER_SETUP, MEMBERSHIP_ACTIVATED, USER_ROLE_CHANGED,
    DELEGATION_CREATED/DEACTIVATED, APPROVAL_* , RECONCILIATION_IMPORTED,
    RECON_EXCEPTION_CLOSED, DEMO_SEEDED) ve şifre/token sızıntısı yok.
16. Demo reset scoped; UAT hesabı korunur; 5 hatalı login sonrası 429; Setup
    limitinin login/register limitinden ayrı olması.

## Doğrulanmayan / çalıştırılmayan

- **SQL Server**: bağlantı, migration apply, decimal, transaction rollback ve
  gerçek eşzamanlılık testi yapılmadı.
- **Manuel UAT / tam görsel regresyon** yapılmadı; 7 gerçek screenshot alındı
  (`docs/screenshots/`) ancak bunlar otomatik assertion değildir.
- **Yük, sızma, erişilebilirlik denetimi** yapılmadı.
- **VS Code GUI SDK keşfi** kullanıcı launchd adımına bağlı.
- **Dağıtık login throttle, secret manager, audit retention/immutability** yok.

## Sonuç

Bu ortamda build, 150 xUnit testi ve 280 smoke kontrolü başarılıdır. Bu sonuçlar
SQL Server, production veya manuel kabul kanıtı değildir.
