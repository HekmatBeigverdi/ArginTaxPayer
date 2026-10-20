# ArginTaxPayer — Phase 08: Invoice Workflows and UI

**Status:** Steps 01–20 owner-approved; Step18 import integration tests deferred; Step21 Ubuntu acceptance runner committed, operator evidence pending.
**Branch:** `phase/08-invoice-workflows-ui`
**Baseline:** `main` at `364462848914f289916962a0578c9fac9cd85ce1` (GitHub inspected 2026-10-18).
**Roadmap source:** `docs/roadmap/18-phases.md`, phase 08 of 18.
**Official scope:** “Draft/edit/review/approve, dynamic forms, settlement methods, calculations, import preview”.
**Plan policy:** Exactly **22** fixed numbered steps; scope/order changes require explicit owner authorization and a dated amendment here.

## Baseline and Phase07 handoff

- Phase07 main merge commit: `364462848914f289916962a0578c9fac9cd85ce1`.
- Git annotated tag `phase-07-v1.0.0` references that commit. GitHub Release published 2026-10-18 18:44:54 UTC, non-draft/non-prerelease. Annotated tag is unsigned; do not describe as cryptographically signed.
- Phase07 authoritative file: `docs/phases/phase-07-invoice-domain-rule-engine.md`; separate release notes: `docs/phases/phase-07-release-notes.md`. Release reports Step23 macOS/Ubuntu owner acceptance, **but does not archive raw operator logs**.
- Domain: `src/ArginTaxPayer.Domain/Invoices/` contains aggregate, rule engine, lifecycle, financial model, references, snapshots, revisions and operation registry.
- Application: `src/ArginTaxPayer.Application/` and tenancy contracts; API: `src/ArginTaxPayer.Api/Invoices/InvoiceDraftDevelopmentEndpoints.cs` is **development-only**, not a production workflow API.
- Persistence: `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/` includes `InvoiceDraftApplicationService`, `InvoiceEntryWorkspaceService`, `EfInvoiceAtomicWriter`, model configuration and storage entities. DbContext registers invoice documents, revisions, operation claims and resumable workspaces.
- Existing invoice UI: `apps/web/app/(console)/invoices/page.tsx` delegates to `apps/web/app/invoices/workspace`; do not duplicate existing wizard.
- Latest invoice-related migration present in code: `20261018150000_AddResumableInvoiceEntryWorkspaces.cs`; **whether it is applied to the test DB is not verified**. Existing migration files are inventory, not evidence of an applied schema.
- Root `.gitignore` previously covered `.next/` but not explicit `.next-dev/` and `next-env.d.ts`; Step01 adds ignore rules. If either was historically tracked, inspect and remove from index in a separately reviewed action; ignore alone does not untrack.
- Baseline test evidence is inherited from Phase07 release claims only. No build, HTTP or database tests were executed by GitHub read/write operations in Step01. Local test DB's schema and `__EFMigrationsHistory` remain operator verification items.

## Architecture and non-negotiable boundaries

1. Preserve .NET 9 / EF Core / PostgreSQL and Next.js 15 / TypeScript / RTL Persian. Extend existing Phase07 aggregates and deterministic rules, rather than building a second tax engine. No assumed current VAT rate.
2. Customer account owns taxpayer profiles; Level2 controls only the currently assigned commercial customer relationship, subject to Level1 governance. Do **not** authorize using obsolete taxpayer-to-Level2 grants. Query scoping and commands must enforce this on server.
3. Internal `Approved` is never Tax Authority accepted, submitted or digitally signed. Signing/CSR is Phase09, live connector Phase10, async inquiry Phase11, entitlement and real collections/partner settlements Phase12.
4. Preserve immutable Phase06 catalog provenance and Phase07 header/line snapshots, explicit rule-set versions, invoice references and revisions.
5. Use exact decimal server-side calculations. Payment conditions (cash/credit/mixed) in this phase do not create financial receipts, charges or partner profit allocation.
6. Atomic operations with idempotency, expected revision/CAS, tamper-proof append-only audit, and role-scoped reads. Distinguish developer-only endpoints from externally supported endpoints.
7. Only `argintaxpayer_test` is allowed. Require independent current_database() check before **any** migration or database test. Review generated SQL and seek owner approval before destructive or potentially risky schema changes. No automatic migrations, production data, live SMS, official signing or tax dispatch.
8. macOS is primary; Ubuntu verifies the identical commit SHA with independent build cache. Use `zsh scripts/dev-up.zsh`, `dev-status.zsh`, `dev-down.zsh` and `bash scripts/ubuntu-dev.sh verify`. Standard dev-up runs AutoSSH; no plain legacy tunnel.
9. Ignore generated `.next`, `.next-dev`, and `next-env.d.ts` and never commit them. No actual customer data, keys or secrets in repo, tests or logs.

## Fixed implementation and acceptance matrix

| Step | Title | Primary result | Acceptance | Status |
|---|---|---|---|---|
| 01 | Architecture baseline and branch hygiene | Freeze Phase07 interfaces, database migration inventory, role matrix, branch and build hygiene | Baseline reviewed; ignore generated files; no code or migration | Owner-approved 2026-10-18 |
| 02 | Workflow state contracts | Define internal Draft/InReview/ChangesRequested/Approved state transitions | Allowed and forbidden transitions deterministic | Owner-approved 2026-10-18 |
| 03 | Actor authorization and ownership | Implement role/scope matrix for customer, Level1 and owning Level2 | All reads/writes scoped before data return | Owner-approved 2026-10-19; runtime wiring deferred to Steps05/07 |
| 04 | Workflow persistence and migration plan | Add minimal additive workflow and audit entities when existing Phase07 storage insufficient | No silent loss of historical invoices; migration separately authorized | Owner-approved 2026-10-19; migration application not independently verified |
| 05 | Workflow command services | Create draft submission, change request, internal approval | State and history consistent after commit | Owner-approved 2026-10-19 |
| 06 | Concurrency and idempotency | Prevent lost updates, duplicate review submissions and simultaneous approvals | One accepted write, deterministic conflict/replay | Owner-approved 2026-10-19; local logs not independently archived |
| 07 | Workflow API contracts | Expose scoped commands, queries and stable typed errors | Backward compatible, stable response contracts | Development command API committed; operator tests pending |
| 08 | Invoice workspace list | Show customer-scoped drafts, review queue, filters, pagination | Correct statuses and scoped results | Owner-approved 2026-10-19 |
| 09 | Dynamic invoice forms | Drive conditional fields by official rule-set and pattern metadata | Valid combinations enabled; invalid blocked | Owner-approved 2026-10-19 |
| 10 | Header and party entry | Taxpayer, memory, buyer, issue date, header validation | Localized field errors and valid persisted values | Owner-approved 2026-10-19 |
| 11 | Line and catalog entry | Search/select Phase06 catalog items, quantity, unit, price and snapshots | Historic invoice unchanged after catalog edit | Owner-approved 2026-10-19 |
| 12 | Calculations and financial summary | Display discount, charges, VAT and final totals using Phase07 engine | UI/API totals match canonical model | Owner-approved 2026-10-19 |
| 13 | Settlement methods and fields | Cash, credit and mixed invoice terms and reconciliation | Valid mix accepted; mismatch rejected | Owner-approved 2026-10-20 |
| 14 | Save/resume draft UX | Enhance existing resumable workspaces and unsaved-change handling | Draft resumes exactly once without lost fields | Owner-approved 2026-10-20 |
| 15 | Self-service draft review and validation UI | User independently views saved draft and preflight findings; errors are handled by user, with external support when needed | No administrator approval gate; saved draft validation and editing remain accessible | Owner-approved 2026-10-20 |
| 16 | Audit and revision history | Show immutable actor/timestamp/reason/revision timeline | No historical rewrites or unauthorized reads | Owner-approved 2026-10-20; test DB table created |
| 17 | Import mapping and preview | CSV/XLSX bounded parsing, column mapping and row-level preview | Explain valid/invalid rows before approval | Owner-approved 2026-10-20 |
| 18 | Controlled batch draft import | Explicit import confirmation with per-row durable outcomes | Retry creates no duplicate invoices; partial failures explained | Owner-approved 2026-10-20 with deferred real import tests and failure-ledger improvements |
| 19 | Comprehensive regression and security | Domain, API, web, migration safety and Phase03-07 compatibility | Required tests green without disabling tests | Owner accepted; runtime evidence pending |
| 20 | macOS operator acceptance | Independent macOS build and workflow/browser/db/HTTP acceptance | Commit SHA, operator results and caveats recorded | Owner-approved 2026-10-20; regression PASS, API 200, Web 307; browser acceptance accepted by owner without itemized evidence |
| 21 | Ubuntu operator acceptance | Same SHA, independent Ubuntu build and integration tests | Matching SHA and Ubuntu evidence documented | Acceptance script committed, awaiting VPS operator evidence |
| 22 | Phase closeout and publication | Close one master doc, review final evidence; optional main merge/tag/release only upon separate owner approval | Explicit owner signoff and verified GitHub artifacts | Not started |

## Per-step delivery contracts

### Step 01 — Architecture baseline and branch hygiene

- **Goal/scope:** Freeze Phase07 interfaces, database migration inventory, role matrix, branch and build hygiene.
- **Planned paths (verify precise changes before commit):** `docs/phases/phase-08.md`; `.gitignore`.
- **Architecture:** Document Domain/Application/API/EF/UI boundaries and non-production boundaries.
- **Security/validation:** Do not touch runtime, database or secrets.
- **Acceptance:** Baseline reviewed; ignore generated files; no code or migration.
- **Tests/evidence:** Read-only repository inspection; git diff/compare only. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Documentation and branch preparation completed; owner-approved 2026-10-18.

### Step 02 — Workflow state contracts

- **Goal/scope:** Define internal Draft/InReview/ChangesRequested/Approved state transitions.
- **Planned paths (verify precise changes before commit):** `Domain/Invoices`; `Application/Contracts`.
- **Architecture:** Reuse Phase07 invoice aggregate, lifecycle and revision; separate external authority status.
- **Security/validation:** Actor permissions and transition preconditions server-side.
- **Acceptance:** Allowed and forbidden transitions deterministic.
- **Tests/evidence:** Domain transition unit tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Pure contracts and focused unit tests committed, owner approval and macOS execution pending. No API, database or UI change.

### Step 03 — Actor authorization and ownership

- **Goal/scope:** Implement role/scope matrix for customer, Level1 and owning Level2.
- **Planned paths (verify precise changes before commit):** `Application/Tenancy`; `Api/Invoices`.
- **Architecture:** Authorize through current customer commercial owner and taxpayer membership.
- **Security/validation:** Deny former partner, cross-customer, non-member, privilege escalation.
- **Acceptance:** All reads/writes scoped before data return.
- **Tests/evidence:** Role matrix and negative isolation tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 04 — Workflow persistence and migration plan

- **Goal/scope:** Add minimal additive workflow and audit entities when existing Phase07 storage insufficient.
- **Planned paths (verify precise changes before commit):** `Infrastructure/Persistence/Invoices`; `Migrations`.
- **Architecture:** Use revision token, immutable audit entries, FK/unique constraints.
- **Security/validation:** Review migration SQL, DB test-only gate and rollback.
- **Acceptance:** No silent loss of historical invoices; migration separately authorized.
- **Tests/evidence:** Model snapshot and SQL inspection; test-only migration after approval. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 05 — Workflow command services

