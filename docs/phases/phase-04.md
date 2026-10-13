# ArginTaxPayer — Phase 04: Multi-Taxpayer Management

**Status:** Steps 01–02 owner approved (2026-10-13; Step 02 test evidence owner-reported); Step 03 implemented in repository, verification and owner approval pending
**Branch:** `phase/04-multi-taxpayer-management`
**Started:** 2026-10-13
**Baseline:** `docs/roadmap/18-phases.md`, Phase 03 identity/authorization contracts
**Policy:** This single file is the authoritative evolving plan. No per-step files. Changes to step count/scope require dated owner-approved amendment.

## Objective
Manage natural/legal taxpayer profiles, fiscal memories, certificate metadata and custody references, and Level 1 controlled assignments to Level 2 delegates. Enforce server-side scope isolation and auditable lifecycle changes. Actual key generation/CSR/signing belongs to Phase 09; tax authority transmission belongs to Phase 10; billing and settlement belong to Phase 12.

## Security and domain invariants
- Level 1 controls taxpayer assignment, reassignment, and revocation; Level 2 can operate only within explicitly assigned scope.
- No Level 2 access to other delegates' taxpayers, global audit, raw private keys, or platform secrets.
- Taxpayer identity and fiscal-memory identifiers have appropriate unique constraints; historical ownership/assignment is auditable.
- A taxpayer may have multiple historical fiscal memories, but active-memory policy must enforce the approved subscription/business invariant and prevent ambiguous operations.
- Store certificate metadata, validity, fingerprints, status, and secret references only; never raw private keys in database responses, logs, or source control.
- Do not imply official registration or certificate issuance by merely storing metadata.
- All schema changes apply only after verifying `current_database() = 'argintaxpayer_test'`; no automatic migration at startup.
- Keep Phase 03 authentication and recovery workflows backward compatible. Production security gaps from Phase 03 remain tracked blockers until explicitly closed.

## Fixed Step Plan

| Step | Title | Deliverable | Acceptance | Status |
|---|---|---|---|---|
| 01 | Domain Contracts and Lifecycle Matrix | Taxpayer/memory/assignment state models, invariants, API contracts, migration plan | Identity, lifecycle and authorization matrix reviewed; no unapproved mutation | Owner approved (2026-10-13; automated test evidence not supplied) |
| 02 | Taxpayer Persistence and Migration | Natural/legal taxpayer entities, normalized identifiers, constraints, EF migration | Safe migration and duplicate/constraint tests against test DB | Owner approved (2026-10-13; owner-reported) |
| 03 | Taxpayer Creation and Validation | Level 1 controlled taxpayer registration, type-specific validation | Invalid/duplicate inputs rejected; authorization enforced | Implemented — local verification pending |
| 04 | Taxpayer Profile and Lifecycle | Read/update/deactivate workflows, optimistic concurrency, history | Stale updates rejected; history preserved | Planned |
| 05 | Fiscal Memory Persistence | Memory identifiers, taxpayer relationship, lifecycle and constraints | No cross-taxpayer linking; unique/active policy tested | Planned |
| 06 | Fiscal Memory Management API | Register/update/retire memory metadata and lifecycle | Unauthorized and invalid transitions denied | Planned |
| 07 | Certificate Metadata and Custody Boundary | Fingerprint, expiry, issuer, secure key-reference contracts, rotation metadata | No secret leakage; expired/revoked states handled | Planned |
| 08 | Delegate Assignment Model | Level 1-to-Level 2 taxpayer grants, revocation, effective dates, history | Only Level 1 may grant/revoke; audit trail complete | Planned |
| 09 | Server-Side Scoped Query and Command Policies | Query filters, resource policies, assignment checks | Cross-delegate isolation for reads/writes, deny by default | Planned |
| 10 | Taxpayer and Memory RTL Management UI | Persian responsive taxpayer list, forms, detail, memory views | RTL, validation, loading/error states, guarded routes | Planned |
| 11 | Level 1 Assignment Console and Level 2 Workspace | Assignment/revocation UI, scoped delegate views | Revocation effective immediately; no unauthorized data shown | Planned |
| 12 | Integration and Negative-Path Regression | PostgreSQL/API/UI security, concurrency and lifecycle tests | Identity regression and cross-scope negative paths pass | Planned |
| 13 | Hardening and Phase Handoff | Review, local regression, final Ubuntu acceptance, documentation | Evidence, migration review, unresolved risks and owner signoff recorded | Planned |

## Testing Cadence — Owner Approved
**Every step (macOS):** focused automated tests and relevant local UI/API smoke only. Existing commands:
```bash
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
zsh scripts/dev-down.zsh
```
The launcher owns the stable AutoSSH tunnel to the dedicated VPS `argintaxpayer_test` database and loopback local services. No repeated manual tunnel/connection setup. Add only step-specific test commands as necessary.

