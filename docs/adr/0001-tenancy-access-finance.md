# ADR-0001 — Administrator hierarchy, taxpayer grants and segregated settlement accounts

- Status: Proposed / baseline for Phase 01
- Date: 2026-10-10

## Context
ArginTaxPayer is a hosted SaaS. Level 1 administrators administer the platform and delegate company/individual taxpayer access to Level 2 administrators (for example accounting firms). Level 2 administrators may sell entitlements and receive customer cash/gateway payments independently. The platform must also account for amounts payable/receivable between Level 2 and Level 1.

## Decision
Adopt a **platform root + delegated administrator workspace + taxpayer scope** model:
- `PlatformAdmin` (Level 1): global privilege and exclusive permission to read platform-wide audit/security logs, secrets metadata, security incidents and cross-workspace financial balances.
- `DelegateAdmin` (Level 2): business-management rights to only explicitly assigned taxpayers; cannot self-assign or escalate, access unrelated workspaces, manage root admins, export raw signing keys or view global logs.
- `Taxpayer`: natural/legal person; one or more fiscal memories if allowed by license/policy.
- `TaxpayerGrant`: explicit, revocable Level 1 grant with delegateAdminId, taxpayerId, grantedBy, effective timestamps and grant status. Deny by default.
- `DelegateWorkspace`: isolated commercial/finance configuration for each Level 2 administrator.
- `FinancialAccount`, `GatewayProfile`, `CashReceipt`, `Payment`, `JournalEntry`, `JournalLine`, `SettlementAgreement`, `Settlement`: each owned by a workspace, with explicit inter-party payable/receivable reconciliation.
- Subscription contracts are owned by a defined customer/workspace and usage is checked against the assigned taxpayer/memory scope. Quantity packs may cover unlimited taxpayers but expire no later than 12 months.
- Introduce distinct business history and global security logs. Redacted taxpayer business history is visible to authorized Level 2 operators; platform-wide audit/security logs remain Level 1 only.

## Financial lifecycle
1. Level 2 records cash or receives a verified gateway callback via its own profile.
2. An idempotent payment transaction is journaled in that workspace; no credit granted on unverified callbacks.
3. Invoice entitlements are granted at most once using stable business keys.
4. Contract-specific charges/commission owed to Level 1 are recorded as intercompany settlement liability and corresponding Level 1 receivable, without mixing bank balances.
5. Settlement is separately initiated, approved, executed and reconciled; partial settlement and refunds must be supported.
6. Sum(debits) = sum(credits) per posted journal; every mutation has actor, scope, timestamp and correlationId.

## Isolation and security invariants
- Enforce authorization at API handlers and data-access layer, including bulk imports, background workers, exports, search and file downloads.
- Never accept workspaceId/taxpayerId supplied by a request without verifying live grants.
- Revocation immediately blocks new operations; ongoing queued jobs must recheck authorization policy or act under explicit captured delegated authority.
- Signing private keys encrypted at rest with KMS/secrets management, never returned as readable downloads to delegates; certificate lifecycle stored separately.
- Audit of privileged operations is append-only with protected retention. Level 2 receives only non-sensitive business activity in its scope.
- Payment gateway credentials are isolated by delegate; callback authenticity and ownership must be checked.
- Settlement price and commission strategy are configurable by approved contracts; no assumed fixed percentage.

## Validation scenarios
- Delegate A cannot read/update taxpayer assigned only to B by guessed ID, listing, export, API or background job.
- Revoking an assignment prevents access and does not delete historical invoices.
- Delegate A never sees B's cash account, gateway credentials, payment ledger, settlement statements or private keys.
- Two callbacks for the same transaction cannot grant two quota units.
- Two simultaneous invoice-submission requests cannot consume quota twice or create a duplicate external submission.
- A Level 2 user cannot read global log records even with a direct URL/API request.
- Partial settlement, refund, payment failure and gateway charge reversal preserve balanced accounting.

## Open policy questions
- Can one taxpayer be delegated to multiple Level 2 workspaces at the same time? Default recommendation: one primary commercial owner per taxpayer, potentially multiple scoped operators, requiring explicit Level 1 approval for overlap.
- How do commission schedules, tax obligations and payout intervals work between levels?
- Is Level 2 permitted to create subordinate staff users? Recommend optional member users with permissions bounded by Level 2's current grants.
- On reassignment, who retains financial receipts and operational history? Recommend immutable historical ownership with carefully scoped new access.

## Alternatives rejected
- One global admin role with client-side filtering: insecure.
- Independent databases for every administrator in phase one: significantly raises operational complexity; revisit for contractual segregation.
- Mutable account balance as only financial source of truth: cannot safely reconcile callbacks/refunds/settlements.
