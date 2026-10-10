# Phase 01 — Step 04: Bounded contexts and integration contracts v1

Status: Architecture draft; no runtime code implemented. Architecture: modular monolith with separate background worker processes, ASP.NET Core API, Next.js UI, PostgreSQL.

| Context | Owns | Consumes/publishes | Forbidden dependency |
|---|---|---|---|
| IdentityAccess | users, MFA, roles, sessions, root-only audit authorization | principal and scoped policies | cannot determine finance by frontend filters |
| Delegation | workspaces, Level 1 grants, authorized taxpayers, assignment history | AssignmentGranted/Revoked | no delegation of root-only security privileges |
| TaxpayerMemory | legal/natural person, memory IDs, certificate lifecycle, vault references | MemoryActivated/Revoked | never expose raw private key |
| PartyCatalog | counterparties, products/services, official units | MasterDataChanged | cannot write fiscal submissions |
| FiscalRules | versioned type/pattern/subject constraints, money calculation | InvoiceValidated | no network or payment dependencies |
| Invoicing | draft, immutable issue snapshot, source mapping, reference chain | InvoiceIssued | no direct SDK singleton |
| FiscalGateway | authenticated API adapters, JWS/JWE, UID/reference, inquiry | SubmissionQueued/Accepted/Rejected | cannot modify commercial receipts |
| Subscription | plan catalog, private offers, purchased entitlements, expiration, usage | EntitlementGranted/Consumed | no quota deduction in HTTP callback without verified payment |
| Collections | verified cash/gateway receipts, allocations, chargebacks | CashAllocated/Refunded | never confuse customer collection with partner settlement |
| PartnerFinance | approved expenses, sharing contracts, profit slices, partner liabilities, settlement allocations, ledger | ExpenseApproved, ProfitCalculated, SettlementConfirmed | delegate cannot approve own cost or edit root-only records |
| ImportIntegration | ArginAccounting adapters, legacy staging, idempotent mapping | ImportValidated/Synced | no blind resubmit of imported invoice |
| Reporting | read-only projections, delegate-scoped reporting, Root oversight | consumes posted events | no writable settlement flags |
| Administration | tariff policy, system controls, support, security audits | Root decisions | privileged global logs never visible to delegate |

## Transaction boundaries

- One database transaction: verify customer payment, allocate collected money to an order, post immutable ledger and outbox event; idempotency uniqueness guards callbacks.
- Root expense approval is an independent audited transaction, and finalized profit-share calculation references approved expense version and collection allocation.
- Partner bank settlement confirmation is distinct from earning profit share; allocate only up to existing root liability, with row lock / optimistic concurrency.
- Sending fiscal invoice is an external process through outbox + retries + query-before-retry when outcome uncertain; never hold a DB transaction during tax API call.
- Entitlement activation and its payment threshold must be configurable per signed commercial policy. Do not infer it from partner settlement.

## Isolation architecture

Every workspace-owned row carries workspaceId or provably inherits it from a workspace aggregate; authorization policy checks must verify user, workspace, taxpayer assignment and operation. Cross-workspace access never relies on UI filtering. Root privileged logs are stored in a separately guarded subsystem. Secret material is referenced through a vault with tightly restricted signing operations.

## Cross-system IDs

`workspaceId, taxpayerId, fiscalMemoryId, internalInvoiceId, sourceSystem, sourceRecordId, taxId, requestUid, authorityReference, orderId, paymentId, agreementVersion` remain distinct, with unique indexes for imported provenance and idempotency.

## API design proposals (not deployed)

- `/api/v1/workspaces/{workspaceId}/taxpayers` — grants authorized by root only
- `/api/v1/workspaces/{workspaceId}/offers` — delegate owns pricing within approved scope
- `/api/v1/workspaces/{workspaceId}/orders` — purchases / renewals / top-ups
- `/api/v1/workspaces/{workspaceId}/collections` — verified payments and allocation
- `/api/v1/root/expenses/reviews` — root-only approval
- `/api/v1/workspaces/{workspaceId}/partner-settlements` — delegate read/submit evidence, root confirm
- `/api/v1/taxpayers/{taxpayerId}/fiscal-invoices` — scopes validated server-side
- `/api/v1/fiscal-invoices/{id}/submissions` — submission state and inquiry

## Handoff to Phase 02/03

Create project folders per context, DI boundaries, EF Core schemas/migrations, policy-based auth, server-side scope filters, outbox, idempotency and integration contract tests before exposing production write endpoints. Document decisions for assigning a taxpayer across delegate workspaces and loss/cost allocation.
