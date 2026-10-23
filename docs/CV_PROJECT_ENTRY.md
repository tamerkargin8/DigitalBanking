# CV project entry — DigitalBanking

This is a source document for the user’s CV. No external CV/LinkedIn file was
edited by this project. All numbers below are local, synthetic verification from
2026-10-22; SQL Server, GitHub Actions, manual UAT and video are **not** claimed.

## One-line title

**DigitalBanking — maker-checker banking back office with double-entry accounting
and an operations control centre (.NET 10, ASP.NET Core, EF Core, Razor Pages).**

## English bullets

- Built a synthetic retail-banking back office in **C# / .NET 10 / ASP.NET Core**
  with a **Razor Pages** dashboard, **EF Core 10**, SQL Server-oriented migrations
  and a disposable SQLite development path.
- Implemented **maker-checker for every financial request** (transfer, deposit,
  withdrawal, account opening), **inclusive delegated limits**, self-approval
  blocking, reserve-aware balances, live role/delegation revocation and
  idempotent posting.
- Designed an **append-only double-entry journal** (one currency per entry,
  positive 2-decimal legs, debit = credit) and integrated it into the approval
  transaction so balance, legacy ledger, journal, approval, timeline and audit
  commit or roll back together.
- Added an explicit **Owner baseline** for pre-journal balances without silently
  backfilling history, plus per-currency trial balance, journal drilldown and
  read-only accounting API/UI.
- Built an **operations control centre** with pending age/SLA, overdue and
  unassigned counts, type/currency/status/age filters, bounded pagination, safe
  assignment (no approval-rights bypass), live delegation-scoped visibility and
  unresolved reconciliation differences.
- Delivered **178 passing xUnit tests**, **280 isolated HTTP smoke checks**, and
  **25/25 accounting + 24/24 operations** live E2E checks on synthetic SQLite,
  with real screenshots and evidence reports.
- Practiced security-relevant engineering: database-backed authorization, IDOR
  protection, JWT identity, PBKDF2 hashing, rate limiting, idempotency keys,
  audit trails, CSV formula-injection prevention and secret-free screenshots.

## Turkish bullets (CV’ye uygun)

- **.NET 10 / ASP.NET Core / EF Core / Razor Pages** ile sentetik bankacılık arka
  ofisi; transfer, yatırma, çekme ve hesap açılışında istisnasız maker-checker.
- **Kapsamlı delegasyon**: operasyon + para birimi + kapsayıcı limit + günlük
  limit/süre; canlı rol/statü kontrolü, self-approval engeli, rezervasyonlu
  bakiye ve idempotent onay.
- **Append-only çift kayıtlı journal**: tek para birimi, pozitif 2 ondalık
  bacaklar, borç = alacak; onayla aynı transaction’da bakiye, legacy kayıt,
  journal, onay, timeline ve audit.
- Eski bakiyeler için sessiz backfill yerine **Owner baseline preview/post**;
  para birimi bazlı mizan, journal detayı ve salt okunur muhasebe API/UI.
- **Operasyon kontrol merkezi**: bekleyen yaş/SLA, geciken/atanmamış sayıları,
  tür/para birimi/durum/yaş filtreleri, sınırlı sayfalama, güvenli atama ve
  çözülmemiş mutabakat farkları; Delegated yalnız canlı yetkisini görür.
- **178 xUnit**, **280 smoke**, **25/25 muhasebe** ve **24/24 operasyon** canlı
  E2E testi; gerçek ekran görüntüleri ve kanıt raporları.
- Veritabanı tabanlı yetkilendirme, IDOR koruması, JWT kimlik, PBKDF2, rate
  limit, idempotency, audit, CSV formül enjeksiyonu koruması ve secretsız
  ekran görüntüleri.

## 30-second pitch (English)

“This is a synthetic retail-banking back office that focuses on controls rather
than CRUD. Every money request is Pending and needs a different authorized
approver; delegated limits are scoped by operation, currency, amount and time and
are re-read from the database on every decision. Approved movements write a
balanced, append-only double-entry journal in the same transaction as the
balance, approval and audit, and old balances are handled by an explicit Owner
baseline instead of a silent backfill. An operations centre adds SLA/age,
safe assignment and unresolved reconciliation gaps. It is verified with 178 unit
tests, 280 smoke checks and two live E2E suites on synthetic data. SQL Server and
hosted CI are deliberately not claimed — they were not run here.”

## 30-second pitch (Turkish)

“Bu, CRUD yerine kontrol odaklı sentetik bir bankacılık arka ofisi. Her para
talebi Pending başlar ve farklı yetkili bir onaycı gerektirir; delegasyon
operasyon, para birimi, limit ve süre ile sınırlıdır ve her kararda veritabanından
yeniden okunur. Onaylanan hareketler bakiye, onay ve audit ile aynı transaction’da
dengeli ve append-only bir çift kayıt journal’ı üretir. Eski bakiyeler sessizce
doldurulmaz; Owner açık baseline uygular. Operasyon merkezi SLA/yaş, güvenli atama
ve çözülmemiş mutabakat farklarını ekler. 178 unit, 280 smoke ve iki canlı E2E
sentetik veriyle doğrulandı. SQL Server ve hosted CI burada çalıştırılmadığı için
iddia edilmiyor.”

## Likely interview questions and honest answers

**Q: Did you use a real SQL Server?**
A: No. The SQL Server migration is generated and its DDL was rendered, but this
macOS ARM64 environment has no supported SQL Server runtime. Eight SQL Server
integration tests compile and are skipped. SQLite is not SQL Server proof.

**Q: How do you know the journal is correct?**
A: Unit tests cover deposit/withdraw/transfer/opening, 0.01/100,000 amounts,
unbalanced/cross-currency rejection, duplicate `SourceKey`, replay and rollback;
an isolated live E2E checks balanced legs, trial balance, baseline and role
boundaries. A forced unique-key failure proved the balance, legacy row, approval
and audit roll back together.

**Q: What stops a delegated approver from approving above their limit?**
A: `ApprovalAuthorizationService` re-reads the active delegation row on every
decision, including operation, currency, max amount, optional daily total and
expiry. Assignment is only metadata; it does not grant rights. The UI hiding is
not the control.

**Q: What happens if the journal write fails after the balance update?**
A: All writes are in one DB transaction owned by the approval service. A failure
rolls back the balance, legacy transaction, journal, approval state, timeline and
audit. The test simulates this with a duplicate `SourceKey`.

**Q: Did you backfill old balances?**
A: No. The trial balance exposes the gap. Owner preview/post creates explicit
`OpeningBalanceOffset`/`CustomerLiability` baseline entries and never changes
account balances.

**Q: Is this production-ready?**
A: No. It is a synthetic portfolio demo. SQL Server apply, hosted CI, manual UAT,
EULA, secrets management, deployment and security/accessibility audits are not
done.

**Q: What was AI’s role?**
A: AI-assisted implementation and review were used for code, tests, documentation
and evidence. The design decisions and claims were checked against executed
commands; no real SQL Server, bank or UAT result is claimed. This document is
transparent about that contribution.

## Do not put on the CV

- “Production banking system”, “real bank integration”, “PCI/SOC2/ASVS certified”.
- “SQL Server verified”, “GitHub Actions green”, “UAT passed” or “video recorded”.
- “Handled real customer data”.
