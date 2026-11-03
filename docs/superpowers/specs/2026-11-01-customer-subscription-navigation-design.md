# Customer Subscription Navigation and Plan Selection

## Intent

Make subscription actions easy for customer users to find, and make plan choice easier to compare and understand before ordering.

## Goals

- Move the compact current-plan/trial status from the strip above the header to the right of the account logo in the top bar.
- Put customer-owned payment/subscription history in the non-admin “حساب من” menu, and show the software-plan link there for Customer users.
- Keep plan selection at `/customer-plans` and payment history at `/financial-history`; remove their links and subscription prompt from the profile page.
- Replace the plain radio-card list with a responsive, polished plan-card selector that clearly shows plan type, term or invoice quota, price, selection state, and the order action.
- Keep the existing server-side checkout, cash-pending flow, official-invoice option, and online-payment safety behavior.

## Non-goals

- Change plan prices, plan eligibility, or checkout authorization and persistence.
- Change administrator navigation or the admin subscription management page.
- Add new payment methods or imply that an order is paid or activated before server confirmation.

## Approach

Use plan cards with a clear selected state and a visible order summary/action. A comparison table was considered, but the existing plan data is more readable as individual choices and adapts better to mobile. The user selected the card approach.

## Experience

- In the top bar, show the compact plan status alongside the account control, immediately to the right of the logo.
- In the customer account menu, show “پلن‌های نرم‌افزار” and “پرداخت‌ها و اشتراک‌ها” as separate destinations.
- Keep the profile page focused on identity information.
- On the plan page, let a customer select one plan card, review its term or quota and price, choose the official-invoice option, and submit a cash-pending order. Clearly label unavailable online payment and keep the existing disabled behavior.
- Preserve server-computed tax, the order identifier, and the notice that submission does not mean payment or activation.

## Constraints

- Use the existing Next.js routes, customer checkout API, role guards, and application visual system.
- The plan link is Customer-only; the financial-history API returns rows scoped to the current account. Administrator subscription management stays in its current navigation.
- Support narrow screens without horizontal page overflow.

## Implementation choices left to the builder

- Card spacing, color, icons, and responsive breakpoints, following existing Argin console styles.
- Whether selected-order details sit inside the selected card or in a compact summary next to the card grid, while keeping the action clear on mobile.
