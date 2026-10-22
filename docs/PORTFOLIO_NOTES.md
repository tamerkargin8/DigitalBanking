# Portfolio / CV notes

These notes describe only work that exists in this repository and was actually
verified locally. No production, employer, certification or performance claim is
made.

## 30-second English narrative

> DigitalBanking is a .NET 10 banking-controls portfolio project: a Razor
> dashboard and API where every customer financial request stays pending until
> an Owner or an explicitly delegated approver authorizes it. It demonstrates
> inclusive per-operation/currency limits (100,000.00 TRY passes, 100,000.01
> goes to the Owner), owner self-approval blocking, live DB-backed revocation,
> reserve-aware balances, idempotent posting, customer ownership/IDOR protection,
> real payment timelines and receipts, day-end reconciliation with a
> maker-checker exception workflow, an append-only double-entry journal with an
> explicit Owner baseline, and an operations centre with SLA/age, safe assignment
> and unresolved reconciliation gaps. It includes 177 xUnit tests, 280 end-to-end
> smoke checks, 25/25 accounting and 24/24 operations live E2E checks; SQL Server
> migrations are documented but not applied, with a clear "demo, not production"
> boundary.

## CV bullets (3–4)

1. **Built a three-role banking approval platform (Owner / DelegatedApprover /
   Customer) in ASP.NET Core 10 and EF Core 10.** Enforced maker-checker on every
   transfer, deposit, withdrawal and account opening; blocked self-approval even
   for the Owner; implemented inclusive per-operation/per-currency limits,
   optional daily caps, expiry and immediate DB-backed revocation. Verified the
   99,999.99 / 100,000.00 / 100,000.01 TRY boundary with unit and end-to-end tests.

2. **Designed a safe customer self-service boundary.** Added `Customer.UserId`
   ownership with server-side joins, minimal PendingActivation registration,
   one-time localhost Owner bootstrap, last-Owner protection, IDOR-as-404, masked
   account data, persisted payment timelines and a status-gated, DEMO-marked
   printable receipt. The dashboard has separate Owner, delegated and customer
   navigation with server-side guards.

3. **Implemented reserve-aware money movement and operational reconciliation.**
   Pending outflows reduce available balance so two requests cannot overcommit;
   approvals revalidate ownership, currency, balance and current authority inside
   one transaction with `RowVersion` double-approval protection and
   `Idempotency-Key` replay. Added strict CSV preview/import, SHA-256 duplicate
   detection, deterministic match classes, formula-safe export and an
   Open→InReview→ResolutionProposed→Closed/Rejected exception workflow that never
   adjusts balances.

4. **Produced verifiable engineering evidence.** 177 xUnit tests, 280 end-to-end
   smoke checks, 25/25 accounting E2E and 24/24 operations E2E checks passing
   locally, 0 build warnings/errors, documented SQL Server migrations and an
   honest limitations/UAT report. Kept SQLite as a disposable Development
   playground rather than claiming SQL Server proof. Final SQL Server apply,
   hosted CI and UAT remain explicitly NOT RUN.

## Türkçe özet (CV/portföy için)

- **Üç rollü onay platformu:** Owner, DelegatedApprover ve Customer; transfer,
  yatırma, çekme ve hesap açılışının tamamı onaya tabi. Owner kendi talebini
  onaylayamaz; limitler operasyon/para birimi bazında ve **100.000,00 TRY dahil**,
  100.000,01 Owner'a kalır. Günlük limit, expiry ve anında iptal desteklenir.
- **Güvenli müşteri self-servisi:** `Customer.UserId` sahipliği, PendingActivation
  kayıt, tek seferlik localhost Owner kurulumu, son Owner koruması, IDOR'da 404,
  maskeli hesap, kalıcı timeline ve tamamlanmış işlemde DEMO uyarılı dekont.
- **Para ve mutabakat:** rezervasyonlu bakiye, atomik ledger+audit, idempotent
  tekrar, `RowVersion` çift onay koruması; CSV önizleme/import, SHA-256 mükerrer
  kontrolü, eşleşme sınıfları, formül-safe export ve bakiye düzeltmeyen istisna
  akışı.
- **Çift kayıtlı muhasebe + operasyon:** append-only dengeli journal, atomik
  onay transaction'ı, Owner baseline, mizan/journal ekranı; SLA/yaş, güvenli
  atama ve çözülmemiş mutabakat farkları.
- **Kanıt:** 177 xUnit, 280 smoke, 25/25 muhasebe ve 24/24 operasyon canlı E2E,
  0 uyarı/0 hata; SQL Server migration'ları belgelendi ancak uygulanmadı.
  SQLite sonucu SQL Server kanıtı değildir.

## What not to claim

- No real bank/payment-network integration, real customer data or live money.
- No production hardening, compliance certification, penetration test, load test
  or accessibility certification.
- No verified SQL Server run. The migrations are operator-ready documentation,
  not proof.
- No user counts, transaction volumes or performance numbers; none were measured.
- Real screenshots are committed in `docs/screenshots/` (headless Chrome, synthetic
  data). No fabricated/edited screenshots exist.

## AI contribution (honest disclosure)

- The implementation and documentation were produced in an AI-assisted workflow:
  a DeepSeek-based coding agent in the DSH harness, directed and reviewed by the
  user/GPT. Architecture, scope and rejection decisions came from the user.
- The agent had no production or SQL Server access and used synthetic data only.
  It did not push, publish or deploy.
- Tests, smoke runs, build results and limitations are recorded with commands and
  evidence dates. A future interview must not invent metrics beyond these.
- Manual UAT and SQL Server verification remain human/operator tasks; screenshots
  should be refreshed after UI changes.

## Interview talking points

- **Why all requests need approval?** Low-risk auto-approval was explicitly
  removed after the role/limit model changed: authority is per person and per
  operation, so "low risk" cannot silently bypass an approval policy.
- **Why inclusive limits?** `amount <= MaxAmount` is unambiguous; 100,000.00 is
  allowed and 100,000.01 is not. Tests pin both sides.
- **Why re-read the DB on every approval?** A JWT is a snapshot. Revocation,
  expiry, role change or daily-limit use must take effect immediately.
- **Why reserve pending outflows?** Creation-time checks alone allow two pending
  requests to overcommit one balance.
- **Why `RowVersion` + status check?** Two reviewers must not move money twice.
- **Why no balance adjustment on exception close?** The reconciliation record is
  operational evidence; money changes require the normal ledger workflow.
- **Why is SQL Server unverified?** No SQL Server environment was available.
  SQLite is a disposable Development convenience, not evidence of SQL Server
  decimal/concurrency behavior.

## Publish checklist

- [x] Real screenshots captured from the current dashboard, synthetic data only,
      no `.local` content/passwords/tokens.
- [ ] README test counts match the last run.
- [ ] `.local/credentials.json`, JWT secret, tokens and real data absent from git.
- [ ] SQL Server "not applied/verified" statement preserved.
- [ ] UAT checklist filled by a human; no item marked done without evidence.
- [ ] No production/compliance/metric claims added.
