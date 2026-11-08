# Production Invoice and Legal Operations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve the owner-tested Production original send and Sandbox operations, and enable owner-led Production tests for correction, cancellation, and sales return.

**Architecture:** Preserve the existing legacy SELF_TSP sender verified by the owner for both Production and Sandbox, with the persisted memory choosing its pinned endpoint. Reuse the Sandbox legal rules and UI behavior while implementing Production-specific successor persistence, lineage, and dispatch binding. Keep SDK family/version explicit behind a provider contract so a future SDK generation can be added without changing fiscal rules or silently replacing the tested sender.

**Tech Stack:** .NET 8, C#, EF Core, PostgreSQL, supplied TaxCollectData SDK source/package references (legacy 0.0.x and modern 2.0.32), Next.js BFF and existing Node contract tests.

**Spec:** `docs/superpowers/specs/2026-11-06-production-invoice-legal-operations-design.md`

## Global Constraints

- Use RC_IITP.IS V7.9 for fiscal-document rules; SDK DTOs never define legal rules.
- Consult both supplied SDK generations and their guides; never treat legacy 0.0.34 and modern 2.0.32 as interchangeable.
- Current Production and Sandbox protocol: existing legacy SELF_TSP adapter, with the owner-confirmed Production original-send path preserved.
- Sandbox remains on its existing service, isolated tables and credentials; do not point Sandbox services at Production.
- Environment and predecessor relationships come from persisted scoped records, never caller-provided host/environment values.
- Unknown post-send results remain Indeterminate; do not retry or resend automatically.
- No live authority request, live invoice, Production enablement, key migration, or deployment activation in implementation tests.
- Keep SDK family, package version, API contract version and route profile explicit; unknown selections fail closed.

## Review Focus

- Production must remain pinned to `tp.tax.gov.ir` and Sandbox to `sandboxrc.tax.gov.ir`, both selected from persisted memory; prove cross-environment rejection.
- Existing Production original sends must retain behavior; test its classified preparation/dispatch contracts without sending a live invoice.
- A queued/acknowledged invoice is not fiscally accepted; test pending and indeterminate states cannot authorize a legal successor.
- Concurrent correction/return claims and repeated cancellation attempts must remain exclusive; test database admission/idempotency.
- A future SDK version must not change behavior without explicit adapter/version selection; test unknown versions fail closed and both supported version contracts remain pinned.

---

### Task 1: Versioned authority provider boundary for the tested legacy protocol

**Files:**
- Create: `src/ArginTaxPayer.Application/Invoices/IAuthorityInvoiceProvider.cs`
- Create: `src/ArginTaxPayer.Infrastructure/Signing/Connection/AuthorityProtocolProfile.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18AuthorityEndpointRoutingService.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/DependencyInjection.cs`
- Test: `tests/ArginTaxPayer.Tests/Phase18AuthorityProtocolProfileTests.cs`

**Interfaces:**
- A versioned protocol profile is recorded in each new prepared operation/outbox; supported profile is the existing legacy SELF_TSP contract, with environment-pinned endpoints.
- Unknown profiles fail closed; the future modern 2.0.x family is documented as a separate adapter, not active.
- Do not change the tested Production sender's wire behavior.

- [ ] **Step 1: Write failing tests** proving explicit legacy profile selection, Production/Sandbox origin pinning from persisted operation environment, and unknown profile rejection.
- [ ] **Step 2: Run tests and confirm the expected failures.**
- [ ] **Step 3: Add the smallest versioned protocol-profile boundary** used by newly prepared Production legal-successor operations; do not alter the current original send.
- [ ] **Step 4: Record SDK evidence/version and profile in sanitized operation metadata** without storing secrets or invoice payloads in logs.
- [ ] **Step 5: Run focused profile and existing endpoint-selector tests.**
- [ ] **Step 6: Commit Task 1** as `feat: add versioned authority protocol profile`.

### Task 2: Original-send compatibility guard

**Files:**
- Test: `tests/ArginTaxPayer.Tests/Phase18ClassifiedProducerDatabaseTests.cs`
- Test: `tests/ArginTaxPayer.Tests/Phase18PersistedAuthorityOperationGateTests.cs`

- [ ] **Step 1: Run the existing endpoint-selector and scoped outbox contract tests** for both Production and Sandbox.
- [ ] **Step 2: Run the database-gated producer test if its dedicated local PostgreSQL opt-in is available; do not create or migrate a database for this task.**
- [ ] **Step 3: Make no Production-sender behavior change. Record the owner's live Production original-send and complete Sandbox-operation confirmations.**

### Task 3: Production correction successor

**Files:**
- Create: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ProductionCorrectionService.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ProductionInvoicePreparationService.cs`
- Create: additive migration for Production legal-successor relations and correction workspaces
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18InvoiceMemoryBindingService.cs` only if successor binding needs a scoped predecessor resolution
- Modify: `src/ArginTaxPayer.Api/Program.cs` for Production operation service registration
- Modify: `src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs`
- Test: `tests/ArginTaxPayer.Tests/Phase18ProductionLegalSuccessorAdmissionTests.cs`
- Test: `apps/web/tests/phase18-production-legal-successor-routing.test.mjs`

