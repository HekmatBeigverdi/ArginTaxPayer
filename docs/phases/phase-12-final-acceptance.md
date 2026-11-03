# Phase 12 — final acceptance and release gate

Branch: `phase/12-subscriptions-billing-settlements`.
Candidate tag: `phase-12-v1.0.0` **only after acceptance**.

## Functional policy and scope

- Current owner policy: Level1 or Level2 may approve and activate a valid, non-voided subscription order for its authorized customer with full, partial, or zero collection. This is credit approval, not receipt certification. Debt remains in commercial orders and is collected through verified receipt ledger. Partner share and cash settlement use verified receipts only.
- Financial area, subscription plans/prices, cash-pending approval, customer plan and finance views, Level1 application settings, add-on/renewal and reporting implementations require end-to-end verification; an API/source check alone is not sufficient.
- Changes to official tax invoices, production payment gateway (SEP/ZarinPal) and two-confirmed-invoice trial dispatch must not be represented as live or accepted unless specifically verified.
- Phase10 dispatch and Phase11 safety regression must remain unaffected.

## Automated offline acceptance

On **both** macOS and Ubuntu, checkout the exact same branch commit and run:

```bash
cd ~/projects/ArginTaxPayer  # Ubuntu: ~/projects/ArginTaxPayer
git fetch origin
git switch phase/12-subscriptions-billing-settlements
git pull --ff-only origin phase/12-subscriptions-billing-settlements
cd apps/web && npm ci && cd ../..
bash scripts/phase12-final-acceptance.sh
```

The script refuses a dirty tracked tree, wrong branch or mismatched `PHASE12_EXPECT_SHA`; logs under ignored `artifacts/phase12-final` contain the .NET restore/build/tests, the approval-on-credit tests, web tests/typecheck/build, and explicit pending manual evidence. Repeat Ubuntu with the **SHA from the successfully tested Mac HEAD** exported as `PHASE12_EXPECT_SHA`.

## Database gate (test only)

Requires reviewed and applied **pending migrations on an isolated `argintaxpayer_test` database**, not production. The operator must inspect `dotnet ef migrations list` and any generated migration SQL before updating. Running this gate is read-only and makes no schema changes.

Mac, with local AutoSSH Postgres tunnel:

```bash
PHASE12_PGHOST=127.0.0.1 PHASE12_PGPORT=15432 PHASE12_PGUSER=argintaxpayer \
  bash scripts/phase12-final-db-audit.sh
```

Ubuntu, using local Docker Postgres port:

```bash
PHASE12_PGHOST=127.0.0.1 PHASE12_PGPORT=5432 PHASE12_PGUSER=argintaxpayer \
  bash scripts/phase12-final-db-audit.sh
```

The audit checks Step07, Step08, updated Step10 approval-on-credit, checkout tax snapshots, general application settings, and batch settlement if present.

## Required manual acceptance (not yet supplied)

- [ ] Exact same SHA on macOS and Ubuntu, tracked tree clean and both offline scripts PASS
- [ ] Test DB migrations are applied; both guarded financial audit scripts PASS
- [ ] Customer plan/checkout request, VAT calculation, admin cash confirmation and credit activation (including unpaid and partial payment), debt/history, tenant boundaries all verified through API and browser
- [ ] Level1 general-settings page accessible only via account menu and role correctly restricted
- [ ] Quantity add-on, renewal expiry, cash ledger, partner revenue settlement, SMS wallet scenarios verified
- [ ] Phase10 send/issue/correct/cancel/return and Phase11 inquiry regression unaffected
- [ ] No unapproved live gateway/Sandbox provider requests or undocumented credit changes
- [ ] Release approval: reviewer confirms known limitations and grants explicit production rollout permission

## Release steps — DO NOT EXECUTE before acceptance

1. PR `phase/12-subscriptions-billing-settlements` → `main` and resolve any review findings.
2. Merge only after test and database evidence. Record merged SHA.
3. Create and push annotated tag `phase-12-v1.0.0` on the **merged main commit**.
4. Publish GitHub Release with verified evidence, migration notes, and explicit outstanding limitations.

**Status:** Phase12 final gate prepared; release not yet certified. No deployment, production migration, Git merge, tag or release should be claimed based only on operator step approval.
