# Phase 01 — Step 02: Legacy database schema inventory (safe structural review)

Source: owner-provided MySQL 8 `taxpayermaindb` SQL export, 2026-10-10. This document records **only table and non-secret field names**, never row values, password hashes or private-key bytes. **Status: initial inventory, not migration signoff.**

## Inventory (20 tables)

| Legacy table | Key observed columns (selected) | Proposed new area | Migration issue |
|---|---|---|---|
| appuser | Id, UserName, PasswordHash, Active | Identity | Validate legacy password compatibility or force reset; no hash export |
| customers | Id, IdentityId, MemoryId, PrivateFileName, Tins | Taxpayer, FiscalMemory | Verify IdentityId meaning; secret-file custody |
| customeractivations | IdentityId, ActivationCode | Identity | Historical/obsolete codes must not be migrated active |
| customersoftplans | SoftPlanId, CustomerIdentityId, Remained, Expired, start/end | Subscription/Entitlement | Reconcile credit count and expiry |
| softplans | SoftPlanType, SoftPlanQty, SoftPlanPrice, SoftPlanDuration | Plan | Translate to annual/quantity while preserving historical price |
| payments | Authority, PaymentType, Amount, PaidAmount, DebtAmount, IsPaid, Checkout | Payment/Receivable | Never assume IsPaid alone proves bank settlement; reconcile with gateway |
| products | CustomerIdentityId, ProductUniqueCode, ProductVat, ProductUnitCode | GoodsService | Per-customer ownership and tax code |
| productunits | ProductUnitCode, ProductUnitTitle | UnitReference | Map to current official units |
| mycustomers | CustomerIdentityId, CustomerNationalId, CustomerEcoNo | Counterparty | Dedupe and validate IDs |
| vendors | CustomerIdentityId, VendorMemoryId, VendorPrivateKey, VendorEcoNo | Seller/Taxpayer | **HIGH RISK: private-key field**; import only via isolated secret procedure |
| invoiceoneheaders | Taxid, Indatim, Indati2m, Inty, Inp, Ins, Tins, Tbill, Uid, ReferenceNumber, Status, CustomerIdentityId | FiscalInvoice | Preserve historic IDs/status/date; no resubmission |
| invoiceonebodies | InvoiceOneHeaderId, Sstid, Mu, Fee, Dis, Vra, Vam, Tsstam | InvoiceLine | Reconcile totals and numeric precision |
| invoiceoneheadermycustomers | InvoiceOneHeaderId, MyCustomerId | Invoice-Counterparty | Relational link |
| invoiceoneheadervendor | InvoiceOneHeaderId, VendorId | Invoice-Seller | Relational link |
| invoicesendhistories | InvoiceOneHeaderId, ReferenceNumber, Uid, Status, Errors, Raw | FiscalSubmission | Raw may carry personal/business information; segregate |
| files | FileName, CustomerId, VendorId, TicketId | DocumentReference | Inspect external file store; do not copy sensitive files automatically |
| tickets | CustomerIdentityId, Subject, Status | Support | Retention decision |
| conversations | TicketId, Message, IsCustomer | Support | PII redaction/retention |
| events | Title, Important, Desc | Content | Optional historical import |
| __efmigrationshistory | MigrationId, ProductVersion | Migration provenance | Never treat as new EF migration state |

## Important observations

- The old schema contains direct taxpayer/memory/key references and basic subscription/payment structures; it does **not** prove it implements the new multi-delegate profit-sharing and per-company settlement model.
- Both `vendors.VendorPrivateKey` and user `PasswordHash` are highly sensitive. No values are committed or exposed here.
- Legacy invoice header/body naming reflects an invoice-one-oriented implementation. New schema must support all official combinations rather than mirror old table names.
- Confirm cardinality from full foreign-key definitions and sanitized counts; field-name resemblance alone does not establish referential truth.

## Migration acceptance checklist (pending)

1. Prepare sanitized counts and orphan/duplicate checks offline, keyed by legacy IDs but without PII.
2. Define legacy IdentityId → user/workspace/taxpayer mapping and whether delegate assignments must be created manually.
3. Map fiscal memory and key material with customer consent and secure import; never expose plaintext keys to Level 2.
4. Reconcile historical sent invoices and send histories, persist original tax ID/UID/reference/status and never resend.
5. Reconcile legacy plan balances and paid/unpaid payments with gateway records.
6. Perform idempotent dry-run with row counts, error report and rollback, then sign off before cutover.