- [x] **Step 1: Add focused admission/lineage contracts** for accepted Production predecessor requirements, separate immutable successors, allowed correction edits, revision CAS, shared correction/return claim, same-memory environment, and source immutability.
- [x] **Step 2: Confirm the admission tests expose the missing relation/preparation guard before implementation.**
- [x] **Step 3: Implement Production correction draft/read/save/prepare/send** using shared V7.9 domain validation, production-only persistence and Phase18 dispatch/outbox.
- [x] **Step 4: Route through the authenticated API/BFF.** The current UI/BFF already use these API routes and DTOs, so environment selection is in the API router and the UI is unchanged.
- [x] **Step 5: Run focused correction and Sandbox rule regression tests.**

**Plan amendment (2026-11-06):** Initial file list omitted schema admission and the existing Production preparation gate, which currently rejects all non-original subjects. Those are required for a real end-to-end path; changing them is limited to a persisted legal-relation-qualified successor and must preserve the Original path byte-for-byte at the protocol boundary. The API must resolve the persisted environment and dispatch to the Production or existing Sandbox implementation; it must never accept an environment from the caller.

### Task 4: Production cancellation successor

**Files:**
- Create: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ProductionCancellationService.cs`
- Modify: `src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs`
- Modify: `apps/web/app/invoices/invoice-formal-preview.tsx`
- Modify: `apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/cancel-and-inquire/route.ts`
- Modify: `apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/cancellation-eligibility/route.ts`
- Test: `tests/ArginTaxPayer.Tests/Phase18ProductionLegalSuccessorAdmissionTests.cs`
- Test: `apps/web/tests/phase18-production-legal-successor-routing.test.mjs`

- [x] **Step 1: Add shared admission/environment contracts** and reuse existing cancellation-wire/domain tests.
- [x] **Step 2: Confirm the relation/schema guards and route contracts are covered.**
- [x] **Step 3: Implement Production cancellation successor persistence/admission/dispatch** using the versioned legacy profile and V7.9 rules.
- [x] **Step 4: Wire the existing cancellation UI/API route** through the Production or Sandbox router.
- [x] **Step 5: Run focused cancellation and Sandbox regression tests.**

### Task 5: Production sales-return successor

**Files:**
- Create: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ProductionSalesReturnService.cs`
- Modify: `src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs`
- Modify: `apps/web/app/invoices/invoice-sales-return-modal.tsx`
- Modify: `apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/sales-return-draft/route.ts`
- Modify: `apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/sales-return-send-and-inquire/route.ts`
- Test: `tests/ArginTaxPayer.Tests/Phase18ProductionLegalSuccessorAdmissionTests.cs`
- Test: `apps/web/tests/phase18-production-legal-successor-routing.test.mjs`

- [x] **Step 1: Add shared admission/environment contracts** and reuse existing V7.9 sales-return rule tests.
- [x] **Step 2: Confirm the relation/schema guards and route contracts are covered.**
- [x] **Step 3: Implement Production return draft/save/prepare/send** using shared V7.9 rules and Production-specific lineage/storage.
- [x] **Step 4: Wire the existing return UI/BFF route** through the Production or Sandbox router.
- [x] **Step 5: Run focused return and Sandbox regression tests.**

**Tasks 3–5 implementation note:** The authenticated UI/BFF routes and response types already covered the same draft and result contract, so all three environment selections are performed in small API routers. Common production relation/reservation and environment-isolation contracts are tested together; existing V7.9 correction, cancellation and return rule tests remain the domain-level behavior checks. No database integration test is available without an approved migrated test database.

### Task 6: SDK upgrade contract, staged operator checklist, and release gate

**Files:**
- Create: `docs/phase18-production-invoice-operations-operator-checklist.md`
- Modify: `docs/references/tax-authority-sdk-catalog.md`
- Modify: `docs/phases/phase-18-consolidated-dual-authority-status.md`
- Test: `apps/web/tests/phase18-production-sdk-version-pinning.test.mjs`

- [ ] **Step 1: Write failing tests** that require explicit SDK family/version and demonstrate unsupported versions fail closed.
- [ ] **Step 2: Document the SDK upgrade sequence:** preserve archive/checksum; compare official guide and public API; add a version-specific adapter without altering the active legacy profile; add contract fixtures; run Sandbox contract tests and applicable cross-environment checks; obtain owner approval; pin on deployment; retain rollback.
- [ ] **Step 3: Write one-at-a-time manual test steps** for Correction, Cancellation and Sales Return; include Original as an existing passing baseline, and document expected acknowledgement/status transitions, indeterminate handling and safe stop conditions.
- [ ] **Step 4: Run all relevant .NET tests, web tests, typecheck and build** without live tax-authority calls; report any blocked contract compatibility or credentials rather than claiming live readiness.

**Task 6 verification note:** Focused .NET, SDK profile, routing contracts, typecheck and web build pass. The full web suite currently has 10 failures across existing unrelated UI contract files (719 total, 709 passed); therefore the plan's all-web-tests completion gate remains open. No database migration or authority call was run.

## Completion boundary

The implementation is ready for owner testing only if the existing original send remains unchanged, each legal successor has a separate readiness/send/status path, and all offline tests pass. The owner tests the successor operations one at a time with an authorized Production memory. No actual invoice is sent by this plan's automated checks. A future switch to a newer SDK is a separately versioned and contract-tested change; it is not silently enabled by this implementation.
