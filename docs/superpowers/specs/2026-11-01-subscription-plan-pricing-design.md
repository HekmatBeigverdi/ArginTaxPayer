# Subscription plans and role-scoped pricing

## Intent

Manage plan terms, direct prices, partner prices, and partner-created quantity plans from one compact subscription-plan table. Level1 controls the main catalog and base prices. Level2 sees the base and its own effective price, may raise or lower its own price, and may submit a partner-specific quantity plan for Level1 price approval and activation.

## Goals

- Show a plan's effective price beside its terms and status.
- Let Level1 create and change root-direct prices and review Level2 prices and quantity plans.
- Let Level2 set its own partner price on a Level1 plan and create a quantity plan with an initial partner price.
- Keep plan visibility and every price scoped to the authenticated administrator and its assigned root.
- Preserve historical price versions and the price snapshots of existing subscriptions when prices change.
- Keep plan and price administration in one table, using row actions and focused dialogs rather than a separate tariff form.

## Non-goals

- Payment collection, settlement, or customer purchase flows.
- Bulk pricing, custom tax/fee calculations, scheduled future tariffs, or discount percentages.
- Editing another Level2 administrator's price or exposing customer records in a partner-price summary.
- Replacing existing price-version history or rewriting existing subscriptions.

## Existing behavior and constraints

The catalog has server-prefixed plan codes, immutable subscription price-version rows with `ValidFromUtc`/`ValidUntilUtc` validity ranges, `RootDirect` and `Partner` audiences, and purchase-time subscription snapshots. This means price history is already represented in storage, although the UI has no read path or history view yet. Level1 creates root-owned plans and prices. Level2 can create prefixed plans and partner prices, while activation requires a root-direct price. Current plan listing only returns plans whose code prefix matches the caller, and the current price form always starts an unbounded window at the current time. A second overlapping price therefore conflicts instead of updating the effective price. There is no database column recording a plan creator; the authenticated UUID in the plan-code prefix is the existing catalog ownership key.

Commercial ownership already maps a Level2 account to its Level1 root. Use that live mapping for list visibility, partner-price ownership, plan submission, and Level1 review. If a Level2 account maps to no unique active root, deny partner pricing and custom-plan creation with a clear UI message.

## Approaches

1. Keep a separate tariff form beside the plan list. This requires switching context and selecting a plan independently, and it does not address the missing price-read path.
2. Use one role-aware plan table with a small row-action dialog, and extend the catalog API to return only the relevant direct and partner price versions. **Chosen:** the user selected this approach because the existing table has space and the user wants prices and pricing actions beside each plan.
3. Edit prices directly inside table cells. This makes effective-date changes hard to review and increases accidental updates; omit it.

## Roles and catalog scope

### Level1

- Lists its own base plans and quantity plans submitted by Level2 administrators currently assigned under it.
- Sees its current root-direct price for each plan, plus a row action for partner-price details. Partner details show the Level2 mobile and that partner's effective price, without customer data.
- Creates base plans and changes only the root-direct price.
- Reviews a Level2-submitted quantity plan. The plan stays pending and unavailable for activation until Level1 sets its root-direct base price; Level1 can then activate it.

### Level2

- Lists active base plans owned by its unique Level1 root and plans it created itself.
- For a base plan, sees the root-direct price and its own current partner price, if one exists. It can set or replace only its own partner price.
- Creates only quantity plans scoped to itself and its unique Level1 root. Creation includes its code, unit limit, validity, and initial partner price in one API operation. The new plan remains pending until Level1 sets a base price and activates it.
- Cannot change the root-direct price, activate a plan, or see another partner's prices.

The table identifies the price audience explicitly: “مبنای مدیر سطح ۱” and “تعرفهٔ من” for Level2; a root-direct price with “تعداد تعرفه‌های اختصاصی” for Level1. A Level1-only details dialog lists each associated Level2 price by mobile and amount. Draft/pending plans display their review state so they are distinguishable from active catalog plans.

## Price-version behavior