- **Goal/scope:** Create draft submission, change request, internal approval.
- **Planned paths (verify precise changes before commit):** `Application/Invoices`; `Infrastructure/Persistence/Invoices`.
- **Architecture:** Atomic transitions and repository transaction.
- **Security/validation:** Role gates and no bypass of rule engine.
- **Acceptance:** State and history consistent after commit.
- **Tests/evidence:** Command and transaction integration tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 06 — Concurrency and idempotency

- **Goal/scope:** Prevent lost updates, duplicate review submissions and simultaneous approvals.
- **Planned paths (verify precise changes before commit):** `Domain/Invoices`; `Infrastructure/Persistence/Invoices`.
- **Architecture:** CAS/revision and operation-scoped idempotency key.
- **Security/validation:** No replay of prior actor's operation.
- **Acceptance:** One accepted write, deterministic conflict/replay.
- **Tests/evidence:** Concurrent PostgreSQL integration and replay tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 07 — Workflow API contracts

- **Goal/scope:** Expose scoped commands, queries and stable typed errors.
- **Planned paths (verify precise changes before commit):** `Api/Invoices`; `Application/Contracts`.
- **Architecture:** Reuse identity middleware and Phase07 development-only endpoints without promoting them.
- **Security/validation:** Require auth, deny leakage; distinguish 400/401/403/404/409.
- **Acceptance:** Backward compatible, stable response contracts.
- **Tests/evidence:** HTTP smoke and negative authorization tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 08 — Invoice workspace list

- **Goal/scope:** Show customer-scoped drafts, review queue, filters, pagination.
- **Planned paths (verify precise changes before commit):** `apps/web/app/(console)/invoices`; `Api/Invoices`.
- **Architecture:** Server-side query filtering and pagination.
- **Security/validation:** No cross-tenant metadata leakage.
- **Acceptance:** Correct statuses and scoped results.
- **Tests/evidence:** Next.js tests and HTTP paging/role checks. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 09 — Dynamic invoice forms

- **Goal/scope:** Drive conditional fields by official rule-set and pattern metadata.
- **Planned paths (verify precise changes before commit):** `apps/web/app/invoices`; `Domain/Invoices`.
- **Architecture:** Frontend consumes backend rule decisions; no parallel tax logic.
- **Security/validation:** Server revalidates untrusted inputs.
- **Acceptance:** Valid combinations enabled; invalid blocked.
- **Tests/evidence:** TypeScript rule/form matrix tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 10 — Header and party entry

- **Goal/scope:** Taxpayer, memory, buyer, issue date, header validation.
- **Planned paths (verify precise changes before commit):** `apps/web/app/invoices`; `Api/Invoices`.
- **Architecture:** Use Phase04/05 party and membership snapshots.
- **Security/validation:** Check every selection against actor scope.
- **Acceptance:** Localized field errors and valid persisted values.
- **Tests/evidence:** UI, API and cross-scope tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 11 — Line and catalog entry

- **Goal/scope:** Search/select Phase06 catalog items, quantity, unit, price and snapshots.
- **Planned paths (verify precise changes before commit):** `apps/web/app/invoices`; `Infrastructure/Catalog`.
- **Architecture:** Retain provenance and immutable line snapshots.
- **Security/validation:** No arbitrary catalog ID access.
- **Acceptance:** Historic invoice unchanged after catalog edit.
- **Tests/evidence:** UI, handoff and history tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 12 — Calculations and financial summary

- **Goal/scope:** Display discount, charges, VAT and final totals using Phase07 engine.
- **Planned paths (verify precise changes before commit):** `apps/web/app/invoices`; `Application/Invoices`.
- **Architecture:** Exact decimal server-calculated source of truth.
- **Security/validation:** Reject client-price/tax tampering; unresolved VAT fail closed.
- **Acceptance:** UI/API totals match canonical model.
- **Tests/evidence:** Rounding and calculation regression tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 13 — Settlement methods and fields

- **Goal/scope:** Cash, credit and mixed invoice terms and reconciliation.
- **Planned paths (verify precise changes before commit):** `Domain/Invoices`; `apps/web/app/invoices`.
- **Architecture:** Reuse Phase07 settlement model; not receipt/payment ledger.
- **Security/validation:** Validate sums, pattern eligibility, no unsupported defaults.
- **Acceptance:** Valid mix accepted; mismatch rejected.
- **Tests/evidence:** Domain/UI/API settlement test matrix. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 14 — Save/resume draft UX

- **Goal/scope:** Enhance existing resumable workspaces and unsaved-change handling.
- **Planned paths (verify precise changes before commit):** `Infrastructure/Persistence/Invoices`; `apps/web/app/invoices`.
- **Architecture:** Preserve Phase07 workspace identity and revision CAS.
- **Security/validation:** Prevent unauthorized resume and stale overwrite.
- **Acceptance:** Draft resumes exactly once without lost fields.
- **Tests/evidence:** Multi-tab concurrency and resume tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 15 — Review and internal approval UI

- **Goal/scope:** Review summary, validation feedback, approve and return with reason.
- **Planned paths (verify precise changes before commit):** `apps/web/app/(console)/invoices`; `Api/Invoices`.
- **Architecture:** No tax authority status mutation.
- **Security/validation:** Enforce role and workflow constraints.
- **Acceptance:** Readonly approval snapshot and reasoned change requests.
- **Tests/evidence:** UI/HTTP role and transition tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 16 — Audit and revision history

- **Goal/scope:** Show immutable actor/timestamp/reason/revision timeline.
- **Planned paths (verify precise changes before commit):** `Infrastructure/Persistence/Invoices`; `apps/web/app/invoices`.
- **Architecture:** Append-only event history and snapshot preservation.
- **Security/validation:** Redact protected data; scope all history.
- **Acceptance:** No historical rewrites or unauthorized reads.
- **Tests/evidence:** Audit integrity and privacy tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 17 — Import mapping and preview

- **Goal/scope:** CSV/XLSX bounded parsing, column mapping and row-level preview.
- **Planned paths (verify precise changes before commit):** `Application/Invoices`; `apps/web/app/invoices/import`.
- **Architecture:** Preview does not mutate documents or database.
- **Security/validation:** Size limits, formula injection prevention, deny foreign references.
- **Acceptance:** Explain valid/invalid rows before approval.
- **Tests/evidence:** Parser, file limits and non-mutation tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 18 — Controlled batch draft import

- **Goal/scope:** Explicit import confirmation with per-row durable outcomes.
- **Planned paths (verify precise changes before commit):** `Infrastructure/Persistence/Invoices`; `Api/Invoices`.
- **Architecture:** Transaction boundaries and deterministic idempotency per row.
- **Security/validation:** Accept only scoped validated drafts.
- **Acceptance:** Retry creates no duplicate invoices; partial failures explained.
- **Tests/evidence:** PostgreSQL batch replay and HTTP integration tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 19 — Comprehensive regression and security

- **Goal/scope:** Domain, API, web, migration safety and Phase03-07 compatibility.
- **Planned paths (verify precise changes before commit):** `tests`; `apps/web/tests`; `scripts`.
- **Architecture:** Cross-layer verification and contract freeze.
- **Security/validation:** Negative tenant tests, concurrency, import hardening.
- **Acceptance:** Required tests green without disabling tests.
- **Tests/evidence:** dotnet test; TS typecheck; Next build; DB/HTTP tests. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 20 — macOS operator acceptance

- **Goal/scope:** Independent macOS build and workflow/browser/db/HTTP acceptance.
- **Planned paths (verify precise changes before commit):** `scripts`; `docs/phases/phase-08.md`.
- **Architecture:** Standard dev-up/status/down; AutoSSH test DB.
- **Security/validation:** No production DB or live provider work.
- **Acceptance:** Commit SHA, operator results and caveats recorded.
- **Tests/evidence:** macOS full suite + manual browser checks. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 21 — Ubuntu operator acceptance

- **Goal/scope:** Same SHA, independent Ubuntu build and integration tests.
- **Planned paths (verify precise changes before commit):** `scripts/ubuntu-dev.sh`; `docs/phases/phase-08.md`.
- **Architecture:** Separate .next and compile output from macOS.
- **Security/validation:** Test DB only; no live send or signing.
- **Acceptance:** Matching SHA and Ubuntu evidence documented.
- **Tests/evidence:** Ubuntu verify, dotnet/Next/DB/HTTP smoke. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

### Step 22 — Phase closeout and publication

- **Goal/scope:** Close one master doc, review final evidence; optional main merge/tag/release only upon separate owner approval.
- **Planned paths (verify precise changes before commit):** `docs/phases/phase-08.md`; `GitHub`.
- **Architecture:** Freeze Phase08 boundaries and handoff Phase09.
- **Security/validation:** No premature merge, migrations, tag or release.
- **Acceptance:** Explicit owner signoff and verified GitHub artifacts.
- **Tests/evidence:** Final compare, independent platform evidence and GitHub checks. Record commands and actual outcomes; do not claim tests that were not run.
- **Implementation status:** Not started.

## Step 01 — Completion evidence (2026-10-18)

- **Branch:** `phase/08-invoice-workflows-ui` created from immutable inspected `main` SHA `364462848914f289916962a0578c9fac9cd85ce1`.
- **Changed files:** `docs/phases/phase-08.md` (this fixed plan); `.gitignore` (generated Next.js cache/type artifacts).
- **Runtime/API/UI changed:** No. **EF migration created/applied:** No. **Database accessed/mutated:** No. **External authority operations:** None.
- **Tests:** No .NET, TypeScript, Next.js, PostgreSQL or HTTP test was executed in the GitHub-only inspection/commit process. Operator code-build checks and test DB introspection are pending.
- **Current DB unknown:** Read-only `SELECT current_database()` and `__EFMigrationsHistory` check must precede future changes, never assume migration state from files.
- **Owner approval:** Pending. Step02 is blocked until explicit confirmation.
- **Commit SHA and remote comparison:** To be recorded after branch commit/push.

## Verification and change protocol

For every implementation step: perform source changes on phase branch only, commit/push, verify SHA, provide short macOS pull and focused test commands, record actual vs pending evidence, and wait for owner signoff before next step. Ubuntu is the second verification environment on the identical commit. Use synthetic test fixtures, negative tenant/role cases and proper HTTP semantics. If a new migration is proposed, first provide impact review and SQL for approval; no migration execution is inferred from green builds. Never waive tests to report success.

**Step01 operator read-only checks (after pull):**
```bash
git fetch origin
git switch phase/08-invoice-workflows-ui
git pull --ff-only
git rev-parse HEAD
git status --short
git check-ignore apps/web/.next-dev/sample apps/web/next-env.d.ts apps/web/.next/sample
```
For database introspection, first start `zsh scripts/dev-up.zsh` (which owns AutoSSH) and confirm `argintaxpayer_test` using existing Phase07 read-only DB gate, then inspect `__EFMigrationsHistory` with test-only credentials. Do not apply migrations in Step01. Build checks may be operator-run if requested, but this documentation-only step does not mark them successful.

## Phase release gate

