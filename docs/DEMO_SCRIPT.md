# Three-minute demo script

Status: **prepared — video NOT RECORDED**.
Environment: local synthetic SQLite demo (`.local/` is git-ignored), 1440p
screen, Turkish UI. No real customer, bank, payment or cloud system is used.
Do not show `.local/credentials.json`, tokens or the terminal secrets.

## Preconditions (before recording)

1. Fresh synthetic database; start API/dashboard on 5180/5181:
   `python3 scripts/start_local_detached.py --api-port 5180 --dashboard-port 5181`
2. Complete `/Setup` once (Owner), then use the Demo Center to seed scenarios.
3. Confirm `approver.100k` has the transfer/deposit/withdrawal/account-opening
   TRY 100,000 delegation. Keep `customer.a` (TRY 250,000) available.
4. Have a mismatch CSV ready: `/Admin/Reconciliation/Sample?kind=mismatch`.
5. Tabs pre-opened if possible: Owner approvals, Members, Accounting, Operations,
   Audit. Close personal browser tabs.

## Timeline

### 0:00–0:20 — frame the problem

- Show the Owner overview and approval queue.
- Say: “Every financial request starts Pending. No auto-approval, no direct
  movement endpoint. The Owner cannot approve their own request.”
- Point at the risk and “Bekliyor” status.

### 0:20–1:00 — new member → activation → first account → deposit

- Log out; `/Register` a synthetic member. Show that login is blocked until
  activation.
- Log in as Owner → **Üyeler** → activate the member and set role Customer.
- Log in as the new member → **Hesaplarım** → **Hesap Aç**; submit an opening
  request (TRY, Vadesiz).
- Owner → approval queue → open the request → approve.
- Customer → **Hesaplarım** → **Para Yatır** → request a small deposit.
- Owner approves. Show the balance only changes after approval and that the
  pending request had no completed receipt.

### 1:00–1:40 — inclusive delegated limit

- As `customer.a`, create a `100,000.00 TRY` transfer to `2000000001`.
- Log in as `approver.100k`; the request is in the scoped queue. Approve it.
  Say: “The limit is inclusive: 100,000.00 is allowed.”
- Create `100,000.01 TRY`; it is outside the delegated queue/authority and needs
  the Owner. Do not approve it. Cancel/reject as appropriate for the demo.
- Point out that a revoked delegation or stale JWT would lose authority
  immediately because the service re-reads the DB.

### 1:40–2:10 — journal is balanced and append-only

- Owner → **Muhasebe**. Show the per-currency trial balance:
  debit = credit; customer-liability journal total matches account balances.
- Open the transfer journal: two legs, source liability debit, target liability
  credit, same currency, same amount.
- Say: “The journal is append-only. There is no update/delete endpoint; a
  correction would be a new linked reversal.”
- Optional: show the Owner-only baseline panel and explain old accounts are not
  silently backfilled.

### 2:10–2:40 — operations and reconciliation gap

- Owner → **Operasyon Merkezi**. Show pending/overdue/oldest age, filters,
  assignment and the unresolved reconciliation card.
- Upload the mismatch CSV in **Mutabakat** (preview → import). Return to
  Operations: the batch appears as an unresolved difference.
- Open the batch/exception screen; explain that closing an exception is
  operational only and never changes a balance or journal.
- Mention Delegated sees only live-permitted rows; Customer gets `403`.

### 2:40–3:00 — audit and close

- Owner → **Denetim**. Show request creation, approval, journal/accounting and
  assignment audit events.
- Close: “Synthetic demo. SQL Server and GitHub Actions are not verified here;
  177 unit tests, 280 smoke checks, and the accounting/operations E2E suites are
  the real local evidence. Video is prepared, not recorded.”

## Short version (if time is tight)

Skip the new-member activation; use seeded `customer.a` and show:

1. 100,000.00 approved vs 100,000.01 routed to Owner.
2. Accounting journal balanced after approval.
3. Operations overdue filter + assignment.
4. Mismatch CSV → unresolved reconciliation.
5. Audit trail.

## Claims to avoid

- Do not call it production-ready, certified, ASVS-compliant or audited.
- Do not claim SQL Server was applied/verified, GitHub Actions passed, UAT was
  completed, or a video exists.
- Do not claim real bank/payment connectivity, FX, fees or interest.