Opening “تعریف/تغییر تعرفه” from a plan row preselects that exact plan and the actor's allowed audience. The dialog displays the current amount and says the new amount takes effect immediately. On save, the backend opens a serializable transaction, rechecks live role, session, MFA, plan visibility, and audience, ends the current effective price window for that exact `(plan, root, partner, audience)` scope at one captured UTC instant, and inserts a new version from that instant. If a future version is already scheduled, the replacement ends at that version's start time so the existing schedule remains intact. Do not update or delete previous price rows except setting the end boundary of the replaced effective version. Existing subscription rows keep their `PriceVersionId` and price snapshot.

Price reads return the effective version for the request instant (`from <= now < until`). If no price is currently effective, show “تعرفه تعریف نشده” and the appropriate price action. Return all Level2 prices only to the owning Level1 root, in a separate endpoint or scoped response designed for the details dialog. Historical versions are read-only and displayed newest first with amount, audience, start date, end date (or “در حال اجرا”), and current/expired state. Level2 may see its own partner-price history and the root-direct base-price history, but never another partner's history. Level1 may inspect root-direct history and each associated partner's history within its own root scope.

## UI behavior

- One compact table is the sole workspace for plan, price, and activation administration for both roles. Preserve plan search and create/view/edit/archive behavior. Do not retain separate tariff or activation forms/sections elsewhere on the page.
- Show current base/partner tariff and activation state in the plan row. Put “تعریف/تغییر تعرفه”, “تاریخچه تعرفه”, and, for Level1 only, “فعال‌سازی” together in that row's operations cell. Level2 price actions appear only on plans visible within its root scope.
- Use a focused RTL dialog showing the plan code, the price audience, the current amount, the new amount in IRR, and the immediate effective-time rule. If a future version exists, explain that the new price ends when that version begins. Disable repeat submission and keep the row current after success.
- The row's “تاریخچه تعرفه” action opens a read-only RTL dialog or drawer. Let Level1 switch between root-direct and an individual partner's history; Level2 sees only its root's base history and its own partner history. Show validity boundaries clearly and preserve the full timestamps alongside a readable local date/time.
- Add one Level2-only “پلن تعدادی من” dialog with code, unit limit, validity, and initial own price. State clearly that it remains pending until Level1 adds the base price and activates it. Do not silently activate partner plans.
- Activation is an inline row action using a brief confirmation that names the plan and its current root-direct price. Keep the activation state in the same row; require root price before activation and refresh that row on success.
- Reuse console colors, compact table, shared button, and modal styles. Keep desktop content aligned to the full console width and make the table horizontally usable on small screens.

## API and persistence approach

- Extend the Level1/Level2 plan listing so it applies server-side visibility rules and returns role-appropriate effective-price metadata. Never trust a client-provided root or partner ID.
- Add a Level1-only scoped read for partner-price details, or equivalent data in a distinct Level1 response that cannot be fetched by Level2.
- Add a transactional price-replacement service/API operation for the caller's one permitted price audience.
- Add an atomic Level2 quantity-plan submission operation that validates the single-root scope and writes the partner-owned plan plus its initial partner price together. Keep it inactive pending Level1 base pricing and activation.
- Use the existing plan code prefix for creator scope and the existing `CustomerCommercialOwners` mapping for Level2-to-Level1 scope. No schema migration is planned.
- Keep the existing root-only activation rule. Map duplicate, overlap, stale, and not-in-scope outcomes to clear 400/403/404/409 responses; never return sensitive exception detail.

## Open implementation choices

- Use a dedicated response record instead of anonymous objects if that makes the role-scoped plan/price response clear and testable.
- Select a brief existing partner mobile label for Level1 price details; do not return full names or customer account data.
- Capture a single `DateTime.UtcNow` per replace operation and use it to close and open the two version boundaries exactly.

## Approval requested

Please approve this revised design before implementation. Tariff creation/change, tariff history, and Level1 activation are all available from the same plan table row; there is no separate tariff or activation work area. The UI exposes the validity history already represented by immutable price-version records.
