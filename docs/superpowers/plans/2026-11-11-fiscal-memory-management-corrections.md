# Fiscal Memory Management Corrections Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Restore complete Production and Sandbox fiscal-memory setup, durable visible Production connection results, and admin-reviewed Production memory changes for single-taxpayer time plans.

**Architecture:** Keep Production `fiscal_memories` and Sandbox `sandbox_fiscal_memories` isolated. Production may register one memory alongside Sandbox; Sandbox management gains explicit replacement while preserving old rows and key bindings. A successful Production connection locks the subscription's taxpayer/memory binding; a verified replacement memory can be requested and only Level 1 approval changes that binding. Expose review queues through authorized BFF pages.

**Tech Stack:** ASP.NET Core/.NET 9, EF Core/PostgreSQL, Next.js 15, React 19, node:test, xUnit.

**Spec:** Existing scope and data-preservation decision in `docs/superpowers/specs/2026-11-11-fiscal-memory-connection-workspace-design.md`; this plan extends it for admin-reviewed Production memory replacement.

## Global Constraints

- Work only in the existing macOS checkout; no commit, push, deploy, or live invoice submission.
- Keep Production and Sandbox identifiers, keys, endpoint calls, connection evidence, and restrictions separate.
- Never rebind a single-taxpayer subscription to a different taxpayer; approved change may replace its Production memory only.
- Do not discard old Sandbox rows, existing key envelopes, invoice references, or connection evidence.
- Rebinding requires successful Production authentication for the proposed memory and active Level 1 MFA approval.

## Review Focus

- A Sandbox-only taxpayer must still be able to register a Production memory; a second same-environment Production memory must be rejected.
- Sandbox replacement while existing invoices reference the old row must keep the old row and key usable.
- Production probes that return authenticated, key, or network failures must refresh the displayed last-attempt timestamp; prior success remains visible.
- A one-taxpayer time plan locks changes only after successful Production connection; Sandbox changes remain available.
- Direct API calls, stale approvals, other taxpayers, and unverified memories cannot change an active subscription binding.

### Task 1: Production registration and Sandbox replacement

**Files:** `FiscalMemoryManagementService.cs`, `TaxpayerDevelopmentEndpoints.cs`, taxpayer BFF validation, workspace/panels, Sandbox metadata service/endpoints, migration and focused tests.

**Interfaces:** Add one-per-environment Production registration. Add Sandbox listing and selected-row registration/replacement keyed by stable sandbox memory ID; retain historical records and encrypted key rows.

- [ ] Add failing service/API/UI tests for Production registration with existing Sandbox, duplicate Production denial, Sandbox second memory, and row retention.
- [ ] Add additive migration to allow multiple Sandbox rows per taxpayer and a unique owner+taxpayer+identifier constraint; preserve existing rows/IDs.
- [ ] Add owner-scoped Sandbox list and selected-memory management; key/custody actions target selected stable ID.
- [ ] Expose Production add-memory form whenever no Production memory exists, even with Sandbox present.
- [ ] Run focused tests and typecheck.

### Task 2: Accurate last-attempt and success projection

**Files:** connection endpoint, history service, memory list API/BFF and connection panel/summary; focused xUnit and node tests.

- [ ] Add failing tests that latest Production attempt/time updates on success and failures, while last success remains independent.
- [ ] Persist the safe attempt for every completed authority probe and return connectionRecorded; keep list timestamps synchronized with the scoped history.
- [ ] Refresh selected card after test completion without clearing older success; render recording-unavailable distinctly.
- [ ] Run focused backend/web tests and typecheck.

### Task 3: Production memory replacement request and admin review

**Files:** subscription binding policies/repository/review service/endpoints, subscription BFF routes, new customer/admin components/pages, registration navigation and tests.

- [ ] Add failing policy/integration tests for locked binding after Production authentication, Sandbox exemption, same-taxpayer-only request, verified proposed memory, stale request, and Level 1 MFA decision.
- [ ] Add customer request/read endpoints bound to active single-taxpayer timed plans and current verified binding; permit requests only for another Production memory of the same taxpayer with recorded successful authentication.
- [ ] Extend approval workflow to atomically replace only `FiscalMemoryId` on the existing binding; never change `TaxpayerId`; retain request history and actor audit.
- [ ] Restore the Level 1 pending request queue and customer-visible pending/approved/rejected history in navigation, with same-origin/cookie BFF allowlists.
- [ ] Ensure UI and backend prevent direct Production mutation while locked; do not block Sandbox.
- [ ] Run focused integration/UI tests, full relevant suites, and production build/typecheck.

## Execution

Native implementation in the existing checkout, since the owner explicitly chose local-only changes and no commits. Existing uncommitted changes are user-reviewable and must be preserved.
