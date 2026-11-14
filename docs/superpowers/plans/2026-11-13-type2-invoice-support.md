# Type 2 General Sale Invoice Support Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow original Type 2 general-sale invoices (`inty=2`, `inp=1`) to pass review and use the existing separately scoped Sandbox and Production send paths.

**Architecture:** Keep the common fiscal signer/transport and immutable outbox. Make draft rules and the final frozen projector type-aware, projecting Type 2 fields from the owner-scoped immutable invoice. Type 2 has no buyer requirement, and its rules omit `tob`/buyer fields plus `setm`, `cap`, and `insp`; its total is treated as cash per V7.9.

**Tech Stack:** C#/.NET domain and infrastructure, xUnit, Next.js/TypeScript, Node test suite.

**Spec:** `docs/superpowers/specs/2026-11-13-type2-invoice-support-design.md`

## Global Constraints

- The only newly enabled classification is `ElectronicInvoiceType.Second`, pattern `1`, subject `Original`.
- Preserve Type1/Pattern1 and Type1/Pattern4 behavior; keep all other Type2 patterns and Type2 non-original subjects rejected.
- Derive source-backed rules from user-provided `Docs/RC_IITP_IS_V7_9_2.pdf` (SHA-256 `fb333770cddafd92d4ed3a5db0a21c1b0c47305431598f297f70fe5dd4c416be`) and record its exact pages/tables.
- For Type2/1, omit buyer fields and `setm`, `cap`, `insp`; represent the invoice total as cash by type rule. Do not create an internal settlement amount to satisfy the Type1 path.
- Keep user confirmation, signing-key custody, Sandbox/Production endpoint selection, serial reservation, no-retry semantics, outbox and reconciliation gates intact.
- No live authority calls, deployments, migrations, key changes, or service activation in tests or implementation.

## Review Focus

- Type2/1 with no buyer and no internal settlement must save, review, freeze, and project.
- Type1/1 without its required buyer must continue to fail.
- Type2/3, Type2/4, Type2 corrections, cancellations and returns must continue to fail.
- Type2 wire JSON must omit every buyer and settlement field classified `ignore` by V7.9 Table 1.
- A Sandbox memory must never reach Production preparation and a Production memory must never reach Sandbox preparation.

---

### Task 1: Pin the type-specific V7.9 draft and review contract

**Files:**
- Modify: `tests/ArginTaxPayer.Tests/Phase07ComprehensiveRegressionTests.cs`
- Modify: `tests/ArginTaxPayer.Tests/Phase07ResumableInvoiceDraftTests.cs`
- Modify: `tests/ArginTaxPayer.Tests/Phase07InvoiceValidationReferenceUiTests.cs`
- Modify: `apps/web/tests/phase10-internal-snapshot-preparation.test.mjs`

**Interfaces:**
- Consumes: `InvoiceTypePattern`, `SaveInvoiceEntryRequest`, `InvoiceValidationReferenceService.ValidateAsync`.
- Produces: Regression cases that state Type2/1 original allows an absent buyer and `Unknown` settlement with null cash/credit; Type1/1 still requires buyer; unsupported Type2 patterns remain denied; review no longer emits `invoice.snapshot.pattern.unsupported` for Type2/1.

- [ ] **Step 1: Add failing tests** for the cases in the produced contract. Pin review output to no unsupported-pattern error for Type2/1 and pin cash/credit omission for Type2/1 final step.
- [ ] **Step 2: Run focused tests** with `dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase07ComprehensiveRegressionTests|FullyQualifiedName~Phase07ResumableInvoiceDraftTests|FullyQualifiedName~Phase07InvoiceValidationReferenceUiTests"`.
  Expected: Type2-specific cases fail against current Type1-only validation.
- [ ] **Step 3: Implement the smallest rules update** in `InvoiceEntryWorkspaceService.Valid` and `InvoiceValidationReferenceService.ValidateAsync`: recognize only Type2/1 original; permit no buyer and no settlement amounts; retain the existing Type1 buyer and settlement rules.
- [ ] **Step 4: Re-run the focused tests** and confirm the Type2/1 and Type1 regression cases pass.

### Task 2: Make the invoice form collect Type2-specific data

**Files:**
- Modify: `apps/web/app/invoices/workspace.tsx`
- Modify: `apps/web/tests/phase08-step09-dynamic-form.test.mjs`
- Modify: `apps/web/tests/phase08-step13-settlement.test.mjs`
- Modify: `apps/web/tests/phase07-invoice-entry-ui.test.mjs`

