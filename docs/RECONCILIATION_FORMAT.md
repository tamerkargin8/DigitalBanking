# Reconciliation CSV format and exception workflow

This is the strict synthetic external-statement format accepted by
`POST /api/reconciliation/preview`. Files are operator-supplied; this demo never
contacts a real bank.

## File rules

| Rule | Value |
|---|---|
| Encoding | Strict UTF-8; optional BOM; invalid UTF-8 is fatal |
| Extension | `.csv` (case-insensitive) |
| Size | 2 MB maximum |
| Rows | 5,000 data rows maximum |
| Header | Exactly the six names below; no extra/missing/duplicate columns |
| Quoting | RFC-4180-style quoted fields, `""` escapes, embedded commas/newlines |
| Amount | Positive decimal, maximum 2 decimal places, invariant `.` separator |
| Currency | `TRY`, `USD`, `EUR`, `GBP` (normalized upper-case) |
| Date | `yyyy-MM-dd`, `yyyy-MM-dd HH:mm:ss`, ISO-8601, or a parseable invariant date; stored UTC |
| Direction | `Debit` or `Credit` (case-insensitive) |
| Internal reference | A GUID. A transaction id matches directly; otherwise it is treated as a correlation id |

```csv
external_reference,internal_reference,amount,currency,transaction_date,direction
EXT-2026-0001,11111111-1111-1111-1111-111111111111,125.00,TRY,2026-10-20,Debit
EXT-2026-0002,22222222-2222-2222-2222-222222222222,125.00,TRY,2026-10-20,Credit
```

Malformed rows are persisted as invalid with a message and the batch is marked
`HasErrors`; **import is refused** until the file is corrected. A malformed
header or invalid UTF-8 is fatal and no importable batch is produced.

## Scope and duplicate rules

- Optional preview filters: `currency`, `direction`, `from`, `to`. They form the
  scope key together with defaults `ALL` for unscoped dimensions.
- File identity = SHA-256 of the bytes + scope key. Re-uploading the same file in
  the same scope is flagged `replayed` and does **not** create a second batch.
- Import is idempotent; an already imported batch is returned unchanged.
- Batches are immutable after import. Re-importing does not rewrite results.

## Matching

Rows are matched deterministically:

1. Duplicate `external_reference`, or duplicate
   `(internal_reference, direction)` across the file → `DuplicateReference`.
2. No system transaction for the internal GUID → `OnlyExternal`.
3. Direction mismatch or no candidate in that direction → `OnlyExternal`.
4. More than one candidate correlation direction → `DuplicateReference`.
5. Currency differs → `CurrencyMismatch` (no FX conversion).
6. Amount differs → `AmountMismatch` with `difference_amount = external - system`.
7. Exact transaction id/correlation + direction + currency + amount → `Matched`.
8. System transactions in scope not matched to a valid external row →
   `OnlySystem` generated rows.
9. Pending approval requests are **not** treated as system ledger transactions;
   they are counted separately as `pendingApprovalCount`.

All amounts are decimal (18,2); no floating-point comparison.

## Exception workflow

An `OnlyExternal` (or other mismatch) row can open an exception:

```text
Open -> InReview -> ResolutionProposed -> Closed
                                     \-> Rejected -> Reopen -> Open
```

- Only Owner or an approver with an active `ReconciliationException` delegation
  may act. No monetary cap is applied to this operation.
- The person who proposes the resolution cannot approve/reject it. A different
  authorized person is required.
- Closing records the operational decision and audit entry. **It never adjusts a
  customer balance or creates a ledger transaction.** Any financial correction
  must use the normal approval-request workflow.
- Reopen clears resolution/review fields and returns the item to `Open`.

## Export

`GET /api/reconciliation/batches/{id}/export` returns UTF-8 CSV with:

```text
line_number,external_reference,internal_reference,amount,currency,transaction_date,
direction,match_status,matched_transaction_id,system_amount,system_currency,
system_date,difference_amount,validation_error
```

Cells starting with `=`, `+`, `-`, `@`, TAB or CR are prefixed with `'` before
CSV quoting, preventing spreadsheet formula execution. Exported results are a
view of the immutable batch.

## Sample files

- [`samples/reconciliation-matched.csv`](samples/reconciliation-matched.csv)
- [`samples/reconciliation-mismatch.csv`](samples/reconciliation-mismatch.csv)

These are **format examples only**; their GUIDs do not refer to seeded
transactions, so they will legitimately appear as only-external in a demo. For a
live demo use **Owner → Mutabakat → Örnek CSV** to download a file generated from
the current demo transfer.
