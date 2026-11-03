# Phase 15 — Step 02: KPI Dictionary & Read-only Analytics API Contract

Date: 2026-11-03 | Baseline: `phase/15-role-based-dashboard`
Status: **DESIGN COMPLETE, IMPLEMENTATION PENDING STEP 03**
Prerequisite: `docs/phases/phase-15-step01-dashboard-baseline-audit.md`

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
