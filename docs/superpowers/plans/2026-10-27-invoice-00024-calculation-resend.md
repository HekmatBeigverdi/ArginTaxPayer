# Invoice 00024 Calculation and Resend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Correct the legacy Sandbox invoice projection so the fiscal wire uses V7.9 truncation and explicit required fields, expose calculation findings for invoice `00024-05-AAA`, and add a guarded resend action in the invoice detail panel.

**Architecture:** Keep the immutable invoice snapshot and existing dispatch pipeline. Add a server-owned calculation audit beside the existing readiness service; it reads the frozen snapshot, recalculates line/header amounts with `InvoiceFinancialCalculator`, checks the buyer/date/other-tax fields, and returns bounded findings. The BFF forwards only allowlisted findings. The UI renders the audit and routes a valid resend through the existing `prepare-legacy → arm → send` gates.

**Tech Stack:** .NET 8/C#, xUnit, ASP.NET Minimal APIs, Next.js 15/React 19, Node test runner, TypeScript.

**Spec:** `docs/superpowers/specs/2026-10-27-invoice-00024-calculation-resend-design.md`

## Global Constraints

- V7.9 monetary calculation uses truncate-to-rial, not midpoint rounding.
- VAT rate is valid only when backed by the server-side/date-specific catalog resolution.
- `W2` is emitted as an explicit zero when no other tax/legal fee exists.
- Existing tenant scope, immutable revisions, review/finalization, Sandbox-only configuration, and no-auto-retry rules remain mandatory.
- No Production endpoint, private key, provider body, or unbounded snapshot data may reach the browser.

## Review Focus

- A fractional VAT result must truncate consistently in both preview and legacy projection; test a value where AwayFromZero differs.
- A missing buyer person type must produce code `00010` and block resend; test null and valid values.
- A missing/omitted W2 must produce code `00026`, while explicit zero is valid; test both.
- An invalid UTC issue timestamp must produce code `0200201`; test malformed or non-UTC source data.
- Existing `Acknowledged`/`Indeterminate` dispatch state must not allow resend; test the UI guard and backend state gate.

### Task 1: Align legacy projection with V7.9 calculations and required wire fields

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyFrozenInvoiceProjector.cs`
- Modify: `src/ArginTaxPayer.Domain/Invoices/Phase10InvoiceWireMapper.cs`
- Modify: `src/ArginTaxPayer.Domain/Invoices/InvoiceFinancialModel.cs` only if a shared projection helper is needed
- Test: `tests/ArginTaxPayer.Tests/Phase10LegacyFrozenInvoiceProjectorTests.cs`
- Test: `tests/ArginTaxPayer.Tests/Phase10InvoiceWireMapperTests.cs`

**Interfaces:**
- Consumes the existing `StoredInvoice` snapshot and `InvoiceFinancialCalculator` input model.
- Produces a wire projection whose line VAT is the V7.9 truncated value and whose line/header W2 and buyer-person-type fields are explicit where required.

- [ ] Write failing tests for a fractional VAT case, explicit `W2=0`, buyer person type mapping, and invalid non-UTC issue data.
- [ ] Run the focused projector/mapper tests and verify they fail because the current projector rounds AwayFromZero and omits the required fields.
- [ ] Implement the smallest projection change: derive gross/discount/net/VAT/total from the shared truncating calculator, validate snapshot buyer/date fields, and add only the required wire properties.
- [ ] Run the focused tests and verify they pass.
- [ ] Commit with `fix: align legacy invoice calculations with v7.9`.

### Task 2: Add server-owned calculation audit and guarded resend API

**Files:**
- Create: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoiceCalculationAuditService.cs`
- Modify: `src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs`
- Modify: `src/ArginTaxPayer.Api/Program.cs` if DI registration is required
- Test: `tests/ArginTaxPayer.Tests/Phase10InvoiceCalculationAuditTests.cs`
- Test: `tests/ArginTaxPayer.Tests/Phase10DispatchPreflightTests.cs` if the resend state guard belongs in the domain preflight

**Interfaces:**
- `Phase10InvoiceCalculationAuditService.AuditAsync(string? bearer, Guid taxpayerId, Guid invoiceId, CancellationToken ct)` returns `InvoiceCalculationAuditResult` containing `InvoiceApiDecision`, current revision, `CanResend`, and bounded findings `{ Code, Field, MessageKey, Expected, Actual }`.
- Add `GET /api/dev/taxpayers/{taxpayerId}/invoices/{invoiceId}/dispatch/calculation-audit`.
- Add `POST /api/dev/taxpayers/{taxpayerId}/invoices/{invoiceId}/dispatch/resend-check` only if the existing prepare flow cannot atomically perform the check; it must never send by itself.

- [ ] Write failing tests for all five organization codes, valid zero W2, current revision mismatch, and tenant denial.
- [ ] Run the focused tests and verify they fail because no audit service/endpoint exists.
- [ ] Implement scoped read-only audit using the immutable snapshot and shared calculator; map `0204501`, `0304401`, `0200201`, `00010`, and `00026` without trusting client-supplied values.
- [ ] Make the existing preparation/send path reject a non-empty audit finding or a prior `Acknowledged`/`Indeterminate` operation before creating another operation.
- [ ] Run focused .NET tests and verify they pass.
- [ ] Commit with `feat: expose guarded invoice calculation audit`.

### Task 3: Add BFF validation and invoice detail resend controls

**Files:**
- Create: `apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/calculation-audit/route.ts`
- Modify: `apps/web/app/invoices/invoice-send-readiness-panel.tsx`
- Modify: `apps/web/app/globals.css`
- Test: `apps/web/tests/phase10-invoice-calculation-resend.test.mjs`

**Interfaces:**
- BFF GET returns only `{ invoiceRevision, canResend, findings[] }`, with bounded strings and the five allowlisted codes.
- UI consumes the BFF report and adds an accessible button labelled `بررسی محاسبات و ارسال مجدد`; it is disabled for findings, stale revision, `Acknowledged`, `Indeterminate`, non-finalized, or disabled Sandbox states.

- [ ] Write failing Node tests for BFF allowlisting, Persian error rendering, button guard conditions, and no resend on indeterminate/acknowledged state.
- [ ] Run the focused web test and verify it fails because the route and control are absent.
- [ ] Implement the BFF proxy with same-origin/auth checks and the UI refresh/action flow, reusing the existing guarded preparation/send callbacks.
- [ ] Add concise RTL styles for the calculation findings and primary resend control.
- [ ] Run the focused web test, typecheck, and verify they pass.
- [ ] Commit with `feat: add invoice calculation resend control`.

### Task 4: Full verification and handoff

**Files:**
- Modify: `docs/superpowers/plans/2026-10-27-invoice-00024-calculation-resend.md` to mark execution notes only if needed
- Modify: `.superpowers/sdd/.../progress.md` through the execution workflow

- [ ] Run `dotnet test ArginTaxPayer.sln`.
- [ ] Run `npm --prefix apps/web test`.
- [ ] Run `npm --prefix apps/web run typecheck` and `npm --prefix apps/web run build`.
- [ ] Review the diff for scope, security, and the five organization codes; record any deferred minor findings.
- [ ] Report exact verification results and note that the real Sandbox send remains operator-controlled.
