# ArginTaxPayer — 18-Phase Product Roadmap

Status: Architecture baseline v1.0, Phase 01 design handoff ready, 2026-10-10. Roadmap phases are sequential milestones, though delivery tasks may overlap. Every phase requires acceptance tests, changelog, documentation and an explicit signoff.

| Phase | Name | Scope / acceptance outcome |
|---|---|---|
| 01 | Discovery and architecture | Domain model, authorization and ledger ADRs, source inventory, API contracts, ERD draft and risk register |
| 02 | Foundation and developer platform | ASP.NET Core Web API, Next.js, PostgreSQL/EF Core, CI, migrations, configuration and observability baseline |
| 03 | Identity and authorization | Registration, login, MFA/recovery, roles, scoped resource policies, token/session handling |
| 04 | Multi-taxpayer management | Person/company, fiscal memory, certificate/key lifecycle, Level 1 assignment of customer accounts to Level 2 commercial partners, with each customer owning one or more taxpayers |
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

## Annual taxpayer binding, authority diagnostics and legacy key onboarding (owner-approved 2026-10-13)

The [Phase 04 master plan](../phases/phase-04.md) records the detailed approved acceptance contract. This supplements, without renumbering, the 18-phase roadmap.

- **Phase 04:** model one fiscal memory active per taxpayer, annual subscription binding/exception-review contracts, Level 1-approved audited reset requests, scoped history and a visible connection-test capability state. Step 05 contract clarification does **not** claim live tax authority tests, annual subscription enforcement or private-key ingestion.
- **Phase 09:** securely import/derive/validate an existing `private.txt` key pair and optional registered public key/certificate, as an alternative to new key generation; strict restricted vault/HSM custody and no raw-key leaks.
- **Phase 10:** implement authentic read-only connectivity/authentication diagnosis against supported, versioned tax authority endpoints; separate invalid token, signature mismatch, authority outage, timeout, network failure and inconclusive/unsupported operations. No fabricated successful test.
- **Phase 11:** robust timeout/retry/reconciliation behavior without unintended invoices or charges.
- **Phase 12:** annual subscription allows unlimited invoices for **one** taxpayer and locks taxpayer/memory on successful authority connection. No customer self-switch. Level 2 can request a reviewed exception, Level 1 approves; paid validity, historic invoice linkage and ledgers remain immutable and auditable.
- **Phase 14:** associate legacy taxpayer/memory identities using controlled import; no automatic plaintext legacy private-key dumps.
- **Phases 16–17:** scoped histories, administrative exception tooling, race/isolation/secret handling/outage tests. Never reset annual binding because the authority is temporarily unavailable.

## Customer-level partner ownership and cash-based settlement — owner-approved correction (2026-10-13)

**This supersedes every earlier reference to assigning a taxpayer/fiscal memory directly to a Level 2 administrator.** Level 2 is a commercial partner who owns a *customer relationship*, not a fiscal taxpayer or cryptographic key. The customer account owns its taxpayer profiles (potentially many) and separately purchases plans/subscriptions. Monthly and annual single-taxpayer entitlements apply to an individual active plan/binding, never to the number of customers acquired by a partner. Legacy taxpayer-to-Level2 grants are deprecated for access and must never determine accounting, profit or payout ownership.

**Source and ownership:**
- Level 1 creates customer → `customer.rootLevel1UserId = creatingLevel1`, `partnerLevel2UserId = NULL`; 100% of directly acquired customer business belongs to Level 1 and must not create partner-profit participation.
- Level 2 creates customer → assign customer to **that specific Level 2** and the controlling Level 1. Multiple taxpayers later created by that customer inherit the same partner relationship; there is no manual taxpayer-by-taxpayer grant.
- Public customer registration → default Level 1 owns the customer. When multiple Level1 accounts exist, require an explicit configured default root, never guess financial ownership. An existing public/customer owned by Level1 may be reassigned to Level2 only through a **manual, auditable Level1 approval** after an introduction/referral claim. Level2 submission of a claim grants no access, discount, settlement or ownership until approved.
- Ownership history is append-only, records prior/new partner, actor, reason, effective UTC timestamp and revision; reassignment affects future commercial attribution prospectively and must **not rewrite original sales, collections, deductions, ownership snapshots, settled profit or historic submissions**.
- Each taxpayer profile must have a `CustomerUserId` relationship. Historic unlinked taxpayer accounts and pre-existing customer owners require a supervised backfill/reconciliation; do not infer matches from tax ID, mobile number or expired taxpayer-level grants.

