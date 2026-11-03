# Phase 15 — Step 01: Role-Based Dashboard Baseline Audit

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

## Next Step: Step 02 — KPI contract

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
