# Local dual-authority submission implementation plan

> **For agentic workers:** Use superpowers:subagent-driven-development for independent components, then integrated verification.

**Goal:** Locally test real fiscal memories against tp.tax.gov.ir and Sandbox memories against sandboxrc.tax.gov.ir, as requested by the owner.
**Architecture:** Retain archived certificate-free SELF_TSP protocol and existing Sandbox behavior. Bind real invoice operations to persisted fiscal memory and reuse Phase18 scoped transport, key custody and one-attempt outbox consumer. No remote deployment or automatic live submission.
**Tech stack:** .NET 9 / EF Core / PostgreSQL / Next.js.
**Spec:** Owner instructions in this session; archived SDK guide in ../Docs and docs/references/tax-authority-sdk-catalog.md.

## Global constraints
- Environment comes from owned persisted memory, not a code-format guess, client JSON or host environment.
- Production origin https://tp.tax.gov.ir/req/api/; Sandbox origin https://sandboxrc.tax.gov.ir/req/api/.
- Retain encrypted custody, single-attempt/Indeterminate handling and Sandbox database guards.
- Local changes and acceptance only; do not activate Ubuntu or submit a real invoice automatically.

## Review focus
- Cross-environment or cross-owner keys and invoices must be rejected.
- Retried sends must return existing receipt or reconciliation state, never resend.
- Missing credentials/configuration must fail before invoice POST.
- Existing Sandbox regression behavior must stay intact.
- Real memory selection must reach a working local entry/submit flow.

## Task 1: Production invoice producer
- [ ] Add Phase18ProductionInvoicePreparationService.PrepareAsync(string? bearer, Guid taxpayerId, Guid invoiceId, CancellationToken ct=default), returning Phase10LegacyPreparationResult.
- [ ] Preserve validated finalized snapshot, review, ownership, membership, catalog and calculation checks.
- [ ] Add separate production serial reservation migration/table with per-memory locking and monotonic uniqueness; never mutate Sandbox evidence.
- [ ] Build an encrypted Production-classified outbox and immutable operation using database key loader and pinned Production token/server endpoints; no enqueue.
- [ ] Extend frozen projector to explicitly validated Production snapshots and add meaningful tests.

## Task 2: Workflow and HTTP integration
- [ ] Add Phase18InvoiceSubmissionService.SendAsync(string? bearer, Guid taxpayerId, Guid invoiceId, CancellationToken ct), returning Phase10OneClickResult.
- [ ] Resolve owned invoice memory environment; delegate legacy Sandbox path unchanged; Production uses internal validation/finalization, Task1 producer and scoped Phase18 worker.
- [ ] Wire existing UI send endpoint to this router, support genuine fiscalMemoryId promotion, and expose correct dispatch environment/status.
- [ ] Add authenticated Production inquiry routing using existing scoped reference inquiry; retain pending acknowledgement semantics.
- [ ] Add regressions for environment and no-resend behavior.

## Task 3: Memory connection and UI
- [ ] Inspect archived legacy SDK guide. Route read-only fiscal information by explicit persisted memory environment.
- [ ] Ensure local UI allows real memory selection and reports correct environment without disabling or relabeling Sandbox.
- [ ] Add focused offline tests of request host and negative mismatches.

## Task 4: Local acceptance and configuration
- [ ] Register new services, reconcile shared interfaces and migrate only approved local test DB after SQL review.
- [ ] Document archived protocol evidence and local configuration, activate private local flags needed for owner-driven tests.
- [ ] Run full .NET suite, web tests/typecheck/build and database integration checks. Record live acceptance as owner pending; Phase18 remains unclosed until owner verifies.

## Discovered prerequisite: explicit memory authority onboarding
- [ ] Add Level1+MFA-only one-time classification of unclassified memories with no dispatch history, expected-revision checks and append-only audit.
- [ ] Add a separate explicit verify-and-activate action; read-only Test remains read-only. Activation requires authenticated GET_FISCAL_INFORMATION ACTIVE (archived enum0), matching taxpayer NationalId, same selected key/revision/owner, membership and active subscription.
- [ ] Preserve pending/unclassified state on rejected verification; no bulk backfill. UI exposes the explicit actions and refreshes persisted status.
- [ ] Verify policy and PostgreSQL mutations against fake authority responses, never make a real submission in acceptance.
