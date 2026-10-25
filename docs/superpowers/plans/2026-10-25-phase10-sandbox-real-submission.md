# Phase 10 Sandbox Real Submission Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the existing Phase 10 architecture for owner-authorized Sandbox invoice finalization, durable one-shot submission, receipt persistence and inquiry without enabling Production or agent-run live transmission.

**Architecture:** Extend the existing invoice review, fiscal serial, dispatch persistence, custody, Sandbox-v2 transport, inquiry and BFF/UI boundaries. Persist immutable operation/packet identity before HTTP, use strict Sandbox endpoint and credential gates, and append sanitized receipt/inquiry evidence.

**Tech Stack:** ASP.NET Core 9, EF Core/PostgreSQL, xUnit, Next.js/TypeScript, Node test runner, existing protected key custody and local AutoSSH PostgreSQL tunnel.

**Spec:** `docs/superpowers/specs/2026-10-25-phase10-sandbox-real-submission-design.md`

## Global Constraints

- Sandbox only; Production endpoints and records remain unreachable.
- No real transmission by Codex; operator performs it after local gates and explicit invoice approval.
- No fabricated credentials, certificates, authority keys, receipts or final acceptance.
- No private-key logging, export, browser exposure or replacement.
- Apply no migration until database identity, migration state and backup are verified and operator-approved.
- Preserve tenant isolation, RBAC, consent binding, append-only audit and no-blind-retry policy.
- Reuse existing Phase 10 architecture; do not create a parallel submission system.

## Review Focus

- Stale invoice revision: finalization/send must reject it; test in Task 1.
- Owner/customer or taxpayer mismatch: no serial, packet, outbox or HTTP side effect; test in Task 1.
- Invalid/missing official item, VAT, settlement, fiscal memory or certificate evidence: actionable readiness denial; test in Tasks 1–3.
- Timeout/connection drop after POST: persist indeterminate state and prohibit blind retry; test in Task 4.
- Provider response with wrong/missing UID/reference: persist sanitized evidence but never mark accepted; test in Task 4.

### Task 1: Finalization and owner-consent-bound admission

> **Current implementation boundary (2026-10-25):** The owner-scoped, server-owned
> admission gate is implemented and tested. Internal finalization remains fail-closed
> until the existing invoice pipeline persists a trusted, revision-pinned validation
> receipt and application approval; this task is not marked complete by the admission
> endpoint alone.

**Files:**
- Modify: existing `Phase10LiveSendReadinessService`, invoice review/finalization service and `SandboxSingleInvoiceConsentGuard` boundaries.
- Test: existing Phase 10 preflight, consent, dispatch contract and database-contract test files; add focused tests where needed.

- [ ] Write failing tests for owner-only consent, exact invoice/revision binding, stale revision rejection and zero side effects on readiness denial.
- [ ] Run focused tests and confirm the new cases fail for the missing finalization/admission behavior.
- [ ] Implement the smallest server-side finalization/admission orchestration using existing authorization and immutable revision services.
- [ ] Run focused tests and confirm all pass.
- [ ] Commit `feat(phase10): bind sandbox admission to finalized owner invoice`.

### Task 2: Fiscal ID, certificate/key binding and immutable packet construction

**Files:**
- Modify: `SandboxInvoiceFiscalSerialRepository`, `Phase10FiscalInvoiceIdentifier`, existing custody/certificate services and packet cryptography integration.
- Test: fiscal identifier, certificate metadata, Sandbox cryptography, key isolation and serial durability tests.

- [ ] Add failing tests proving finalized revision hash, Sandbox memory, serial and certificate/public-key binding are required and exact packet hashes are stable.
- [ ] Run focused tests and verify RED.
- [ ] Implement packet construction through protected custody, strict certificate evidence, JWS/JWE generation and immutable packet metadata; never expose private material.
- [ ] Run focused tests and verify GREEN.
- [ ] Commit `feat(phase10): build immutable sandbox invoice packet`.

### Task 3: Durable outbox and Sandbox transport wiring

> **Current implementation boundary (2026-10-25):** The immutable encrypted
> packet is now committed to the existing durable outbox in a database transaction
> with Sandbox-only metadata, deterministic deduplication and no automatic retry.
> The external HTTP worker remains disabled until operator-supplied Sandbox
> credentials, nonce authentication and a verified live endpoint are available.