After Steps01–21 have owner-approved evidence, Step22 requires separate final owner permission to merge into `main`, produce an **annotated** tag and publish a GitHub Release. Record exact commit, tag object, release URL, macOS/Ubuntu outcomes and any outstanding test caveats in this same file. No automatic completion or Phase09 start.

## Step 02 — Completion record (2026-10-18)

- **Implementation:** Added `InvoiceReviewState` (Draft, InReview, ChangesRequested, Approved), `InvoiceReviewAction`, pure `InvoiceReviewPolicy.CanApply`, and immutable `InvoiceReviewSnapshot` with revision/CAS proposal, actor, UTC monotonic timestamp and mandatory sanitized change-request reason. Approved is terminal for this internal review workflow; it does not mean tax authority acceptance or Phase07 finalization.
- **Source:** `src/ArginTaxPayer.Domain/Invoices/InvoiceReviewWorkflow.cs`.
- **Tests added:** `tests/ArginTaxPayer.Tests/Phase08InvoiceReviewContractsTests.cs` includes transitions and resubmission, replay/stale revision, invalid actor/clock/reason, and non-interference with Phase07 lifecycle and authority state.
- **Scope guard:** Application-level actor authorization, trusted validation, CAS persistence, audit trail and API integration are intentionally postponed to approved Steps03–07. The pure review snapshot does not grant rights or independently authorize approval. Do not wire it directly to user-provided requests.
- **Migration / database / API / UI:** None.
- **Executed test status:** GitHub connector commits files but does not execute .NET tests. Local macOS test and review remain pending; no test result is claimed.
- **macOS (operator):**
  ```bash
  git fetch origin
  git switch phase/08-invoice-workflows-ui
  git pull --ff-only
  dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter FullyQualifiedName~Phase08InvoiceReviewContractsTests
  dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter FullyQualifiedName~Phase07InvoiceLifecycleTests
  ```
- **Owner approval:** Pending; Step03 is blocked.

## Step 03 — Delivery record (2026-10-18)

- **Files:** `src/ArginTaxPayer.Application/Tenancy/InvoiceWorkflowAccessPolicy.cs`; `tests/ArginTaxPayer.Tests/Phase08InvoiceWorkflowAccessTests.cs`; this master plan.
- **Permission matrix:** Customer reads/edits/submits own taxpayer only; Level1 can read/review/approve customers under own controlling root; Level2 can read/review/approve customers owned by that **current** partner. Customer cannot approve; administrators cannot impersonate customer-edit in this contract. Root-direct customer does not create partner access.
- **Inputs:** Immutable `InvoiceWorkflowScope` must be built exclusively from authenticated server-side user, current `CustomerCommercialOwners`, active customer→taxpayer membership, and current role/MFA verification. A forged object is not proof of authorization. Null/invalid roles, missing membership, other customers, former partners and unaffiliated administrators fail closed.
- **Critical scope boundary:** Phase03 designs the *pure policy* and unit-test matrix; no runtime endpoint or persistence wiring was changed. Actual scoped DB query enforcement and pre-write recheck before mutations are separate mandatory acceptance gates in Steps05/07, not claimed complete here.
- **Security regression fixtures:** role matrix; former Level2 after transfer; wrong customer/Level1; inactive membership; root direct; missing context; invalid role/permission.
- **Database migrations:** none; **API/UI changes:** none; **customer data used:** none.
- **Tests added, not executed in GitHub connector:** `dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter FullyQualifiedName~Phase08InvoiceWorkflowAccessTests`. Full suite and local operator checks remain pending.
- **Step02 approval:** owner explicitly confirmed all acceptance criteria before Step03. **Step03 approval:** pending operator test and explicit signoff.

## Step 04 — Implementation record (2026-10-19)

- **Owner signoff Step03:** explicitly received on 2026-10-19; specific operator evidence provided by owner in prior turn, not independently logged here.
- **Files:** `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceReviewStorageEntities.cs`, `InvoiceReviewPersistenceConfiguration.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/ArginTaxPayerDbContext.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261019080000_AddInvoiceReviewWorkflowPersistence.cs`, `tests/ArginTaxPayer.Tests/Phase08InvoiceReviewPersistenceModelTests.cs` and this document.
- **Schema:** additive `invoice_reviews` cursor keyed by invoice, FK scoped by customer/taxpayer/invoice to existing `invoice_documents`, positive review revision, state constraint and index; `invoice_review_events` append-only audit, composite scoped FK, unique (invoice,revision), action/transition checks, immutable UPDATE/DELETE trigger, and restricted FK deletions.
- **Backward compatibility:** no modifications to Phase07 invoice tables, no data backfill, no automatic state initialization for existing invoices. Existing documents without a review row remain valid and must be interpreted as legacy/uninitialized until explicitly adopted through an authorized workflow service (Step05).
- **Security and CAS:** review revision is EF concurrency token. Actual compare-and-swap update, actor authentication, transaction and append-only event insert must be implemented and tested in Steps05–06. Do not expose direct unsafeguarded DbSet mutations as a workflow API.
- **Migration execution:** NOT APPLIED. Precondition: authorized operator explicitly verifies `current_database() = 'argintaxpayer_test'`, presence of preceding Phase07 migrations, reviews SQL and rollback behavior, and grants approval. DB snapshot and EF pending-model-change checks require scrutiny at implementation validation; a manually authored SQL migration may need a regenerated EF model snapshot before any later EF scaffold. Do not assert EF snapshot synchronized until verified.
- **Potential risks:** table creation locks, future migration snapshot drift, state adoption of legacy drafts, audit integrity if privileged database roles disable trigger. Mitigations: additive tables, FK, checked transitions, append-only guard, no automatic rollout.
- **Test added:** `Phase08InvoiceReviewPersistenceModelTests` inspects EF mappings without opening a connection. Not executed by GitHub connector. No PostgreSQL write, API, UI or HTTP tests performed. Run focused tests and full .NET test on macOS. Before database execution, inspect migration SQL from source and read-only gate, never run `dotnet ef database update` in this Step04 review.
- **Owner signoff:** pending. Step05 blocked.

### Step 04 — Controlled migration execution gate (2026-10-19)

- Added `scripts/phase08-step04-migration-preflight.sh`. It is strictly read-only and fails closed unless connection uses loopback, actual `current_database()` is `argintaxpayer_test`, Phase07 prerequisite migrations and tables exist, new Phase08 tables do not exist, and this migration is not already in `__EFMigrationsHistory`.
- This preflight **does not apply or authorize migration**. Operator must run focused .NET tests, inspect generated SQL and consider safe backup/restore readiness before explicit execution on the approved test DB.
- Generate review-only SQL with `dotnet ef migrations script 20261018150000_AddResumableInvoiceEntryWorkspaces 20261019080000_AddInvoiceReviewWorkflowPersistence --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api -o /tmp/phase08-step04-review.sql`. Confirm script has only the two new tables, indexes, audit trigger/function and EF migration-history insert.
- After explicit test-DB operator approval, run preflight immediately before `dotnet ef database update 20261019080000_AddInvoiceReviewWorkflowPersistence --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --connection "$ConnectionStrings__ArginTaxPayerDb"`; this is **not performed via GitHub**. Re-check EF history and schema and run smoke afterwards.
- EF model snapshot baseline was previously hand-maintained / may not exactly track SQL migration state; Step04 build and `dotnet ef migrations has-pending-model-changes` results must be reported rather than assumed. Do not auto-scaffold a new migration to silence a drift warning. Investigate diffs in Step04 before authorizing any future scaffold.
- **Execution status:** preflight script committed; never run on a live database here. Tests, generated SQL inspection, DB history, migration application all await operator evidence.

### Development ergonomics — Adminer forward (2026-10-19)

At owner request, standard macOS developer scripts now manage a separate **optional** AutoSSH localhost forward for remote Adminer (`127.0.0.1:18081 -> VPS 127.0.0.1:8081`). This does not alter the existing PostgreSQL tunnel `15432`, API `15089`, or Web `3000`. The change touches `scripts/dev-up.zsh`, `scripts/dev-down.zsh`, and `scripts/dev-status.zsh`: dedicated PID/log `~/.argintaxpayer-test/adminer.{pid,log}`, safe non-hijacking behavior when port 18081 is occupied, and independent Adminer readiness probe. The VPS **must already host** Adminer on its loopback port 8081; opening an SSH forward cannot install/start Adminer. No PostgreSQL migration or runtime DB mutation is performed by these script edits.

Operator checks: `zsh -n scripts/dev-up.zsh scripts/dev-down.zsh scripts/dev-status.zsh`, `zsh scripts/dev-up.zsh`, `zsh scripts/dev-status.zsh`, browser `http://127.0.0.1:18081`, then `zsh scripts/dev-down.zsh`. These checks remain pending on macOS; no assertion of functional test success.

## Step 05 — Implementation and validation record (2026-10-19)

- Owner explicitly approved Step04. Database migration application was **not independently verified** by this GitHub-only workflow. Before any integration test, operator must establish live test DB name and applied migration history; do not infer application from approval.
- Added `src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceReviewCommandService.cs`: authenticated, database-scoped command orchestration for `SubmitForReview`, `RequestChanges` and `Approve`, with current actor roles, current partner commercial ownership and active customer/taxpayer membership rechecked inside a serializable transaction. A separate review cursor and append-only event are committed together; CAS revision checked, concurrency/duplicate conflicts mapped to a conflict outcome.
- Existing historical invoices without a review row are treated as implicit review Draft with review revision 1. First accepted transition inserts the cursor and revision 2 history event in a single transaction; subsequent transitions compare `revision` and `state` in UPDATE predicate.
- Submit and approval require the Phase07 invoice lifecycle to be `Validated`; approved review status remains strictly internal and does **not** call Phase07 `FinalizeInternally`, mutate tax authority state, or transmit documents.
- The service currently has **no API exposure or DI registration** (Step07). Real operation replay identifiers and exact-once idempotency remain in Step06; this step provides revision/transaction conflict protection, not a completed idempotency guarantee.
- Added `tests/ArginTaxPayer.Tests/Phase08InvoiceReviewCommandContractTests.cs` for malformed requests before database use. **Tests not executed through GitHub connector**; full integration scenarios (valid customer submission, Level2/Level1 approval, ex-partner denials, concurrent submissions, audit trigger and forced rollback) remain mandatory operator evidence, with concurrency/idempotency extensions in Step06.
- macOS checks:
```bash
git switch phase/08-invoice-workflows-ui
git pull --ff-only
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter FullyQualifiedName~Phase08InvoiceReviewCommandContractTests
dotnet test ArginTaxPayer.sln
```
- No new migration, live official API, SMS, external authority submission, or customer data manipulation in this step.
- **Step05 status:** committed and awaiting macOS build/regression evidence and owner signoff.

## Step 06 — Idempotency / concurrency delivery (2026-10-19)

