# Invoice Entry Stage Waiting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give users clear, local waiting feedback while data for each invoice entry stage loads.

**Architecture:** Reuse the existing small spinner and status conventions. Add local statuses around the existing async requests in the invoice workspace and keep waiting indicators inside the relevant section so ready controls remain visible.

**Tech Stack:** Next.js, React, TypeScript, existing CSS in `apps/web/app/globals.css`.

**Spec:** `docs/superpowers/specs/2026-11-15-invoice-entry-stage-waiting-design.md`

## Global Constraints

- Show waiting feedback inside the relevant stage and do not cover the whole stage.
- Stage 1 waits independently for seller memory, buyer, and form rules.
- Stage 2 preserves current catalog search behavior and shows its local waiting status.
- Stage 3 keeps history and validation waiting local to each panel.
- Expose waiting text to assistive technology with `role="status"` or `aria-busy`.
- Do not change stage order, save behavior, tax validation, or dialog layout.

## Review Focus

- Switching seller/customer during an in-flight stage 1 request: stale request results must not clear the newer waiting state; existing effect cleanup must remain effective.
- Buyer lookup is not required for invoice type 2 pattern 1: its loading state must clear when lookup is skipped.
- Catalog query or kind changes: keep the latest request's waiting state authoritative.
- Stage 3 history or validation failure: stop waiting and retain existing error behavior.
- Reduced-motion preference: use the existing spinner CSS behavior, which already disables animation.

---

### Task 1: Add local waiting feedback to the three invoice entry stages

**Files:**
- Modify: `apps/web/app/invoices/workspace.tsx`
- Modify: `apps/web/app/globals.css` only if existing spinner classes need a compact inline style.

**Interfaces:**
- Consumes existing `headerLoading`, `buyerLoading`, `catalogBusy`, `InvoiceHistoryPanel` loading state, and `InvoiceValidationPanel` loading state.
- Produces local status text/spinner markup in each relevant section; no new shared component or public API.

- [x] Stage 1: identify the existing request state for header options, buyer search, and form metadata; add visible `role="status"` messages next to the corresponding control groups, including a clear loading state for form metadata.
- [x] Disable only controls whose options are not ready, preserving the ability to edit other fields while requests complete.
- [x] Stage 2: keep the existing `SearchableCatalogPicker` loading behavior and verify its status is visibly and accessibly announced within the catalog section; add compact spinner styling only if needed.
- [x] Stage 3: inspect the history and validation panels' existing loading markup and ensure each waiting message remains inside its own panel, with `aria-busy` on the panel content if missing.
- [x] Run `git diff --check` and inspect the resulting component markup and state transitions.

**Verification constraint:** Do not add or run tests unless requested by the user.
