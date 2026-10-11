# Phase 02 — Step 03: Modular Monolith Boundaries

Date: 2026-10-11
Status: BOUNDARY CONTRACTS COMMITTED; BUILD/TEST EXECUTION PENDING
Branch: `phase/02-foundation`

## Objective

Translate Phase 01 bounded contexts into discoverable, documented, testable source locations without introducing premature domain behavior, tenant authorization, payment settlement, database schemas, or HTTP endpoints.

## Boundary layout

Each bounded context has an explicit declaration in `src/ArginTaxPayer.Application/Modules/<Context>/<Context>Module.cs`. A central `ModuleCatalog` lists the thirteen approved contexts. `IModuleDescriptor` declares ownership and security invariants. These descriptors are **architectural metadata only** and do not silently register DI services, enforce user permissions or publish events.

| Context | Owns |
|---|---|
| IdentityAccess | Users, roles, sessions and root security |
| Delegation | Workspace assignments and taxpayer grants |
| TaxpayerMemory | Taxpayer identity, fiscal memories and certificate references |
| PartyCatalog | Counterparties, products and official units |
| FiscalRules | Versioned invoice validators and calculation policies |
| Invoicing | Invoice lifecycle and immutable issue snapshots |
| FiscalGateway | Tax authority adapters and submission state |
| Subscription | Subscription orders and entitlement usage |
| Collections | Verified receipts and payment allocations |
| PartnerFinance | Expense approvals, profit distribution and settlement ledger |
| ImportIntegration | Legacy and ArginAccounting import boundaries |
| Reporting | Scoped read-model projections |
| Administration | Root-level global settings and security administration |

## Dependency policy

- `Domain` must remain independent of Application, Infrastructure, API, Workers, and Tests.
- `Application` may depend on Domain only.
- `Infrastructure` may depend on Application (and transitively Domain).
- API and Workers act as composition roots and may reference Application and Infrastructure.
- Modules cannot call each other's persistence tables directly. Future interactions use explicit application ports or versioned integration events.
- `IntegrationEventEnvelope` is a **contract only**, with EventId, EventType, OccurredAtUtc, CorrelationId and optional WorkspaceId. Event delivery, outbox, version-specific payloads and deduplication are later steps.

## Security invariants retained

- Delegation and taxpayer assignment must be validated server-side; a WorkspaceId is never authorization proof.
- Only Level 1/Root approves eligible delegate expenses and has platform-wide sensitive logs.
- Commercial partner profit is derived from verified **collected and allocated** cash less approved costs, not outstanding invoice balance.
- Collections and PartnerFinance maintain separate collection, profit allocation and bank settlement states.
- Fiscal memory private keys require secure, consented custody and must never be materialized into diagnostics.
- ImportIntegration never auto-sends imported historical invoices.
- FiscalGateway cannot directly adjust commercial payment receipts.

## Structural tests

`ModuleBoundaryTests` checks the exact Phase 01 module catalog, nonempty ownership/security notes, that descriptors belong to Application, and forbidden cross-layer references among project assemblies. This is not complete static source dependency scanning and does not prove runtime tenant isolation.

Run from repository root:

```bash
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~ModuleBoundaryTests
dotnet test ArginTaxPayer.sln -c Release --no-build
```

## Acceptance record

- [x] Thirteen source module declarations correspond to Phase 01 bounded contexts.
- [x] Shared interface and explicit module catalog established.
- [x] Minimal cross-module envelope contract established with no transport implementation.
- [x] Structural xUnit test source added.
- [x] English documentation added; no changes to Phase 01 design artifacts.
- [ ] Local or CI restore/build/test output verified.
- [ ] Owner sign-off for Step 03.

## Non-goals

No DI registrations (Step 04), PostgreSQL/EF Core mappings, tenant data filters, finance code, fiscal SDK calls, outbox worker or access-control enforcement. Do not claim that architectural metadata enforces isolation.

## Completion record

Source and documentation delivered exclusively to `phase/02-foundation`. No merge, release, or Step 04 implementation.
