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

### Step 03 follow-up — Exception approval backend (2026-10-31)

Added `SubscriptionExceptionReviewService` as a server-side-only workflow: authenticates the presented access token from existing IdentityAuthorizationService; verifies current Level2 role and current customer commercial ownership before accepting a request; verifies proposed taxpayer and fiscal memory ownership; permits only a Level1 belonging to the customer's current controlling root to approve/reject; decision history remains and the pre-existing annual binding is never mutated. Duplicate pending review has a pre-existing partial unique database index. No public endpoint yet, so no user workflow is asserted.

**Still blocked for full Step 03:** trustworthy authority connection evidence source and server-side adapter; existing `RegisterVerifiedAnnualBindingAsync` still accepts a string reference from its caller and is NOT authorized to handle untrusted inputs. API integration of request/review endpoints and qualified fiscal evidence, role-scoped integration tests, local .NET build, and live Sandbox validation have NOT_RUN. User has reported macOS Build, EF snapshot and Step03 migration applied successfully, but this new service was committed afterward and remains untested. Do not treat it as accepted or proceed to Step04 before closing this gate. No new migration introduced by this follow-up.

## Owner-approved Phase 12 Step 03 scope correction — 2026-10-31

The owner explicitly rejects tax-authority connectivity/evidence as a subscription prerequisite. **This amendment supersedes all preceding Step 03 wording** that calls for tax-authority connection evidence, Sandbox inquiry or a new Level1 exception workflow.

- Monthly and annual plans are **single taxpayer/company**, with no quantity-based invoice limit. Quantity plans permit multiple authorized customer-owned taxpayers, with a finite invoice unit limit.
- Monthly plan kind 3 introduced, validity 28–31 days in catalog terms. Calendar-month anniversary/timezone semantics need separate implementation when actual activation is integrated with paid billing.
- Internal binding registers the chosen taxpayer and existing fiscal-memory record; no new authority call/test/connection proof. A change to fiscal memory code is governed **only by the pre-existing Level1 approval process**, not a new Phase 12 approval endpoint.
- Earlier Step03 exception/request/review components are not exposed from Program.cs. They must be considered obsolete scaffolding and not deployed as a competing approval workflow.
- Existing applied Step03 migration is never rewritten. New additive migration `20261031210000_AlignSingleTaxpayerCommercialPlans` extends plan types and database binding guard. Inspect generated SQL and EF snapshot before applying to `argintaxpayer_test`; no implicit Production migration.
- Implementation keeps Phase10 invoice send and Phase11 status logic untouched.

**Important evidence limits:** GitHub source edits and commit only; no macOS build, EF migration, integration tests, or Ubuntu runs performed by assistant. Owner-reported previous macOS tests applied to prior SHA, not new commits. Do not mark Step03 fully verified before same-SHA local acceptance, EF snapshot check, test DB migration, unique taxpayer/active membership checks and existing Level1 memory-approval regression. Specifically, selecting an existing taxpayer via repository is not yet a fully exposed subscription onboarding flow.


## Owner correction — five commercial plan categories (2026-10-31)

This **supersedes** all previous Phase12 assumptions that there are only annual/monthly/quantity plans.

1. **Monthly (1 calendar month), quarterly (3 calendar months), semiannual (6 calendar months), annual (12 calendar months):** each is a single-company/single-taxpayer entitlement, unlimited invoice count during the valid paid period. Multiple taxpayer registrations against the same active entitlement must be rejected server-side.
2. **Quantity:** configurable positive invoice-unit package size (examples 50, 100, etc.); permitted for multiple customer-owned taxpayers, subject to the purchased balance and configured validity no longer than one year.
3. Level1 or Level2 can define a quantity-based **commercial package and selling price** within their ownership and permissions. A Level2-created package must be scoped to that Level2; it must not mutate a global Level1 catalog or another partner's prices. Package creation does not waive financial ownership, approvals, or audit requirements.
4. Time entitlement expiry must be computed with calendar-month arithmetic from the authorized activation instant and an explicit agreed timezone/time-of-day convention; `30 * months` is not equivalent. Preserve exact periods and purchase terms in immutable snapshots.
5. Fiscal-memory code changes remain governed solely by the pre-existing Level1 approval process; this phase does not make new tax authority calls or introduce another approval process.
6. **Implementation gate:** current `SubscriptionPlanKind` exposes only Annual=1, Quantity=2, Monthly=3, and current schema relies on `validity_days`. Quarterly and semiannual contracts, period-month persistence, admin package creation/scoping and comprehensive regression tests are **not yet implemented**. Do not mark Step03 complete or issue an unreviewed migration; design a forward-only migration preserving existing records and the prior applied migration history. No database write was performed for this amendment.