- Step05 acceptance explicitly confirmed by owner.
- Added `invoice_review_requests`, a new append-only table with a unique index on `(customer_user_id,taxpayer_id,actor_user_id,idempotency_key)`, immutable payload SHA-256 fingerprint and durable result review revision/state. FK references the scoped review cursor, preventing cross-tenant attachment. New EF entity/configuration and DbContext registration.
- New additive migration: `20261019090000_AddInvoiceReviewIdempotency`. It includes immutable-result trigger and rollback data guard. **NOT EXECUTED**. Do not modify an already applied Step04 migration; this new migration follows it.
- Every valid `InvoiceReviewCommand` now requires a bounded non-control idempotency key. Once actor and invoice scope are revalidated, an identical recorded key returns its persisted outcome before current-state revision comparison. Reusing the same actor/key for a different fingerprint or invoice results in `IdempotencyKeyConflict`; a new key with stale revision results in `Conflict`.
- For newly accepted operations, status cursor, event, and idempotency result are persisted together inside the serializable transaction. Unique-constraint or serialization collisions produce a conflict; after the winning request commits, an identical retry can obtain the stored result. No tax authority submission or side effect occurs.
- Tests added: `Phase08InvoiceReviewIdempotencyTests` for EF composite scoped index and invalid input; existing `Phase08InvoiceReviewCommandContractTests` remain. **No tests have been run by GitHub connector.** Actual simultaneous requests, replay against persisted test fixtures, request collision and rollback must be exercised on `argintaxpayer_test` as operator acceptance before signoff. DI/API remains pending Step07.
- **Migration prerequisite:** first independently confirm `20261019080000_AddInvoiceReviewWorkflowPersistence` in the test database `__EFMigrationsHistory`. Review `dotnet ef migrations script 20261019080000_AddInvoiceReviewWorkflowPersistence 20261019090000_AddInvoiceReviewIdempotency --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api -o /tmp/phase08-step06-review.sql`. With explicit approval and live `current_database() = 'argintaxpayer_test'`, operator may apply only the new migration. Never apply on production. Check EF model snapshot drift before scaffolding subsequent migrations.
- Focused macOS command: `dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter FullyQualifiedName~Phase08InvoiceReviewIdempotencyTests`; also run `dotnet test ArginTaxPayer.sln`.
- **Status:** committed, migration still pending, full PostgreSQL concurrency evidence and signoff pending. Step07 blocked.

## Step 07 — API contracts delivery (2026-10-19)

- Owner confirmed all Step06 acceptance criteria. Independent macOS/Ubuntu raw test transcripts are not included in this repo.
- Added `src/ArginTaxPayer.Api/Invoices/InvoiceReviewDevelopmentEndpoints.cs` and registered scoped `InvoiceReviewCommandService` and endpoint mapping in `src/ArginTaxPayer.Api/Program.cs` only when existing Development identity mode is enabled. Requests additionally require local loopback source IP.
- **Routes**: POST `/api/dev/invoices/{invoiceId}/review/submit`; POST `/api/dev/invoices/{invoiceId}/review/request-changes`; POST `/api/dev/invoices/{invoiceId}/review/approve`.
- **Body**: `{"expectedReviewRevision":1,"idempotencyKey":"unique-request-key","reason":null}` (reason mandatory and bounded for request-changes, forbidden for other actions). Bearer token required; authorization and tenant scoping re-evaluated by the command service on every attempt.
- **Stable outcomes**: 200 `{"state":"InReview|ChangesRequested|Approved","revision":N,"authoritySubmitted":false}`; 400 `invalid_invoice_review_request`; 401 unauthenticated; 403 forbidden; 404 invoice hidden/not found or non-loopback; 409 `invoice_review_revision_conflict` or `invoice_review_idempotency_conflict`. Do not leak raw exception or tenant metadata in errors.
- **Limits**: These are explicitly **development-only command contracts**, not publicly hosted production invoices APIs. Query/read contracts and UI listing are planned for Step08; do not claim this step delivers review GET endpoints.
- **Tests added**: `tests/ArginTaxPayer.Tests/Phase08InvoiceReviewApiContractTests.cs` checks HTTP code mapping. All code/tests are committed but GitHub connector does not execute them; owner must run full solution test, launch localhost API, test no-token 401, wrong-tenant denial, permitted transitions, idempotent replay, stale revision 409, malformed 400 and non-loopback 404.
- **Migrations**: No new migrations or database writes have been executed through this change. Step06 migration must already be installed on `argintaxpayer_test` before operational HTTP write tests.
- **Current status**: awaiting operator verification and explicit owner signoff. Step08 blocked.

### Step 07 — HTTP 500 corrective patch (2026-10-19)

Operator curl of POST `/api/dev/invoices/{invoiceId}/review/submit` without bearer token returned HTTP 500: `System.Text.Json` attempted to serialize `AsyncTaskMethodBuilder...AsyncStateMachineBox` because Minimal API handler was declared `async` yet returned the un-awaited `Task<IResult>` from `Run` as its expression body. Fixed all three review routes (`submit`, `request-changes`, `approve`) by removing outer `async`: delegate now directly returns `Task<IResult>`, which ASP.NET Core awaits and writes as `IResult`. No DB schema/API shape changes.

**Operator retest required:** run `dotnet test ArginTaxPayer.sln`; restart the API with `zsh scripts/dev-down.zsh && zsh scripts/dev-up.zsh`; then repeat no-token curl (expected 401). This HTTP 500 is a confirmed defect; step is **not accepted** until actual smoke result is provided. On an absent/unreachable test DB, initial transaction setup may produce a separate error; confirm `/health/ready` and the test database.

## Step08 — Filter forwarding regression correction (2026-10-19)

- Operator reported that changing review status or typing search had no effect on invoice list.
- Root cause verified in `apps/web/app/api/invoices/[...segments]/route.ts`: the Next.js authenticated proxy forwarded only `customerId`, `taxpayerId`, `page`, `pageSize` and silently stripped newly introduced `reviewState` and `search`. This made server-side filters unreachable from UI.
- Fixed overview-only forwarding of allowlisted `reviewState` values and bounded, control-character-free `search`. Existing authenticated cookie, same-origin and legacy query behavior remain unchanged; invalid filter values fail 400.
- Added `apps/web/tests/phase08-invoice-overview-filter.test.mjs` to guard the forwarding code against regression. These are code contract tests; they do not substitute for browser + HTTP acceptance.
- **Operator retest:** `git pull --ff-only`; `npm --prefix apps/web test`; `npm --prefix apps/web run typecheck`; restart Next.js with dev-down/up; verify filter InReview/Approved/WorkingDraft and search for partial invoice UUID, including no-results and paging reset.
- **Status:** patch committed, live execution pending; Step09 blocked until explicit owner acceptance.

### Step08 — Business search, support IDs, buyer column and loader repair (2026-10-19)

Operator reported HTTP 400 when entering partial invoice IDs, and requested business-oriented lookup, an opaque globally unique system-support identifier, buyer column, and stable table loading.
- Root cause for spurious 400 found in `apps/web/app/api/invoices/[...segments]/route.ts`: a double-escaped regular expression matched ordinary digits/letters. Corrected to actual control-character-only validation and added proxy regression checks.
- `InvoiceDraftApplicationService.ListVisibleAsync` no longer uses GUID `ToString().Contains()` for general search. Scoped search now covers seller display name and normalized identifier, seller invoice-participant economic code, and canonical stored invoice/workspace JSON content (including buyer name when saved); for an exact system support code, it resolves back to the UUID and uses indexed equality. Buyer economic code and normalized buyer identifier are now matched through the scoped Party record plus `BuyerPartyId` / `buyerPartyId` extracted from JSONB; this new SQL translation must pass operator tests before being considered validated.
- `InvoiceSupportCode.Encode` is an exact reversible, URL-safe base64 encoding of the complete existing random UUID (22 characters). No truncation or sequential number is used. No migration and no independent key allocation. Its uniqueness is identical to that of the original UUID; UUID v4 collision probability is negligible but not mathematically zero, and there is no cross-table unique database constraint between workspaces and invoice documents.
- Buyer display name and seller display name are included in list responses; buyer is read from existing canonical JSON snapshot, never from untrusted client input. Some incomplete historic drafts may have an empty buyer.
- Client displays support code instead of truncated UUID with click-to-copy, adds buyer column, search help, debounces search, ignores stale in-flight responses, keeps previous results under the loading overlay and uses reduced-motion-respecting shimmer skeleton to avoid the brief empty-table flicker.
- Files include invoice application service, new support-code helper, workspace UI, globals.css, Next.js proxy, tests and this document.
- **Acceptance still pending:** full macOS .NET build/tests, web typecheck/tests/build, authenticated end-to-end search of seller/buyer, support code, filter combinations, out-of-order requests, loader, existing data. This GitHub-only session has not executed user machine tests. No new migration, data mutation or tax-authority connection.

#### Step08 follow-up: explicit JSONB search fix
- Both invoice snapshots and workspaces are `jsonb`; PostgreSQL does not support direct `ILIKE` against a JSONB column. Registered SQL-translated built-in `jsonb_extract_path_text` and now filter only named buyer fields. Added scoped buyer Party and buyer economic-code lookup through the stored buyer-party GUID. No migration.
- Tests: `Phase08InvoiceSupportCodeTests`, `Phase08InvoiceJsonSearchTranslationTests`, and updated Next.js proxy regex test. These have not yet executed on macOS; do not mark acceptance complete.
- Operator checks: `dotnet test ArginTaxPayer.sln`, `npm --prefix apps/web test`, `npm --prefix apps/web run typecheck`, `npm --prefix apps/web run build`, restart local services, and verify actual searches in the logged-in UI.

### Step08 — Approved human-readable identifiers (2026-10-19)

- Owner approved identifier format `00001-05-MTU`, where five digits are per-admin/per-Persian-year serial, `05` is Persian year 1405, and a fixed three-letter admin code identifies the **current** controlling partner Level2 or root Level1 when no partner exists. After customer transfer, new invoices take the new current admin code and already allocated invoice codes remain unchanged.
- New additive migration `20261019100000_AddInvoiceHumanReferences`, **NOT APPLIED**: creates `invoice_admin_codes` (global stable 3-letter code allocation, alphabetic AAA..ZZZ), `invoice_support_counters` (atomic UPSERT per admin/year, hard cap 99999), `invoice_support_references` (globally unique immutable full codes, scoped source-kind/source-id), AFTER INSERT triggers on both `invoice_documents` and `invoice_entry_workspaces`. Uses owner row lock to serialize against transfers. Existing records backfilled in deterministic modified-date order using ownership at **migration time**; historic owners are not reconstructed. Migration must run only on `argintaxpayer_test` after SQL review. Backfill fails closed if any invoice has no commercial owner and rolls back the migration. No customer/source records are deleted. Existing DB UUIDs remain primary keys.
- Calendar boundary function covers 2024–2040 explicitly and rejects unsupported years rather than silently mislabelling a Persian year. An operator must extend/review this before unsupported future calendar years. Code issuance uses the current Tehran-local creation time, not an invoice's backdated issue timestamp.
- Invoice-list service now returns the stored `SystemCode` and searches its exact value. UI shows the code, supports copying it, and never silently substitutes the UUID. The earlier `InvoiceSupportCode` reversible base64 helper remains legacy utility but is no longer used for list identifiers.
- **Caveat:** admin code allocation is automatic (AAA, AAB, etc.) and once assigned immutable. `MTU` is an illustration, not automatically assigned to any known existing admin. 17,576 possible admin codes and BIGINT serials per admin/year (minimum 5 digits, growing to 6+ digits without reset); exhaustion of BIGINT causes a transaction error rather than reuse.
- **Verification pending:** full .NET tests, web typecheck/tests/build, explicit migration preflight, zero orphan ownership rows, successful test-db migration, seeded/backfilled invoice counts and uniqueness queries, simultaneous insert/transfer scenarios, and final owner acceptance. No test DB migration or integration tests were executed remotely by the GitHub connector. Step09 blocked.

