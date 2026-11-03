# Phase 15 — Role-Based Analytics & Dashboard

> Single canonical phase record. Append future step notes to this file only. Phases 13 and 14 deferred. All planned APIs described in Step02 are contracts until implementation is recorded in Step03.

---

## STEP 01 — Baseline audit


Date: 2026-11-03
Branch: `phase/15-role-based-dashboard`
Baseline: `main` at `f9b7d35273b417a263b90250e0315600c8d753db`
Status: Repository source audit completed; runtime/database validation pending.

## Phase decisions

- Phases 13 and 14 are deferred, not prerequisites for Phase 15.
- Scope: meaningful role-based operational analytics for Customer, commercial/financial/operational analytics for Level1 and Level2.
- Dashboard is NOT an accounting profit-and-loss system. Customer must not see platform financial KPIs. Invoice face values must never be mislabeled as platform revenue.
- Preserve existing Persian RTL shell, green visual identity, Jalali filtering, tenant access controls, and operational flows from Phases 10–12.
- No automated inquiry, restart-recovery, or multi-worker coordination is implemented by this phase. Existing Phase11 inquiry observer is read-only.

## Verified source-level inventory

| Area | Existing source | Finding / follow-up |
| --- | --- | --- |
| Dashboard | `apps/web/app/(console)/dashboard/page.tsx` | Static role-based module cards, welcome and a subscription widget; no aggregate analytics; stale labels (“development”, “next phase”). |
| Dashboard test | `apps/web/tests/dashboard-redesign.test.mjs` | Current assertions couple to literal module-copy/layout. Update carefully when replacing page; preserve security/navigation assertions. |
| Identity | `apps/web/lib/identity.ts` | `currentIdentity()` resolves user ID and role from authenticated backend; no scope claims should be trusted from the client. |
| Invoice overview proxy | `apps/web/app/api/invoices/[...segments]/route.ts` | `GET /api/invoices/overview` routes to `/api/dev/invoices`. Inspect authoritative scope/lineage/status query before aggregating. |
| Invoice dispatch | `src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs` | Existing dispatch operations and inquiry flows; use authoritative server state, not mutable UI status. |
| Inquiry queue | `src/ArginTaxPayer.Api/Invoices/Phase11InquiryQueueObserver.cs` | Observer only, reads operations in states 5/6 for test database; no automatic inquiry-processing feature. |
| Customer subscription | `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/CustomerSubscriptionOverviewService.cs` | Authenticated customer subscription plan, expiry, days, and quantity-pack remaining units. Display model must not become dispatch entitlement authority. |
| Financial sales | `apps/web/app/subscriptions/plan-sales-report.tsx` | Existing monthly/annual Jalali-aligned report: gross sales, verified collection and outstanding; partner selector must remain Level1-scoped. |
| Revenue ledger | `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/PartnerRevenueLedgerReadService.cs` | Collection-backed partner/root allocations; SQL-scope applied before totals and pagination; Level2 must not see root financial share. |
| Settlement | `src/ArginTaxPayer.Api/Subscriptions/Phase12PartnerSettlementEndpoints.cs` | Balance and settlement recording/reversal endpoints. Distinguish posted, outstanding and reversed entries. |
| Collections | `src/ArginTaxPayer.Api/Subscriptions/Phase12CollectionEndpoints.cs` | Manual confirmation of verified collections. Never count unverified gateway attempts as collected cash. |
| Finance proxy | `apps/web/app/api/phase12/[...parts]/route.ts` | Role-gated explicit proxy action allowlist. Prefer a dedicated read-only dashboard backend and explicitly scoped proxy path. |
| Invoice access | `src/ArginTaxPayer.Application/Tenancy/InvoiceWorkflowAccessPolicy.cs` | Current ownership and active membership required for invoice authorization; dashboard queries require equivalent server-side scoping. |

## Planned dashboard KPI definitions (to validate against live DB schema in Step 02)

### Customer
- `invoice_success_confirmed`: number of distinct logical invoice chains whose latest authoritative relevant outcome is confirmed success; distinguish successful original from later accepted cancellation/return.
- `invoice_pending`: distinct logical invoices with pending/prepared/acknowledged/inquiry-needed status, without treating an indeterminate outcome as failure.
- `invoice_attention`: rejected, actionable validation issue, or indeterminate requiring explicit inquiry (shown separately).
- `invoice_drafts`: current, editable drafts only; never double-count lineage versions.
- `invoice_trend`: per Tehran/Jalali bucket, distinguish issue date from confirmation time.
- `subscription`: current plan, expiry, remaining days, remaining invoice quota where applicable.
- `recent_actions`: server-scoped latest invoice/dispatch activity.
- No monetary profit/revenue widgets and no misleading “automatic jobs” counter.

### Level2
- All operational aggregates only across CURRENT owned customer memberships.
- Active customers/subscriptions, renewals due, own commercial order gross, verified collected amount, outstanding receivables and partner share.
- Do not leak root share, cross-partner balances or customers.

### Level1
- Root-scoped aggregates, all authorized customers/partners, commercial sales, verified collections, outstanding, revenue allocation/settlements, partner comparisons and approval queue.
- Distinguish gross order value, confirmed collections, allocated share and actual settlement; never equate them.

## Data integrity and security acceptance gates