## Owner correction: Five commercial plan types (2026-10-31)

**Supersedes all previous interpretations of Phase 12 duration-plan types.** Monthly, three-month, six-month and annual are each single-company/single-taxpayer unlimited-invoice plans. Quantity packages (50, 100, or arbitrary positive limits) can cover multiple taxpayer profiles owned by the same customer, and expire no later than 12 months after activation. Level1 and Level2 may define packages/pricing within their authorized commercial scope. Fiscal memory redefinition remains governed by the previously implemented Level1 approval process. No new tax-authority connection dependency in Phase12.

Initial source edits add enum kinds 4/5, expand term validation, single-taxpayer detection and an additive SQL migration `20261031220000_AddQuarterlyAndSemiannualSubscriptionPlans`; focused tests added. **Do not consider this full acceptance yet**: contract currently stores duration as days, not calendar-month intervals; exact calendar-month anniversary semantics, role-authorized plan creation and active subscription integration still need implementation. Existing EF snapshot, same-SHA Mac build, migration SQL and PostgreSQL execution are unverified. Never mark new migration as applied until verified and reviewed on `argintaxpayer_test`. Ubuntu only Step11.

The authoritative model should use calendar months (1/3/6/12) for the four duration plans, with explicit UTC/time-zone and end-of-month expiry policy, rather than relying on fixed day counts. Quantity remains bounded by <=12 calendar months. Preserve past price and membership snapshots.

## Step 03 delivery delta — calendar, administration and migration repair (2026-10-31)

- `SubscriptionCalendar.ExpirationUtc` computes 1/3/6/12 calendar-month expiries in UTC with end-of-month rollover policy; quantity packages accept explicitly bounded 1–12 calendar months. Domain tests cover Jan31/Feb28, leap day, six-month crossing, quarter and invalid parameters.
- `SubscriptionPlanAdministrationService` restricts internal creation to authenticated, active Level1/Level2 operators with database-confirmed enabled MFA; plan code is prefixed by the server-derived administrator ID to avoid namespace impersonation. An endpoint under `/api/dev/subscriptions/plans` is loopback-only, rejects browser Origins and is rate-limited. Newly created plans are **inactive**, so this endpoint does not sell or activate them; payment and activation remain future steps.
- The two additive migration sources (20261031210000 and 20261031220000) now use `DROP CONSTRAINT IF EXISTS` when replacing optional old check constraints: the EF-generated Phase12 catalog migration did not create those named constraints.
- The owner requires **five** plan types: 1/3/6/12 month single-taxpayer unlimited and quantity multiple-taxpayer with count limit and expiration <=1 year. Memory-code changes remain under the pre-existing Level1 process; no authority connectivity or evidence dependency.
- **Sign-off blockers:** No local dotnet runtime or PostgreSQL is available in this assistant execution context, therefore current GitHub SHA has NOT been compiled/tested or migrated here. Owner must verify Mac build, migration SQL and `argintaxpayer_test` updates, all backend tests and Level1/Level2/Customer negative auth tests. Do NOT apply pending migrations to any database other than the verified test database; check `has-pending-model-changes` first. Further live subscription assignment and active-use enforcement require authenticated plan-purchase workflow and cannot truthfully be considered delivered by this catalog API alone. Ubuntu remains Step11.

## Step 03 — owner acceptance (2026-10-31)