**Files:**
- Modify: dispatch admission repository, outbox writer/worker boundary, `IInvoiceSubmissionTransport`, `SandboxV2InvoiceHttpTransport`, API registration and endpoint allowlist configuration.
- Test: dispatch persistence, outbox integration, transport, endpoint-origin, duplicate/concurrency and no-Production tests.

- [ ] Add failing tests proving outbox persistence precedes HTTP, same revision is idempotent, conflicting revision is denied and Production/redirect/insecure endpoints never execute.
- [ ] Run focused tests and verify RED.
- [ ] Implement one-shot Sandbox send wiring with explicit operator consent, durable operation state, no automatic retry and sanitized response handling.
- [ ] Run focused tests and verify GREEN.
- [ ] Commit `feat(phase10): connect finalized sandbox invoice to durable outbox`.

### Task 4: Receipt, inquiry and reconciliation

> **Current implementation boundary (2026-10-25):** Append-only evidence
> persistence and state-revision CAS are implemented for initial receipts;
> inquiry evidence is recorded without claiming fiscal acceptance. The live
> inquiry adapter remains disabled until the official endpoint and credentials
> are operator-verified.

**Files:**
- Modify: `Phase10AuthorityResponseHandoff`, `Phase10InquiryReconciliation`, dispatch fault/history repositories and API routes.
- Test: response parser, inquiry/reconciliation, timeout/recovery, duplicate response and immutable history tests.

- [ ] Add failing tests for correlated acknowledgment, missing/wrong reference, indeterminate outcome and subsequent inquiry.
- [ ] Run focused tests and verify RED.
- [ ] Implement append-only initial receipt and inquiry evidence persistence with final status only from correlated authoritative evidence.
- [ ] Run focused tests and verify GREEN.
- [ ] Commit `feat(phase10): persist sandbox receipts and inquiry evidence`.

### Task 5: Existing Persian RTL dispatch UI

**Files:**
- Modify: `apps/web/app/invoices/invoice-send-readiness-panel.tsx`, `invoice-dispatch-panel.tsx`, `invoice-dispatch-history-panel.tsx`, BFF routes and related styles.
- Test: Step22 readiness, dispatch UI/history, tenant/RBAC and no-network-from-browser suites.

- [ ] Add failing UI tests for actionable blockers, owner confirmation, finalized revision/fiscal memory display, progress, reference and inquiry action.
- [ ] Run web tests and verify RED.
- [ ] Connect the UI to server-side finalization/send/inquiry routes while preserving compact RTL modal conventions and no browser credentials.
- [ ] Run web tests, typecheck and build; verify GREEN.
- [ ] Commit `feat(phase10): connect sandbox dispatch UI lifecycle`.

### Task 6: Database gate, integration verification and operator handoff

**Files:**
- Create/modify: local verification script and Phase 10 documentation only as needed.
- Test: complete .NET and web suites, PostgreSQL integration, concurrency/network simulations and protocol compatibility checks.

- [ ] Verify database name, migration state and backup without changing schema.
- [ ] Apply migration only after explicit operator approval if the implementation requires one; otherwise retain the existing schema.
- [ ] Run complete verification: `.NET build/test`, web test/typecheck/build, focused offline gate, PostgreSQL integration and no-secret audit.
- [ ] Resolve discovered errors and rerun the failed command, then the full suite.
- [ ] Document exact operator commands for invoice `00010-05-AAA`, explicit approval prompt, displayed taxpayer/memory/revision/environment, one-shot Sandbox send and inquiry.
- [ ] Commit `docs(phase10): document sandbox submission operator handoff`.

## Final verification checklist

- [ ] `git diff --check` passes.
- [ ] Worktree is clean at the tested SHA.
- [ ] No Production endpoint or database was used.
- [ ] No credential/private-key value appears in logs, diffs or artifacts.
- [ ] Local tests pass with counts recorded.
- [ ] Real Sandbox transmission is reported separately as operator-run evidence, not inferred from local tests.
- [ ] Fiscal acceptance is reported separately from HTTP acknowledgment and remains pending without authoritative inquiry evidence.