1. Perform filtering on the DB/API side before grouping, sorting, totals and pagination.
2. Prevent cross-tenant inference through totals, timeseries, error cases and exported data.
3. Resolve invoice chain canonical identity and latest accepted status before aggregating; avoid double-counting correction/cancellation/return child rows.
4. Use `AsNoTracking`, projection and server-side aggregation where appropriate; query plans/indexes to be evaluated before adding migration.
5. Use parameterized dates and server-defined timezone boundaries: Asia/Tehran; the API contract should use an explicit period and deterministic start/end.
6. Treat `Indeterminate` as unknown; no automatic resend triggered by dashboard.
7. Never expose signing custody, credentials, private keys or raw financial personal data via analytics.
8. No write operations from read-only dashboard endpoints.
9. React loading/empty/error states, accessible Persian charts with tabular alternatives and mobile RTL.
10. Preserve existing Phase10–12 workflows; run macOS first, then independent Ubuntu acceptance and final release.

## Gaps and unresolved validations

- Invoice overview, logical chain joins, latest status precedence, and ownership SQL: inspect implementation and live schema before writing aggregation.
- Existing sales report API accounting/time-window semantics: inspect source and test against finance audit to prevent counting future/outside-period receipts incorrectly.
- Scope distinction Level1 root vs Level2 partner: verify actual ownership and authorization of all new dashboard SQL.
- Current `main` contains Next.js 15.5.28 and no charting library in `apps/web/package.json`; prefer accessible lightweight SVG or consciously approved dependency.
- Data-volume and query performance on real PostgreSQL are unverified; no runtime tests have been run as part of Step01.

## Next Step: Step 02 — KPI contract (completed; see below)

Produce an exact per-role KPI dictionary with numerator/denominator, time dimension, empty state, owner-scope predicate, source tables, status-to-bucket mapping, lineage deduplication, and SQL verification samples. Include sample payloads, API read-only routes and explicit negative authorization tests. Implement only after source/DB contract confirmation.

## Phase checklist (18 steps)

01 Audit baseline (this document).
02 Role KPI dictionary and API contract.
03 Read-only aggregate services/endpoints.
04 Tenant isolation and authorization tests.
05 UI/UX dashboard design.
06 Shared chart, card and filters.
07 Customer dashboard.
08 Level2 dashboard.
09 Level1 dashboard.
10 Sales/collection/receivables/settlement charts.
11 Subscription and renewal views.
12 Actionable attention center.
13 Recent activity and quick actions.
14 Loading, error, empty-data handling.
15 Performance and large-data validation.
16 macOS acceptance.
17 Ubuntu acceptance.
18 Documentation, merge, tag and release.


---

## STEP 02 — KPI & API contract


Date: 2026-11-03 | Baseline: `phase/15-role-based-dashboard`
Status: **DESIGN COMPLETE, IMPLEMENTATION PENDING STEP 03**
Prerequisite: STEP 01 in this same file

## Decision record

KPI means **Key Performance Indicator**. Every widget is an authenticated, server-scoped, evidence-backed metric and not a substitute for accounting profit/loss or external authority processing.

This file is a proposed **v1 contract** for future implementation; no endpoint declared here exists yet. No migration or external API call is authorized in Step 02.

### 1. Scope and security

- Identity comes from bearer/session validated by the existing backend, NOT from `role`, `customerId`, `partnerId`, or `ownerId` supplied by browser.
- Customer: only own accessible taxpayer + invoice + subscription rows.
- Level2: only **current** commercial-owner customers (`customer_commercial_owners.partner_level2_user_id = actor`) and linked authorized taxpayers; finance transactions must additionally be scoped by authoritative order/ledger ownership.
- Level1: root authorized domain only. In the current single-Level1 model, cover allowed root's records, not arbitrary unrelated records.
- Default role precedence when multiple roles exist: Level1 > Level2 > Customer, with authorization re-evaluated server-side. An unprivileged role cannot force an administrator response.
- An optional `taxpayerId` must belong to the resolved scope; otherwise return 404 without partial aggregate results. An optional `partnerUserId` allowed only for Level1 and restricted to its root; unauthorized partner returns 404.
- Apply scope **before** counts, time-series grouping, pagination and totals. No user-provided WHERE clause, UUID whitelist or role flag is security evidence.
- All responses: `Cache-Control: private, no-store`, no raw invoice JSON, private keys, sensitive details or provider errors. No write operations or external tax-service calls.

### 2. Time contract

- Query: `period=7d|30d|90d|year|custom`; default `30d`; for custom, `from=YYYY-MM-DD&to=YYYY-MM-DD` both ISO Gregorian **dates**, interpreted as inclusive calendar days in `Asia/Tehran`.
- UI displays Jalali, but dates are normalized to a UTC half-open interval `[fromUtc, toUtcExclusive)` on the server. Gregorian custom inputs are an API transport convention, not a Gregorian UI requirement.
- Max custom length 366 local days, no inverted intervals, no future bounds beyond today's Tehran date; return 400 for invalid combinations. Avoid timezone-dependent frontend bucketing.
- `period=year` means current Jalali year-to-date; `7d/30d/90d` are trailing local calendar days inclusive today. Display fixed labels and `asOfUtc`.
- Every metric declares its own event time: invoice **issued** date for invoice issue series; latest authoritative report observation time for acceptance series; order issued time for sales; collection posted time for cash; allocation posted time for revenue-share; settlement event time for transfer/reversal.
- **Lifetime status snapshot** and **within-period activity** are different concepts. `invoiceStatus` counts currently visible logical invoices with latest issue date in the selected period; `invoiceActivity` counts distinct external outcomes observed in the period. A late inquiry may change the former; this is expected. No misleading comparability between them.
- Currency in backend: `IRR` long integer; UI may show toman by exact division and explicit unit, but never assume persisted amounts are toman.

