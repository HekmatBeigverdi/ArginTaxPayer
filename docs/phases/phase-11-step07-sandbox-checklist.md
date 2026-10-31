# Phase 11 Step 07 — Sandbox manual acceptance checklist

Operator must record each result as PASS, FAIL or NOT_RUN, with time, branch SHA and redacted evidence. A previous Phase 10 acceptance is a baseline, not a substitute for current-sha live evidence.

**Preconditions:** isolated Sandbox credentials and fiscal memory, authorized taxpayer, known approved official item identifiers, fresh test invoices, and no Production endpoint. Confirm `git rev-parse HEAD` matches the offline gate SHA. Back up the test database before any manual schema work (none is required by Step 07).

| ID | Scenario | Required result |
|---|---|---|
| S01 | Original invoice: create fresh valid invoice and submit once | Single tax identifier and Outbox operation; accepted history accessible |
| S02 | Correction of approved original | Original unchanged, one linked correction, latest subject and chain correct |
| S03 | Cancellation of eligible invoice | Linked cancellation appears as latest state; no duplicate top-level invoice row |
| S04 | Sales return with selected items/quantities | Legal quantities, totals and VAT recalculate; linked history intact |
| S05 | Reference-number inquiry of acknowledged operation | Existing receipt/reference used, no second invoice POST |
| S06 | UID inquiry of indeterminate operation | Inquiry only; original invoice never automatically resent |
| S07 | Two browser tabs / repeated submit / double click | One operation per immutable revision, safe conflict handling |
| S08 | Level1 / Level2 / customer scoped access | Unauthorized taxpayer and invoice are inaccessible via list, detail, history and inquiry |
| S09 | Simulated request timeout/HTTP 401/429/5xx against mocks, not authority | Sanitized errors; no auto invoice resend and no false fiscal acceptance |
| S10 | Restart API and review same invoice/chain | Outbox, attempt, receipt and history are durable; no unintended send on startup |
| S11 | Existing invoice history UI | Latest evidence timestamp/count match existing persisted receipts |
| S12 | Production isolation and log hygiene | No Production URL/credential exposure; no key, token, signed packet or raw provider JSON in logs |

**Stop immediately** if any unexpected new invoice POST, second tax ID, modified original legal snapshot, cross-tenant leak, Production route, or irreversible operation without operator intent is detected.

The Step 07 script is offline only. Never run live operations from automated test runners. Do not mark this step operationally accepted until the owner provides the PASS/FAIL outcomes.
