# ArginTaxPayer — Phase 12: Subscriptions, Billing & Settlements

**Status:** Step 01 documentation baseline committed; owner acceptance pending. Steps 02–12 NOT STARTED.
**Date:** 2026-10-31
**Repository:** HekmatBeigverdi/ArginTaxPayer
**Branch:** `phase/12-subscriptions-billing-settlements`
**Baseline:** `main` SHA `4c0c16d3499e4310ff834c2729d59af249c8fcda`, Phase 11 release `phase-11-v1.0.0`.
**Target release:** `phase-12-v1.0.0`.
**Authority:** `docs/roadmap/18-phases.md` and applicable owner-approved Phase 03/04/10/11 plans. This is the single evolving master plan; do not create ordinary per-step report files.

## Scope and non-regression contract

Deliver annual/quantity subscriptions, customer-level entitlements, immutable credit/accounting ledgers, sales/orders/receivables, verified cash/gateway allocations, direct-root and independently scoped partner financial books, partner settlement, SMS wallets and scheduled SMS tariffs. No production charge, gateway activation, paid SMS send or production migration without separate owner approval. Existing Phase 10 original/correction/cancellation/return sending and Phase 11 inquiry behavior must not change.

Phase 11 release has owner-approved deferrals: automatic inquiry, restart-safe recovery, multi-worker coordination. Keep them prominently deferred; this phase cannot claim to implement them or silently schedule invoice sending. Ensure future reconciliation can adjust reservations safely and idempotently.

## Step 01 source inspection and evidence boundary

**Confirmed remotely in GitHub:** Phase 11 release `phase-11-v1.0.0` targets baseline SHA above; Phase 12 branch exists and equals main (no commits ahead/behind at inspection); `docs/roadmap/18-phases.md` defines annual/quantity plans, customer-level partner ownership, cash-collection-based sharing, SMS wallet/promo and scheduled tariffs; `docs/phases/phase-04.md` defines customer ownership and historical reassignment; `docs/phases/phase-11.md` records safe invoice-dispatch boundaries. `apps/web/package.json` exposes `test`, `typecheck`, `build`.

**Not verified:** local macOS Git/worktree, .NET compile/test, Next.js test/build, current PostgreSQL schema/migration catalog, live API/UI, existing entities/services beyond inspected plans, GitHub CI, secrets or provider connections. GitHub code-search attempts for Subscription, CommercialOwner, InvoiceCredit, SmsWallet and Settlement returned no indexed matches; absence of search hits is not evidence of absent implementation. Step 02 must perform targeted repository source inventory and schema audit before adding entities or migrations.

**Step 01 implementation:** documentation only; no application code, migration, database, external service calls, invoice submissions or charge activity. No Ubuntu test in this step.

## Domain and financial architecture freeze

### Ownership and boundaries
- Customer account is the commercial owner relation; each customer can have multiple taxpayer profiles. Level 2 owns the customer relationship, **never** taxpayer cryptographic custody. Level 1 direct customer has no partner share. Reassignment is authorized by Level 1, append-only and prospective only.
- Distinct bounded contexts: Plan Catalog & Price Versions; Subscription & Taxpayer/Memory Binding; Invoice-Unit Entitlement & Reservation; Orders & Receivables; Payments & Verified Collections; Financial Ledger; Partner Agreement & Settlement; SMS Wallet, Tariffs & Provider Expense.
- Tax document, authority status, customer billing order, payment receipt, credit reservation and partner settlement are distinct identifiers and state machines. No status-only inference of monetary collection or fiscal finality.
- Level 1/2 prices may differ; commercial deal captures owner/partner ID, agreed profit percentage, customer, payer, product, currency, tax/cost policy and effective-at snapshots at purchase time. No retroactive re-pricing.
- Accounting uses append-only balanced postings, immutable source snapshots, idempotency keys, external-reference uniqueness, scoped ownership and audit actor/correlation. Fix historical errors with linked compensating entries; do not edit/delete settled postings. Require explicit IRR rounding/currency and amount-sign conventions before implementation.

### Subscription and credit contract
- Annual: one locked taxpayer and fiscal-memory binding after verified successful authority connection; unlimited eligible invoice sends during paid validity; no customer self-switch. Level 2 exception request and Level 1 approval cannot rewrite paid period or invoice linkage.
- Quantity: one or more taxpayers under authorized account, finite invoice-unit balance and validity <= 1 year. Determine exactly which fiscal events consume one unit in Step 03/04 before enabling billing; leave unknown/indeterminate sends reserved rather than deduct twice or release early.
- Manual opening balance: audited owner-authorized adjustment for legacy remaining days/units, no automated import of legacy plans or passwords.
- Enforce entitlements in server-side command boundary, not UI alone; atomic reservation and unique send-intent keys with concurrency tests.

