# Quantity correction after a partial sales return — 2026-11-09

Base commit: `6984d0846eb75c7768fe77d810633bd73bea3027` (working-tree changes).

## Reproduction and cause

Invoice `00020-05-ACR` contains three units and has one one-unit sales return.
Its correction baseline is two units. Saving a correction reducing that baseline
from two to one returned HTTP 400 `illegal_correction_fields` when the form sent
`reductionCorrectsOriginalMistake=false`. Saving the unchanged two-unit draft
already succeeded; the reported failure was the additional reduction to one.

The correction workflow now records a quantity reduction as a quantity correction
and stores a reference to the actual correction workspace for audit. Explicit
evidence references remain supported. Immutable item fields, positive quantities,
prices, discounts and VAT restrictions remain validated. The form explains the
meaning of a quantity reduction without requiring the separate checkbox.

Untouched drafts use the return-adjusted baseline. Saved edits survive reopening.
The overview correction action references the original document when the displayed
effective document is a sales return.

## Test database migration

The owner explicitly approved migration `20261109010000_AllowPartialSalesReturns`
in this conversation. It was applied with a pinned EF target on verified
`argintaxpayer_test` through `127.0.0.1:15432`.

The combined correction/return claim index was replaced by
`ux_authority_legal_correction_claim`, unique for active corrections. Existing
successor and idempotency uniqueness constraints remain present.

Backup: `~/.argintaxpayer-test/backups/before-partial-returns-20261109-094707.dump`.
`pg_restore --list` succeeded (1,506 archive entries); no restore exercise was run.
Reviewed SQL: `~/.argintaxpayer-test/allow-partial-sales-returns-review.sql`.
Migration history and the resulting index definition were rechecked after apply.

## Verification

- Before the fix: the actual web BFF returned HTTP 400 for the two-to-one edit.
- After the fix: saving the one-unit correction, reopening it, and saving again
  returned HTTP 200; the stored draft remains one unit with a 100,000 rial cash total.
- Headless Chromium opened the actual invoice row and correction dialog, entered
  one unit, and clicked Save Draft: HTTP 200 `draft_saved`, quantity one.
- 13 focused .NET tests passed, including connection-local PostgreSQL fixtures for
  the three-unit original / one-unit return / two-to-one correction in Production
  and Sandbox. Temporary tables prevent changes to invoice history in these tests.
- Eight focused web tests passed; TypeScript checking passed.
- The local API is running the rebuilt code. A duplicate development watcher was
  removed and the local API PID file updated to the remaining watcher.

No Tax Authority submission was performed during verification. The actual user's
correction draft was saved with the requested quantity of one; issued invoice and
sales-return snapshots were not changed.

## Follow-up: correction baseline and lineage refresh

The owner subsequently requested that the **Correct** action start with the two
remaining units rather than the one-unit edit saved during the earlier checks.
Correction responses now expose `remainingQuantities` independently of saved line
quantities. Starting a correction initializes quantities from that baseline;
Continue Editing retains saved edits. Opening and reading a draft compute the
baseline from the scoped immutable original and its sales returns without
overwriting the stored draft. The review tab also shows the remaining baseline.

The lineage modal now notifies the invoice overview after a successful inquiry
and after a resumed submission attempt; closing it also refreshes the overview.
Previously it refreshed only its own graph, leaving the table status stale.

Follow-up verification: 33 focused web tests and TypeScript checking passed, as did
13 focused .NET/PostgreSQL tests. The PostgreSQL fixture checks both Production
and Sandbox, verifying a two-unit baseline alongside the saved one-unit edit.
Headless Chromium verified the actual workspace components with deterministic
API fixtures: Correct displayed two despite a saved one-unit draft; a simulated
lineage inquiry triggered an overview request and changed the visible table row
from Pending to Success while the lineage remained open. These browser fixtures
did not change invoice data or contact the Tax Authority.