**Per-phase delivery and acceptance:**
- **Phase 03 correction applied in Phase 04:** existing administrator-provisioned and public email/SMS onboarding now writes customer commercial ownership in the same DB save operation. Level 2-created customers are attributed to creating Level 2; root-created and public customers to root. No silent assignment when default root is ambiguous.
- **Phase 04 Steps 04, 08, 09, 10, 11:** record customer→taxpayer FK, customer owner and owner-change history, Level1-only transfer command, Level2 query scoping via `CustomerCommercialOwners`, customer ownership account console; remove mistaken taxpayer-to-Level2 assignment screens/API. Verify migrations, safe legacy reconciliation, retroactive authorization closure and customer creation under both roles.
- **Phase 05:** complete customer-facing account-to-many-taxpayer CRUD, customer self-management constrained by live plan entitlements; show customer list and taxpayer membership in unified UI, customer-level validation, dedupe and partner-restricted support workflows.
- **Phase 12 (mandatory):** independent Root-direct and per-Level2 partner financial ledgers/receivables/expenses. For each customer's purchase, renewal and invoice-unit pack, snapshot deal owner, partner ID, contracted share percentage, price/tax rules, cost policy and effective time. Settlement base is **real verified collected cash allocated to that transaction**, *not order value or uncollected receivables*. Eligible tax and expense deductions need manual Level1 approval, with original evidence, approval timestamp and allocation keys. Compute net realized participation profit only after approved allocated costs; split by agreed rate (normally 50% each, configurable). A direct Root-owned sale has no Level2 share. Store outstanding receivables independently. Every customer, company/taxpayer, purchase and collection allocation is traceable separately. Handle partial payments, refunds, chargebacks, late costs and reallocation using compensating immutable ledger postings; never silently mutate historical settlement.
- **Phase 16:** Level1 management for claims/approvals/transfers, contracted profit-share and manual cost approval; Level2 reads only its customers, invoices, verified collections, approved expenses, pending payout and history.
- **Phase 17:** negative-path tests for unapproved referrals, former-owner data access after transfer, competing transfers, duplicate acquisition, multi-taxpayer accounts, partial payment share, tax deduction approval, partner-vs-root isolation and audit immutability.

