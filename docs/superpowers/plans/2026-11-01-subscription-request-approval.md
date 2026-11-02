# Subscription Request Approval Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Let Level 1 and assigned Level 2 admins approve customer plan requests independently of payment, and record collections until the balance is settled.

**Architecture:** Reuse customer-created commercial orders and the finance workspace's «ثبت دریافت» section. Expand the owned-order inbox, make approval role-neutral and independent from collection state, and keep an approved order available until all receipts are recorded.

**Tech Stack:** ASP.NET Minimal APIs, EF Core migrations, PostgreSQL, Next.js route proxy, React/TypeScript.

**Spec:** `docs/superpowers/specs/2026-11-01-subscription-request-approval-design.md`

## Global Constraints

- Level 2 administrators can see and manage only orders attributed to them.
- Level 1 administrators can see and manage orders under their root account, including orders attributed to Level 2 administrators.
- Admin MFA remains required for manual receipts and approval.
- Approval activates the subscription with zero, partial, or full collection and leaves the order balance unchanged.
- Manual receipts remain auditable, positive, and no greater than the outstanding balance.
- Preserve immutable plan and price snapshots; an order can activate only once.

## Review Focus

- Unpaid requests must still be approvable — handled in Task 2.
- Paid but unapproved requests must remain visible — handled in Task 1.
- Approved orders with a balance must stay in the inbox — handled in Task 1 and Task 3.
- Level 1 must be allowed across its partners while Level 2 remains limited to assigned orders — handled in Task 1 and Task 2.
- Repeated approval must not create a second subscription — handled in Task 2 using the existing unique order activation link.

---

### Task 1: Scope and enrich the collection inbox

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/CommercialPendingCashService.cs`

**Interfaces:**
- Produces `PendingRow` fields for order, customer, plan, total, collected amount, issue time, plan code, and whether the order is already activated.
- Includes scoped receipt history in each order returned by the existing `GET /api/dev/subscriptions/cash-pending` endpoint.

- [x] Change order filtering to return non-voided orders when they are unapproved or have an outstanding balance. This includes paid-but-unapproved requests and approved-but-unpaid/partially-paid orders.
- [x] Apply Level 2 scope by partner attribution and Level 1 scope by matching order root or current customer root ownership, including partner-attributed orders.
- [x] Add receipt history to the inbox response, restricted to returned orders; include amount, channel, receipt reference, and timestamp.
- [x] Require an active Level 1/Level 2 admin to read this inbox; keep MFA enforcement on approval and receipt actions as specified in the design.
- [x] Review the query conditions for all combinations of payment state and activation state without running tests.

### Task 2: Approve and activate independently from payment

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/CommercialSubscriptionActivationService.cs`
- Modify: `src/ArginTaxPayer.Api/Subscriptions/Phase12ActivationEndpoints.cs`
- Modify: `apps/web/app/api/phase12/[...parts]/route.ts`
- Create: `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261101230000_RenameSubscriptionApprover.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261101070000_AddPhase12SubscriptionActivation.cs` only if migration snapshot/model conventions require it; do not rewrite an already applied migration.

**Interfaces:**
- Add admin action `POST /api/dev/subscriptions/orders/approve` with request `{ orderId: Guid }` and response `{ subscriptionId: Guid }`.
- Keep `POST /api/dev/subscriptions/paid-orders/activate` as a compatibility alias if existing callers depend on it.
- Rename activation audit column `activated_by_level1_user_id` to `approved_by_user_id` in a forward migration.

- [x] Permit approval of any non-voided order regardless of collected amount, while verifying collection ledger total still matches the order's recorded collected amount.
- [x] Authorize the assigned Level 2 admin or the root Level 1 admin and keep MFA mandatory.
- [x] Preserve the unique order activation association and return the existing subscription ID for repeated approvals.
- [x] Add the approval route and allow both admin levels through the Next.js proxy.
- [x] Rename the database audit column with a reversible forward migration.
- [x] Review transaction, audit, and ownership behavior without running tests.

### Task 3: Combine requests, approval, and payment management in «ثبت دریافت»

**Files:**
- Modify: `apps/web/app/subscriptions/subscription-workspace.tsx`
- Modify: `apps/web/app/subscriptions/subscription-workspace.css` if needed for compact inline actions.
- Modify: `apps/web/tests/phase12-step10-finance-ui.test.mjs` to update its existing UI source assertions.

**Interfaces:**
- Consume the enriched inbox row from Task 1 and approval endpoint from Task 2.
- Keep manual receipt endpoint `POST collections/manual` and its existing payload.

- [x] Remove the redundant «سفارش مالی» tab and its manual create/activate forms from the finance UI.
- [x] Show plan, customer, total, cumulative receipts, outstanding balance, and activation state together in the «ثبت دریافت» table.
- [x] Add a «تأیید و فعال‌سازی» action for unapproved rows, enabled even when no receipt exists.
- [x] Add a per-row receipt form for outstanding balances; after each receipt, refresh displayed totals and retain the row until both approved and fully settled.
- [x] Show each recorded receipt's timestamp, method, reference, and amount in the row's payment details.
- [x] Show approval state and disable duplicate approval after successful activation.
- [x] Keep UI copy clear that activation does not waive the remaining balance.
- [x] Update existing source assertions for the integrated collection tab and new approval/history calls; review right-to-left layout, empty/loading/error states, and disabled controls without running tests.

### Task 4: Update Phase 12 workflow documentation

**Files:**
- Modify: `docs/phases/phase-12.md`

- [x] Document admin approval independent of payment and staged manual receipts through full settlement.
- [x] Correct previous notes that describe the request inbox as cash-pending only or activation as full-payment-only.

## Verification Policy

No tests were added or executed and no build commands were run because the user has not requested verification. One existing source-regression test was updated for the renamed workflow. After implementation, summarize the files and behavior changed and state that verification was not run.
