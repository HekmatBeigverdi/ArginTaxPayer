# Phase 01 — Architecture freeze and Phase 02 handoff

Date: 2026-10-10
Status: **DESIGN BASELINE COMPLETE — HANDOFF READY**, contingent on explicit owner approval to defer implementation-dependent verification. Not an operational certification or claim that open security/regulatory tests passed.
Scope: architectural decisions and implementation plan only.

## Final decisions accepted by product owner

1. Platform Root (Level 1) alone assigns taxpayers/companies and sees global sensitive/security logs. Delegates (Level 2) operate only assigned customer companies/people, cannot view peers' data, have isolated gateway/bank/finance accounts and company-specific private plan prices.
2. Root and Delegate partners divide **net distributable profit of verified collected funds** at contract-specific percentages (usually 50/50), after appropriate tax exclusion and **Root-approved eligible costs**. Each first sale/renewal/extra quantity order has separate collection, expense review, calculated share and settlement history. Status of customer collection != share payable != bank settlement.
3. Plan A: 12-month subscription restricted to one taxpayer/fiscal memory. Plan B: quantity credit covering unlimited taxpayers, expires at most one year.
4. New users are manually provisioned by admins (usually mobile username, temporary admin-issued credentials and forced reset). Do NOT migrate password hashes or legacy plan/payment records as new commercial transactions.
5. Admin creates new taxpayer and links `(sourceId, legacyCustomerId)` to read old MySQL business records repeatedly while the old platform is live. Import historical taxpayer data, counterparties, products and invoice/submission history. Idempotent preview, mapping, conflict resolution; never auto-resend legacy invoices or consume new credits. New plan remaining 300 days / opening quantity can be adjusted manually with audit.
6. Fiscal key onboarding: generate keypair/CSR for natural/legal persons; user may **reuse existing private key** whose **matching public key is already registered in the official portal**. Accept fiscal memory identifier and existing private-key content via secure import; derive public key, compare portal/registered fingerprint if available, test real authorized signing/connection; never incorrectly claim that knowledge of memory code + private key alone guarantees connectivity. Reusing the matching key should not require registering a different public key merely for migration; key validity, registration state and SDK compatibility must be checked. No plaintext key in logs/ordinary DB or persistent response, signing-only custody with consent.
7. All officially supported invoice type/pattern/subject combinations remain target scope; protocol/validator profiles are versioned and testable.
8. Modular monolith: ASP.NET Core API + Next.js + PostgreSQL/EF Core; background workers and outbox; secret manager, isolated auth and delegated scope enforcement server-side.

## Deliverable evidence

- `phase-01.md` and ADR-0001 through ADR-0004 — requirements and agreements
- `phase-01-step-02-legacy-schema-inventory.md` and `phase-01-step-02-legacy-relationships.md` — 20 tables and 14 declared FKs
- `phase-01-step-03-fiscal-rules.md` and `phase-01-step-03-rule-traceability.md` — fiscal rule index / selected rule traceability (NOT exhaustive machine-readable manifest)
- `phase-01-step-04-bounded-contexts.md` — modules / transaction boundaries
- `phase-01-step-05-logical-erd.md` — initial logical ERD / constraints
- `phase-01-step-06-api-security-contracts.md` — endpoint and security design (NOT final OpenAPI)
- `phase-01-step-07-architecture-review.md` — initial NO-GO assessment; updated disposition is this handoff with formally deferred requirements
- `docs/roadmap/18-phases.md` — frozen high-level roadmap

## Deferment / phase-specific hard gates

| Issue | Phase due | Required evidence | Blocking milestone |
|---|---|---|---|
| Comprehensive field-by-type/pattern/subject rule manifest, tables 1–88 and golden JSON | 07 | Full table-driven validation + tests | Fiscal invoice issuance |
| Read-only legacy live/snapshot field mapping, orphan/duplicate counts, change detection | 14 (schema foundations in 02) | Dry-run report, reconciled sample, repeated-import tests | Production migration |
| Actual old key material and existing public key registration correspondence | 09–10 | Safe test with authorized key + signed tax authority request; certificate/PKCS8 compatibility | Taxpayer connection |
| Final OpenAPI, endpoint auth tests, threat model | 02–03, 17 | Generated API schemas, negative tenant scope test, threat assessment | Production API exposure |
| Loss handling, approved cost categories, partial cash/VAT distribution, refunds | 12 | Accountant-reviewed financial rules/fixtures and automated tests | Live partner settlement |
| Taxpayer grant transfer/exclusivity and entitlements activation threshold | 03–04, 12 | Decisions and scoped integration tests | Production authorization and sales |
| SDK version interoperability and signed API integration | 09–11 | Sandbox verification | Live tax submission |

**Contractual default assumptions** (provisional, not final business signoff): single active delegate assignment per taxpayer until Root transfers it; subscription entitlement starts on verified full payment unless Root-authorized credit adjustment exists; an order producing negative distributable profit cannot settle negative amount automatically; refund reverses profit slices and settlement liability. Any of these may require changes before the corresponding implementation milestone.

## Phase 02 acceptance blueprint

Create branch `phase/02-foundation` **after** this design handoff is accepted. Deliver:
- `ArginTaxPayer.sln`, modular C# project structure for Domain/Application/Infrastructure/API/Workers and isolated tests.
- Next.js App Router + TypeScript RTL/Persian base application, design system, base route skeleton.
- PostgreSQL dev provisioning, EF Core migrations baseline, schema separation and tenant root/workspace models (implementation schema reviewed with Phase 01 ERD).
- Config/env example with no embedded secrets; Vault abstraction; baseline auth policies and protected route scaffolding.
- OpenAPI health endpoints, logging/correlation/redaction, outbox and idempotency infrastructure.
- GitHub Actions CI (dotnet restore/build/test, npm lint/typecheck/build), local setup commands on macOS and Windows.
- Testable tenant-scoped boundary example and one ledger invariant test; no real production credential or gateway.
- English developer architecture notes + Persian admin/user UI labels and explicit completion record.

## Release decision

This document closes **architectural discovery/design scope only**. The project can proceed to Phase 02 foundation without waiting for late-phase integration/production evidence. Phase 01 should not be described as exhaustive fiscal/financial validation completed, and should not be tagged as a production release. Merge PR only when owner reviews final design documents; no automatic PR merge assumed.