### 3. Invoice status and deduplication policy

Authoritative source investigation:
- `invoice_documents`, `invoice_entry_workspaces` (editable drafts), `sandbox_invoice_legal_relations` (predecessor-successor legal edges), `invoice_dispatch_operations`, `sandbox_authority_inquiry_reports`.
- Relevant: `Phase10EffectiveInvoiceQuery`, `Phase10InvoiceLineageReadService`, `InvoiceEntryWorkspaceService`, `InvoiceDispatchState`.
- Latest operation selection: per invoice + owner + taxpayer, `ORDER BY invoice_revision DESC, created_at_utc DESC, id DESC`.
- Latest inquiry report by authoritative observation time and stable ID; its status has precedence over local operation status **only after validating report provenance/relevance to invoice and operation revision**.
- Invoice chain root is the canonical oldest ancestor from legal relations; traverse cycle-safe and depth-bounded with deduplicated root; orphan/suspect cycles must be flagged as data integrity issues, not silently inflated.
- The **logical current state** is deduplicated per root + owner + taxpayer and may show the last accepted legal successor (correction, cancellation, return); latest unaccepted draft/attempt must not erase last accepted outcome. For operational pending alerts, expose the actionable newest successor separately.
- `invoice_entry_workspaces` are private editable drafts; never add a workspace and its promoted `invoice_documents` entry as separate invoices.
- `SUCCESS` official inquiry report is authoritative acceptance evidence. `Acknowledged` only means receipt of submission, not accepted. `Indeterminate` remains unknown; never auto-resend. Rejected statuses are accepted as rejection only when recognized from the SDK/authority schema; unknown strings are `unclassified`, not guessed.
- State map (local operation when no definitive authority outcome):

| Dispatch state | Canonical analytics bucket | Reason |
| --- | --- | --- |
| No operation / draft workspace | draft | local edit only |
| Prepared (1), Signed (2), Reserved (3) | prepared | locally staged, not sent |
| Sending (4) | processing | external outcome not established |
| Acknowledged (5) | awaiting_inquiry | receipt-only, NOT success |
| Indeterminate (6) | unknown | requires safe inquiry/reconciliation |
| RetryEligible (7) | retry_eligible | not a grant to resend from dashboard |
| FailedTerminal (8) | attention | terminal dispatch failure |
| Verified official SUCCESS | accepted | confirmed authority outcome |
| Verified official rejection | rejected | only from recognized official evidence |
| Other/unrecognized official result | unclassified | never turn into success/failure |

A legal cancellation with official SUCCESS is recorded as `accepted_cancellation` in the *legal subject/status*, not a fresh independent original accepted invoice. Correction and sales return follow same lineage rule. Status buckets in `invoiceStatus` must be **mutually exclusive**: sum buckets = `logicalInvoiceCount` for the selected issue cohort.

**Success rate denominator:** `accepted + rejected` where each is a logical invoice with a definitive official acceptance/rejection outcome of the **current relevant legal event**. Thus `successRatePercent = accepted / (accepted + rejected) × 100`; null and `notEnoughEvidence=true` if denominator 0. Do not count prepared/acknowledged/indeterminate as failures or successes. A separately labeled `completionRate` can use a larger denominator only when precisely explained.

### 4. Required KPI dictionary

| id | Roles | Definition | Source of truth | Time/scope |
| --- | --- | --- | --- | --- |
| `logicalInvoiceCount` | all | distinct legal invoice roots in selected issue cohort | invoice documents + legal relations | issue date, actor taxpayer scope |
| `invoiceAccepted` | all | roots with current relevant definitive official success, with accepted cancellation/return separately labeled | inquiry report + legal chain | issue cohort; authority status |
| `invoiceRejected` | all | roots with current relevant definitive official rejection | verified inquiry report | issue cohort |
| `invoiceProcessing` | all | roots current state `Sending` | dispatch | issue cohort |
| `invoiceAwaitingInquiry` | all | roots current state `Acknowledged` | dispatch + report | issue cohort |
| `invoiceUnknown` | all | roots current state `Indeterminate` or inconclusive unresolved outcome | operation + inquiry | issue cohort |
| `invoicePrepared` | all | roots with prepared/signed/reserved evidence and no official outcome | dispatch | issue cohort |
| `invoiceAttention` | all | actionable rejected / terminal failed / retry eligible (subtypes separately), excluding unknown where separate | dispatch + validated report | issue cohort |
| `invoiceDrafts` | all | editable non-promoted workspaces (distinct, scoped) | invoice_entry_workspaces | draft updated time |
| `invoiceSuccessRatePercent` | all | `accepted/(accepted+rejected)*100`; nullable | derived from authoritative statuses | issue cohort |
| `invoiceIssuedTrend` | all | distinct canonical roots newly issued per bucket | invoice documents | issued-at, Tehran |
| `invoiceAcceptedTrend` | all | authoritative acceptance events per bucket, deduped per operation/event | inquiry report | observed-at, Tehran |
| `activeSubscription` | customer | current active plan, expiry, days, quantity-pack remainder | CustomerSubscriptionOverviewService | as-of, not period-filtered |
| `upcomingExpiryCount` | admins | active subscriptions due in <= 30 days; exclude already expired | customer_subscriptions | as-of, scoped owner |
| `activeCustomerCount` | admins | distinct active authorized customer accounts | identity + commercial owners | as-of |
| `activeSubscriptionCount` | admins | distinct active subscriptions | customer_subscriptions | as-of |
| `salesGrossIrr` | admins | gross commercial orders issued in interval, excluding explicitly voided/refunded where defined | commercial_orders, commercial invoices | order issued-at, owner |
| `salesCollectedForCohortIrr` | admins | verified collections against orders issued in interval up to as-of | commercial orders + verified receipts | order cohort, clearly labeled |
| `collectionsInPeriodIrr` | admins | net verified collections posted during interval (including older orders) | verified collections | receipt date |
| `salesOutstandingIrr` | admins | remaining collectible balance on selected order cohort as-of | order/receivable read model | order cohort |
| `partnerEarnedShareIrr` | admins | posted partner allocation amount | commercial_revenue_allocations | allocation posted-at |
| `partnerSettlementNetIrr` | admins | transfers minus reversals posted within interval | partner_settlement_events | settlement event time |
| `partnerOutstandingIrr` | admins | earned less net settled as-of, no period leakage | PartnerSettlementReadService | as-of |
| `rootEarnedShareIrr` | Level1 only | root allocation share from verified collections, not P&L | PartnerRevenueLedgerReadService | allocation posted-at |
| `partnerComparison` | Level1 only | restricted per-partner active customer, verified collection and net share data | commercial owner + ledger | consistent server intervals |
| `pendingApprovals` | Level1, eligible Level2 | active authorized signature/memory/commercial review requests | corresponding authorized approval read-models | as-of |

