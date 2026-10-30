# Correction experience implementation plan

Goal: implement the six requested correction UI and chain behavior changes.
Architecture: retain root row identity and system reference; resolve the newest submitted legal successor recursively within owner/taxpayer scope. Use its identity for preview, further corrections, inquiry and history. Keep receipt state distinct from authority acceptance.
Tech stack: existing React/Next, CSS, EF/PostgreSQL; no new dependencies.

- [x] Test pending and nested corrections, failed inquiry, and scoped chain selection.
- [x] Resolve effective successor across the full chain; retain cancellation acceptance behavior.
- [x] Replace correction browser confirmations with persistent, actionable toasts; compact SVG buttons.
- [x] Show structured receipt and actual save/send/inquire progress; prevent repeated submission after receipt.
- [x] Refresh open details after changes; use effective invoice for preview and actions; blue correction badge and row.
- [x] Run web suite, typecheck, .NET suite and targeted PostgreSQL tests; review diff and restart local API.

Review focus: acknowledged is not authority success; failed inquiry retains receipt and never retries send; nested chains remain scoped and cycle-safe; open detail refresh keeps original human reference; confirmation cancellation sends nothing.

Validation: 455 web tests pass; TypeScript check passes; 3 PostgreSQL tests pass in connection-local temporary tables; .NET suite passes with optional integration tests skipped. Browser checks use synthetic data and cover desktop/mobile, toast, progress, receipt and a single submit call. Existing local invoice was read only: latest correction is acknowledged, explaining the old SUCCESS-only overlay failure. No real invoice was submitted during validation.

Final review: preserved structured receipt for created successors returned with HTTP 409; unknown inquiry reports retain acknowledged/indeterminate dispatch presentation. Both regression tests reproduced their failures and passed after fixes.
