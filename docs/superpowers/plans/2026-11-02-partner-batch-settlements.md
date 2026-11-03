# Partner batch settlement implementation plan

Goal: partner-selected immutable cash-based settlement vouchers with incremental receipts/costs, snapshot rates and printable detail.
Spec: ../specs/2026-11-02-partner-batch-settlements-design.md

- [x] Backend/domain: additive tables+constraints, scoped partner/terms/preview/batch/expense/read services and endpoints. Use exact DTO contract in spec. Lock/idempotency/stale-preview safeguards, frozen rates and signed deltas; real10-row queries and complete printable snapshot.
- [x] Frontend: dedicated workspace, mandatory partner, consistent spinner/table/pager, new voucher preview/registration, terms/expense entry, detail and isolated print. Proxy allowlist and parent integration.
- [x] Verification: meaningful math tests and rollback-only PostgreSQL tests for incremental receipts, costs, duplicates, stale preview, role scope, current-owner fallback, missing terms, snapshots and paging. Frontend rendered behavior/proxy tests and TypeScript. Review code and record results.
- [x] Documentation/local acceptance: explain changed transfer direction vs old journal and include targeted additive schema application evidence. Restart local API only after verification; preserve configuration. No commits/deployments requested.


Verification results (2026-11-02):

- Domain math: 16 passed. Seven rollback-only PostgreSQL integration tests passed in 4m13s; the extended legacy double-claim and cross-root-history regressions passed again after the fix (2 tests, 57s). Post-run audit found zero persisted synthetic users, batches, lines or expenses.
- Full backend suite: 1,388 passed, 28 skipped, 0 failed. TypeScript passed.
- Eight settlement UI tests and two proxy tests passed. Full frontend suite: 528 passed, one unchanged baseline source-regex failure (`Customer cash checkout is server-owned and fail-closed for VAT/PSP`).
- Independent review found and resolved legacy-path double-claim and partner history across root transfers. Both have actual PostgreSQL regressions.
- Reviewed and applied only migration 20261102020000 to argintaxpayer_test; no other pending migrations. The read-only invariant audit returned no mismatches.
- Local API restarted with the new build, readiness 200; partners/preview endpoints return 401 without authentication, proving they are registered and guarded. No production action or deployment.
- Browser print dialog/visual acceptance was not exercised interactively. Rendered voucher escaping, saved detail completeness and 23-line print output were verified by tests.
