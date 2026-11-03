# Admin VAT Settings Implementation Plan

> **For agentic workers:** Apply this plan inline in the current session.

**Goal:** Allow Level 1 administrators to save the software checkout VAT rate and show admin settings at the console's full content width.

**Architecture:** Extend the existing same-origin Phase 12 proxy with the already implemented backend `PUT` operation. Restrict this settings endpoint to Level 1, matching its backend authorization. Give the admin settings page its own width class so account and profile pages keep their current layout.

**Tech Stack:** Next.js App Router, TypeScript, global CSS.

**Spec:** User-approved approach in this conversation: permit Level 1 `PUT` through the proxy; make the admin settings page full-width without changing the account or profile pages.

## Global Constraints

- Preserve existing unsaved workspace changes.
- Keep VAT write access limited to Level 1.
- Do not change account or profile page sizing.

## Review Focus

- Level 2 cannot save VAT settings: enforced by the proxy's Level 1 role check.
- GET remains available for the settings page to load the current rate.
- The page uses available console content width at desktop and mobile sizes.

### Task 1: Allow Level 1 VAT setting updates through the web proxy

**Files:**
- Modify: `apps/web/app/api/phase12/[...parts]/route.ts`

- [ ] Recognize both GET and PUT for `subscriptions/checkout-tax`.
- [ ] Apply the existing Level 1 only policy to this endpoint.
- [ ] Export the PUT route handler so the save request reaches the API.

### Task 2: Make admin settings full-width

**Files:**
- Modify: `apps/web/app/(console)/admin-settings/page.tsx`
- Modify: `apps/web/app/globals.css`

- [ ] Add a page-specific class to the admin settings wrapper.
- [ ] Remove the 980px width cap only for that page, preserving compact settings styling and other pages.

### Verification

- [ ] Run `npm run typecheck` from `apps/web`.
- [ ] Run `git diff --check` and inspect the final diff.
