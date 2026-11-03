# Invoice subscription enforcement implementation plan

**Goal:** Enforce package expiry and invoice balance at every external submission and navigate blocked users to purchase.
**Architecture:** Precheck authenticated invoice workflows and enforce again inside the dispatch-attempt transaction. Reserve quantity before network send, consume with receipt, retain unknown reservations.
**Tech stack:** .NET 9, EF Core/Npgsql, Next.js/React, xUnit and Node tests.
**Spec:** `docs/superpowers/specs/2026-11-03-invoice-entitlements-design.md`

## Constraints and review focus
- Draft CRUD and inquiry remain unrestricted; never send live test packets.
- Handle the exact expiry instant, concurrent last unit, an expired former subscriber, lost send responses, and acknowledged replay.
- Preserve current trial policy for never-subscribed users pending clarification.

## Tasks
- [x] Add failing domain tests for entitlement decisions, then implement the policy.
- [x] Add database tests for transactional reservation/consumption and implement the shared persistence gate; integrate attempt and receipt transactions and workflow prechecks.
- [x] Add failing browser/API tests for subscription denial and implement forwarding and navigation for all four send actions.
- [x] Run backend suite, web suite and TypeScript validation; review the diff for scope, concurrency, idempotency and unguarded send paths.

Execution: inline in the current checkout to keep the requested changes reviewable in the user's workspace. No commits, publication or remote changes.

## Verification results
- Backend suite: 1,404 passed, 34 skipped, zero failures (integration opt-in disabled for the general run).
- Explicit isolated-schema PostgreSQL/domain run: 22 passed, including six database tests; no authority submissions and no changes to existing customer data.
- Web suite: 534 passed, three pre-existing failures. Reproduced those same three from HEAD in a temporary directory: `Customer cash checkout is server-owned and fail-closed for VAT/PSP`, `Step10 keeps approval and collections together and never enables PSP settlement from callback`, `Step10 includes scoped tariffs, admin approval, staged collection history, and SMS wallet`.
- TypeScript validation and git diff whitespace check passed.
- Independent code review found no actionable issues.
- Receipt replay test exposed an existing rollback-with-open-reader error; dispose the reader before rolling back.
- Known outcome: unknown network results retain their reserved unit; this prevents overspending before reconciliation.
