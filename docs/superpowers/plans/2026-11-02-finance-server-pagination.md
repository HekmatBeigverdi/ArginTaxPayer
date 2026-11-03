# Finance tables: database pagination

Goal: Align the sales period filter and reload button, show the table shell and centered spinner immediately, and page every finance table in batches of ten on the database server.

- Sales/inbox: share scoped SQL; use a separate full-scope count/sum query and LIMIT/OFFSET data query. Fetch receipt history through an authorized order-specific paged endpoint.
- Plan catalog and price history: apply scope/search before CountAsync/Skip/Take, then load prices only for visible plan IDs.
- Settlements: aggregate scoped positions in SQL, calculate whole-scope totals separately from the ten visible rows.
- UI: each table owns its page and loading state, shows previous/next and full count, resets pages when filters change and preserves loading table shell. Use fixed pageSize=10 throughout.
- Keep authorization and financial snapshots unchanged; no schema migration.
- Verify pagination validation, rendered loading/table behavior, actual SQL LIMIT/OFFSET and scoped totals, frontend/backend suites and TypeScript. Restart local API with the new build after verification.


Verification completed:

- Database paging is applied to all six tables; complete-scope totals are independent of the visible ten rows.
- Four opt-in PostgreSQL integration tests passed (sales/inbox, receipt history, root and partner settlements), using synthetic rollback-only fixtures. Catalog/history scope and generated SQL have four passing tests in the full backend suite.
- Full backend suite: 1,364 passed, 28 skipped, 0 failed. TypeScript passed.
- Full web suite: 518 passed and one unchanged baseline checkout source-regex failure (`Customer cash checkout is server-owned and fail-closed for VAT/PSP`).
- Review P2 fixes: generation guards prevent old inbox/settlement responses from replacing newer pages; sales automatically refetches the valid last page after a shrinking count. Regression tests demonstrated failures before fixes and passed afterward.
- Local API restarted with the updated build; readiness 200 and the new authorized receipt-history route responds 401 without credentials. No migration/deployment.
- Browser visual acceptance was not run; alignment and table/loading output checked by CSS review and rendered-component tests.