**Accounting invariant:** `attributed_collection = verified_cash_receipt_allocated_to_deal`; `approved_eligible_costs = approved_tax_and_expense_allocated_to_that_collection`; `net_realized_profit_base = attributed_collection - approved_eligible_costs` (subject to the deal's accepted tax-exclusive accounting treatment, adjustments and sign conventions); `partner_share = net_realized_profit_base * contracted_partner_percentage`. No settlement based on invoiced value, projected cash, automatically proposed expenses, or later customer-owner changes.

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


## Canonical manual PostgreSQL CLI verification on macOS (owner-approved 2026-10-14)

For **all subsequent ArginTaxPayer steps and phases**, do not pass the Npgsql semicolon-delimited `ConnectionStrings__ArginTaxPayerDb` directly to `psql`; it is not a libpq connection string. Use the verified Codex/operator approach: load private `.env.dev.local`, split exact Npgsql keys, connect with `psql -W`. Do not echo the password or commit the environment file. When testing schema or migration, verify the actual database using `SELECT current_database();` before changes and require exactly `argintaxpayer_test`.

```bash
set -a
source .env.dev.local
set +a

npgsql_value() {
  printf '%s\n' "$ConnectionStrings__ArginTaxPayerDb" |
    tr ';' '\n' |
    awk -F= -v key="$1" '$1 == key { sub(/^[^=]*=/, ""); print; exit }'
}

DB_HOST="$(npgsql_value Host)"
DB_PORT="$(npgsql_value Port)"
DB_NAME="$(npgsql_value Database)"
DB_USER="$(npgsql_value Username)"

[[ "$DB_HOST" == "127.0.0.1" && "$DB_PORT" == "15432" && "$DB_NAME" == "argintaxpayer_test" ]] || {
  echo "Refusing non-test DB settings" >&2
  return 1
}

psql -X -h "$DB_HOST" -p "$DB_PORT" \
  -U "$DB_USER" -d "$DB_NAME" -W \
  -c 'SELECT current_database();'
```

For repeated `psql` commands in the same terminal, reuse the extracted `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER` and `-W`. Confirm actual returned database equals `argintaxpayer_test` before applying migrations. The existing `scripts/dev-up.zsh` AutoSSH launcher remains the canonical way to prepare Mac connection. Keep this convention in every new phase master plan; do not recommend the former invalid `psql "$ConnectionStrings__ArginTaxPayerDb"` command.


## Authoritative entity separation for all phases — 2026-10-15
Three independently persisted bounded contexts: (1) `IdentityUser` = software login, session, roles and partner-owned customer account; (2) `Party` + `CustomerPartyLink` = fiscal invoice buyer/counterparty with person/company fields, no login; (3) `TaxpayerProfile` + fiscal memories/certificates = fiscal seller, separate from buyer and login. A customer can own many buyers and many sellers. Merely registering or creating a software account MUST NEVER create a buyer or seller list row, and no party/taxpayer registration creates login credentials. Level1 sees/manages its authorized root portfolio, Level2 its assigned customers, Customer only its own records, with server query-level isolation and checked mutations.
- Phase03: login identity only. Administrative provisioning, activation/locking/deletion must be authorized and independently audited.
- Phase04: seller ownership and fiscal-memory lifecycle only; does not define buyer creation as part of login registration.
- Phase05: separate role-scoped buyer/seller CRUD and independent fiscal record server-side pagination/filtering. `/api/dev/fiscal-participants` is an internal development-only read model combining distinct Party and TaxpayerProfile projections; underlying tables remain independent, with no automatic conversions.
- Phase06: goods/services catalog belongs to account tenant; seller selection is explicit.
- Phase07–08: invoice snapshot references one seller TaxpayerProfile and optional/required buyer Party per official invoice type/pattern; never reference IdentityUser as an invoice party. Validation is rule-versioned and uses archived snapshots.
- Phase09–11: keys, fiscal memories, tax authority submission, receipts and reconciliation reference fiscal seller, never a buyer or authentication session.
- Phase12: plans/billing belong to customer accounts; entitlements constrain use of taxpayer sellers and invoice quotas but never turn buyer into login identity.
- Phase13–16: imports, accounting synchronization, administration, reporting and migrations preserve typed IDs and foreign-key relationship provenance; no guessed buyer from phone/email, no global account pagination as substitute for fiscal person pagination.
- Phase17–18: enforce integration regressions proving account-only registration yields zero new buyer/seller rows and no cross-customer record leakage.


## Immutability of fiscal invoice counterparties — owner clarification (2026-10-15)
Phases07/08 MUST add typed `buyer_party_id` and `seller_taxpayer_id` FK fields to every issued invoice header, with restrictive deletion, and retain immutable name/identifier/location/tax snapshots at issuance time. Edits to Party or TaxpayerProfile master data affect only future drafts and new invoices; historical invoices and transmitted payloads preserve exactly the values used at issuance. Linked buyers and sellers must not be physically deleted; use soft archive and keep their FK targets. Phase05 defines soft archive and stable identity IDs but cannot enforce invoice-reference queries until invoice tables are implemented. Every later import, correction, credit note, inquiry and accounting integration must preserve historical invoice participant snapshots and original FK traceability.