### Cash, partner participation and settlements
- Deal-level receivables remain distinct from collections. Verified provider or approved manual cash receipt is assigned to orders using immutable allocations; partial collections/refunds/chargebacks use compensating postings.
- Partner settlement base is **verified cash allocated to the deal** minus approved eligible allocated tax/expense under the contracted accounting policy. Contracted percentage is snapshotted (often 50%, never assumed globally). Unapproved expenses do not reduce participation. Root-direct sales have zero Level 2 participation.
- Financial visibility: Level 1 full authorized financial administration; Level 2 only its own commercial customers/orders/receipts/approved costs/share/history; customer only own plans, invoices, payments, credit and wallet. Enforce application and SQL query scope, including former-owner access after reassignment.
- Settlement proposal, approval, payment execution, reversal and finalization are separate auditable states. Prevent double payouts on retries.

### SMS wallet
- One-time promotional credit **2,000,000 IRR** per eligible customer under an idempotent grant rule; do not repeat on restart/re-registration.
- Customer-facing tariffs and Ghasedak provider expense are separate amounts/ledgers. Effective price is selected for provider panel + category at atomic reservation timestamp, using Asia/Tehran scheduling converted to UTC. Exactly one applicable tariff at a time; missing tariff blocks paid send. Snapshot the selected rate across retries. Future price administration by Level 1 in Phase 16; no unapproved live SMS activation.

## Fixed twelve-step delivery and acceptance matrix

| Step | Deliverable / likely migration impact | Acceptance gate |
|---|---|---|
| 01 | Read-only audit, design and this master plan; **none** | Plan, boundaries, risks and test cadence documented; owner signoff |
| 02 | Versioned plans, prices, subscriptions, purchase rules; **likely yes** | Annual and quantity lifecycle, role-specific price and historical snapshot tests |
| 03 | Taxpayer/memory binding and subscription authorization; **likely yes** | Annual single binding, exception approval, expiry, cross-tenant denial and immutable past linkage |
| 04 | Invoice-unit reservation/consumption/release and credit ledger; **likely yes** | Atomic double-send/idempotency tests; Indeterminate never double-debited |
| 05 | Orders, billing snapshots and receivables; **likely yes** | Purchase/renewal/add-on and partial receivable reconciliation without retrospective edits |
| 06 | Manual verified cash, gateway abstraction, callbacks/refunds; **likely yes** | Verified allocation, replay rejection, webhook authenticity, no real gateway without approval |
| 07 | Root/Level 2 separate ledgers, partner agreement, approved costs; **likely yes** | Direct sale zero partner share, partial receipts, historical ownership isolation and net-profit formula |
| 08 | Approval/execution of settlements, compensating corrections; **likely yes** | No duplicate payouts, separation of duties, reversal/refund and immutable audit |
| 09 | SMS wallets, idempotent promo grant, versioned effective tariffs; **likely yes** | Exactly one rate, price-boundary, retry snapshots, provider-cost separation, no live paid SMS |
| 10 | Compact Persian RTL finance UX + macOS regression; **avoid unless defect** | All roles and scopes, ledger reconciliation, local UI acceptance, prior invoice flows preserved |
| 11 | **Ubuntu-only** same-SHA independent acceptance; **none** | macOS SHA = Ubuntu SHA, build/tests, DB name/migration review, smoke, role isolation and finance audit |
| 12 | Owner approval, documentation, merge/release; **none** | Clean tree, explicit signoff, merge to main, tag/release separately authorized |

Do not renumber without explicit owner-approved plan amendment. In each step report touched files, backend/frontend impacts, migrations, tests PASS/FAIL/NOT_RUN, risks, rollback, actual commit SHA, and owner approval.

## Acceptance scenarios to implement before release

1. Root direct sale, partner sale at contract-specific percentage, partner reassignment after sale, customer with multiple taxpayers.
2. Annual single active binding vs quantity multi-taxpayer; expired, cancelled, pending, renewal/add-on and supervised manual-opening states.
3. Two browser tabs reserve same last unit, network timeout/indeterminate authority result, repeated send/replay, cancellation/correction/return treatment per explicit fiscal policy.
4. Order value without cash gives no partner payable; partial verified cash creates proportional eligible participation; approved expenses only; refunds, chargebacks, late expense and settlement reversal.
5. Cross-customer and cross-partner URL/API authorization, former-owner access, concurrent approval, user/role audit, secrets and bank webhook validation.
6. SMS promo grant once; scheduled tariff boundary, timezone conversion, missing rate rejection, immutable rate on retries, separate provider expense.
7. Migration rollback/review, backfill without fabricated balances, ledger double-entry totals and per-customer reconciliation; no access to Production DB.

## Operational cadence and safety

**macOS is sole development/migration test host.** After each step: `git pull --ff-only origin phase/12-subscriptions-billing-settlements`; run applicable focused and regression tests; for UI use ONLY `zsh scripts/dev-up.zsh`, `zsh scripts/dev-status.zsh`, `zsh scripts/dev-down.zsh`. Do not instruct manually starting autossh; the dev-up script manages transport. It must never auto-apply EF migrations. Review SQL and assert actual `current_database()='argintaxpayer_test'` before approved migration. Separate local environment secrets from Git.

**Ubuntu tests happen exclusively in Step 11 (penultimate).** Verify identical clean Git SHA; run independent .NET/web build/test and PostgreSQL audit then. Step 12 is release/closeout only; no hidden Ubuntu re-test.

