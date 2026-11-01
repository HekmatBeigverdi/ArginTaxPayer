# Unified Subscription Pricing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Manage plans, role-scoped tariff versions/history, and Level1 activation from one plan table.

**Architecture:** Extend the existing ASP.NET catalog services/endpoints with scoped plan/pricing reads, atomic immutable price replacement, and atomic Level2 quantity-plan creation. Replace the separate tariff/activation panel in the Next.js plan catalog with row actions and focused RTL dialogs.

**Tech Stack:** .NET 8, EF Core, PostgreSQL, Next.js 15, React 19, TypeScript.

**Spec:** `docs/superpowers/specs/2026-11-01-subscription-plan-pricing-design.md`

## Global Constraints

- Keep plan and price administration in one table; no separate tariff or activation work area.
- Derive actor, role, root, and partner scope on the server; never trust client-supplied identities.
- Price history is preserved; replace the effective row in one serializable transaction, at one UTC instant, and preserve a future version by ending the replacement at its start.
- Existing subscriptions retain their price version and snapshot.
- Level2 custom plans are quantity plans and remain inactive until Level1 sets the base price and activates them.
- Do not add or run automated tests in this task; higher-priority workspace instructions prohibit tests unless requested. Use build/type checks for verification.

## Review Focus

- A Level2 account with zero or multiple roots cannot read or write out-of-scope catalog/prices.
- A root sees partner plans/prices only for partners assigned beneath that root.
- Price replacement preserves exact window boundaries and any future scheduled version.
- Repeated activation or stale rows return a conflict and do not change scope.
- Existing subscription snapshots and historic price rows remain unchanged.

---

### Task 1: Scoped catalog and price reads

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/SubscriptionPlanAdministrationService.cs`
- Modify: `src/ArginTaxPayer.Api/Subscriptions/Phase12PlanAdministrationEndpoints.cs`
- Modify: `src/ArginTaxPayer.Api/Subscriptions/Phase12PriceEndpoints.cs`
- Modify: `apps/web/app/api/phase12/[...parts]/route.ts`

- [x] Add typed plan catalog responses with effective root/partner prices and partner-price counts, enforcing Level1/Level2 catalog visibility on the server.
- [x] Add scoped read operations for historical versions; Level1 can read root and assigned-partner history, Level2 only root base and own history.
- [x] Expose GET routes through the local API and Next BFF allowlist, with no-store behavior and role/MFA checks.
- [x] Build the API and run the web typecheck.

### Task 2: Safe price replacement and custom-plan submission

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/SubscriptionPriceAdministrationService.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/SubscriptionCatalogPersistence.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/SubscriptionPlanAdministrationService.cs`
- Modify: `src/ArginTaxPayer.Api/Subscriptions/Phase12PriceEndpoints.cs`
- Modify: `src/ArginTaxPayer.Api/Subscriptions/Phase12PlanAdministrationEndpoints.cs`

- [x] Validate plan and price scope from the actor's live role and root mapping.
- [x] Replace a current price at one captured `DateTime.UtcNow` in a serializable transaction: close the effective row, append a new version ending at the next scheduled start when present, and preserve old rows/snapshots.
- [x] Restrict Level2 plan creation to quantity kind and save the new inactive plan plus initial partner price atomically.
- [x] Keep Level1-only activation gated on that root's price; return useful HTTP 400/403/404/409 results without exception detail.
- [x] Build the API.

### Task 3: Unified table and role-aware dialogs

**Files:**
- Modify: `apps/web/app/subscriptions/plan-catalog.tsx`
- Modify: `apps/web/app/subscriptions/subscription-workspace.css`
- Modify: `apps/web/app/(console)/subscriptions/page.tsx` only if required to pass the role/catalog props.

- [x] Add base/own price and activation state columns to the existing compact plan table.
- [x] Move price definition/change and Level1 activation into row actions; remove the standalone tariff/activation section.
- [x] Add a read-only tariff-history dialog with validity start/end timestamps and scope-appropriate history.
- [x] Keep Level2 quantity-plan creation with initial own price in one focused dialog and explain pending Level1 review.
- [x] Keep RTL, shared console colors, responsive overflow, loading/empty/error states, and disable repeated submissions.
- [x] Run the web typecheck and build the API; review `git diff` against every spec section.