### Step08 update — Unbounded five-digit display minimum (2026-10-19)
Owner approved yearly reset and expanding serial length. Updated **pending/unapplied** migration `20261019100000_AddInvoiceHumanReferences`: `invoice_support_counters.last_serial` and `invoice_support_references.serial` are BIGINT, serial checks require positive values rather than `<=99999`, and `system_code` format accepts at least five numeric digits. `lpad(serial::text,5,'0')` preserves all digits above five while padding smaller values. Each admin and full Persian year still has its own atomic counter; `now()` uses Tehran-local creation time for year lookup. No reset within a year and no reuse of old codes.

**Migration status:** Must verify that the migration has not already been applied on the test database. If applied, do not edit or rerun it as-is; instead create a new additive ALTER migration for the existing database. No migration has been run by this GitHub-only session. Build, tests and operator approval pending.

### Step08 — Test DB migration P0001 due to orphan legacy ownership (2026-10-19)
- User-provided actual migration log: EF Build succeeded; `20261019100000_AddInvoiceHumanReferences` failed with `P0001 Missing customer ownership for system code allocation` in the historical invoice backfill. This is a missing `customer_commercial_owners` row for at least one pre-existing invoice customer, NOT a SQL syntax problem.
- Updated the unapplied migration: backfill joins owners and **skips only orphaned historical invoice rows**, emits aggregate NOTICE, and leaves their original invoice/workspace records untouched. Future inserts still fail closed when owner missing; no guessing an administrator, no global defaults silently assigned.
- Operator must verify the migration is absent in EF history after rollback and run **only on argintaxpayer_test**. Afterwards use a left-join query to list invoices lacking support refs. Assign a verified commercial owner using the existing privileged ownership workflow; then perform controlled `phase08_allocate_invoice_support(source_kind, source_id, customer_user_id)` for each orphaned record, with explicit operator authorization and tenant validation. Do not directly fabricate ownership or run automatic mass attribution.
- This patch is not a substitute for owner reconciliation. Build, integration and test database migration are still pending verification.

### Step08 — operator policy update: default Level1 for orphaned legacy invoice owners (2026-10-19)
- Operator explicitly approved default Level1 ownership for legacy customers without a commercial owner; root admin may later transfer these customers using the existing approved transfer workflow. Existing ownership links, including Level2 links, must remain untouched.
- Revised *unapplied* migration: Before historic invoice reference assignment, find invoice customers missing `customer_commercial_owners`. If and only if **exactly one active user has Level1 role**, insert missing owner rows assigning that Level1 as root with partner NULL, channel `Level1Created` (DB-allowed value 2), revision 1. Do not overwrite existing ownership; `ON CONFLICT DO NOTHING`. If multiple/no active roots, raise descriptive error and leave transaction unchanged.
- Fix SQL syntax `END $;` to `END $$;` by replacing backfill block. After assigning default owners, backfill all legacy invoice codes. No legacy invoice deletion. Future invoice creation continues to depend on verified commercial ownership.
- Pending: operator Build + test DB migration, inspect Level1 candidates and reassigned customers, uniqueness and post-transfer behavior. No migration was executed by GitHub connector.

### Step08 primary Level1 selection correction (2026-10-19)
- Operator verified two active Level1 accounts; confirmed principal account as UUID `3054f5ac-38b4-429e-8dc2-8f9e1125bc7f` and normalized mobile `+989192424119`; second is a STEP10 test administrator.
- Updated unapplied migration so orphan legacy invoice customer ownership is assigned **only to the confirmed primary Level1**, requiring an active matching UUID, mobile, and Level1 role. Missing/mismatched primary account fails closed. Existing owned accounts and test admin remain unchanged.
- Operator must build, verify target `argintaxpayer_test`, migrate and validate owner counts. Not yet verified by a live DB execution.

### Step08 follow-up — invoice register loading and search UX (2026-10-19)
- Reused the existing `argin-loader`, `argin-loader-overlay` and `argin-loader-spinner` styles instead of a custom skeleton.
- Fixed first-load race: `/api/taxpayers/list` no longer controls invoice-table `loading`; only the current invoice overview request can dismiss the overlay, including time spent in search debounce. Stale requests are ignored by request sequence checks. While loading, the prior data remain mounted under an overlay, preventing intermediate empty-table flash.
- Made the invoice search form control take the full available row width on desktop and mobile. Changed passive `?` span into an actual toggle button with an accessible tooltip.
- Added `apps/web/tests/phase08-invoice-loading-ui.test.mjs` as a static regression test. Live macOS UI checks and operator approval pending; Step09 not started.

### Step 09 — Implementation and acceptance (2026-10-19)
- Owner explicitly approved Step08 after browser/table-spinner/search checks. Step09 begun according to fixed 22-step plan.
- Added a **development-only** authenticated GET `/api/dev/invoices/form-metadata`, backed directly by `OfficialV79InvoicePatternMatrix.Create()` in the domain. Returns official edition, explicit not-verified-for-submission flag, allowed (type,pattern) pairs and rule-derived conditional-field requirements (contract, bill, flight type, gold fields). Current Phase07 resumable wizard only accepts types 1 and 2, so no type-3 save path is presented.
- Extended allowlisted Next.js BFF route `/api/invoices/form-metadata`. Frontend now fetches entries from backend and uses them as sole selector options; disables selects while metadata unavailable, shows explicit error, rule edition and notice for patterns requiring supplemental fields. Pattern display labels remain localized UI copy, not eligibility rules. Existing server-side `InvoiceEntryWorkspaceService.Valid` revalidation is unchanged.
- Added `apps/web/tests/phase08-step09-dynamic-form.test.mjs` contract checks. **No runtime tests have been executed by GitHub connector.**
- **Outstanding acceptance / scope:** Conditional-field data entry and persistence (contract, service bill, flight type, gold fields) were implemented in subsequent Step09 commits; full UI/API execution, field-specific response errors and official submission authorization are still not verified. Do not represent this step as complete merely because metadata selectors are now dynamic. Operator must run macOS `npm --prefix apps/web test`, typecheck, build, `dotnet build ArginTaxPayer.sln`, `dotnet test ArginTaxPayer.sln` and HTTP/browser validation before signoff. Never claim tax authority issuance readiness.

#### Step09 — Conditional field entry, persistence and server validation
- Wizard renders conditional inputs from the official metadata `requiredFields` entries. It stores contract reference, service-bill identifier, flight type and the four gold-specific amount fields in the existing JSON workspace payload; reopening a draft restores them. No schema change.
- Extended the allowed proxy fields and the typed `SaveInvoiceEntryRequest` / `InvoiceEntryView` contracts. Backend validation enforces the domain V7.9 type/pattern matrix, proper conditional field shape, disallows stray condition-specific fields and requires relevant fields when completing step 1 or 2. Incomplete stage-0 drafts remain resumable.
- **Acceptance pending:** macOS .NET build/test + web tests/typecheck/build; authenticated form-metadata GET, new/save/reload pattern 4/5/6/3, invalid combination/direct API rejection, scoped access and no regression to regular sales patterns. No connector-side test execution was possible. Step10 not started; Step09 awaits owner approval.

### Step 10 — Seller, fiscal memory and buyer header integration (2026-10-19)
- Step09 explicitly owner-approved; fixed step sequencing preserved.
- Added development-only authenticated `GET /api/dev/invoices/header-options/{taxpayerId}` returning seller identity and fiscal memories from authorized `TaxpayerScopeService.ScopedQuery`. Unowned taxpayer IDs return 404, unauthenticated session 401; no key material exposed.
- Next.js invoice proxy allowlists the scoped header-options GET. Wizard loads on selected taxpayer, resets selected fiscal memory when taxpayer/customer changes; displays identifier and option status. A fiscal-memory ID is optional for an incomplete working draft: absence of a verified memory cannot be interpreted as permission to submit to the tax authority.
- The existing save request and canonical draft view now persist `fiscalMemoryId` in draft JSON, with server-side taxpayer equality validation; saves reject memory IDs belonging to another taxpayer. No DB schema migration.
- Buyer text must resolve to a selected buyer ID (free-text buyer name is rejected on client), and backend pre-existing buyer validation still verifies active and linked customer. Existing Persian calendar selector and UTC timestamp serialization are preserved, not replaced with an unverified timezone conversion. Existing type/pattern validation continues.
- Added web contract tests. **GitHub edits were not run through .NET or Node tests in this session; no local DB mutation or live provider connection.** Operator must run macOS full build+tests, visit invoice modal, test seller-memory switching, buyer selection, save/reload, negative cross-taxpayer memory on POST, Jalali date and revision CAS. Step 10 awaits explicit acceptance; Step11 not started.

### Step10 owner-requested fiscal-memory connection specification (2026-10-19)
- User requested two connection modes (without independent certificate, prioritized, and certificate-backed signing deferred), with prioritized mode allowing (a) generation of new public/private key pair after mandatory taxpayer profile completion, and (b) import of already connected private-key text + portal fiscal-memory identifier, followed by genuine read-only connectivity verification.
- User also requires one fiscal memory per active time-based single-memory plan unless a governed admin exception approves an additional memory; quantity plans retain configurable capacity. These are application entitlement policies and are not asserted as a tax-authority-imposed rule.
- Full source-qualified architecture and requirements: [Fiscal Memory Connection & Key Onboarding](../architecture/fiscal-memory-connection-onboarding.md).
- **Official-source caution:** older authority connection instruction `RC_TICS.IS_V01` requires authenticated/signed request handling; a modern no-certificate path is **not validated** against user's current SDK, which was not retrievable from accessible Project files this turn. Do not equate “without independent signature certificate” with unsigned authority API requests or claim real connectivity.
- This is **documentation only**; no key material was generated, uploaded, or committed; no database migration/permission changes or live authority calls were performed. This expands requirements for scheduled Phases09/10/12/16 without silently altering the fixed 22-step Phase08 plan. Step10 operator acceptance is still pending.

### Step10 corrective patch — first fiscal memory permission error (2026-10-19)
- Reproduced root cause from source review: `FiscalMemoryManagementService.ListAsync` enforced Level1-only access. Taxpayer UI always loads `GET /memories` after opening a taxpayer, so owners and currently assigned Level2 hit 403 and could not complete the onboarding flow. The registration check also used `ParticipantScopeService.CanManageAsync` with a software-profile prerequisite potentially stricter than the current official taxpayer scoped-query.
- Replaced first-memory registration/list checks with a consistent `TaxpayerScopeService.ResolveAsync` + current `ScopedQuery(actorId,level1)` + active taxpayer membership. This grants access to active taxpayer's customer account and currently authorized Level2/Level1 only; returns 401 for unauthenticated and 404 for out-of-scope taxpayer IDs. It does NOT grant access to other customers, former partners, inactive taxpayers, or certificate-management transitions; those retain Level1-only policy.
- The existing first-memory-only invariant, serializable transaction, revision checks, pending-verification status and no real authority connection remain unchanged. A second memory still requires the future reviewed exception process; no private keys or JWTs are accepted by this endpoint.
- Added `apps/web/tests/phase08-step10-fiscal-memory-access.test.mjs` guard. Changes are code-only, no migration. **Live macOS integration and browser acceptance pending**. Test roles: owning customer, current assigned Level2, Level1, other customer, former Level2, missing MFA, second memory attempt. Step11 remains blocked.