**Important distinction:** `salesCollectedForCohortIrr` is NOT the same as `collectionsInPeriodIrr`. Never sum them together. Allocation and settlement metrics are NOT net profit; do not label them as profit/loss.

### 5. Planned read-only endpoint topology (not yet implemented)

Next.js authenticated same-origin proxy:
- `GET /api/dashboard/overview?period=30d[&taxpayerId=...]` — role-specific summary + invoice status + subscription summary.
- `GET /api/dashboard/trends?period=30d[&taxpayerId=...]` — scoped invoice trends.
- `GET /api/dashboard/finance?period=30d[&partnerUserId=...]` — Level1/Level2 only, server-decided response projection; returns 403 for Customer.
- `GET /api/dashboard/attention?period=30d[&taxpayerId=...]` — bounded list (max 10) of actionable items with safe deep links, no direct processing.
- `GET /api/dashboard/recent?period=30d[&taxpayerId=...]` — bounded list (max 10) of recent relevant activity.

Corresponding local .NET API:
- `GET /api/dev/dashboard/{overview|trends|finance|attention|recent}` (backend policy consistent with existing loopback-only dev endpoints). Do NOT introduce a publicly accessible route or bypass existing deployment posture.
- Return 401 missing/expired credential; 403 authenticated but insufficient role; 404 for inaccessible scoped entity (avoid enumeration); 400 invalid date/range/input; 503 data source unavailable.
- Not implemented in Step02; Step03 will build route, service, DI registration and thin explicit Next.js proxy after cross-check.

#### v1 conceptual JSON response example (illustrative ONLY)

```json
{
  "schemaVersion": 1,
  "role": "Customer",
  "asOfUtc": "2026-11-03T08:00:00Z",
  "period": {
    "key": "30d",
    "timeZone": "Asia/Tehran",
    "fromUtc": "2026-10-04T20:30:00Z",
    "toUtcExclusive": "2026-11-03T20:30:00Z"
  },
  "invoiceStatus": {
    "logicalInvoiceCount": 20,
    "accepted": 12,
    "acceptedCancellation": 1,
    "rejected": 2,
    "processing": 1,
    "awaitingInquiry": 1,
    "unknown": 1,
    "prepared": 1,
    "attention": 1,
    "unclassified": 0,
    "successRatePercent": 85.71
  },
  "invoiceDrafts": 3,
  "subscription": {
    "planName": "بسته ۱۰۰ تایی",
    "remainingInvoices": 56,
    "expiresAtUtc": "2027-01-01T00:00:00Z"
  }
}
```

*Example note:* `acceptedCancellation` is an exclusive status category, while `attention` represents terminal failures or retry-eligible actionable roots; the sum of exclusive categories above is 20. Actual response must be schema-validated before implementation. Do not render example figures as live data.

Finance responses should use an **independent admin-only DTO** so customer payloads cannot contain financial fields even with null values.

### 6. Test contract for Step03–04

| Case | Expected |
| --- | --- |
| Customer calling finance endpoint | 403; no monetary data |
| Level2 requesting another partner's data | 404; no totals leak |
| Level2 without selected partner | server scopes to own partner |
| Level1 own-partner comparison | only own authorized root |
| Unknown taxpayer ID | 404, zero partial results |
| Session revoked / disabled user / missing MFA where required | denied |
| Acknowledged only, no inquiry success | awaiting_inquiry, never accepted |
| Indeterminate and 401/404 inquiry fault | unknown, no resend |
| Accepted cancellation/correction/return | one root; latest valid legal event |
| Unaccepted correction/return draft | retains previous confirmed legal state; actionable separate |
| Duplicate receipts/inquiry observations | no duplicate invoice counting |
| RetryEligible/FailedTerminal | attention category but never implicitly authorized to retry |
| Order manually activated with debt | activation not classified as full cash receipt; outstanding remains |
| Failed gateway callback | excluded from collections |
| Settlement reversal | net settled decreases, outstanding increases |
| Date crossing Tehran midnight and Jalali year-end | assigned exactly once to correct local bucket |
| Empty dataset | zeros and empty arrays; percentage null |
| Unavailable backend | explicit unavailable state, never fake zero |
| All buckets | exclusive invoiceStatus buckets reconcile to logicalInvoiceCount |
| Large volume | no fetching entire invoice or ledger histories into web app memory |

