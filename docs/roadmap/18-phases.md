# ArginTaxPayer — 18-Phase Product Roadmap

Status: Architecture baseline v1.0, Phase 01 design handoff ready, 2026-10-10. Roadmap phases are sequential milestones, though delivery tasks may overlap. Every phase requires acceptance tests, changelog, documentation and an explicit signoff.

| Phase | Name | Scope / acceptance outcome |
|---|---|---|
| 01 | Discovery and architecture | Domain model, authorization and ledger ADRs, source inventory, API contracts, ERD draft and risk register |
| 02 | Foundation and developer platform | ASP.NET Core Web API, Next.js, PostgreSQL/EF Core, CI, migrations, configuration and observability baseline |
| 03 | Identity and authorization | Registration, login, MFA/recovery, roles, scoped resource policies, token/session handling |
| 04 | Multi-taxpayer management | Person/company, fiscal memory, certificate/key lifecycle, Level 1 assignment of taxpayers to Level 2 administrators |
| 05 | Parties and customers | Economic identifiers, natural/legal persons, validation, deduplication, contacts |
| 06 | Goods and services | Tax goods/service IDs, official unit codes, VAT classifications and versioned catalog |
| 07 | Invoice domain and rule engine | Schema/version-specific types, patterns, subjects, correction/cancellation/return references, all officially valid combinations |
| 08 | Invoice workflows and UI | Draft/edit/review/approve, dynamic forms, settlement methods, calculations, import preview |
| 09 | Digital signatures, key generation and CSR | Natural/legal-person public/private key generation, PKCS#10 CSR, certificate import/validation, per-memory secure signing/encryption and key rotation |
| 10 | Tax authority connector | Versioned SDK/API adapters, authenticate, submit, retrieve references, integration contract tests |
| 11 | Queue, inquiry and reconciliation | Durable outbox, idempotency, status inquiry, retries, terminal results, accounting-safe credit handling |
| 12 | Subscriptions, billing and settlements | Annual/quantity entitlements, audited manual opening adjustments for remaining days/units, cash/gateway payment, independent Level 2 finance, Level 2↔Level 1 settlements |
| 13 | ArginAccounting integration | API + file import, master-data reconciliation, invoices and return of tax outcomes |
| 14 | Legacy migration | Admin creates new identities and links legacy customer IDs; repeatable read-only import of related taxpayers, counterparties, products, historical invoices with mapping, conflicts, preview, reconciliation and no automatic legacy plan/password import |
| 15 | Dashboards and reporting | Tenant-scoped reports, credit, financial statements, invoice outcome and exports |
| 16 | Platform & delegate administration | Super Admin console, Level 2 tenant consoles, policy control, support/financial operations |
| 17 | Security and quality hardening | Pen tests, isolation/invariant tests, load, disaster recovery and audit validation |
| 18 | Production release and documentation | Deployment, operator runbooks, user guides, release/tag, verification and rollback plan |

## Cross-cutting engineering requirements
1. Tenant and assignment constraints are enforced by server-side authorization and database query scoping, never only by UI filtering.
2. Financial ledgers use immutable postings, balanced entries and unique idempotency keys. Settlement approval and execution are separated.
3. Payment fulfillment, entitlement consumption, tax submission and external inquiry are independently auditable state machines.
4. External specifications and validator rules are versioned and testable; do not hard-code an assumed universal VAT rate, time limit, or pattern requirements.
5. Security events and globally sensitive logs are Level 1 only. Delegated administrators may receive redacted business-operation history for their assigned taxpayers, not global logs.
6. Underlying signing SDKs are adapters; avoid process-wide mutable singleton state when multiple fiscal memories sign concurrently.
7. Cross-system correlation: internalInvoiceId, sourceSystem, sourceRecordId, fiscalMemoryId, taxId, requestUid and authorityReference are separately stored.
8. Public GitHub repository: no production database dump or actual certificates/private keys.

9. Delegates maintain private workspace and customer-specific selling prices; Root/Level 2 partnership uses agreed percentages of **verified collected, tax-exclusive revenue less Root-approved eligible allocated expenses**, usually 50/50, NOT a wholesale/platform baseline price. Historical order, collection, agreement and expense approval snapshots remain immutable.
10. Key/CSR generation and fiscal-memory onboarding are distinct: the official portal issues the memory identifier and may require a public key or recognized certificate. Private keys must not leak to delegates or diagnostics; custody is explicitly consented to and protected.

## Release gate
Each phase closes only after architecture impact assessment, unit/integration tests, permission-isolation tests when relevant, migration review and explicit approval.

## Phase 01 design artifacts
- [Fiscal rule matrix](../phases/phase-01-step-03-fiscal-rules.md) — outline, exhaustive applicability pending
- [Bounded contexts](../phases/phase-01-step-04-bounded-contexts.md)
- [Logical ERD](../phases/phase-01-step-05-logical-erd.md)
- [API and security contracts](../phases/phase-01-step-06-api-security-contracts.md)

11. Legacy migration is repeatable while old MySQL app remains active; admin-created new accounts/credentials and manual subscription credits are authoritative (ADR-0004).

## Phase 01 signoff and Phase 02 entry
- [Architecture freeze and Phase 02 handoff](../phases/phase-01-architecture-freeze-handoff.md) closes **design scope**, not operational compliance testing.
- Phase 02 may begin with solution skeleton, CI, configuration, EF Core and security baseline. Fiscal rule expansion, real key signing connectivity and legacy source import acceptance remain explicit blocking checks in their designated implementation phases.

## Per-phase documentation policy (effective Phase 02 closeout)

Each new phase must have exactly one **authoritative, evolving** plan at `docs/phases/phase-XX.md`. Establish its numbered step plan, deliverables, acceptance criteria and dependencies **before** starting implementation. Update the same file after each step with evidence, commit SHA, macOS/Ubuntu test outcomes, migration impact and explicit owner approval. Do not create new per-step documentation files for ordinary progress. Preserve historical Phase 02 per-step documents as archives, but maintain the [Phase 02 master plan](../phases/phase-02.md) as the single source of truth. Any expansion, renumbering or material scope change must be recorded as a plan amendment approved by the owner; a phase-closing report belongs in the master file. Release/tag/main merge are separate approval gates.