### Step11 — Phase06 catalog-linked invoice line snapshots (2026-10-19)
- Operator explicitly approved Phase08 Step10 after local tests, including first fiscal-memory registration permission fixes.
- Preserves existing searchable Phase06 product/service catalog and manual line entry. Selecting catalog items now captures `BaseUnitId` and customer `InternalCode`; these are typed nullable `UnitId` / `CatalogCodeSnapshot` fields in the existing `InvoiceEntryLine` JSON, without a DB migration.
- The server does not trust client-side snapshot fields: it verifies catalog item belongs to the current software customer, fetches the canonical base unit/code and serializes canonical line metadata. Reopening existing saved entries returns the original snapshot from draft JSON, including when the catalog's current code/unit changes; inactive catalog items may be retained only if already referenced with a prior snapshot. Selecting newly inactive catalog items is forbidden. Manual unlinked lines cannot smuggle unit/catalog metadata.
- Existing amount/quantity/VAT validation, revision/CAS, access scoping and calculation preview are retained. Catalog names are copied to the draft description; later catalog title edits do not rewrite the saved description.
- Added static regression contract test `apps/web/tests/phase08-step11-catalog-snapshots.test.mjs`.
- **Outstanding acceptance:** .NET build/test + npm test/typecheck/build on macOS; add catalog product/service with unit, save, edit catalog title/internal code/base unit, resume draft and ensure saved line stays stable; verify cross-customer catalog ID rejection, inactive new selection rejection, manual line success and arithmetic. Existing old drafts without stored unit metadata are initialized with current catalog values at their next save; cannot reconstruct historical past values retroactively. Formal regulatory tax-line frozen aggregate/snapshot conversion remains governed by Phase07 rules and should not be confused with draft JSON.
- No migration, tax-authority call or production data update. Step12 blocked until owner acceptance.

### Step12 — Deterministic decimal financial summary (2026-10-19)
- Operator explicitly approved Step11.
- Canonical server-side `InvoiceDraftSettlementPolicy.Calculate` now calculates monetary Gross, Discount and VAT in .NET decimal **per line**, rounding each to 2 digits with `MidpointRounding.AwayFromZero`, then sums the rounded line amounts, and constructs Net and Grand Total. Preview only; not an authority-accepted tax amount. The existing per-invoice settlement consistency rule now uses these canonical values.
- The invoice wizard no longer aggregates binary floating-point products directly. New `invoice-financial-preview.mjs` uses exact parsed base-10 input and BigInt arithmetic to mirror server's per-line rounding, with an accompanying TS declaration. Initial invalid inputs report an error at save rather than silently issuing a financial value. Currency display remains Rial.
- Updated UI preview in Step2 for gross, discount, net, VAT and total. No fixed VAT rate is inferred (each line retains explicit VAT rate). There is no model for other charges in current wizard: the UI continues to explicitly show these as unsupported; this does not claim full financial approval or tax-authority compliance.
- Regression tests: `Phase08Step12FinancialSummaryTests.cs` and `phase08-step12-financial-preview.test.mjs`; covers VAT, discounts, decimal half rounding, split cash/credit reconciliation and invalid entries. Existing draft JSON and financial summary read contract are unchanged. No migration.
- **Acceptance gate:** run `dotnet build ArginTaxPayer.sln`, `dotnet test ArginTaxPayer.sln`, `npm --prefix apps/web test`, `npm --prefix apps/web run typecheck`, `npm --prefix apps/web run build` on macOS. In browser test multiple decimal quantities, small fractional Rial values, discount, VAT, save/reload and compare UI with the API's `financialSummary`; verify mixed settlement and old draft opening. **Potential limitation:** BigInt cents are converted to JavaScript Number for UI formatting and mixed-settlement input; totals above JS safe integer precision require dedicated string-decimal formatting and are NOT fully guaranteed by this step. No provider calls or production updates.
- Step13 not started; awaiting explicit owner acceptance.

### Step13 — Cash, credit and mixed payment terms (2026-10-20)
- Owner explicitly approved Step12. The existing UI and server settlement mechanisms have been tightened without implementing collections or ledger accounting.
- Canonical server policy supports Unknown (only for incomplete draft), Cash (cash equals total, credit 0), Credit (cash 0, credit equals total), Mixed (both positive and their sum exactly equals canonical grand total). Both amounts may be omitted for unfinished drafts; partially specified pairs or contradictory mixes are rejected.
- Completing last wizard step (`LastCompletedStep == 2`) rejects Unknown or missing cash/credit amounts even if the client omits them; intermediate steps remain resumable.
- UI shows the unknown-method warning, disables final completion for unknown or invalid mixed terms and permits saving an incomplete draft as completed-step 1, without falsely presenting it as settlement-ready. Backend remains authoritative and checks direct API tampering.
- Tests: `Phase08Step13SettlementPolicyTests.cs` covers cash/credit/mixed edges including mismatch/zero; `phase08-step13-settlement.test.mjs` checks UI/server contract.
- No DB schema change, financial receipt, subscription charge or actual partner payout is created. Versioned invoice-pattern-specific authority submission constraints are not inferred from generic payment choices.
- Operator acceptance pending: macOS `dotnet build/test` and npm `test/typecheck/build`; browser check all four methods, save/reopen mixed amounts, direct API invalid totals and unfinished draft preservation. No tests executed by the repository connector. Step14 awaits owner signoff.

### Step13 bugfix — premature settlement error when navigating from line entry (2026-10-20)
- Owner screenshot showed the **line entry screen** reporting settlement selection required before the final settlement screen was accessible.
- Root cause: `nextStep()` passed `completeStep=2` into `saveWorkspace(2,false)`. The step13 validation erroneously treated navigation to the summary as final settlement completion, and the server's `LastCompletedStep==2` check would have rejected a direct work-around as well.
- On transition from UI step 1 to step 2, persist `lastCompletedStep=1` (lines saved, settlement not completed) while still navigating to visible step 2; only final save `saveWorkspace(2,true)` enforces settlement. This preserves server-side authoritative validation. Existing incomplete drafts remain resumable.
- Added `phase08-step13-navigation.test.mjs` static regression guard. No migration; macOS operator test and owner signoff still pending.

### Step13 corrective patch — review action and catalog dropdown keyboard dismissal (2026-10-20)
- Reported operator issues: (1) `بررسی پیش‌نویس ذخیره‌شده` appears unresponsive when its `dirty` flag disables the action, (2) pressing Escape in an open searchable catalog closes the enclosing invoice modal instead of just the options list; no visible close button.
- The review panel now offers an explicit `ذخیره تغییرات و بررسی` action on dirty drafts. It invokes the existing scoped revision-aware save; after the saved revision is accepted and the dirty state clears, it fetches the stored-draft validation. Failed saves release the button and display errors rather than leaving a disabled spinner. Clean drafts still directly refresh validation.
- The catalog picker now offers a visible close button, outside-pointer dismissal, and handles Escape on the whole picker with preventDefault/stopPropagation while open. Other modal Escape behavior is preserved.
- Added `phase08-step13-review-dropdown-regression.test.mjs`. These are application-only changes without migration, connection test, or tax-authority calls.
- Operator acceptance still needed: macOS npm test/typecheck/build; in step2 open catalog list, press Escape and confirm the modal stays; use close button and outside click. In step3 change a field and select Save+Review, confirm one revision increment and updated preview; on server error the action must become clickable again. Step13 not owner-approved until operator confirmation.

### Step13 additional Escape regression fix (2026-10-20)
- Owner confirmed first picker-level Escape handling did not prevent modal closure. Root cause: parent `document` keydown handler independently closed the modal whenever Escape arrived; child suppression was not a sufficient global invariant.
- Parent now detects the open catalog via `data-invoice-catalog-open` before closing; dispatches a dismissal request and returns without closing the modal. Picker listens to that request, clears open state, and retains its dedicated close button and outside-pointer dismissal. With picker closed, Escape still closes modal.
- Regression guard: `apps/web/tests/phase08-step13-modal-escape-priority.test.mjs`. Requires operator macOS/browser acceptance; not claimed runtime verified.

### Step14 — Draft UX persistence and conflict safety (2026-10-20)
- Owner explicitly approved Step13, including the latest Escape-priority fixes.
- Existing server `InvoiceEntryWorkspaceService.SaveAsync` already provides persisted draft JSON, revision compare-and-swap and authorized reloading. These invariants were preserved; no schema changes.
- The UI now captures a form-data snapshot after opening/rehydrating its modal, warns before closing via close button or Escape if fields diverge, and warns before browser unload. On successful save the baseline is reset. The snapshot includes header, seller memory, buyer, conditional data, line rows, description and settlement. It is a UI safeguard, not a persistent autosave.
- Reopening a saved draft resumes at one step beyond its last completed step (up to final step2), instead of repeatedly presenting an already completed step. First save and subsequent saves update the same existing workspace ID rather than deliberately creating another record.
- Concurrent edits remain rejected via server `ExpectedRevision`; the UI 409 message now explicitly says the form was not saved and that the operator must reopen the workspace. No silent conflict overwrite.
- `phase08-step14-draft-resume.test.mjs` covers snapshot/discard checks, resume offset and CAS contract.
- **Test acceptance needed:** macOS dotnet build/test, npm test/typecheck/build; create draft and resume; edit a saved line then click close or Escape and verify cancel; test browser refresh warning; save and reopen ensuring exactly one workspace; try two browser sessions to confirm 409 avoids overwrite and requires reload. These actions were not executed by GitHub connector. No database migration, authority call or production data update. Step15 remains blocked pending owner signoff.

