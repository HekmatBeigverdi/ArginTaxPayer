# Phase 01 — Step 07: Architecture review and go/no-go report

**Status: CONDITIONAL NO-GO for completing Phase 01**. Review dated 2026-10-10. This review is a design gate, not a claim that implementation or tests passed.

## Accepted design baseline proposed for owner review

- Single ASP.NET Core modular monolith with Next.js client, PostgreSQL, isolated outbox/worker and versioned official tax adapter.
- Root (Level 1) grants assigned taxpayer scopes to delegate (Level 2); delegate has full authorized business operations within granted companies, but no global security logs, other delegates' finances or raw signing keys.
- Each delegate may set own and company-specific prices privately. Commercial order snapshots are immutable.
- Root/delegate relationship is a **net-profit partnership** with agreed percentages (normally 50/50) on **verified collected cash allocated to the order**; not a base-price resale nor accrual sharing. Output VAT/pass-through must not be counted as sale profit.
- Root approves or rejects each supported eligible expense before finalization; bank settlement per order/company is separate from collecting money.
- First sale, renewal, quantity extension and refunds maintain separate identifiable orders and partial settlements.
- Certificate onboarding supports natural and legal persons, keypair and CSR generation, and custody/official memory registration independently.
- Legacy history migrated without resending invoices; source+legacy IDs preserved.

## Evidence inventory

| Deliverable | Document | Review status |
|---|---|---|
| Scope and commercial rules | `phase-01.md`, ADR-0001–0003 | design baseline recorded |
| Legacy DDL inventory | `phase-01-step-02-legacy-schema-inventory.md` | initial table mapping |
| Physical FK audit | `phase-01-step-02-legacy-relationships.md` | 14 declared FKs verified; logical cardinality open |
| Fiscal patterns and subjects | `phase-01-step-03-fiscal-rules.md` | allowlist drafted |
| Rule source traceability | `phase-01-step-03-rule-traceability.md` | selected source rules checked; exhaustive matrix open |
| Bounded contexts | `phase-01-step-04-bounded-contexts.md` | architecture draft |
| Logical ERD | `phase-01-step-05-logical-erd.md` | design draft, no migrations |
| API and security | `phase-01-step-06-api-security-contracts.md` | contract draft, no OpenAPI or tests |

## Risks / unresolved decisions

| ID | Priority | Concern | Gate action |
|---|---|---|---|
| R01 | Critical | Full official field applicability and validators not extracted | finish Table 1/2 and 3–88 matrix, reviewed fixtures |
| R02 | High | Payment / plan balances in legacy DB may be inconsistent | anonymized reconciliation, bank evidence, migration rehearsal |
| R03 | High | Legacy signing keys and Identity credentials | independent vault/consent/reset design, no dump in repo |
| R04 | High | Expense deductibility, partial cash collection tax allocation, losses and refunds | approve written legal/accounting policy, deterministic examples |
| R05 | High | Fiscal SDK and authentication compatibility | sandbox connectivity and signed/unsigned certificate profiles |
| R06 | High | Scope collisions if same taxpayer assigned to two delegates | product owner decides assignment exclusivity and transfer process |
| R07 | Medium | Partial payment: when entitlements activate | choose full-payment/credit terms before order APIs |
| R08 | Medium | Ledger recognition accounting vs cash basis profit distribution | define tax/accounting accrual separately from partner payout policy |
| R09 | Medium | Disclosures for admin/logs and encryption | threat model, retention and authorization test plan |
| R10 | Medium | Account owner and delegate bank/gateway segregation | gateway onboarding permissions and settlement evidence procedure |

## Required before phase acceptance

- Owner sign-off on unresolved commercial rules R04, R06 and R07.
- Exhaustive fiscal rule manifest or a clearly approved staged-scope release agreement, not implied full coverage.
- Legacy referential/duplicate/orphan count check on isolated read-only dataset.
- OpenAPI schema, STRIDE threat model and concrete negative authorization test cases.
- Update status table to DONE only after evidence and owner approval. **Do not merge main or tag release now.**

## Phase 02 preparation

Can provision repository solution skeleton, CI and test infrastructure **on a dedicated Phase 02 branch only after Phase 01 sign-off**. No executable APIs or database were created as part of this review.
