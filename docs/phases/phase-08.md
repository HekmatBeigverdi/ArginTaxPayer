# ArginTaxPayer — Phase 08: Invoice Workflows and UI

**Status:** Step 01 owner-approved; Step 02 implemented and awaiting owner approval and local test evidence.
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
| 02 | Workflow state contracts | Define internal Draft/InReview/ChangesRequested/Approved state transitions | Allowed and forbidden transitions deterministic | Implemented; tests/owner approval pending |
| 03 | Actor authorization and ownership | Implement role/scope matrix for customer, Level1 and owning Level2 | All reads/writes scoped before data return | Not started |
| 04 | Workflow persistence and migration plan | Add minimal additive workflow and audit entities when existing Phase07 storage insufficient | No silent loss of historical invoices; migration separately authorized | Not started |
| 05 | Workflow command services | Create draft submission, change request, internal approval | State and history consistent after commit | Not started |
| 06 | Concurrency and idempotency | Prevent lost updates, duplicate review submissions and simultaneous approvals | One accepted write, deterministic conflict/replay | Not started |
| 07 | Workflow API contracts | Expose scoped commands, queries and stable typed errors | Backward compatible, stable response contracts | Not started |
| 08 | Invoice workspace list | Show customer-scoped drafts, review queue, filters, pagination | Correct statuses and scoped results | Not started |
| 09 | Dynamic invoice forms | Drive conditional fields by official rule-set and pattern metadata | Valid combinations enabled; invalid blocked | Not started |
| 10 | Header and party entry | Taxpayer, memory, buyer, issue date, header validation | Localized field errors and valid persisted values | Not started |
| 11 | Line and catalog entry | Search/select Phase06 catalog items, quantity, unit, price and snapshots | Historic invoice unchanged after catalog edit | Not started |
| 12 | Calculations and financial summary | Display discount, charges, VAT and final totals using Phase07 engine | UI/API totals match canonical model | Not started |
| 13 | Settlement methods and fields | Cash, credit and mixed invoice terms and reconciliation | Valid mix accepted; mismatch rejected | Not started |
| 14 | Save/resume draft UX | Enhance existing resumable workspaces and unsaved-change handling | Draft resumes exactly once without lost fields | Not started |
| 15 | Review and internal approval UI | Review summary, validation feedback, approve and return with reason | Readonly approval snapshot and reasoned change requests | Not started |
| 16 | Audit and revision history | Show immutable actor/timestamp/reason/revision timeline | No historical rewrites or unauthorized reads | Not started |
| 17 | Import mapping and preview | CSV/XLSX bounded parsing, column mapping and row-level preview | Explain valid/invalid rows before approval | Not started |
| 18 | Controlled batch draft import | Explicit import confirmation with per-row durable outcomes | Retry creates no duplicate invoices; partial failures explained | Not started |
| 19 | Comprehensive regression and security | Domain, API, web, migration safety and Phase03-07 compatibility | Required tests green without disabling tests | Not started |
| 20 | macOS operator acceptance | Independent macOS build and workflow/browser/db/HTTP acceptance | Commit SHA, operator results and caveats recorded | Not started |
| 21 | Ubuntu operator acceptance | Same SHA, independent Ubuntu build and integration tests | Matching SHA and Ubuntu evidence documented | Not started |
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
