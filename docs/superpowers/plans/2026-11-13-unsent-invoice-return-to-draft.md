# Unsent Invoice Return to Draft Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the existing rejection flow's “بازگشت به پیش‌نویس” action for an original invoice with no dispatch operation, preserving its invoice ID and system code.

**Architecture:** Add an authenticated service and loopback-only endpoint that validates ownership and absence of any dispatch operation, then reopens the existing same-ID workspace and moves the existing invoice back to Draft with append-only history. Never insert a new workspace or InvoiceDocument. In the invoice detail UI, show the same action section/button pattern used for rejected invoices; after success close details and refresh the table, where the original code appears with “ویرایش و ادامه”.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core/PostgreSQL, Next.js/React/TypeScript.

**Spec:** `docs/superpowers/specs/2026-11-13-unsent-invoice-return-to-draft-design.md`

## Global Constraints

- Preserve prior snapshots and lifecycle history; record any updated snapshot as a new revision of the same invoice.
- Never make an invoice with any dispatch operation eligible for this action.
- Recheck eligibility on the server for every request; UI visibility is not authorization.
- Reuse the workspace model and Persian button style already used by the rejected-invoice return-to-draft section.
- Preserve existing rejection correction behavior.

## Review Focus

- A dispatch operation may appear between initial UI read and action: endpoint must conflict and leave both invoice and workspace untouched.
- Repeated requests after a lost response: return the same editable workspace rather than producing duplicates.
- A malformed/missing source workspace or snapshot mismatch: reject without creating a workspace.
- A returned invoice keeps its same identity and remains editable only while Draft and without dispatch operations.
- A user without current taxpayer ownership: reject regardless of supplied invoice/taxpayer IDs.

## File Structure

- Create `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/UnsentInvoiceDraftService.cs` for authorization, eligibility, source reconstruction and idempotent workspace creation.
- Modify `src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs` to expose a loopback-only POST action with existing API result conventions.
- Modify `src/ArginTaxPayer.Api/Program.cs` to register the service.
- Modify `apps/web/app/invoices/workspace.tsx` to show the same return-to-draft section and button pattern for eligible unsent invoices, then close details and refresh the table without opening the editor.
- Modify an existing focused contract test under `apps/web/tests/` only if an applicable test already exists; do not create or execute tests unless the user asks for testing.

### Task 1: Server-side return-to-draft command

**Files:**
- Create: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/UnsentInvoiceDraftService.cs`
- Modify: `src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs`
- Modify: `src/ArginTaxPayer.Api/Program.cs`
- Create: `apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/return-to-draft/route.ts`

**Interface:** `CreateAsync(string? bearer, Guid taxpayerId, Guid invoiceId, CancellationToken ct)` returns a result with `InvoiceApiDecision`, safe code, workspace ID, and whether the result was an existing workspace.

- [x] Confirm owner and active taxpayer membership using the established `TaxpayerScopeService` authorization pattern.
- [x] Confirm the invoice is still the requested original frozen snapshot in `Draft`, `Validated`, or `Finalized` state, its source workspace/revision and snapshot correspond, and no dispatch operation is reserved, sending, acknowledged, or otherwise present for that invoice.
- [x] Create a deterministic, recoverable editable workspace from the source workspace's `SaveInvoiceEntryRequest`; reset review metadata and use a fresh identity while preserving invoice content and fiscal memory.
- [x] Add loopback-only `POST /api/dev/taxpayers/{taxpayerId}/invoices/{invoiceId}/dispatch/return-to-draft`, returning `workspaceId`, `authoritySubmitted=false`, and normal auth/not-found/conflict errors.
- [x] Add the authenticated same-origin Next.js proxy at `/api/invoice-dispatch/{taxpayerId}/{invoiceId}/return-to-draft`.
- [x] Register the service in `Program.cs`.

### Task 2: Detail UI action

**Files:**
- Modify: `apps/web/app/invoices/workspace.tsx`

**Interface:** Use the endpoint above; on success close the detail dialog, refresh the list and show a Persian notice directing the user to “ویرایش و ادامه”; on conflict show a Persian message explaining that an operation appeared or the invoice is no longer eligible.

- [x] Derive eligibility only for original invoices with no dispatch status/operation and no successful replacement or correction in progress, using the loaded invoice data already available to this view.
- [x] Keep the returned-to-draft item as an editable workspace row with the existing “ویرایش و ادامه” action; do not open the editor until that action is clicked.
- [x] Add the action section using the same classes, icon and loading/error states as the rejected-invoice “بازگشت به پیش‌نویس” section.
- [x] Recheck by calling the endpoint when clicked; do not rely on the UI predicate for permission.

### Task 3: Review implementation

- [x] Review only the new service, endpoint, DI registration and UI diff; preserve any unrelated existing working tree changes.
- [x] Check the final diff for snapshot immutability, authorization, no-dispatch gating, idempotent retry behavior and parity with the existing rejected-invoice button.
- [x] Do not run tests unless the user explicitly requests testing or verification.