**Interfaces:**
- Consumes: `FormMetadata`, `invoiceType`, `pattern`, existing `buyerDetailsNotRequired` and form save payload.
- Produces: Type2/1 form hides buyer and settlement controls, persists null buyer and null cash/credit with `settlementMethod=0`, and leaves Type1 form unchanged.

- [ ] **Step 1: Add failing UI source/behavior assertions** for hidden Type2 buyer and settlement controls, null settlement values in the payload, and unchanged Type1 settlement controls.
- [ ] **Step 2: Run focused Node tests** using `node --test tests/phase08-step09-dynamic-form.test.mjs tests/phase08-step13-settlement.test.mjs tests/phase07-invoice-entry-ui.test.mjs` from `apps/web`.
- [ ] **Step 3: Update the form** to derive `type2GeneralSale` from `(invoiceType === 2 && pattern === 1)`, remove buyer selection and settlement requirements for that combination, and serialize the documented no-buyer/no-settlement shape.
- [ ] **Step 4: Re-run the focused Node tests**; verify Type1 UI assertions stay unchanged.

### Task 3: Freeze Type2/1 originals without Type1-only ownership requirements

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceWorkspacePromotionService.cs`
- Modify: `apps/web/tests/phase10-internal-snapshot-preparation.test.mjs`

**Interfaces:**
- Consumes: the Type2/1 draft contract from Task 1.
- Produces: Promotion of a no-buyer Type2/1 original into the same owner-scoped immutable Draft snapshot; Type1/1 still requires its buyer; Type2/1 snapshots retain `invoiceType=2`, `pattern=1` and do not fabricate settlement values.

- [ ] **Step 1: Add failing promotion tests** for Type2/1 no buyer/no settlement, Type1/1 missing buyer, Type2/3 denial and Type2 non-original denial.
- [ ] **Step 2: Run the focused .NET tests** and confirm the Type2 promotion currently fails closed at the Type1 buyer and pattern checks.
- [ ] **Step 3: Change promotion validation** to condition buyer and settlement rules on the exact classification; leave owner item, official ID, unit, memory, revision, and no-dispatch checks unchanged.
- [ ] **Step 4: Re-run the focused .NET tests** and verify the immutable snapshot records the original classification and no fabricated buyer or settlement.

### Task 4: Project the V7.9 Type2/1 original wire shape

**Files:**
- Modify: `src/ArginTaxPayer.Domain/Invoices/Phase10InvoiceWireMapper.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyFrozenInvoiceProjector.cs`
- Modify: `tests/ArginTaxPayer.Tests/Phase10InvoiceWireMapperTests.cs`
- Modify: `tests/ArginTaxPayer.Tests/Phase10LegacyFrozenInvoiceProjectorTests.cs`
- Modify: `apps/web/tests/phase10-step27-archived-sdk.test.mjs`

**Interfaces:**
- Consumes: `StoredInvoice`, the frozen `SaveInvoiceEntryRequest`, reserved `fiscalTaxId`, trusted seller identifier, verified unit codes, and `Phase10InvoiceWireMapper.Project(InvoiceWireInput)`.
- Produces: Type2/1 JSON with `inty=2`, `inp=1`, mandatory issuer/item/totals, no buyer keys, and no `setm`/`cap`/`insp`; `IsSubmissionAuthorized` remains false until existing dispatch gates establish authorization.

- [ ] **Step 1: Add failing wire tests** asserting exact Type2/1 header classification, buyer/settlement field omissions, totals, and rejection of unsupported Type2 patterns/subjects or buyer/settlement values not accepted by the no-buyer UI contract.
- [ ] **Step 2: Run focused .NET mapper/projector tests** and confirm the frozen projector rejects `invoiceType=2`.
- [ ] **Step 3: Add a Type2/1 projector branch** that uses the frozen Type2 classification, omits buyer lookups/fields and settlement fields, and keeps the Type1 branch byte-for-byte compatible.
- [ ] **Step 4: Re-run focused .NET tests** and verify deterministic JSON plus existing Type1 wire regression tests.

### Task 5: Admit Type2/1 through existing Sandbox and Production preparation

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyInvoicePreparationService.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ProductionInvoicePreparationService.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LiveSendReadinessService.cs` only if its current readiness calculation blocks the now-supported source type.
- Modify: `tests/ArginTaxPayer.Tests/Phase10SandboxSubmissionAdmissionTests.cs`
- Modify: `tests/ArginTaxPayer.Tests/Phase18ProductionInvoicePreparationTests.cs`
- Modify: `tests/ArginTaxPayer.Tests/Phase18InvoiceSubmissionRoutingTests.cs`
- Modify: `apps/web/tests/phase10-legacy-preparation-contract.test.mjs`
- Modify: `apps/web/tests/phase18-step17-legal-successor-routing.test.mjs` only if its assertions encode the Type1-only original-invoice gate.

