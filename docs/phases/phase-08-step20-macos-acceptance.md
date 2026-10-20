# Phase 08 — Step 20 macOS acceptance

Date: 2026-10-20

Step 19 was approved by the owner without supplying macOS execution logs. Step 18 bulk-import QA remains explicitly deferred and must not be marked as tested.

Run on macOS:

```bash
git switch phase/08-invoice-workflows-ui
git pull --ff-only
zsh scripts/dev-up.zsh
bash scripts/phase08-step20-macos-acceptance.sh
```

The runner stores a timestamped record under `~/.argintaxpayer-test/phase08-step20/`. It performs a clean-branch check, .NET and Next.js regression, status inspection, and read-only local HTTP reachability probes. No migrations are executed.

## Browser acceptance

- [ ] Sign in, MFA and roles
- [ ] User and taxpayer scoping across accounts
- [ ] Invoice list filtering and paging
- [ ] New invoice: taxpayer, buyer, Jalali date
- [ ] Add, edit and delete 20 lines with grid scrolling
- [ ] Financial totals and cash/credit/mixed settlement
- [ ] Save and reopen draft
- [ ] Two-session stale revision conflict
- [ ] Revision history: newest first, Jalali, user names
- [ ] Conditional pattern fields and required validation
- [ ] No compulsory admin approval or live tax submission
- [ ] Bulk import deferred by explicit owner decision

Step20 status: implementation prepared; manual and runtime acceptance results pending. Step21 must use the same source SHA as the successful Step20 operator run.
