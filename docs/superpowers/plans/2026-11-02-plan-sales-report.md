# Plan sales reporting implementation plan

**Goal:** Show the current responsible Level 2 administrator in collections and retain plan sales in a period-filtered revenue report after settlement.

**Architecture:** Reuse the scoped commercial-order query for the inbox and sales report. The inbox keeps its existing pending/approval predicate; the report includes all non-voided orders issued in the current Persian month or Persian year through now in Tehran. Receipts are cumulative verified collections against those orders; outstanding is their remaining balance. The revenue tab displays only sales; settlements load independently in their own tab.

**Tech stack:** ASP.NET Core, EF Core/PostgreSQL, Next.js, xUnit.

## Tasks

- [x] Add tests for Persian month/year boundaries (including Tehran midnight and Nowruz), invalid periods, and report totals/pagination with fully paid, partly paid and unpaid orders.
- [x] Add a domain report-period helper and extend CommercialPendingCashService with attributed partner display names and a paged sales report. Apply authorization and period scope before totals; remove the inbox's 100-row cap for report reads. Reuse existing endpoint guards for a sales-report route and allowlist its query parameters in the web proxy.
- [x] Add Level 2 column and correct inbox colspans; add current-month/current-year filter, sales table, full-period totals, pagination, loading/error/empty states in the revenue tab.
- [x] Update phase-12 and prior approval specification to explain report persistence, period basis and receipt totals.
- [x] Run backend tests, frontend tests, TypeScript checks and diff review; record results.

## Review focus

- Fully paid orders remain reportable and contribute to totals.
- Totals include all matching sales, independently of the selected page.
- Persian calendar and Tehran boundaries are computed server-side.
- Missing partner profiles retain identity fallback; direct sales have no Level 2 attribution.
- Period/page changes clear stale report data and cannot overwrite newer requests.

## Execution decisions

- Work in the existing feature branch; no commits or publishing requested.
- The original request is the implementation specification; proceed without an additional approval round.

## Verification results

- Backend suite: 1,352 passed, 28 skipped, 0 failed. PostgreSQL integration is opt-in and was not enabled.
- Finance/report frontend tests: 10 passed. Full frontend suite: 509 passed, 1 pre-existing failure (`Customer cash checkout is server-owned and fail-closed for VAT/PSP`, a source-text regex expecting `wantsOfficialInvoice:official`). That test and all its input files are byte-identical to HEAD.
- TypeScript check and Next.js production build passed. Build emits existing CSS compatibility warnings from globals.css.
- `git diff --check` passed. Read-only independent code review found no critical/important findings.
- Browser and live PostgreSQL acceptance were not run. No migration or deployment was performed.
- Review note: report now uses database aggregation plus LIMIT/OFFSET; page requests return only ten sales, with full-scope totals.