### 7. SQL verification checklist (to use against test DB, read-only)

1. Inspect schema columns for `invoice_documents`, `invoice_entry_workspaces`, `sandbox_invoice_legal_relations`, `invoice_dispatch_operations`, `sandbox_authority_inquiry_reports`.
2. Verify current active ownership through `customer_commercial_owners` and `TaxpayerScopeService.ScopedQuery` equivalent before counting.
3. Compare authority report ordering and uniqueness by operation revision; detect conflicting outcomes.
4. Cross-check financial totals with Phase12 plan sales report; cohort sales and in-period collection different dates.
5. Cross-check allocations, settlement payments and reversals with existing finance audit scripts.
6. Run EXPLAIN (ANALYZE, BUFFERS) ONLY on isolated test dataset, with bounded execution; never on production without approval.

### 8. Deferred feature policy

Automated inquiry jobs, restart recovery, multi-worker handling and phases 13/14 remain postponed. Dashboard may include **hidden/disabled future extension slots**, not fake job counters or simulated operational status.

### 9. Step02 acceptance

- [x] KPI definition and roles documented
- [x] Monetary units, rate denominator, timeline and scope defined
- [x] Dispatch-state mappings grounded in existing enum
- [x] Legal invoice lineage / dedup rules documented
- [x] Read-only API topology and failure semantics proposed
- [x] Security and negative acceptance cases specified
- [ ] Runtime DB schema validation — Step03 prerequisite
- [ ] Endpoint implementation and automated tests — Steps03–04

**No source code, schema migrations or runtime test results are claimed by this design-only step.**

---

## STEP 03 — Backend aggregate read-model and Next.js proxy (2026-11-03)

### Delivered on phase/15-role-based-dashboard

- `src/ArginTaxPayer.Api/Dashboard/Phase15DashboardReadService.cs`: read-only PostgreSQL-backed role-aware query service. Supports Tehran-local `7d`, `30d`, `90d`, `year`, and bounded `custom`; canonical invoice lineage-root grouping; official inquiry SUCCESS distinct from Acknowledged; unsigned/unknown state visibility; customer subscription; admin commercial order gross/collected/outstanding, receipts by posting period, partner share, root share and net settlements.
- `src/ArginTaxPayer.Api/Dashboard/Phase15DashboardEndpoints.cs`: read-only `GET /api/dev/dashboard/{overview|trends|finance|attention|recent}`, local-only and authenticated, no-store and rate limited, no mutation or authority traffic.
- `src/ArginTaxPayer.Api/Program.cs`: service registration and mapping inside the existing Development and DB-enabled scope.
- `apps/web/app/api/dashboard/[section]/route.ts`: authenticated strict Next.js GET-only route with role/parameter allowlist, local API destination and short timeout; customer finance denied even before backend.
- `tests/ArginTaxPayer.Tests/Phase15DashboardContractTests.cs`: deterministic classification and Iran date window tests.
- `apps/web/tests/phase15-step03-dashboard-proxy.test.mjs`: route isolation, forwarding and input rejection tests.

### Important implementation semantics

- Dashboard is **not** wired into the visual page yet. UI is scheduled for steps 05–09.
- `accepted` requires a latest official `SUCCESS` evidence; `Acknowledged` is awaiting inquiry. Unrecognized statuses are not presented as accepted. Legal successors are associated with their original invoice root, and accepted cancellation is separate from accepted ordinary issuance.
- `invoiceDrafts` excludes a workspace already promoted to invoice_documents, by shared workspace/document ID.
- Revenue metrics are IRR and cannot be interpreted as accounting profit. Cohort collections and receipts-in-period are separate.
- Existing development-only gate is retained deliberately. No production exposure, schema change or migration.
- `trends` currently exposes `AcceptedByIssueDate` (issued cohort), **not** a report-observed acceptance series. Replace or extend in Step 06 with independently bucketed authority observation events before displaying an observed-acceptance chart.
- `recent` is currently invoice issue-date recency; dispatch activity chronological feed is a Step 13 enhancement.
- `finance` covers verified order/collection ledger and allocations. More comprehensive active subscription and approvals aggregates belong to Steps 10–12.

### Steps 03–04 validation/acceptance requirements (NOT claimed to have run)

The connected GitHub environment is source-level only; this session has not connected to the test Postgres or executed `.NET`, Next.js or live HTTP tests. These must be executed before accepting Step 03 as runtime-validated:

```bash
# macOS, from repo root, with existing dev DB tunnel already active
git fetch origin
git switch phase/15-role-based-dashboard
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --configuration Release --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --configuration Release --no-restore --filter FullyQualifiedName~Phase15DashboardContractTests
cd apps/web
npm ci
npm run typecheck
node --test tests/phase15-step03-dashboard-proxy.test.mjs
npm run build
```

