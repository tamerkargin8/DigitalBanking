# ADR-0002 — Double-entry journal for customer money movements

- Status: Accepted and implemented (Stage 3; synthetic SQLite verification)
- Date: 2026-10-22
- Context: existing `LedgerOperations` is a single-entry customer account ledger;
  it proves balances but not accounting equality.

## Decision

Keep the existing `Transactions` table as the customer-facing statement/legacy
ledger and add an append-only balanced journal alongside it.

- Each posted `JournalEntry` belongs to exactly one currency and one source
  operation (`Deposit`, `Withdrawal`, `Transfer`, `AccountOpening`, `Baseline`).
- Lines are `Debit` or `Credit`, positive, 2dp. For each entry:
  `sum(debit) == sum(credit)` and every line has the entry currency.
- Bank viewpoint:
  - customer deposits are **liabilities** (`CustomerLiability`);
  - deposits debit `CashClearing` and credit customer liability;
  - withdrawals debit customer liability and credit `CashClearing`;
  - same-currency transfers debit source customer liability and credit target
    customer liability; cross-currency transfers are rejected;
  - a nonzero opening balance debits `OpeningBalanceOffset` and credits customer
    liability; zero opening produces no journal.
- Customer account lines carry `AccountId`; system lines carry a
  `JournalAccountType` and no customer account id. Missing account/currency or
  unbalanced entries are rejected.
- `SourceKey` is unique (`approval:<id>`, `baseline:<accountId>`,
  `reversal:<entryId>`), so replay/double approval cannot duplicate a journal.
  `RowVersion`, approval status, limits, revocation and self-approval checks stay
  in the existing approval transaction.
- Approval writes balance + legacy transaction + journal + approval + timeline +
  audit in one DB transaction. A journal failure rolls everything back.
  Pending/rejected/cancelled requests write no money journal.
- Posted entries are never updated or deleted through the app. A correction would
  be a new balanced reversal entry linked with `ReversalOfJournalEntryId`; the
  current stage does not expose an automatic correction/deletion endpoint.
- Existing history is **not silently backfilled**. Old accounts without journals
  are shown as an explicit baseline gap. The gap is
  `account.Balance - net CustomerLiability already in journals`; Owner-only
  `preview`/`post baseline` adjusts only that gap in the appropriate direction
  (debit offset/credit liability for a positive gap, reverse for a negative gap).
  Fully journaled accounts and accounts with an existing baseline are skipped.
  This is a one-time opening adjustment, not fabricated history and not a rewrite
  of a later deposit/withdrawal.
- Baseline totals are per currency; no mixed-currency total or FX conversion.
  Posting uses a serializable transaction and the unique `baseline:<accountId>`
  source key so a concurrent approval cannot produce a duplicate gap.

## Implementation references

- `DigitalBanking.Infrastructure/Services/JournalPoster.cs`
- `DigitalBanking.Infrastructure/Services/AccountingService.cs`
- `DigitalBanking.Infrastructure/Services/PaymentApprovalService.cs`
- `DigitalBanking.API/Controllers/AccountingController.cs`
- `DigitalBanking.Dashboard/Pages/Admin/Accounting/`
- `DigitalBanking.Tests/AccountingJournalTests.cs`
- `docs/STAGE_03_REPORT.md`

## Consequences

- Trial balance is per currency; no FX aggregation is claimed.
- The journal is a synthetic demonstration, not a real general-ledger,
  regulatory, fee, interest or FX-accounting implementation.
- SQL Server migration is additive. Existing SQLite databases need the provided
  additive upgrade path; UAT databases are not modified by this project.
