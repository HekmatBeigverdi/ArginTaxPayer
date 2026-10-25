# Phase 10 Legacy Sandbox Readiness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prepare one owner-scoped Sandbox invoice through the legacy submission path, while keeping fiscal identifiers, signing evidence, owner approval, and authority receipts evidence-based.

**Architecture:** Build a server-owned sequence from finalized immutable invoice revision to transactional serial reservation, legacy packet preparation, durable outbox admission, and an explicitly owner-approved Sandbox send. The v2 certificate/JWS/JWE path remains deferred. No production endpoint or fabricated authority result is permitted.

**Tech Stack:** ASP.NET Core/.NET 9, EF Core/PostgreSQL, Next.js/TypeScript, legacy `SELF_TSP` transport contract, xUnit and Node tests.

**Spec:** `docs/superpowers/specs/2026-10-25-phase10-legacy-sandbox-submission-design.md`

## Global Constraints

- Sandbox only; Production endpoints are unreachable through configuration and code allowlists.
- Legacy `SELF_TSP` framing is used for this path; v2 certificate/JWS/JWE validation remains deferred.
- Never fabricate a fiscal identifier, serial, signature, receipt, or authority acceptance.
- Persist dispatch admission/outbox evidence before any external HTTP call.
- Owner, taxpayer, fiscal-memory, invoice revision, and private-key scope must be checked server-side.
- Network calls require explicit operator configuration and are not performed by offline tests.

## Review Focus

- Draft or changed invoice revision: must stop before serial reservation and packet creation.
- Missing or mismatched Sandbox fiscal memory/key custody: must deny without network activity.
- Duplicate or conflicting revision: must be idempotent/denied, never double-send.
- Legacy transport misconfiguration: must fail closed and never reach Production.
- Timeout or malformed authority response: must produce an indeterminate result requiring reconciliation.

### Task 1: Evidence inventory and invoice finalization gate

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LiveSendReadinessService.cs`
- Test: `tests/ArginTaxPayer.Tests/Phase10LiveSendReadinessTests.cs`
- Document: `docs/phase10-sandbox-operator-handoff.md`

**Interfaces:**
- Consumes the existing owner-scoped invoice snapshot, lifecycle state, and Sandbox memory binding.
- Produces an explicit readiness report distinguishing missing evidence from protocol blockers.

- [x] Add tests proving Draft/missing immutable snapshot blocks without mutating the database.
- [x] Implement evidence-derived readiness fields and preserve legacy-only protocol diagnostics.
- [x] Run focused readiness tests and the existing Phase 10 web contract tests.
- [x] Record the exact invoice/taxpayer identifiers and unresolved operator evidence in the handoff.

### Task 2: Transactional Sandbox serial and fiscal-identifier preparation

**Files:**
- Modify: existing fiscal serial repository/service identified by Task 1 inventory.
- Test: `tests/ArginTaxPayer.Tests/Phase10SandboxSerialTests.cs`
- Modify: related EF model/migration only if an existing additive schema is insufficient.

**Interfaces:**
- Consumes a finalized invoice revision and valid Sandbox fiscal-memory identifier.
- Produces one durable serial reservation and a deterministic fiscal-identifier request/value only when authority rules permit it.

- [ ] Add red tests for duplicate reservation, cross-tenant reservation, and non-finalized invoice.
- [x] Implement transactional reservation using the existing database uniqueness/CAS boundary.
- [x] Register `SandboxInvoiceFiscalSerialRepository` in the development persistence boundary.
- [ ] Keep authority-issued values distinct from locally generated correlation values.
- [ ] Run persistence tests against the isolated test database/read-only audit policy.

### Task 3: Legacy packet preparation and signing custody

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Signing/Connection/LegacyInvoiceSubmissionHttpTransport.cs` only for transport input contracts if required.
- Create/modify: legacy packet builder and protected packet persistence identified by Task 2.
- Test: `tests/ArginTaxPayer.Tests/Phase10LegacyPacketTests.cs`

**Interfaces:**
- Consumes the immutable invoice snapshot, reserved serial, fiscal-memory binding, and protected private-key custody.
- Produces opaque packet bytes/hash plus the legacy transfer signature; plaintext private keys and secrets never leave custody.