Re-test role security manually with authenticated Customer, Level2 and Level1 sessions against `/api/dashboard/overview?period=30d` and `/api/dashboard/finance?period=30d`. Verify Customer finance =>403; invalid `taxpayerId` =>404; Level2 foreign partner =>404/403; unauthenticated=>401. Compare aggregates against the test database and existing Phase12 finance audits.

### Runtime review gates and known follow-up

1. Confirm PostgreSQL query plans and numeric totals on seeded, real database; validate empty data/large histories.
2. Confirm source builds on macOS; fix all compilation, route and SQL runtime defects before marking Step03 accepted.
3. Expand official status classification when actual archived authority report vocabulary is confirmed; unknown remains unclassified.
4. Check whether `issued` versus `accepted` charts and recent-activity wording accurately describe their actual timestamp dimension.
5. Step04 must include cross-customer, cross-partner, revoked grant, changed ownership and MFA negative tests.

**Step03 implementation committed; runtime acceptance remains PENDING local tests.**

### Canonical file rule

This is the **only Phase 15 document inside `docs/phases/`**. Append Step04 and later updates here as new sections, rather than creating additional per-step documents. Other non-Phase15 phase files must not be renamed or touched.

---

## STEP 04 — Authorization isolation and financial/invoice integrity (2026-11-03)

**Source review and regression test expansion completed; execution on owner's macOS/test PostgreSQL is still required for runtime approval.**

### Findings fixed in this step

1. **Customer-role invoice predicate hardening.** The original `ReadInvoices` query allowed `d.customer_user_id=actor` without requiring the selected role to be `Customer`. The predicate now explicitly requires `{6}=Customer` for that branch, while Level1 and Level2 use their distinct authorized owner predicates. This prevents an administrator's historical customer identity from broadening data visibility under a different role.
2. **Level2 collections scope fix.** In the initial Step03 implementation, cohort order sales and revenue share queries checked Level2 current ownership but `collectionsInPeriodIrr` checked historical `o.partner_user_id` only. That inconsistency could show a transferred customer's historical receipts to a former partner. The collections query now also checks current `customer_commercial_owners.partner_level2_user_id` before aggregating.
3. **Read-only audit.** `scripts/phase15-step04-security-audit.sql` runs only on `argintaxpayer_test`, under a read-only transaction; it reports counts of invoice ownership/lineage issues, receipt reconciliation mismatches, over-settled allocations, historical partnership changes and acknowledgement without official SUCCESS. The last two are **diagnostic** and may be nonzero legitimately.
4. **Level2 response minimization.** The optional `rootEarnedShareIrr` finance field is omitted entirely for Level2 (not returned as zero); root-only financial data remains Level1-only.
5. **Proxy negative test coverage.** Expanded `apps/web/tests/phase15-step03-dashboard-proxy.test.mjs` to assert backend 401/403/404/503 propagation, non-caching, Customer finance denial, Level2 partner-filter denial, and unsupported finance filter rejection.

### Security matrix

| Actor/request | Expected result |
| --- | --- |
| Customer / own overview and trends | HTTP 200 with only own authorized taxpayer data |
| Customer / finance | HTTP 403, no money fields |
| Level2 / finance with no partner filter | HTTP 200 scoped to current owned customer financial records |
| Level2 / finance with `partnerUserId` | HTTP 403 |
| Level1 / finance for owned root | HTTP 200 |
| Level1 / finance with a foreign `partnerUserId` | HTTP 404 |
| Authorized actor / foreign `taxpayerId` | HTTP 404 |
| Missing session | HTTP 401 |
| Disabled session, inactive user, missing MFA for admin | Denied (status based on identity boundary) |
| Unknown dashboard section | HTTP 404 |
| Duplicate or unknown query parameters | HTTP 400 |
| Provider unavailability | HTTP 503 through Next.js proxy |

### Important limitations (not misreported as PASS)

- No .NET, Next.js or PostgreSQL live execution was available in the GitHub-only editing environment. The above expectations are **tests and source-reviewed conditions**, not proven runtime outcomes.
- Step03 `ReadInvoices` still materializes all scoped invoice summaries in app memory for the selected time window. Large-volume performance and SQL execution plans remain an explicit Step15 issue. No unbounded PII snapshots are sent to the web app.
- Step03 `invoiceAcceptedTrend` is currently an **issue-date-cohort accepted count**, not a time series of official confirmation observed at date. The UI must not label it "daily confirmed by authority" until the separate authority-observed series is implemented.
- Official rejection string vocabulary, revision provenance and complex legal-chain collision scenarios require live SDK/DB fixture verification. Unknown reports remain unclassified, not accepted.
- Current ownership versus historical commercial revenue attribution is explicit; finance audit should compare the same ownership/time filter across all amount categories.

### macOS validation commands, from project root

```bash
git fetch origin
git switch phase/15-role-based-dashboard
git pull --ff-only
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj -c Release --no-restore \
  --filter "FullyQualifiedName~Phase15DashboardContractTests"
cd apps/web
npm ci
npm run typecheck
node --test tests/phase15-step03-dashboard-proxy.test.mjs
npm run build
```

For database validation, with the previously configured macOS AutoSSH tunnel to VPS PostgreSQL:

```bash
# Run from the repository root (not apps/web)
PGPASSWORD='' psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test \
  -f scripts/phase15-step04-security-audit.sql
```

The example deliberately does not inline a password. If `psql` prompts for one, enter it interactively; `PGPASSWORD=''` is optional and can be omitted.

