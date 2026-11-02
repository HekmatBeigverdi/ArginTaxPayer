# Customer Subscription Experience Implementation Plan

> **For agentic workers:** Apply this plan inline in the current session.

**Goal:** Make customer subscription navigation clear and give customers a polished, separate plan-selection page.

**Architecture:** Keep the existing `/customer-plans` and `/financial-history` routes and checkout API. Move customer entry points into the account dropdown, place compact subscription status beside the logo, remove subscription links from the profile, and restyle plan selection as accessible responsive cards with a selected-order summary.

**Tech Stack:** Next.js App Router, React, TypeScript, existing Argin console CSS.

**Spec:** `docs/superpowers/specs/2026-11-01-customer-subscription-navigation-design.md`

## Global Constraints

- Keep administrator navigation and `/subscriptions` management unchanged.
- Use the existing customer-owned checkout routes and payload.
- Keep online payment unavailable; never imply an order is paid or active before server confirmation.
- Keep customer plan and history links available from the account menu.
- Support mobile widths without horizontal overflow.

## Review Focus

- Customer navigation to the two separate routes: retain the Customer-only plan route and the current-user-scoped finance route.
- Profile completion gate: retain the existing `/profile` requirement and routing behavior.
- Plan order submission: preserve the operation key, cash pending order request, official-invoice choice, and status notice.
- Unavailable online payment: present as unavailable and keep order submission on the supported cash flow.
- Narrow viewport selection: cards and order summary must stack without clipping.

### Task 1: Move customer subscription links and status into the account header/menu

**Files:**
- Modify: `apps/web/app/components/console-shell.tsx`
- Modify: `apps/web/app/components/subscription-status.tsx`
- Modify: `apps/web/app/(console)/profile/page.tsx`
- Modify: `apps/web/app/(console)/financial-history/page.tsx`

- [x] Remove the customer finance route from the side navigation.
- [x] Add account-menu links for `/customer-plans` (Customer role only) and `/financial-history` (non-admin accounts); include these destinations in the active header title.
- [x] Place compact subscription status to the right of the account/logo group in the top bar.
- [x] Remove plan/history teaser links from the profile and give the separate finance page its account-facing title.

### Task 2: Redesign the customer plan-selection page

**Files:**
- Modify: `apps/web/app/customer-plans/customer-plans.tsx`
- Modify: `apps/web/app/(console)/customer-plans/page.tsx`
- Modify: `apps/web/app/globals.css`

- [x] Replace inline radio tiles with accessible selectable plan cards showing plan type, term or invoice quota, and price.
- [x] Add selected-plan summary, official-invoice option, supported cash-pending choice, clear unavailable online-payment state, and order action.
- [x] Preserve existing API payload, operation-key refresh on selection, server-calculated VAT copy, and pending-order notice.
- [x] Add responsive styling for plan cards, summary, alerts, and payment choice using the shared console palette.

### Verification

- [x] Run `npm run typecheck` from `apps/web`.
- [x] Run `git diff --check` and inspect changed routes and components.
