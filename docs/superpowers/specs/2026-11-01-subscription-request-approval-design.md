# Subscription request approval and cash collection

## Intent

Let the assigned Level 2 administrator or a Level 1 administrator review customer plan requests, record optional partial cash/bank receipts, and activate the requested subscription from the finance page.

## Goals

- Put customer requests, collection progress, and approval in the existing «ثبت دریافت» section; remove the redundant manual «سفارش مالی» UI tab.
- Show each order's plan, customer, total, amount received, outstanding balance, and activation state in one compact table.
- Let an administrator record a receipt of any positive amount up to the outstanding balance. Display the updated cumulative received and balance.
- Keep an auditable per-order list of each receipt, including date, method, reference, and amount.
- Make approval independent of payment: an authorized admin may activate an order with zero, partial, or full payment. Outstanding balance remains visible and collectible after activation.
- Limit Level 2 administrators to orders assigned to them. Let Level 1 administrators review, approve, and record receipts for all orders under their root account, including orders attributed to a Level 2 administrator.
- Preserve admin MFA checks and immutable order price/plan snapshots. Record the approving user in the activation audit row.

## Non-goals

- Changing customer checkout, plan pricing, identity login enablement, or payment gateway behavior.
- Forgiving or changing the amount owed when a plan is approved.

## Approaches

- **Chosen: one finance inbox.** Add receipt entry and an explicit «تأیید و فعال‌سازی» action to the existing «ثبت دریافت» table. It keeps the full request and payment context together and makes the no-payment approval case explicit.
- Separate approval and collection pages. This would split the same customer request across two workflows, so it does not fit the requested experience.

## Behavior and implementation calls

- Approval creates the subscription once regardless of the amount collected. It does not mark the commercial order as paid; collection status and outstanding balance remain governed by confirmed receipts.
- After approval, the order stays in the inbox while a balance remains, so administrators can continue recording receipts. Its approval state prevents a second activation.
- Level 1 scope includes orders with a matching root administrator snapshot or a current customer ownership row for that root; Level 2 scope is restricted to orders attributed to that user.
- Rename the activation audit column to a role-neutral approver field because both admin levels can approve.
- Keep the existing order creation API available for internal compatibility, but remove its redundant manual creation/activation tab from this finance UI.

## Constraints

- Build on the existing customer checkout orders, manual collection service, activation service, and Next.js finance workspace.
- Keep manual cash/bank receipts auditable and disallow gateway receipts through this flow.
