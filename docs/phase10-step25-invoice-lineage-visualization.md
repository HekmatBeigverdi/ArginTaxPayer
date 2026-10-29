# Phase 10 — Step 25 — Invoice legal lineage and visual timeline

## Purpose
A compact lineage action on invoice list rows **only after an actual Sandbox dispatch attempt**.
One node per **legally distinct invoice** and one directed reference edge to its immediate predecessor.
Never confuse internal workspace revisions with separate fiscal documents.

## User interface
- Single row action: `زنجیره صورتحساب` with chain icon. Display only when the current invoice or its connected chain has a persisted dispatch attempt.
- Label `تک‌ارسال` for exactly one dispatched fiscal invoice without related documents; `دارای تغییرات` when multiple linked fiscal documents exist; `در انتظار نتیجه` when dispatch outcome is unconfirmed.
- Modal: right-to-left, horizontally or vertically scrollable for many nodes; source node -> newer related node; use arrows carrying `مرجع صورتحساب`.
- Each node shows subject (original/correction/cancellation/sales return), official 22-character tax ID if reserved (otherwise unknown), issue date, dispatch timestamp, authority inquiry status, reference number if known. `SUCCESS` is distinct from enqueue acknowledgement.
- Click node to open that invoice in readonly detail; the latest active node is highlighted; cancel node is red only after authority-confirmed cancellation.
- Events within a single document (saved draft, finalized, sent, acknowledged, queried) are chronological entries inside its node; edits to a draft do not create a separate fiscal node.
- Source of truth must be backend tenant-scoped query. No client-side inferred graph, generated IDs, or guessed links.

## Backend and persistence
- Introduce durable `invoice_legal_relations`: owner, taxpayer, predecessor invoice, successor invoice, predecessor fiscal tax ID, relation subject, lifecycle status, creation time, confirmation time, uniqueness constraints for active reference claims and indexes. Parent/child belong to same tenant; reject cycles.
- One scoped read-only `GET /api/dev/taxpayers/{taxpayerId}/invoices/{invoiceId}/dispatch/lineage` should return nodes, edges, flags `hasDispatchAttempt`, `hasChanges`, `outcomeUnknown`. Use an authoritative persisted reservation for fiscal tax IDs and latest authority report for fiscal outcomes.
- Do not show any action if no dispatch attempt exists. Never infer acceptance from a reserved ID or an HTTP 200.
- Tree/chain relationship is created transactionally at the time a legally related draft is created, never by UI. Preserve source and all successors after a cancelled draft or failed send.
- Follow only same-owner same-taxpayer edges; server must revalidate roles/membership on every read.
- Concurrency: prevent duplicate legal successor claims using DB constraints and idempotency keys.
- Backfill only from trusted extant reference fields; records without verified linkage remain unlinked rather than fabricated.

## Acceptance
1. One dispatched original -> 1 node, no changes.
2. Original -> correction -> return -> cancellation where legally allowed: all nodes visible, edges point to predecessor, each tax ID displayed.
3. No dispatch ever -> action absent.
4. Original in Indeterminate -> node indicates unknown; no automatic resend.
5. Cross-tenant read -> forbidden/not found.
6. Repeated reload persists graph.
7. 15-row pagination retains row action, data loaded only when clicked.
8. Real Sandbox integration test after each lawful successor type; no success badge until confirmed by official inquiry.

## Implementation log — Step25 started (2026-10-29)

- **Slice A committed:** scoped .NET read service, `GET /dispatch/lineage`, protected Next.js BFF, conditional invoice-list chain action, compact read-only timeline modal, and contract tests.
- This first slice intentionally reports **only the selected existing invoice** from trusted dispatch, serial reservation and authority-report tables. It does **not** fabricate legal predecessor links or mark unverified related documents as accepted.
- **Remaining Step25 tasks:** add durable lineage edge storage and migration, write-side creation safeguards for correction/cancellation/sales return, integrate actual links in graph and query, expand statuses to `تک‌ارسال`/`دارای تغییرات` grounded in backend graph data, and run full macOS acceptance + Sandbox evidence.
- Not yet accepted: legal successor creation, graph arrows between distinct documents, real Sandbox multi-document chain. No irreversible fiscal operation is introduced by Slice A.
- Tests: `node --test apps/web/tests/phase10-step25-lineage-initial.test.mjs`; full `dotnet build ArginTaxPayer.sln`, web typecheck/build and authenticated browser test are mandatory before acceptance.
