# ArginTaxPayer — 18-Phase Product Roadmap

Status: Baseline v0.1, 2026-10-10. Roadmap phases are sequential milestones, though delivery tasks may overlap. Every phase requires acceptance tests, changelog, documentation and an explicit signoff.

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
| 09 | Digital signatures and cryptography | Per-fiscal-memory protected private-key handling, certificates, signing/encryption abstraction, key rotation |
| 10 | Tax authority connector | Versioned SDK/API adapters, authenticate, submit, retrieve references, integration contract tests |
| 11 | Queue, inquiry and reconciliation | Durable outbox, idempotency, status inquiry, retries, terminal results, accounting-safe credit handling |
| 12 | Subscriptions, billing and settlements | Annual/quantity entitlements, cash/gateway payment, independent Level 2 finance, Level 2↔Level 1 settlements |
| 13 | ArginAccounting integration | API + file import, master-data reconciliation, invoices and return of tax outcomes |
| 14 | Legacy migration | Sanitized discovery, customer/party/product/invoice mapping, staging, dry-run, reconciliation and controlled cutover |
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

## Release gate
Each phase closes only after architecture impact assessment, unit/integration tests, permission-isolation tests when relevant, migration review and explicit approval.
