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

## Scheduled SMS pricing and identity delivery allocation (approved scope extension, 2026-10-12)

This cross-phase requirement extends the existing numbered roadmap **without adding or renaming any of its eighteen phases**. Full specification and acceptance: [Phase 03 master plan](../phases/phase-03.md) and [GitHub issue #4](https://github.com/HekmatBeigverdi/ArginTaxPayer/issues/4).

- **Phase 03, owner-approved 11-step plan (amended 2026-10-12):** Step 08 completes the original Persian RTL email/password/MFA UI verification; **new Step 09** implements phone-first registration, optional email/SMS OTP authentication and recovery, mocked Ghasedak adapter, and backwards-compatible identity migration; **Step 10** delivers identity/authorization integration testing (formerly Step 09); **Step 11** completes security hardening and financial handoff (formerly Step 10). No previously completed Step 01–07 is renumbered or reopened. Phase 03 does not assert full paid-SMS billing completion.
- **Phase 12:** Owner of customer SMS wallets, one-time 2,000,000 IRR promotional credit, append-only wallet/provider expense ledger, paid SMS reservations, top-ups, per-panel/per-category **versioned scheduled tariff** engine and charge snapshots. Rate is resolved at atomic send reservation timestamp (UTC). Every subsequent retry/settlement keeps the immutable original rate; no price retroactivity, no double-charge and no ambiguity if a scheduled increase becomes effective.
- **Phase 16:** Owner of **Level 1-only** scheduled tariff administration, future-dated price changes, cancellation/reschedule audit, role-scoped top-up and expense reports. Level 2 is read-only within explicitly authorized scope, never a pricing administrator.
- **Phase 17:** Security, concurrency, timezone/price-boundary, distributed rate-limit and persistent audit tests, including provider reconciliation and no duplicate promotional credits.
- **Phase 18:** Production seed/activation, Ghasedak secret provision, controlled live send validation, migrations and operational financial reconciliation.

Business contract: a future price may be created now for activation at a specified `Asia/Tehran` local date/time and stored in UTC; for each provider panel + charge category exactly one price applies at any instant. Customer-facing SMS tariff and actual Ghasedak provider expense are independent IRR amounts, each auditable. Missing current rate blocks paid send, never silently falls back to zero. Level 1 controls price schedules; past send snapshots are immutable. Real SMS sends and deductions stay disabled until the responsible phases are implemented and explicitly accepted.

## Canonical macOS PostgreSQL Test Tunnel (Owner-Mandated, 2026-10-12)

Use `autossh`, **not plain `ssh -N`**, for every ArginTaxPayer phase's macOS-to-Ubuntu PostgreSQL tunnel. Keep it open in a dedicated local terminal. Do not alter the target or database without explicit owner instruction:

```bash
AUTOSSH_GATETIME=0 autossh -M 0 -N \
  -o ServerAliveInterval=15 \
  -o ServerAliveCountMax=3 \
  -o ConnectTimeout=10 \
  -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
```

The tunnel is only transport. All migrations and database tests must independently check `current_database() = 'argintaxpayer_test'` before execution. Ordinary `ssh` for opening the Ubuntu remote shell remains unaffected. This convention applies to all future phase master plans and operator checklists.

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

## Shared Local Development Environment (Owner Standard, 2026-10-13)

All remaining phases use one persistent local setup and the **same three commands**:
```bash
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
zsh scripts/dev-down.zsh
```
The project-root untracked `.env.dev.local` (copy from `.env.dev.local.example`, `chmod 600`) stores the **test-only PostgreSQL** connection and any future phase-specific local feature flags. The separate untracked `.env.ghasedak.local` stores the Ghasedak API key, `TaxPayerActivationCode`, operator-owned test recipient and disabled-by-default live-sending flag. The stable random OTP pepper is owned by the developer in `~/.argintaxpayer-test/otp-pepper`, not regenerated on every run. The runner loads these *in the same process context as .NET*, starts the approved AutoSSH tunnel, .NET API on loopback port 15089 and Next.js on local port 3000. Logs live in `~/.argintaxpayer-test/{autossh,api,web}.log`. It fails fast on occupied API ports, missing config, missing tools and failing DB readiness. **The runner does not automatically apply EF migrations, send SMS, or start production services**. Its initial macOS end-to-end testing is operator pending.

**Future step contract:** Implementers append necessary safe flags to the private environment-template instructions and add smoke/regression checks to their existing phase document; do not ask the owner to rediscover old startup commands. Run `zsh scripts/dev-up.zsh` at the beginning of each new UI test, then provide only phase-specific test commands. Never put credentials in Git or screenshots and never enable Ghasedak live SMS without explicit test authorization.

## Owner-Approved Phase Test Cadence and Short Commands (2026-10-13)

**Persistent owner workflow (all future phases and chat sessions):**
- **Every implementation step:** run focused .NET/unit/frontend regression tests and use the Mac UI test environment only when relevant. For interactive local testing run `zsh scripts/dev-up.zsh`; check `zsh scripts/dev-status.zsh`; stop with `zsh scripts/dev-down.zsh`. The Mac environment accesses **only** the dedicated `argintaxpayer_test` database on the Ubuntu VPS via the documented persistent AutoSSH `127.0.0.1:15432 -> VPS:5432` tunnel. These are the standard commands to supply to the owner; do not repeat multi-terminal environment variables or SSH instructions at each step unless troubleshooting.
- **Final verification once per completed phase, BEFORE phase signoff/document completion:** perform a clean Ubuntu `bash scripts/ubuntu-dev.sh verify` against the same dedicated VPS **test** database. Then `bash scripts/ubuntu-dev.sh up`, manually verify the phase's UI/API/security matrix through private SSH port-forwarding from Mac, and `bash scripts/ubuntu-dev.sh down` afterward. All Ubuntu Development ports bind loopback; never open these endpoints publicly. `verify` forces real SMS sending off. Separate explicitly authorized single-recipient Ghasedak tests must be planned independently.
- **Exceptions requiring Ubuntu verification sooner than phase end:** changes to Ubuntu system configuration, networking/AutoSSH/PostgreSQL connectivity, EF migrations or database schemas that might behave differently, deployment/runtime configuration, cross-platform dependencies, or a failure that cannot be reproduced on macOS. Security-critical functionality must still receive focused automated tests in **each step**, not deferred until phase completion.
- **Signoff stages:** Step implementation + focused tests → Mac functional test as needed → accumulated phase regression on macOS → one Ubuntu phase acceptance run → record evidence in the phase's single canonical `docs/phases/phase-XX.md` file → owner approval → phase close. No main merge, immutable tag, GitHub Release or production deployment without the separately established gates.
- **Ubuntu first-time-only setup:** private file `~/.argintaxpayer-test/ubuntu.env`, mode 600, containing `ConnectionStrings__ArginTaxPayerDb` for `Host=127.0.0.1;Port=5432;Database=argintaxpayer_test` with private credentials. The Ubuntu launcher loads this itself; OTP pepper stays at `~/.argintaxpayer-test/otp-pepper`. `dotnet`, `node/npm`, `psql`, and other prerequisites must be installed. Migrations are inspected and applied **only explicitly** to the verified test database, never automatically by the launcher. Default Ubuntu verification and Development launcher force Ghasedak live sending **off**.
- This cadence reduces duplicate manual Ubuntu tests, but does **not** mean that using the same remote PostgreSQL makes macOS and Ubuntu runtimes equivalent; differences in OS/permissions, Node/.NET runtime and process configuration are precisely why each phase still needs its final Ubuntu acceptance.
