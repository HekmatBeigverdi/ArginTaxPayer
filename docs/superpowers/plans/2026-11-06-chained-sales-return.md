# Chained Sales Return Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Prepare a Production sales-return draft against the latest eligible Original, Correction, or SalesReturn node and preserve the full legal chain.

**Architecture:** Keep the immutable invoice nodes and existing server-side legal checks. Pass the effective node ID from the overview into the draft workflow, source the draft from that node's snapshot/reservation, and make the lineage query recursively return descendants.

**Tech Stack:** .NET 9, EF Core/Npgsql, Next.js/React, Node test runner, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-06-chained-sales-return-design.md`

## Global Constraints

- Follow RC_IITP.IS V7.9 §5.2 p16, §5.4 pp17–18, Table8 p30, and existing `OfficialV79SalesReturnRules`.
- Do not send an invoice or treat a reported `SUCCESS` as final fiscal acceptance.
- Preserve Production/Sandbox storage and fiscal-memory isolation.
- Keep the original row ID for overview grouping; use `effectiveInvoiceId` only as the legal predecessor.
- Do not introduce migrations or schema changes.

## Review Focus

- A Correction predecessor uses its own fiscal ID and corrected line quantities — test predecessor routing and source snapshot.
- A prior SalesReturn predecessor uses its remaining quantities — test the same snapshot-driven path.
- A Cancellation must not be offered or accepted as a predecessor — test UI and service policy.
- Descendants beyond the first relation must appear in order — test recursive lineage query contract.
- Draft and send eligibility remain separate — keep source accepted-predecessor guards intact and do not send in verification.

---

### Task 1: Production sales-return predecessor support

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ProductionSalesReturnService.cs`
- Test: `tests/ArginTaxPayer.Tests/Phase18ProductionSalesReturnChainTests.cs`

**Interfaces:**
- `CanBeSalesReturnPredecessor(InvoiceSubject subject, InvoiceLifecycleState state)` returns true for finalized Original, Correction, or SalesReturn only.
- Open and Submit use the selected predecessor's immutable snapshot, reserved fiscal tax ID, operation, and active-child scope.

- [x] Write tests for supported subject/state combinations and service contract selecting the predecessor snapshot/reference.
- [x] Run focused tests and confirm RED.
- [x] Implement the predecessor policy; make active-child detection include existing correction, cancellation, or return claims where active.
- [x] Run focused tests and confirm GREEN.

### Task 2: UI action follows the effective chain node

**Files:**
- Modify: `apps/web/app/invoices/workspace.tsx`
- Modify: `apps/web/app/invoices/invoice-formal-preview.tsx`
- Modify: `apps/web/app/invoices/invoice-sales-return-modal.tsx` only if the existing modal does not accept predecessor ID unchanged.
- Test: `apps/web/tests/phase18-production-legal-successor-actions.test.mjs`

**Interfaces:**
- Return action is offered for Original, Correction, or SalesReturn subjects regardless of reported dispatch status; the server checks whether the selected node can legally continue. Cancellation is excluded.
- Modal receives `effectiveInvoiceId ?? id`; no browser-supplied fiscal tax ID is added.

- [x] Write UI assertions for Correction action availability, Cancellation exclusion, and effective ID propagation.
- [x] Run focused frontend test and confirm RED.
- [x] Implement the shared UI action and pass the effective node ID.
- [x] Run focused frontend test and confirm GREEN.

### Task 3: Recursive lineage read

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoiceLineageReadService.cs`
- Test: `tests/ArginTaxPayer.Tests/Phase10InvoiceLineageQueryTests.cs`

**Interfaces:**
- The lineage GET starts at the original row ID and returns descendants recursively, scoped by owner and taxpayer, ordered by depth then relation creation order.
- Existing node fields and status semantics remain unchanged.

- [x] Add a failing query-contract test for recursive traversal and cycle protection.
- [x] Run the focused test and confirm RED.
- [x] Implement a recursive CTE over Sandbox and Production relations with the same tenant scope.
- [x] Run focused lineage tests and confirm GREEN.

### Task 4: Verification

- [x] Run focused .NET and web tests for these tasks; full .NET suite passed (1,599 passed, 41 skipped), all focused web suites passed. Full web suite has four unrelated existing failures.
- [x] Build API and run web typecheck.
- [x] Run `git diff --check`; confirm no send endpoint was invoked.