### Step15 — Internal invoice review UI, guarded workflow and reasoned rework (2026-10-20)
- Owner explicitly approved Phase08 Step14. Step16 remains deferred until Step15 owner acceptance.
- Persisted internal review metadata in the existing typed invoice-workspace JSON (`InternalReviewState`, `InternalReviewReason`, submitter/reviewer and review UTC). The review is **only local/internal**, never a tax-authority acceptance, digital signature or invoice transmission. No new database migration was introduced.
- Scoped developer-only `POST /api/dev/invoice-workspaces/{id}/review` accepts exactly `expectedRevision`, `action` (`submit`, `approve`, `return`) and optional `reason`. Next.js BFF forwards only the allowlisted operation under authenticated local origin and bounded JSON input.
- Workflow: `Draft|ChangesRequested -> InReview` (complete saved invoice required); `InReview -> Approved` or `InReview -> ChangesRequested`. `return` requires a nonempty, bounded textual reason. Internal review of one's own submitted invoice is forbidden; approval/return require an active Level1/Level2 admin with MFA and current scoped taxpayer access. Every decision uses atomic `ExpectedRevision` compare-and-swap, increments revision and records reviewer/time. A saved draft cannot be overwritten normally while `InReview` or `Approved`; edits to a returned invoice reset review to `Draft`, requiring a new submission.
- Added a read-only review dialog from workspace list: line summaries, money totals, saved revision, validation feedback, internal actions according to actor and current state, error feedback and reason input. The invoice overview receives workspace review state from persisted typed draft JSON; filters include those states. No copy of live uncommitted edits is approved.
- Tests added: `Phase08Step15InternalReviewPolicyTests.cs` checks topology and reviewer segregation; `phase08-step15-internal-review.test.mjs` static contract assertions for UI and proxy.
- **Limitations/next phase:** the existing JSON record stores only *current* review metadata and latest reason, not append-only review events. Phase08 Step16 must add durable immutable audit/history before production use; no immutable historical decision ledger is claimed. In this development step, the approved workspaces are read-only through normal update API, but not yet attested or submitted. Existing UI preflight is informational and not proof of official rule readiness. Connection/signing is deferred to Phases09/10.
- **Operator acceptance pending:** run Mac `dotnet build ArginTaxPayer.sln` / `dotnet test ArginTaxPayer.sln`, and `npm --prefix apps/web test`, `npm --prefix apps/web run typecheck`, `npm --prefix apps/web run build`; exercise owner submit, separate Level1/Level2 approval, reasoned return, edit/resubmit, self-review rejection, other customer denial, dual-tab 409, approved editing rejection and overview filters. No tests were executed by GitHub connector. No DB migration/provider call/production mutation.

### Owner correction — Invoice self-service, administrator support, no approval gate (2026-10-20)
- **Supersedes the earlier Step15 interpretation:** customer software users issue, validate, send and follow up their own taxpayer invoices independently, within subscription/authorization/authority rules. No Level1/Level2 approval is a prerequisite for normal invoice issuance or transmission.
- Level2 represents an accounting company or reseller managing its currently assigned software customers, possibly hundreds. Level2 may search scoped invoices and investigate troubleshooting requests using Argin system reference. If unresolved, Level2 escalates the problem with correlation/reference to Level1.
- Level1 provides cross-system technical/operational troubleshooting and server-log investigation; server trace records and secrets must be redacted/permissioned, and correction actions audited. Level1 is not a mandatory invoice approver.
- The internal review UI/code created during Step15 was based on a mistaken assumption. Treat it as **non-authoritative experimental development UI**; it must not be treated as the obligatory issuance workflow. Rework Step15 acceptance to cover optional preflight, error details, scoped support search and escalation, not approval decisions. Do not set tax authority submission eligibility based on `InternalReviewState` or any admin approval.
- Existing commercial exceptions requiring Level1 authorization (e.g., extra fiscal memory under a single-memory annual plan, approved financial expenses) remain separately governed. This correction is specifically for invoices.
- Immediate implementation follow-up: remove the confusing review approval UI and its route, keep independent preview validation and customer draft editing, then implement scoped support diagnostics and escalation with immutable audit in later controlled work. Step15 cannot be considered complete under the prior approval checklist.

### Step15 final scope clarification and cleanup — operator decision (2026-10-20)
- **Explicit owner instruction:** Support and escalation take place **outside ArginTaxPayer for now**. Do not build support tickets, in-app technical escalations or mandatory invoice admin review in Phase08. Reprioritize an appropriate ticketing system **only after the entire planned project is completed**, alongside other post-release enhancements. Its implementation priority is deliberately not assigned yet.
- Customer users issue and follow up their own invoices independently when their fiscal rules, connection and subscription permit. Level2 handles its customers operationally and advises on issues; Level1 handles harder technical and infrastructure issues if escalated externally. Support receives the **Argin-generated unique system identifier** to search the corresponding authorized record; it is not the legal tax identifier and must not be conflated with it.
- Step15 acceptance is re-scoped to **existing saved-invoice detail, optional local preflight report, errors and warnings**, with customer self-service and correct tenant isolation. The existing `InvoiceValidationReferenceService.ValidateAsync` remains an informational preflight with `IsSubmissionAuthorized=false`, and its rule coverage is not a guarantee of compliance or live transmission.
- Source-level cleanup removed the now-unreachable admin review action endpoint, Next BFF route and invoice list button, plus orphaned `ReviewAsync`, `InvoiceInternalReviewPolicy`, the unused `internal-review-panel.tsx` and its obsolete policy tests. Retained existing draft save/reopen, UI validation panel, scoped API reads, reference lookup and CAS revision checks. Existing JSON backward-compatibility review metadata fields are not used as authorization gates, and legacy records are not rewritten. No DB migration.
- Technical source review confirmed: `GET /api/dev/invoice-workspaces/{id}/validation` reuses scoped `drafts.GetAsync`; list and save are scoped by live `TaxpayerScopeService`; Next BFF uses authenticated same-origin POST controls; no external submission or admin approve path has been added. However **this was static review only**. Runtime compilation, tests and manual UI confirmation are still required; do not claim macOS acceptance until operator confirms.
- Post-project backlog item: design tenant-scoped ticket lifecycle, unique Argin invoice code lookup, support access by current Level2 commercial ownership, Level1 escalation and restricted log correlation, sensitive-field redaction, full audit and service-level operations. Design and prioritize against other work after all existing project phases are completed; explicitly out of scope now.
- Step16 remains on the fixed 22-step plan. It must not silently reintroduce compulsory approvers or invoice issuance gates; capture approved audit/revision requirements appropriate to self-service.

### Step16 — Invoice workspace revision history and immutable audit (2026-10-20)
- Owner approved Step15 as customer self-service preflight. No invoice admin approval/ticketing stage was reintroduced; ticketing remains a post-project backlog item.
- Existing `invoice_document_revisions` already stores canonical immutable Phase07 document snapshots but does NOT include editable Phase08 `invoice_entry_workspaces` saves. Added a separate append-only `InvoiceEntryWorkspaceRevision` history entity with workspace/customer/taxpayer scope, exact revision, authenticated actor ID, server-defined action/reason, complete canonical JSON snapshot and UTC timestamp.
- Each create/update now wraps workspace mutation **and** revision-history insertion in a single DB transaction. The update retains revision CAS; a concurrent stale write returns a conflict without inserting a history record. History is server-controlled, not inferred from untrusted user display names or client-provided reasons.
- New controlled migration: `20261020104500_AddInvoiceWorkspaceRevisionAudit.cs` creates `invoice_entry_workspace_revisions` with FK/PK, scoped index, and PostgreSQL UPDATE/DELETE-rejecting trigger. Down migration rejects dropping nonempty history. This migration is **committed but not executed**, and requires review of generated SQL, verification that `current_database() = 'argintaxpayer_test'`, and explicit operator approval before applying.
- Scoped `GET /api/dev/invoice-workspaces/{id}/history` is dev-local only and uses `TaxpayerScopeService.ScopedQuery`; BFF allowlists GET; UI displays version, actor UUID, UTC occurrence and server-written reason on the saved invoice. Never exposes the stored canonical JSON snapshot over history endpoint; read-only metadata is sufficient for timeline.
- Added `phase08-step16-invoice-audit.test.mjs` static regression test. No real .NET builds, Node tests, schema inspections or HTTP smoke checks were run via this GitHub connector.
- Historical **pre-migration** workspace saves cannot be reconstructed accurately and are not backfilled/fabricated; history begins at the first successful save after the migration. Existing document revision history is unaffected. Future authorized audit export/diff or deletion/retention policy needs separate work.
- **Acceptance gate:** (1) macOS `dotnet build ArginTaxPayer.sln`, `dotnet test ArginTaxPayer.sln`, `npm --prefix apps/web test`, `npm --prefix apps/web run typecheck`, `npm --prefix apps/web run build`; (2) SQL inspection and explicit authorization of only test DB; (3) test DB migration only after approval; (4) create/save and view successive revisions, second-tab stale CAS -> HTTP409 with no history event, cross-customer history GET -> 404, DB UPDATE/DELETE audit -> forbidden, immutable canonical snapshot unchanged. Step17 blocked until acceptance.

### Step17 — CSV/XLSX invoice line import mapping and preview (2026-10-20)
- Owner confirmed Step16 audit table creation and approved Step16. Step17 does not change database schema, submit batches or send tax authority invoices; these remain outside Step17 (Step18 is controlled persistent import).
- New `invoice-import-preview.mjs` provides a bounded CSV parser with RFC-style escaped quotes, comma and newline delimiters, and first-worksheet XLSX parser using ZIP entry bounds and `DecompressionStream('deflate-raw')`. File size capped at 2 MB, 500 data rows, 40 XLSX worksheet columns, 128 archive entries, and XML inflated size capped at 2 MB per extracted part. XLSX browser support requires availability of the native decompression mode; incompatible browsers receive an explicit unsupported-compression error.
- `InvoiceImportPanel` allows file select, column mapping with explicit required description/quantity/price, optional discount and VAT, explanatory per-row failures (empty description, invalid decimal, negative quantity, excessive discount/VAT), counts, and a read-only preview before import. Duplicate source-column mappings are rejected.
- Explicit local confirmation inserts only wholly valid previews into the current wizard lines in React state; any invalid row prevents applying the batch. No API call is made by file parser or import panel. The existing wizard's save flow, original tenant authorization, server validation, and revision CAS continue to govern persistence once the user explicitly chooses to save. Imported lines initially carry textual description, not a fabricated catalog UUID/snapshot.
- A new `phase08-step17-import-preview.test.mjs` covers CSV escaped quoting, invalid-row explanations, mapping duplication, 500-row bound, and absence of import API writes.
- **Limitations:** This is preview and local line population, **not Step18 durable batch import**. It imports line-item rows, not invoice headers, taxpayer data or arbitrary invoice archives. Only worksheet1 of conventional XLSX is supported; formulas/macros and external links are not calculated or followed. UTF-8 CSV is expected. No live official catalogue matching is inferred from free-text imported descriptions. The rule engine's submission-ready state is not affected. This implementation has not yet been executed in macOS/browser test runs.
- Acceptance: `npm --prefix apps/web test`, `npm --prefix apps/web run typecheck`, `npm --prefix apps/web run build`, `dotnet build ArginTaxPayer.sln`, `dotnet test ArginTaxPayer.sln`; browser import CSV with comma quotes, XLSX basic worksheet with shared strings, correct mappings, discount invalid failure, large file bound, approve valid preview into wizard, navigate away before save (no DB import) and only save through existing explicit draft UI. Step18 blocked until operator approves Step17.

### Step17 UX adjustments — profile-style uploader and downloadable XLSX template (2026-10-20)
- Owner requested file selection to match the existing software-profile logo uploader. `InvoiceImportPanel` now uses the existing `console-logo-dropzone` and `console-upload-status` classes, with accessible click/Enter/Space, drag/drop, disabled/busy state, file name and local-read progress. Unlike profile logo, import is client-side parsing, not a network transfer; its progress indicates parsing/preview preparation rather than HTTP upload.
- Added `GET /api/invoices/template` to download a real ZIP/OOXML `.xlsx` sample named `ArginTaxPayer_Invoice_Import_Template.xlsx`. The file contains worksheet `InvoiceLines`, five exact importer columns (`description`, `quantity`, `unitPrice`, `discount`, `vatRate`), three illustrative invoice-item rows, and a green header. The archive generator writes only stable, bounded static content; there is no dependency upgrade, database mutation, or new auth scope. The sample is compatible with the existing first-worksheet XLSX parser, including stored ZIP entries and inline string cells.
- Added `invoice-template.d.mts` and `phase08-step17-upload-template.test.mjs` to cover the template signature, column names and uploader controls.
- **Operator acceptance pending:** macOS `npm --prefix apps/web test`, `typecheck`, `build`, then verify dropzone/keyboard activation, drag-and-drop, valid/invalid files, XLSX download and re-import with auto-mapped headers. No DB migration. Step17 should not be owner-closed until successful operator verification.

