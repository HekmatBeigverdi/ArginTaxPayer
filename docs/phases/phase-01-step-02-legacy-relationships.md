# Phase 01 — Legacy relationship audit and migration hazards

**Status:** Evidence-backed schema audit (structure only, source SQL dump dated 2026-10-10); row-level counts, orphan detection and execution remain pending. No legacy records are copied to this public repository.

## Physical relationships: 20 tables, 14 declared foreign keys

| Child | FK column | Parent | Parent key |
|---|---|---|---|
| conversations | TicketId | tickets | Id |
| customers | IdentityId | appuser | Id |
| customersoftplans | SoftPlanId | softplans | Id |
| files | CustomerId | customers | Id |
| files | EventId | events | Id |
| files | TicketId | tickets | Id |
| invoiceonebodies | InvoiceOneHeaderId | invoiceoneheaders | Id |
| invoiceoneheadermycustomers | InvoiceOneHeaderId | invoiceoneheaders | Id |
| invoiceoneheadermycustomers | MyCustomerId | mycustomers | Id |
| invoiceoneheadervendor | InvoiceOneHeaderId | invoiceoneheaders | Id |
| invoiceoneheadervendor | VendorId | vendors | Id |
| invoicesendhistories | InvoiceOneHeaderId | invoiceoneheaders | Id |
| payments | CustomerId | customers | Id |
| payments | SoftPlanId | softplans | Id |

The relationships above are extracted from actual ALTER TABLE ... FOREIGN KEY clauses, not guessed.

## High-risk logical joins needing explicit validation

- `customers.IdentityId` is physically bound to `appuser.Id`; other tables exposing `CustomerIdentityId` may carry the same identity but lack a declared FK. Confirm with distinct-key and orphan analysis before migration.
- `invoiceoneheaders`, `products`, `vendors`, `mycustomers`, `tickets` and `customersoftplans` use ownership-like IDs whose relational meanings should be verified empirically.
- `payments` has FK links to customer and plan; old paid flags or debt columns alone do not establish independently verified cash settlement.
- `invoiceoneheadermycustomers` and `invoiceoneheadervendor` are associative tables. Preserve both associations, their cardinality and invoice historical metadata.
- `invoicesendhistories` must be migrated as history tied to legacy invoices, not processed as an unsent outbox. Preserve fiscal identifiers and do not resubmit.
- `vendors.VendorPrivateKey` and `appuser.PasswordHash` contain high-sensitivity material; only secure, explicit import workflow may use them. Do not upload dump, row samples or unsanitized diagnostics.

## Proposed migration mapping and controls

1. **Superseded by ADR-0004:** new User is created by an administrator with fresh credentials (temporary password, forced change); legacy AppUser and password hash are not imported. Legacy source IDs only link authorized taxpayer/customer operational records.
2. New Taxpayer is created by an administrator, then linked to legacy Customer/Vendor identifiers to import selected business data. Fiscal memory code and generated/imported private key are configured separately through secure onboarding.
3. Product/ProductUnit/MyCustomer → tenant-scoped GoodsService/Unit/Counterparty, with current code validation and cross-tenant duplicate resolution.
4. InvoiceOneHeader/Body/associative rows/SendHistory → immutable FiscalInvoice, Lines, Parties, SubmissionHistory; link original taxid, UID, reference, status, times.
5. **Superseded by ADR-0004:** do not import old SoftPlan/CustomerSoftPlan/Payment as new commercial transactions. Admin chooses new plan and manually adjusts remaining validity days or units with an auditable opening adjustment. No historical partner share is inferred.
6. **Never infer past Level 1↔Level 2 profit-sharing debt from old payment data** without contractual evidence and a migration policy.

## Automated preflight checks (to run on an isolated read-only copy)

- Counts per table; unique and null checks for link columns.
- Orphan scans for logical references lacking FK.
- Duplicate taxid and repeated uid/reference among send history.
- Per-invoice body-total comparison against header totals, without exposing personal data.
- Cash and refund reconciliation to independent payment provider statement.
- Unexpired plan quantity remaining and dates with timezone analysis.
- Secret field presence counts only, never secret bytes, and a vault import dry run.
- Referential checksum manifests and replay-idempotent migration test on synthetic/anonymized fixtures.

## Acceptance

The physical FK mapping is **complete for the provided DDL**. The business cardinality, orphan counts and actual reconciliations are **not yet established**; Step 02 remains open.

See [ADR-0004](../adr/0004-repeatable-legacy-import-admin-onboarding.md) for repeatable import workflow, checkpoints, conflict policy and limited data scope.
