# DigitalBanking — CV project entry

## CV’ye hazır Türkçe metin

**DigitalBanking | Bankacılık Operasyonları ve Onay Yönetimi**  
**C#, .NET 10, ASP.NET Core, EF Core, SQL Server, Razor Pages, GitHub Actions**  
[GitHub repository](https://github.com/tamerkargin8/DigitalBanking)

- Üyelik aktivasyonu, rol ve limit bazlı yetki devri, hesap yönetimi ve tüm finansal taleplerde farklı kişi onayı (maker-checker) içeren sentetik bankacılık uygulamasının AI destekli geliştirme sürecini yönettim.
- İşlem bütünlüğü için çift taraflı muhasebe, atomik kayıt/geri alma, idempotency, hesap sahipliği kontrolleri ve anlık yetki iptali akışlarını geliştirdim ve doğruladım.
- Owner/müşteri dashboardları, SLA ve görev atama içeren operasyon merkezi, CSV mutabakat ve denetim izi oluşturdum; 178 birim testi, 280 HTTP smoke kontrolü, 104 ek uçtan uca kontrol ve gerçek SQL Server üzerinde 8 entegrasyon testiyle doğrulama gerçekleştirdim.

## English CV entry

**DigitalBanking | Banking Operations & Approval Management**  
**C#, .NET 10, ASP.NET Core, EF Core, SQL Server, Razor Pages, GitHub Actions**

- Directed AI-assisted development of a synthetic banking application with membership activation, scoped approval delegation, account management and maker-checker controls for every financial request.
- Implemented and verified double-entry accounting, atomic posting/rollback, idempotency, account ownership checks and immediate authorization revocation.
- Delivered responsive Owner/customer dashboards, an SLA-based operations centre, CSV reconciliation and audit trails; validated with 178 unit tests, 280 HTTP smoke checks, 104 supplemental end-to-end checks and 8 real SQL Server integration tests.

## Interview pitch

The project demonstrates the controls behind banking operations: who may approve which request, within which currency, amount and time limits; how revoked authority is enforced; and how accounting, balances and audit records remain consistent when an approval fails. It uses synthetic data and has no live banking connection. AI-assisted implementation was guided by requirements, code review and independently executed checks.

## Verified evidence

- [Latest application CI: build-test and smoke succeeded](https://github.com/tamerkargin8/DigitalBanking/actions/runs/40014165687), source `cb64218`.
- [Real SQL Server 2022: 8 passed, 0 failed, 0 skipped](https://github.com/tamerkargin8/DigitalBanking/actions/runs/39990772442).
- Local independent results and boundaries: [final verification](FINAL_VERIFICATION_20261023.md), [dashboard review](DASHBOARD_REFINEMENT_REPORT.md), [responsive correction](RESPONSIVE_FIX_20261023.md).

## Honest boundaries

Manual user acceptance remains pending; the user’s positive visual feedback does not constitute completion of the role/transaction checklist. No production deployment, real banking integration, load/security certification or recorded demo video is claimed. SQLite checks and SQL Server checks are separate evidence. This text is ready to insert into a personal CV; no personal CV file was available in the workspace, so none was edited.