For browser checks, run the API/Web services through the existing `scripts/dev-up.zsh` workflow and verify separate authenticated Customer/Level2/Level1 sessions. Compare server-side metrics with audit results, and run an adversarial test where the Level2 owner changes after a collection was recorded.

**Step 04: source fixes and tests committed. Runtime security and database acceptance: NOT YET VERIFIED.**

### Documentation policy

Continue appending future phase steps to this file only; do not create new `docs/phases/phase-15-stepXX*.md` files.


### Step 04 audit hotfix — 2026-11-03

- User-reported PostgreSQL syntax error at line 8: `DO $ BEGIN` / `END $` had an invalid dollar-quoted delimiter. This was a script-authoring defect; the transaction stopped before integrity queries.
- Replaced the procedural block with a standard read-only SQL gate: `SELECT 1 / (CASE WHEN current_database() = 'argintaxpayer_test' THEN 1 ELSE 0 END) AS test_database_gate;`.
- The SQL file now requires no procedural code; `ON_ERROR_STOP` continues to abort the audit on any SQL failure. Gate returns 1 on the designated test DB and fails on other databases.
- Fix commit: `507e15c4055f42d255455199b339bb73a730ec9e`.
- Runtime test still pending a new macOS execution; no PASS claim.

---

## STEP 05 — Dashboard visual/UX design system (2026-11-03)

**Design system committed; graphical page integration is intentionally scheduled for Steps 06–09.** Existing dashboard and all routes remain unchanged in Step05.

### Visual direction

- Preserve the existing application shell, brand, navigation and RTL Persian typography. Existing CSS tokens are `--argin-primary:#177f81`, `--argin-page:#f3f6fa`, `--argin-line:#dae6ed`, `--argin-text:#20344b`, `--argin-muted:#64788c`; use shared Vazirmatn and Tahoma fallback.
- Use a quiet, professional operational dashboard: light background, white bordered cards, green action emphasis, restrained status colours only for meaningful states. No chart decoration or oversized banner pushing actionable information below the fold.
- Use consistent 16px card radius, 16px desktop grid gap, 12px phone grid gap, card padding 18px/14px, readable labels and 40px+ filter targets. Respect `prefers-reduced-motion`.
- Status colour semantics: green = confirmed official acceptance / paid; amber = pending/attention; red = rejected/terminal failure; neutral blue-grey = prepared/in progress; **indeterminate should be distinctly labeled unknown**, never green.

### Shared information architecture

```text
[Compact page title + user role + as-of timestamp]   [period | taxpayer/partner selector]
[ K P I 01 ] [ K P I 02 ] [ K P I 03 ] [ K P I 04 ]
[Trend chart: issuance / commercial receipts]    [Status chart / composition]
[Actionable attention queue]                     [Latest activity]
[Role-specific secondary KPIs, subscription and contextual shortcuts]
```

Keep no more than **four primary metrics** immediately visible per role at desktop width. Further metrics reside in clearly titled secondary sections. Filters remain visually distinct from cards; loading/error states never masquerade as real zeroes. Every KPI has a description of its source/time dimension accessible in context.

### Role-specific primary and secondary widgets

| Role | Primary four KPIs | Main chart | Secondary/quick actions |
| --- | --- | --- | --- |
| Customer | logical invoices, official accepted outcomes, unresolved/attention cases, remaining subscription quota or expiration | invoices issued by local Tehran calendar date, plus current outcome composition | subscription details, drafts, recent invoices, create invoice, open invoices |
| Level2 | gross sales of subscription orders, verified collections posted in period, outstanding on selected order cohort, own allocated partner share | receipt trend and own invoice status trend | own customer count, own settlement position, renewals and recent customer activity; **no root share or profit** |
| Level1 | subscription sales gross, period-verified cash collections, commercial receivables, root share from approved allocations | cash collection and sales trend, partner comparison | active subscribers, invoice operations, partner earned/net-settled/outstanding, approaching renewals |

**Never use the monetary face amount of a taxpayer-issued fiscal invoice as platform subscription revenue.** Currency amounts use persisted IRR and an explicit user-visible unit.

### Charts and metrics

- For Step06, define reusable primitives: `DashboardPageHeader`, `DashboardPeriodFilter`, `DashboardKpiCard`, `DashboardPanel`, `DashboardStatusDonut`, `DashboardTrendChart`, `DashboardState`, `DashboardQuickActions`.
- Chart x-axis is Tehran local calendar day (display Persian/Jalali date). Provide readable legend, unit, comparison and text/table equivalent. Charts are not the exclusive access path to information.
- **AcceptedByIssueDate** from the Step03 API means current accepted outcomes grouped by **original invoice issue date**, not date of official confirmation. Do not label it as "daily authority confirmations".
- Proposed API topology still has no daily cash receipt series or subscription renewal aggregation; those chart slots must be shown only after actual server support is implemented in subsequent steps, never via invented placeholder figures.
- Count corrected/cancelled/returned invoice chains once per root. Show cancellation as its own last effective subject and keep uncertain outcomes in an explicit neutral/amber state.

### Layout by breakpoint

| Width | KPI grid | Charts | Recent/attention | Filter bar |
| --- | --- | --- | --- | --- |
| Above 1100 px | 4 columns | Trend 1.65fr + status 1fr | two columns | right-aligned, wrapping |
| 641–1100 px | 2 columns | stacked | two columns where space allows | wrapping |
| 381–640 px | 2 compact columns | stacked | one column | full width |
| Up to 380 px | 1 column | stacked | one column | full width |