**Interfaces:**
- Consumes: Task 4's type-aware frozen projection and existing authenticated environment-specific memory, serial reservation and signing-key loaders.
- Produces: Both environment preparation paths admit only Type2/1 original; Sandbox continues to use Sandbox memory/key/endpoint and Production continues to use the resolved Production binding/key/endpoint.

- [ ] **Step 1: Add failing preparation tests** that Type2/1 reaches each environment's projector with the correct persisted memory binding and that cross-environment memory substitution is denied. Assert the Sandbox preparation source no longer requires buyer data for Type2/1 and Production keeps its exact persisted environment resolver.
- [ ] **Step 2: Run focused .NET preparation/routing tests** and confirm current Type1-only projector blocks Type2.
- [ ] **Step 3: Update only type/pattern/buyer/settlement admission** in both preparation paths; do not change credentials, URLs, serial reservations, transaction boundaries, retry policy, dispatch enablement, or no-store evidence checks.
- [ ] **Step 4: Re-run focused tests** and inspect the prepared outbox payloads to prove they contain the exact Type2/1 projection and correct environment label.

### Task 6: Complete review, browser proxy, and full regression verification

**Files:**
- Modify as needed: `apps/web/app/invoices/invoice-validation-panel.tsx`
- Modify as needed: `apps/web/app/api/invoices/[...segments]/route.ts`
- Modify: relevant `apps/web/tests/phase10-internal-snapshot-preparation.test.mjs`, `apps/web/tests/phase10-step27-archived-sdk.test.mjs`, and dispatch contract tests.

**Interfaces:**
- Consumes: Type2 review findings and authenticated same-origin invoice BFF.
- Produces: UI presents the review result without the Type1-only error; BFF continues to allow only existing scoped save/prepare/send operations.

- [ ] **Step 1: Add failing BFF/UI assertions** for Type2 review and ensure no new unauthenticated route or browser-held bearer token is added.
- [ ] **Step 2: Implement only needed error mapping or allowlist adjustments**, preserving server authorization and exact environment binding.
- [ ] **Step 3: Run all verification requested for this repair:** `dotnet test ArginTaxPayer.sln`, `npm test`, `npm run typecheck`, and `npm run build` from `apps/web`.
- [ ] **Step 4: Review `git diff`** for Type1 regressions, accidental live requests, secret/config changes, or production activation changes.

## Operational Acceptance (separate from code tests)

- Owner selects one exact Type2/1 invoice and explicitly sends it to Sandbox through the existing send action; verify durable receipt and inquiry result before any retry or Production use.
- Owner then separately selects and explicitly sends the intended Type2/1 invoice to Production through the existing Production action; verify environment binding, durable receipt and inquiry result.
- Do not claim either authority accepted an invoice from successful local tests or queue receipts alone.

## Execution record (2026-11-13)

- Native implementation completed in the current authorized feature checkout, preserving earlier npm-test repairs. Ruling: work in place so existing session edits remain available for immediate local app use.
- Added Type2-aware draft completion, UI normalization, review, promotion, internal validation, audit, frozen projection, Sandbox/Production preparation, and print treatment.
- Reproduction test failed on the old Type1-only projector, then passed after implementation. Added behavior tests for completed Type2 drafts, full audit, wire omissions, and both exact environment bindings.
- Verification: npm 747 passed; .NET 1623 passed / 55 environment-gated skipped; frontend typecheck and production build passed.
- Two pre-existing .NET lineage source tests expected the old non-interpolated SQL source; updated their source extraction to retain the existing recursive-query assertions.
- Fresh reviewer found no concrete correctness blocker. PostgreSQL Type2 transactions and real authority acceptance remain operational acceptance work; no live invoice was submitted.