- [x] Add red tests for key mismatch, changed revision, malformed payload, and deterministic packet hash.
- [x] Implement the legacy serialization/signature boundary using the existing custody abstraction.
- [x] Verify no private material appears in API responses or logs.
- [x] Run focused packet/signature tests and secret-scan checks.

**Task 3 boundary:** packet construction is complete and tested offline. Packet
persistence and Outbox admission remain Task 4; no external request is made by
this task.

**Current investigation result (2026-10-25):** the repository already contains
an offline legacy RSA/PKCS#1 signer and protected-key custody boundary. The
existing `InvoiceSnapshotSigningService` signs the immutable snapshot JSON for
the Phase09 signing workflow, but it is not yet a Phase10 authority packet
builder and cannot be treated as a submission packet. The existing
`Phase10WireSignatureBinding` explicitly returns
`invoice.signing.wire_cryptographic_verification_pending`. Therefore this task
must add a legacy authority-payload builder and persist its exact signed bytes;
reusing the Phase09 snapshot signature would be incorrect.

### Task 4: Durable Outbox admission and idempotency

**Files:**
- Modify: `src/ArginTaxPayer.Application/Invoices/Phase10SubmissionTransport.cs`
- Modify: `src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs`
- Modify: existing Phase 10 dispatch persistence services.
- Test: `tests/ArginTaxPayer.Tests/Phase10LegacyOutboxAdmissionTests.cs`

**Interfaces:**
- Consumes a prepared legacy packet and owner-approved invoice revision.
- Produces one durable dispatch operation/outbox record before transport execution.

- [ ] Add red tests for same-revision replay, conflicting revision, cross-tenant access, and persistence-before-HTTP.
- [ ] Implement the admission transaction and connect the legacy transport behind the strict Sandbox allowlist.
- [ ] Keep the send endpoint unavailable unless all evidence gates pass.
- [ ] Run concurrency, persistence, and transport contract tests.

**Current implementation result (2026-10-25):** legacy packet admission now
has a separate durable outbox preparation method and registration for the
existing dispatch admission repository. The payload records the exact request
hash and explicitly records `accessTokenStored=false`; the sender/credential
custody boundary is not bypassed. External transport execution and a real
invoice POST remain out of this task's completed scope until token refresh and
receipt persistence are wired.

### Task 5: Owner approval and local send control

**Files:**
- Modify: `src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs`
- Modify: `apps/web/app/invoices/invoice-send-readiness-panel.tsx`
- Create/modify: BFF send/approval route and UI confirmation component.
- Test: `apps/web/tests/phase10-legacy-send-gate.test.mjs`

**Interfaces:**
- Consumes the readiness report and authenticated owner confirmation for the exact taxpayer/invoice/revision.
- Produces a scoped send request; no request is issued when any gate is missing.

- [ ] Add red tests for absent approval, stale revision, wrong tenant, and origin failure.
- [ ] Implement explicit owner approval and a fail-closed UI confirmation.
- [ ] Keep Production unreachable and never expose credentials/packet bytes to the browser.
- [ ] Run web typecheck, web tests, and API authorization tests.

### Task 6: Sandbox receipt and reconciliation evidence

**Files:**
- Modify: existing receipt/history/recovery services.
- Test: `tests/ArginTaxPayer.Tests/Phase10LegacyReceiptTests.cs`

**Interfaces:**
- Consumes only a persisted dispatch operation and sanitized legacy response.
- Produces receipt/indeterminate evidence without claiming fiscal acceptance from HTTP acknowledgment alone.

- [ ] Add red tests for valid reference, malformed response, timeout, and duplicate receipt.
- [ ] Implement sanitized receipt persistence and reconciliation-required state.
- [ ] Run focused tests; perform a live Sandbox call only after the operator supplies explicit same-session approval and current endpoint evidence.

### Task 7: Full gate and operator handoff

**Files:**
- Modify: `docs/phase10-sandbox-operator-handoff.md`
- Modify: `scripts/phase10-sandbox-v2-offline-gate.sh` only if the legacy gate needs a separate offline check.

- [ ] Run .NET build/tests, web typecheck/tests/build, health checks, and read-only DB audit.
- [ ] Verify exact SHA and clean worktree except documented changes.
- [ ] Document remaining manual gates and the exact command the operator may run.
- [ ] Do not report “sent” until a sanitized authority receipt and reconciliation evidence exist.