Maximum content width continues to be managed by the existing console shell. No separate shell, no horizontal body scrolling. Tables may use contained overflow scroll with accessible labels.

### State and security UX

- Loading: accessible skeleton/progress text, never fake KPI digits.
- Empty dataset: descriptive message and permitted action, not a misleading success rate of 0%.
- Missing/invalid session: follow existing identity flow, no stale cached finance data.
- API 403/404: safe generic denial, no hidden-tenant identity clues.
- API 503/timeouts: inline retry of read-only dashboard fetch only; **never send, retry or inquire a fiscal invoice automatically**.
- No cards/links shown solely by frontend role should be mistaken for security enforcement. Server authorizes every API request.
- Focus-visible ring, logical heading sequence, 4.5:1 text contrast target, text labels alongside status colours, reduced motion, `aria-live` for data states, keyboard-operable filters.

### Deliverables

- `apps/web/app/(console)/dashboard/dashboard-design.css`: scoped dashboard-only CSS contract (not imported in Step05; will be used in Step06 shared components).
- `apps/web/tests/phase15-step05-dashboard-design.test.mjs`: static regression checks of brand token reuse, RTL, responsive layouts, accessibility states and preserved existing operational dashboard navigation.
- One single canonical phase document: this file; no additional phase-15 step documents.

### Step05 verification (macOS)

```bash
git fetch origin
git switch phase/15-role-based-dashboard
git pull --ff-only
cd apps/web
npm ci
node --test tests/phase15-step05-dashboard-design.test.mjs
npm run typecheck
npm run build
```

**Step05 source-level design work complete. Runtime tests are pending operator execution; no database migration or Ubuntu acceptance needed in this design-only step.**

---

## STEP 06 — Shared components & visible live dashboard integration (2026-11-03)

**Implemented and committed to `phase/15-role-based-dashboard`; runtime macOS acceptance pending.**

### Delivered

1. `apps/web/app/(console)/dashboard/dashboard-analytics.tsx`: real client-side shared components: `DashboardKpiCard`, `DashboardPanel`, `DashboardState`, `DashboardPeriodFilter`, `DashboardTrendChart`, and `DashboardStatusChart`. Four role-specific KPI cards with real server-supplied numbers, chart with accessible text table and legible status progress bars, attention list and scoped quick actions.
2. `apps/web/app/(console)/dashboard/page.tsx`: imports the Step05 `dashboard-design.css` and mounts `DashboardAnalytics` for Level1, Level2 or Customer. The previous oversized welcome and static status summary are replaced with the visible analytics layout. Existing feature/module shortcuts are retained in a compact expandable `details` panel, and customer subscription widget is retained.
3. `apps/web/app/(console)/dashboard/dashboard-design.css`: styles for status bars, trend chart, cards, error/empty states, accessible navigation and responsive expanded/collapsed modules.
4. `apps/web/tests/phase15-step06-dashboard-components.test.mjs`: TSX syntactic transpilation + static UI contract checks for role-specific finance fetching, no write methods, no fake numbers, stale data clearing, no-store fetches, and chart labels.

### Functional details

- Data requests from real `GET /api/dashboard/{overview|trends|attention}`, plus `/finance` **only for Level1/Level2**; Customer never fetches finance.
- Period options `7d`, `30d`, `90d` and `year` use server Tehran-window semantics and are interactive. Step07–09 may add advanced taxpayer/partner filters and detailed per-role analytics.
- When any required feed is unavailable, the dashboard displays a recoverable error, not sample KPI digits. Changing periods aborts old fetches and clears previous-period numbers; requests are GET only and read-only.
- Invoice acceptance is based only on official outcome; acknowledged/indeterminate are not counted as accepted. Status distribution is based on canonical invoice roots supplied by the Step03 read model.
- **Trend semantics:** `acceptedByIssueDate` is a *current status by invoice's original issue date* series, **not** daily official response events. UI has an explicit clarification and tabular alternative.
- Financial figures use verified subscriptions/commercial ledger amounts in **ریال** with explicit unit, never taxpayer invoice face amount, no invented profit.
- Root share is displayed only to Level1. Customer and partner views do not receive root financial data in browser.
- New dashboard is visible immediately after the code is deployed and the development UI is running; the actual KPI numbers require a healthy authenticated backend API and database.
- No database migration or external tax operation was introduced in Step06.

### Validation commands — macOS

```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/15-role-based-dashboard
git pull --ff-only origin phase/15-role-based-dashboard
cd apps/web
npm ci
node --test tests/phase15-step06-dashboard-components.test.mjs tests/phase15-step05-dashboard-design.test.mjs tests/dashboard-redesign.test.mjs
npm run typecheck
npm run build
```

Then launch the existing dev environment with the API and web app as usual, sign in under each role and visit `http://127.0.0.1:3000/dashboard`. Verify a new analytic header and KPI cards (or explicit unavailable state), chart, 7/30/90/year filters, finance only for admins, and continued access to existing module links. On API error, verify no fake zeros are shown.

### Acceptance and deferrals

- Source-level implementation and design-test file creation: complete.
- Browser visual confirmation on the operator's macOS: pending.
- End-to-end role separation of DB aggregates: continue Step04/Step07–09 runtime validation.
- Per-role advanced screens, additional calendar inputs, money/receipt series, richer activity and large-data query optimization: follow Steps07–15.
- **This remains the only Phase15 documentation file under `docs/phases/`**.
