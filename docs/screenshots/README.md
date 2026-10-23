> The existing screenshots are historical synthetic captures made before the 2026-10-23 visual refinement. The refined screens were independently reviewed via sanitized server-rendered previews; no new image capture is claimed here. See [refinement report](../DASHBOARD_REFINEMENT_REPORT.md).

# Screenshots

Real captures from the local synthetic demo on **2026-10-20/22**, using headless
Chrome against the disposable SQLite Development database. Desktop captures use
1440 CSS pixels width at 2× DPR with variable content height; mobile captures are
390×844 CSS pixels at 2×.

| File | Screen | Persona |
|---|---|---|
| `owner-overview.png` | Owner operations overview | Owner |
| `owner-approval-queue.png` | All-request approval queue | Owner |
| `owner-delegations.png` | Yetkiler & Limitler (inclusive limit, duration, status) | Owner |
| `owner-delegations-mobile.png` | Delegation table on a narrow viewport | Owner |
| `owner-reconciliation.png` | Reconciliation batches | Owner |
| `customer-home.png` | Balances per currency + recent activity | `customer.a` |
| `customer-accounts.png` | Tümü/Vadesiz/Vadeli/Yatırım tabs and account cards | `customer.a` |
| `customer-accounts-time-deposit.png` | Time-deposit account tab/card | activated synthetic member |
| `customer-account-details.png` | Account detail, rename, deposit/withdraw actions | activated synthetic member |
| `customer-home-mobile.png` | Customer home on a narrow viewport | `customer.a` |
| `customer-first-account.png` | Earlier no-account first-account CTA (empty-state evidence) | activated synthetic member |
| `customer-transfer.png` | Transfer wizard | `customer.a` |
| `customer-receipt.png` | Completed, masked, DEMO-marked receipt | `customer.a` |
| `owner-accounting.png` | Per-currency trial balance, journal list, Owner baseline panel | Owner |
| `owner-journal-detail.png` | Balanced journal entry and its debit/credit legs | Owner |
| `owner-operations.png` | Operations center: SLA, overdue, filters, assignment, reconciliation gaps | Owner |
| `delegated-operations.png` | Delegated operations view scoped by live delegation | `approver.100k` |

No passwords, tokens, `.local` contents, real customer data or live banking
systems are shown. Accounting/operations captures were taken from a separate
isolated synthetic SQLite demo (5198/5199), not the UAT environment. These are not mockups and were not edited to add data. The
narrow captures are real viewport emulations, not resized desktop screenshots.
No automated visual-regression assertion or human visual review is claimed; DOM
overflow, label association, role navigation and focus checks were run separately.