**Ubuntu:** defer until Step 13 unless a schema/migration, infrastructure, cross-platform dependency or OS-specific failure requires earlier focused verification:
```bash
bash scripts/ubuntu-dev.sh verify
bash scripts/ubuntu-dev.sh up
bash scripts/ubuntu-dev.sh status
bash scripts/ubuntu-dev.sh down
```
All live SMS sending stays disabled; do not expose Development services publicly. A macOS test against VPS PostgreSQL does not substitute for final Ubuntu runtime acceptance.

## Step Completion Record Template
For each step append here: scope implemented, changed paths, commit SHA, migration impact and confirmed target DB, focused macOS test results, Ubuntu test result or explicitly deferred reason, CI evidence, security negative tests, owner acceptance date, and remaining blockers. Never mark a test passed without actual evidence.

### Step 01 — Domain Contracts and Lifecycle Matrix

**Status:** Owner approved 2026-10-13; automated macOS verification not independently observed. **Commit:** `63a129d25bfd3298fcbb049144e2feccc04a998d`.
**Changed:** `src/ArginTaxPayer.Domain/Taxpayers/TaxpayerContracts.cs`.
**Migration:** None. **macOS:** Not run by assistant; local verification required. **Ubuntu:** Deferred to final phase gate. **CI:** Not verified. **Owner acceptance:** Approved by owner 2026-10-13.

#### Domain model and lifecycle decisions
- A taxpayer is either NaturalPerson or LegalEntity; profile ID is a stable UUID, normalized taxpayer identifier is separate from any user account ID, and Revision supports future optimistic concurrency.
- Taxpayer status: Active → Suspended → Active; Active/Suspended → Archived. Archived is terminal; no physical deletion in routine workflows.
- Fiscal memory status: PendingVerification → Active/Retired; Active → Suspended/Retired; Suspended → Active/Retired. Retired is terminal. An externally issued memory identifier cannot be inferred from a locally created profile.
- Certificate metadata states: MetadataOnly, Valid, Expired, Revoked. Store SHA-256 fingerprint, validity window and opaque custody reference only; signing keys/CSR generation remain Phase 09.
- Delegate grant records retain grantor Level 1 user ID, delegate ID, taxpayer ID, start/revocation times. Revoke rather than hard-delete to preserve auditability.
- Assignment policy: Level 1 alone may grant/revoke access; Level 2 list/read/modify only within current assignment. Direct ID guessing must fail. A customer identity is not an administrator grant.
- Memory active-policy requires explicit authoritative enforcement when Step 05 creates persisted memories; avoid imposing a universal uniqueness rule across historical/inactive memories. Multiple records are allowed, but ambiguous active fiscal memory selection must be rejected.
- Boundary with Phase 03: user authentication and MFA remain unchanged; existing scoped-authorization hooks must be reused, not duplicated.
- API contract plan: authenticated create/list/detail/update/archive taxpayer endpoints; memory lifecycle endpoints; Level 1 grant/revoke endpoints. Commands use concurrency tokens and auditable actor context; all reads use resource scoping.

#### Permission matrix
| Operation | Level 1 | Level 2 | Customer |
|---|---|---|---|
| Create taxpayer | Allowed | Denied (unless explicitly delegated by later owner-approved policy) | Denied |
| Read/update taxpayer | All within platform policy | Only currently assigned | Only explicitly owned portal view in later phase |
| Add/retire fiscal memory | Allowed | Scoped business operation subject to policy | Denied |
| Grant/revoke taxpayer access | Allowed | Denied | Denied |
| Read raw secret/private key | Denied through ordinary API | Denied | Denied |
| View global security audit | Allowed | Denied | Denied |

#### Step 02 implementation handoff
Persist taxpayer profiles with immutable UUID, normalized identifier per taxpayer kind, status, revision and timestamps. Add unique appropriate DB indexes and a dedicated EF migration. Review whether legal entities and natural persons need different normalized field rules, and test collisions per kind. Never mutate existing identity_users table semantics. Check `current_database() = 'argintaxpayer_test'` before applying migrations; schema changes require focused PostgreSQL verification even though routine Ubuntu application testing remains deferred.

**Focused local smoke after pulling:**
```bash
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
```
No build or test pass is claimed until operator output/CI evidence exists.

