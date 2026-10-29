# Phase 10, Step 26 — Official cancellation of a previously submitted invoice

## Status
**In progress; NOT approved for real Sandbox sending.** Domain wire factory and isolation tests committed. Do not expose an enabled cancellation action until the backend writes a distinct legal document and can safely dispatch and reconcile it.

## Regulatory rules (archived RC_IITP.IS V7.9)
- Cancellation subject is `ins=3`; use a newly reserved 22-character `taxid` and the accepted predecessor's `irtaxid`.
- Cancellation has no transmitted body lines, buyer fields or type/pattern (`inty`/`inp`). Reuse the verified seller/fiscal memory from the predecessor.
- The predecessor must be officially registered, not cancelled, tenant-owned, and without a pending or duplicated active cancellation claim.
- Check approved correction/return references before permitting cancellation; verify V7.9 cancellation eligibility using `OfficialV79CancellationRules`.
- The UI requires exactly one explicit irreversible-action confirmation before executing the server command. Cancellation is **pending** until the tax authority confirms success; never mark the original cancelled after an HTTP acknowledgement alone.
- Preserve the original immutable snapshot, original fiscal tax ID and all authority history.

## Required implementation to finish this step
1. Transactional `invoice_legal_relations` with source and successor invoice IDs, fiscal reference, owner/taxpayer, subject, idempotency token and unique active cancellation claim; migrate on isolated test DB.
2. Authenticated, scoped command creates a **new** cancellation `InvoiceDocument` and immutable revision with verified predecessor evidence and internal reason/evidence reference; no overwrite of the original.
3. Extend readiness, serial repository, signed outbox preparation, calculations and lifecycle to handle cancellation's empty body and different legal wire requirements. The current `Phase10LegacyFrozenInvoiceProjector` explicitly rejects any subject other than Original; bypassing that guard is forbidden.
4. Single backend `POST /cancel-and-inquire` operates on a fresh successor through the same locked, idempotent dispatch pipeline. Guard ambiguous sending/Indeterminate outcomes against retries. Return acknowledgement and separately correlated authority decision.
5. UI displays a compact confirmation with a strongly worded warning; operation action only for officially accepted predecessor. On confirmation, issue once, show progress and resulting status. Update listing's effective subject only after confirmed authority acceptance; keep predecessor in the lineage.
6. Sandbox acceptance: original SUCCESS -> new cancellation SUCCESS; original never edited, IDs distinct and relation valid, list and chain correct. Additional cases: rejected/pending predecessor, duplicate clicks, concurrent sessions, missing reference, bad tenant, cancellation attempted twice, failure/Indeterminate then query. Run macOS .NET tests + Next typecheck/build and Ubuntu parity.

## Code delivered in this slice
- `Phase10CancellationWireFactory.Project`: original type/pattern is checked; produces cancellation `ins=3`, new tax ID, predecessor `irtaxid`, zero body lines using existing V7.9 wire mapper. It cannot transmit or authorize anything.
- `Phase10Step26CancellationWireFactoryTests`: tests no-body, reference and distinct-tax-ID invariants.

**Not implemented yet:** successor persistence, schema migration, cancellation dispatch orchestration, authority-confirmed update, active UI operation and live Sandbox results.