### Step17 compact UX refinement and Excel header contrast (2026-10-20)
- Corrected unreadable sample XLSX header in Excel viewers: dark green text on pale green background, with wider columns; canonical importer keys stay unchanged.
- Replaced the large inline importer in invoice Step2 with a small `ورود از فایل` button opening a separately scrollable dialog. Reused the existing profile-style drag/drop uploader, sample template link, column mapping, validation preview and explicit add-to-form action. Import updates only unsaved form lines and never directly persists a document.
- Replaced the tall per-item cards with a dense, labelled inline-edit grid, sticky table headings, max 440px/48vh scroll area, compact delete control and row count. Original line identifiers, catalog snapshots, monetary values, normal save behavior and tenant scope are preserved.
- Escape dismisses the importer before its parent invoice modal; no schema or API migration.
- Regression checks: `apps/web/tests/phase08-step17-compact-ui.test.mjs`. Run macOS npm tests/typecheck/build and verify opening importer, downloading and reimporting the template, then editing 20+ rows with an independent scrollable table. Runtime checks are still pending.

### Step16 UI adjustment (2026-10-20)
- History timestamps are shown in the Persian calendar with Asia/Tehran time zone, while original stored UTC remains unchanged.
- The authenticated actor's software profile first and last name are displayed instead of its UUID, with a neutral fallback for missing profiles.
- History results are ordered by event timestamp descending and then revision descending, both in the scoped API and UI.
- Added regression coverage in phase08-step16-history-display.test.mjs. No migration required; operator tests pending.

### Step18 — Controlled bulk invoice-draft import (2026-10-20)
- Step17 approved by owner. Bulk import is separate from Step17's item-only import. A single workbook can contain several invoices: each row is one line, repeated `invoiceKey` groups lines into one document; all header columns for a key must match. Workbook headers: `invoiceKey,taxpayerId,buyerPartyId,issueDateUtc,type,pattern,settlementMethod,cashAmountRial,description,quantity,unitPriceRial,discountPerUnitRial,vatRatePercent`.
- Excel template is downloadable directly via `GET /api/invoices/bulk-template` (real XLSX ZIP/OOXML); 3 example lines form 2 invoices. Customer MUST replace clearly labeled taxpayer/buyer UUID placeholders with their currently authorized IDs. CSV with the same header is also supported.
- New `BulkInvoiceImportPanel` opened using `ورود گروهی صورتحساب‌ها` on invoice list. Uses existing compact profile-style dropzone, validates and groups client-side, previews invoice keys, item counts, totals and per-invoice field errors. Maximum file 2MB, 500 item rows, 50 invoices per request, 150 items per invoice. Only explicitly confirmed **valid** invoices are sent; invalid ones are reported without blocking valid invoices.
- Dev-only, scoped `POST /api/dev/invoice-workspaces/bulk`, forwarded by the authenticated same-origin Next BFF, returns one independent outcome per submitted invoice (`created`, `duplicate`, `invalid`, `conflict`, `forbidden`, `not_found`, etc.) with source key and newly saved draft ID on success. It creates **draft workspaces only**; no taxpayer authority transmission, signing, plan debit, approval, or fiscal finalization.
- Idempotency is server-owned and **tenant-scoped**: deterministic draft ID = first 16 bytes of SHA-256(customer-user-ID + stable import key); stable import key = `invoice:` + the workbook `invoiceKey` (not file hash). The canonical input fingerprint is calculated server-side, persisted in draft JSON and compared on replay. Same key and same data => existing draft returned with `Replayed=true`; same key but different data => conflict, preventing duplicates even if an amended file is uploaded. Concurrent insert duplicate-key errors are checked against the same fingerprint and failed tracked entities detached.
- Existing `InvoiceEntryWorkspaceService.SaveAsync` still performs live tenant/taxpayer scope, buyer membership, catalog and financial validation, and commits the new draft with its immutable revision-1 history row atomically; no new database migration is required. Invoices are saved independently, so a failure does not roll back earlier successful invoices.
- Batch key = SHA-256 of client file bytes; retained for response/report correlation. It is **not** the durable invoice identity. The outcome list is displayed to the current browser session and successful draft IDs plus revision/audit remain durable. **Known limitation:** rejected/failed import attempts do not yet have a separate persisted append-only batch-outcome ledger, so full durable failed-row reporting from previous sessions is not yet achieved. Do not claim Step18 fully accepted until this is explicitly resolved or owner accepts the limitation.
- Source regression test `phase08-step18-bulk-import.test.mjs` checks grouping, failures, 50-document bound, sample template and idempotent scoped path. Pure JS parser checks were executed in tool sandbox; full .NET/Next build, macOS browser smoke and real PostgreSQL retries have NOT been run.
- **Operator test gate:** `dotnet build/test ArginTaxPayer.sln`, `npm --prefix apps/web test/typecheck/build`; download and populate sample, import 2 invoices, check exactly 2 draft workspaces and revision-1 audit; repeat identical file -> `duplicate` with no revision increment; change values but keep `invoiceKey` -> `conflict`; create one invalid invoice among valid ones -> only valid created; unauthorized taxpayer or buyer -> rejected; concurrent identical requests -> one row; no tax authority send. Manual source review only until these checks pass.

### Step18 follow-up hardening (2026-10-20)
- Invalid invoices stay visible in the same local preview while valid invoices may be confirmed and persisted independently. The explicit button now states `تأیید و ثبت پیش‌نویس‌های معتبر`.
- Added a downloadable `ArginTaxPayer_Import_Results_<hash>.csv` after the API responds; each row contains source invoice key, status, saved draft ID, and error. CSV values are quoted and leading spreadsheet-formula characters neutralized. This allows the operator to keep a copy of failed and successful results without any new database table.
- Extracted `InvoiceBulkIdentity.StableWorkspaceId/Fingerprint` as independently testable pure policy; added `Phase08Step18InvoiceBulkIdentityTests.cs` for same-key replay, tenant separation, and changed-content conflict. Concurrent unique-key collision handling detaches failed EF entities before continuing to the next invoice.
- **Unchanged limitation:** exported reports are local files. Failed attempts are not stored in an append-only server-side batch-outcome ledger. Revisit a separate ledger with explicit schema approval if durable retrieval of past failed attempts becomes an acceptance requirement. Successful draft and audit records ARE durable.
- Template generation and grouping were exercised in the tool sandbox (XLSX ZIP signature and 13 exact headers; 2 documents from 3 lines; invalid-row isolation; 50-invoice bound). Browser, .NET build and test-PostgreSQL acceptance remain pending and are not represented as successful.

### Step18 owner decision — accepted with deferred tests (2026-10-20)
- Owner explicitly accepted Step18 without running external-data import tests due to current product priorities. This is **administrative acceptance**, not evidence of successful macOS, PostgreSQL, concurrency, or browser verification.
- Deferred backlog: fix and exercise all import compile/runtime issues during regression; validate official invoice constraints, header and buyer rights, decimal monetary accuracy, UTF-8/XLSX handling, invalid-row independence, idempotency/concurrent retries and conflict handling; design persistent append-only batch-result ledger for failed attempts if later required; improve template UX and replacing UUIDs with selectable users/taxpayers.
- Keep API developer-only. Do not include Import behavior in a claim of full tested readiness, and never run batch writes against production data. Step19 may proceed for core regression; owner may elect to leave deferred Import integration tests documented rather than block phase planning.

### Step19 — Core regression and security sweep (2026-10-20)
- Added `Phase08Step19RegressionTests.cs`: exact decimal calculation, explicit VAT inputs (not a built-in current rate), all settlement-mode consistency invariants, negative amount rejection, deterministic tenant-scoped import IDs.
- Added `phase08-step19-security-regression.test.mjs`: verify same-origin authenticated Next BFF, local-only developer invoice endpoints, live taxpayer scoping and buyer linkage, wizard revision/unsaved-draft continuity, CSV error guards and import-only draft operations. This is a source-level guard, not proof of live authorization.
- Added `scripts/phase08-step19-verify.sh` for one repeatable operator run on the feature branch: clean Git worktree check, dotnet restore/build/test and web npm test/typecheck/build. **Never runs EF database update**, login operations, network mutation, batch HTTP writes or production commands. Operator can execute it as `bash scripts/phase08-step19-verify.sh`.
- Migration safety review: Step18 introduces no schema changes; audit migration Step16 was owner-reported applied to test database. Future DB tests require independent verification of `current_database()=argintaxpayer_test` with review of EF migration plan before writes. The mere presence of this test runner does not satisfy macOS/Ubuntu integration acceptance.
- Explicit owner-approved exception: Step18 bulk import integration, browser tests, concurrent retries, incomplete persisted failed-outcome ledger, template usability and scenario hardening are deferred for later priority. No tests were disabled or deleted to obtain a green claim.
- **Delivery status:** regression files and command committed; remote GitHub tool cannot execute macOS or VPS tests. Step19's original acceptance criterion "required tests green" remains **unverified** until operator shares output. Step20 macOS acceptance must not be labeled successful before actual evidence; Step21 Ubuntu remains separate.

### Step20 owner signoff and Step21 Ubuntu gate (2026-10-20)
- Owner explicitly approved Step20 after macOS report showed `RESULT source regression: PASS`, API readiness HTTP 200, and web HTTP 307. HTTP 307 is route reachability, not proof of authenticated login. No item-by-item manual browser evidence was supplied; preserve that exception rather than claim it passed. Step18 Import integration tests remain DEFERRED by owner.
- Added `scripts/phase08-step21-ubuntu-acceptance.sh` for native Ubuntu verification with branch, clean worktree and tool preflights, test-only `ubuntu-dev.sh verify`, service status, read-only API/Web HTTP checks, timestamped log under `~/.argintaxpayer-test/phase08-step21/`, and required operator checklist. It never runs migrations or live tax transmission.
- `ubuntu-dev.sh verify` enables test-database PostgreSQL integration, and independently confirms `current_database()=argintaxpayer_test` before proceeding. **Only run against the isolated test DB; integration tests may write to it.** Do not infer database safety just from configured connection-string text.
- IMPORTANT same-SHA gate: subsequent documentation and runner commits mean current Step21 SHA may differ from the macOS Step20 report SHA. Operator must compare logged macOS SHA and Ubuntu SHA and repeat Mac Step20 regression on the final candidate SHA if they differ. Do not claim same-SHA acceptance without that verification.
- Step21 coding/preparation is complete but acceptance is pending actual Ubuntu `verify`, browser and HTTP evidence, and owner confirmation. Step22 closeout must wait.