### Step 02 — Taxpayer Persistence and Migration
**Status:** Owner approved 2026-10-13; previous migration execution and raw test output not independently witnessed. **Owner acceptance:** Approved.
**Files:** `src/ArginTaxPayer.Infrastructure/Persistence/Taxpayers/TaxpayerProfile.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/ArginTaxPayerDbContext.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261013010000_AddTaxpayerProfiles.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/ArginTaxPayerDbContextModelSnapshot.cs`, `tests/ArginTaxPayer.Tests/TaxpayerPersistenceMetadataTests.cs`.
**Commits:** `b9b91659`, `81bec434`, `f91ec9d1`, `2901d2a6`, `cecacb35`.
**Design:** Taxpayer profiles are separate from authentication users. A natural/legal kind discriminator, normalized business identifier, display name, active/suspended/archived status, UTC timestamps and optimistic concurrency revision are persisted. Unique composite (kind, normalized_identifier), check constraints and positive revision protect DB integrity. Identifier semantic checks and normalization algorithms will be implemented in Step 03; Step 02 presently rejects blanks but is NOT an official Iranian identifier validator.
**Migration:** `20261013010000_AddTaxpayerProfiles` is committed but not applied; no existing table is dropped/modified by Up. Run migrations only after verifying `current_database() = 'argintaxpayer_test'`. Existing outbox/idempotency TaxpayerId columns are deliberately not retrofitted with foreign keys until legacy-data assessment.
**Local verification (project root, after scripts/dev-up.zsh):**
```bash
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --filter FullyQualifiedName~TaxpayerPersistenceMetadataTests
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
```
Require actual database output `argintaxpayer_test` before the separate explicit command:
```bash
dotnet ef database update 20261013010000_AddTaxpayerProfiles --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
```
Then inspect `\\d taxpayer_profiles` in psql and verify duplicate identifier rejection in an isolated rollback transaction. Inspect existing migration history before execution, since the added migration must follow the phase 03 migrations in that environment.
**Evidence:** Build not run; unit tests not run; PostgreSQL duplicate/constraint tests not run; migration not applied; CI not checked. Ubuntu runtime verification deferred to Step 13 except required migration integrity validation. Do not mark verified until evidence is provided.



### Step 03 — Taxpayer Creation and Validation

**Status:** Implemented in repository; macOS build/API integration tests and owner acceptance pending.
**Code:** `src/ArginTaxPayer.Domain/Taxpayers/IranianTaxpayerIdentifier.cs`; `src/ArginTaxPayer.Infrastructure/Persistence/Taxpayers/TaxpayerCreationService.cs`; `src/ArginTaxPayer.Api/Taxpayers/TaxpayerDevelopmentEndpoints.cs`; API DI/route wiring in `src/ArginTaxPayer.Api/Program.cs`; tests in `tests/ArginTaxPayer.Tests/TaxpayerIdentifierValidationTests.cs`.
**Commits:** `b483866a`, `e0932c81`, `c991c0da`, `7a8d3d9a`, `841a146c`, `bd975147`.
**Architecture:** The POST `/api/dev/taxpayers` route is restricted to Development + loopback with a configured identity environment. Reuses Phase 03 opaque bearer session authentication. Live database validation requires an active Level 1 user with MFA enabled; Level 2/Customer/anonymous cannot create. Never accept role/actor/tenant IDs from the request. Creates a separate profile with timestamps and optimistic revision, accepts Persian/Arabic digits, checks the natural-person national code check digit, and checks legal-entity IDs for eleven digits and nontrivial pattern only. Official legal-entity ownership/checksum confirmation is NOT claimed: public checksum implementations disagree and the authoritative registry check is deferred to integration with a verified official source. Unique database index catches duplicates including concurrent submissions.
**HTTP statuses:** 201 created, 400 invalid input, 401 unauthenticated, 403 forbidden, 409 duplicate. No production route is exposed.
**Migration:** None (depends on Step 02 schema).
**Verification required:**
```bash
git pull --ff-only origin phase/04-multi-taxpayer-management
zsh scripts/dev-up.zsh
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --filter 'FullyQualifiedName~TaxpayerIdentifierValidationTests|FullyQualifiedName~TaxpayerPersistenceMetadataTests'
zsh scripts/dev-status.zsh
```
**Negative-path HTTP acceptance:** Without Authorization → 401; Level 2/Customer bearer → 403; invalid natural code/length → 400; Level 1 with enrolled MFA → 201; same kind and normalized ID twice → 409. Execute with throwaway test identifiers only, using the existing Phase 03 MFA flow. Cross-process/duplicate races must ultimately be exercised on PostgreSQL; unit test coverage of HTTP denial is not yet established. Never paste actual bearer tokens in issue comments or logs.
**Evidence:** Code commits recorded; build, HTTP, real PostgreSQL duplicates, CI not executed by assistant. Ubuntu runtime deferred to end of phase, per owner cadence. **Owner acceptance:** Pending.

## Phase Exit Gate
All 13 steps accepted, focused regression passing, Ubuntu verification once, schema/security review, owner signoff. Merge to main, immutable tag, GitHub Release and production deployment are distinct gated actions and must never be assumed completed.
