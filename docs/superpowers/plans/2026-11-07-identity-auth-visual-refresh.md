# Identity Sign In and Registration Visual Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the identity screens as a responsive green two-panel experience with distinct inline SVG illustrations for sign in, registration, and password recovery.

**Architecture:** Keep authentication state and all API interactions in `IdentityFlow`. A small client shell owns the visual state and composes the right-hand form with the left-hand SVG panel; the existing server page continues to own the brand, restriction flag, and security note.

**Tech Stack:** Next.js App Router, React client components, TypeScript, inline SVG, global CSS.

**Spec:** `docs/superpowers/specs/2026-11-07-identity-auth-visual-refresh-design.md`

## Global Constraints

- No authentication API, route, validation, or policy changes.
- No external image assets or new design dependencies; illustrations are native inline SVG.
- Keep login, registration, OTP, reset, MFA, error, countdown, and restricted-login flows intact.
- Respect `prefers-reduced-motion` and keep decorative SVG out of the accessibility tree.

## Review Focus

- Restricted-login mode must not expose registration or recovery paths; preserve its current conditionals in the same form panel.
- Registration and recovery must change the illustration without clearing or altering existing flow data beyond current transitions.
- OTP and MFA steps must retain the login/register/recovery visual context and remain inside the viewport.
- Small screens must not gain horizontal overflow or place art between a label and its input.
- Reduced-motion mode must remove transitions and illustration animation.

---

### Task 1: Add a client identity screen shell and mode-aware illustrations

**Files:**
- Create: `apps/web/app/identity/identity-screen.tsx`
- Modify: `apps/web/app/identity/page.tsx`
- Modify: `apps/web/app/identity/identity-flow.tsx`

**Interfaces:**
- `IdentityScreen({ restrictedLogin }: { restrictedLogin: boolean })` owns `AuthVisualState = "login" | "register" | "recovery"` and renders the shell, art panel, and form panel.
- `IdentityFlow({ restrictedLogin, onVisualStateChange })` reports `AuthVisualState` when `tab` or `forgot` changes. OTP and MFA continue to use the parent flow's current purpose.

- [x] Add the client shell with a state-aware inline SVG illustration for each of the three visual states.
- [x] Lift the brand-adjacent identity copy and existing form into the new two-panel hierarchy without duplicating page titles.
- [x] Wire `IdentityFlow` state changes to `IdentityScreen`; keep all request and transition handlers unchanged.
- [x] Keep the Argin brand and production/development security note in `page.tsx`.

### Task 2: Style the responsive identity experience

**Files:**
- Modify: `apps/web/app/globals.css`

- [x] Add the wide desktop two-panel shell with green visual panel, light form panel, and distinct layout spacing.
- [x] Style SVG forms, soft geometric accents, and mode transitions without external assets.
- [x] Stack panels on narrow screens and constrain widths to avoid horizontal overflow.
- [x] Add visible keyboard focus and reduced-motion overrides for all new transitions.

### Task 3: Verify the implementation

**Files:**
- No tests are added or run for this task.

- [x] Run `npm run typecheck` in `apps/web`; clean TypeScript check.
- [x] Run `git diff --check`; no whitespace errors.
- [x] Inspect the final diff to confirm the identity API calls and flow conditions were not changed.