**Step 01 evidence:** remote documentation/source inspection PASS; documentation-only safety PASS; macOS build/test NOT_RUN; frontend typecheck/test/build NOT_RUN; database/migration NOT_RUN; Ubuntu NOT_RUN; live fiscal authority/gateway/SMS NOT_RUN. This distinction is required for honest acceptance.

**Rollback:** revert the Step 01 documentation commit on the phase branch; no database rollback needed. All runtime and financial functionality remains for later owner-approved steps.

**Owner signoff:** PENDING.


## Step 02 — Plan and pricing foundation (2026-10-31)

**State: SOURCE IMPLEMENTATION EXPANDED; LOCAL BUILD/DB VERIFICATION PENDING; NOT ACCEPTED.** Following owner approval of Step 01, the phase branch adds:
- `src/ArginTaxPayer.Domain/Subscriptions/SubscriptionContracts.cs`: annual unlimited vs quantity bounded unit terms, UTC price windows, partner vs Root direct audience, immutable purchase snapshot contracts.
- `tests/ArginTaxPayer.Tests/Phase12SubscriptionContractTests.cs`: focused invariant cases (not executed).
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261031180000_AddPhase12SubscriptionCatalog.cs`: SQL schema for plans, immutable price versions and customer subscriptions, default inactive/pending, scoped owner identifiers and check constraints. **Migration NOT APPLIED**.

**Open before Step 02 signoff:** inspect SQL with a genuine test DB; confirm migration compatibility and any existing subscription schema; add persistence mapping, scoped command/query services, non-overlapping active price window validation, renewal/upgrade/add-on workflow and concurrency tests; run local .NET and Web regression. Do not report these as complete. There is no paid activation, finance ledger, API, UI or production change. Current schema migration is reviewed for syntax only, not verified by executing PostgreSQL. Backout is revert commits on phase branch; if applied to test DB later, use reviewed Down migration only after backup/ownership verification.

**Evidence:** remote source inspection and GitHub commits PASS; local .NET build/test NOT_RUN; Mac DB/migration NOT_RUN; Web build/test NOT_RUN; Ubuntu NOT_RUN; owner approval PENDING.

### Step 02 continuation — persistent EF catalog, pricing guard, instructions

GitHub source commits add `Subscriptions/SubscriptionCatalogPersistence.cs` (EF model mapping, plan creation, non-overlapping price creation and customer-scoped root/partner quote), register its entities in `ArginTaxPayerDbContext`, strengthen DB migration with a transaction-serialized price-overlap trigger and immutable price-version UPDATE/DELETE rejection, and add negative unit-test cases. No routes expose these repository actions; the caller authorization layer and payment activation are intentionally not enabled. Database guard is authoritative against concurrent inserts.

**BLOCKER before acceptance / migration:** model snapshot `ArginTaxPayerDbContextModelSnapshot.cs` has not been regenerated; EF model/schema parity, SQL migrations on the approved test database, real .NET build/tests and lifecycle/renewal/add-on integration tests remain unverified. Schema is not yet production-ready. Do not execute migration blindly; local operator must review its SQL, obtain parity evidence and verify `argintaxpayer_test` before any database mutation. Explicitly distinguish schema foundations from full purchase/renewal/upgrade functionality. Any owner acceptance of missing items requires an express scope change, not silent closure.

**Operational test cadence:** start Mac environment only with `zsh scripts/dev-up.zsh`; source `.env.dev.local` into the migration shell and validate actual DB via psql before any `dotnet ef database update`. Ubuntu exclusively Step 11. All local build/migration tests still NOT_RUN here.

## Step 03 — Binding implementation update (2026-10-31)

**Owner request:** finish persistent annual taxpayer/memory binding, reviewed exceptions, test checklist. Current implementation commits introduce domain policy, unit tests, persistent annual binding and exception entity mappings, new migration `20261031193000_AddPhase12AnnualSubscriptionBinding`, database trigger blocking UPDATE/DELETE and confirming active annual subscription + tenant/memory join, scoped repository for future trusted evidence integration.

**Important non-claims / blockers:** no verified tax-authority evidence adapter is wired to RegisterVerifiedAnnualBindingAsync; caller-supplied evidence text is not independently verified. No authorized Level2 request/Level1 approval service or API is wired, and exception approval must never mutate an existing historic binding. No entitlement enforcement has been connected to real invoice sending; doing so before payment/credit policy exists would regress Phase 10. EF model snapshot not updated with Step03 additions; MAC build, migration SQL and integration tests NOT_RUN. No production readiness or full Step03 signoff is claimed.

**Required completion:** trusted correlation of official successful connection evidence; server-issued scoped authentication/authorization for exception review; immutable exception event trail and safe prospectively effective replacement subscription rather than editing historic binding; stable EF migration/snapshot, actual SQL review, test DB verification, macOS regression including Phase10; explicitly approved operator acceptance. Ubuntu Step11 only.

**Step 03 rollback:** revert branch code/migration before DB update. Once applied, carefully review reversible Down migration with empty binding tables; never drop real historical records.