Owner explicitly approved Step 03 and authorized moving to Step 04. Approved scope: five commercial plans; monthly/quarterly/semiannual/annual are each single taxpayer with unlimited invoices, quantity plans have configurable positive invoice count and <=12-month validity; separate Level1/Level2 commercial pricing. No new tax-authority connectivity. Earlier migration / UI / end-to-end limitations remain documented as outstanding technical risks rather than silently represented as tested.

## Step 04 — immutable credit ledger (implementation in progress)

Credit ledger foundations committed:
- `20261101010000_AddPhase12CreditLedger` creates one balance per subscription and append-only journal; checks balance conservation, non-negative quantities, unique subscription/operation idempotency key; a database trigger rejects entry UPDATE/DELETE.
- `SubscriptionCreditLedgerService` exposes server-internal initialization (quantity only, active scoped subscription), reserve, consume, release; serializable transaction with account-row FOR UPDATE lock and journal append within same transaction.
- `SubscriptionCreditPolicy` unit-tests quantity conservation and invalid transitions; registration in API DI, but **no public credit increment endpoint**.
- The existing invoice-send workflow is intentionally not modified in Step04. A successful monetary payment, activation, or purchase authority adapter is NOT implemented in Step04 and must not be implied by a public grant.
- No .NET SDK or Postgres available in assistant runtime; current SHA source not compiled, integration tested, or migrated here. Current code requires Mac .NET Build and scoped integration QA; database migration must first be SQL-reviewed on verified `argintaxpayer_test`. Ubuntu is reserved for Step11.
- Step04 signoff requires race/idempotent duplicate operation testing, duplicate key with changed payload rejection, insufficient balance rollback, expiry, cross-customer denial, crash-retry and snapshot invariant checks, and operator acceptance. If any check fails, Step04 remains open.

**CAUTION:** Legacy Step03 plan SQL uses later additive migrations; never rewrite already applied migration history, and do not run production database update.

## Step 04 — owner sign-off

Owner explicitly confirmed Step 04 and authorized starting Step 05. The confirmation is recorded; evidence limitations previously listed remain preserved.

## Step 05 — commercial orders, internal financial invoices and receivables

Committed new `SubscriptionBilling` domain records and tests, `20261101020000_AddPhase12CommercialOrders` for customer-scoped commercial order and append-only JSON financial invoice snapshots, and internal `CommercialOrderService` for issuing order+invoice in one serializable transaction with idempotency key and purchase-time price snapshot. Amounts use IRR. **Not** a tax-authority invoice; no calls to tax.gov.ir; no payments booked, captured, or inferred. The receivable balance supports partial collections in domain policy, while actual collection posting is Step06. Newly created orders are never automatic proof of paid subscription or credit grant.

**Outstanding acceptance / implementation risks:** No local .NET or PostgreSQL runtime available; Step05 code is not compiled, migrated or integration tested. Review post-transaction retry and idempotency behavior under price-version changes, same-key conflicting payloads, FK/customer ownership and race conditions before exposing endpoint. A fully secured public order API/UI, receivable read model and immutable order snapshot consistency checks are not yet complete. Do not claim Step05 operational completion or apply migrations before verifying target `argintaxpayer_test` and reviewing SQL. Local Mac acceptance and Ubuntu Step11 sequencing apply.

### Step05 continuation — internal API and receivable query

The order issue route `POST /api/dev/subscriptions/commercial-orders` is loopback-only, bearer-protected, Origin-rejecting and rate-limited, with an internal administrative role/ownership check. Retried operation keys now attempt an existing order lookup before price quotation to preserve historic pricing. `CommercialReceivableReadService` supports scoped, read-only due/collected balances. These are source changes only, not integration-tested; possible live DB mapping and concurrency behavior must be verified during Mac acceptance. No payment posting endpoint exists yet (Step06). The invoice snapshot is an internal financial artifact, not a fiscal invoice.

**Step05 outstanding acceptance:** validate compiled .NET API, migration and real database, idempotency races (including changed amounts and version rollover), scoped Level1/Level2 authentication, legitimate order read after ownership reassignment, historical immutability and allowed partial-payments state in the follow-on Step06. Until proven, this step is NOT fully accepted.
