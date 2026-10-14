# ArginTaxPayer — Phase 04: Multi-Taxpayer Management

**Status:** Steps 01–05 owner approved (2026-10-13; owner-reported acceptance). Step 05 received a dated, owner-approved cross-phase contract clarification; Step 06 owner approved 2026-10-13; Step 07 owner approved 2026-10-13; Step 08 owner approved 2026-10-13; Step 09 implementation approved for progression with integration acceptance deferred to Step12; Step 10 owner approved 2026-10-13; Step 11 customer ownership and paginated user/modal UX owner approved 2026-10-14; Step 12 owner approved 2026-10-14 with explicit acceptance of unexecuted three-account HTTP smoke; Step 13 OWNER APPROVED 2026-10-14; owner reports successful latest local macOS and Ubuntu verification after the 8-test regression fixes. Phase04 merged into main with merge commit 422437c49fd153d5f749d1533d0bee3f5d40383e; annotated tag / GitHub release pending operator execution.
**Branch:** `phase/04-multi-taxpayer-management`
**Started:** 2026-10-13
**Baseline:** `docs/roadmap/18-phases.md`, Phase 03 identity/authorization contracts
**Policy:** This single file is the authoritative evolving plan. No per-step files. Changes to step count/scope require dated owner-approved amendment.

## Objective
Manage natural/legal taxpayer profiles, fiscal memories, certificate metadata and custody references, and Level 1 controlled commercial customer-to-Level 2 partner associations. Enforce server-side scope isolation and auditable lifecycle changes. Actual key generation/CSR/signing belongs to Phase 09; tax authority transmission belongs to Phase 10; billing and settlement belong to Phase 12.

## Security and domain invariants
- Level 1 controls CUSTOMER account ownership and partner reassignment; Level 2 can operate only on owned customer accounts and the taxpayer profiles belonging to those customers. Direct taxpayer-to-Level2 grants are deprecated.
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
| 03 | Taxpayer Creation and Validation | Level 1 controlled taxpayer registration, type-specific validation | Invalid/duplicate inputs rejected; authorization enforced | Owner approved (2026-10-13; owner-reported) |
| 04 | Taxpayer Profile and Lifecycle | Read/update/deactivate workflows, optimistic concurrency, history | Stale updates rejected; history preserved | Owner approved (2026-10-13; owner-reported) |
| 05 | Fiscal Memory Persistence | Memory identifiers, taxpayer relationship, lifecycle and constraints | No cross-taxpayer linking; unique/active policy tested | Owner approved (2026-10-13), additional binding/connectivity/key-import contracts registered |
| 06 | Fiscal Memory Management API | Register/update/retire memory metadata and lifecycle | Unauthorized and invalid transitions denied | Owner approved 2026-10-13; deferred activation/rebind constraints retained |
| 07 | Certificate Metadata and Custody Boundary | Fingerprint, expiry, issuer, secure key-reference contracts, rotation metadata | No secret leakage; expired/revoked states handled | Owner approved 2026-10-13 |
| 08 | Commercial Customer Assignment Model (approved correction) | Level1 ownership of customer accounts, Level2 partner association, customer-level reassignment history | Role-based automatic acquisition and Level1-only partner transfer, historical preservation | Original implementation approved; customer-level correction committed 2026-10-13; local verification pending |
| 09 | Server-Side Customer-Scoped Query and Command Policies | Customer-to-many-taxpayer join, partner owner checks, deny by default | Level2 cannot see another partner's customers or taxpayers | Customer-owner query correction committed 2026-10-13; integration regression pending Step12 |
| 10 | Taxpayer and Memory RTL Management UI | Persian responsive taxpayer list, forms, detail, memory views | RTL, validation, loading/error states, guarded routes | Owner approved 2026-10-13 |
| 11 | Level1 Customer Commercial Ownership Console and Level2 Workspace (approved correction) | Transfer entire customer relationship to Level2 and show scoped customer accounts | No taxpayer-level grants; root-approved customer reattribution, preserve financial history | Owner approved 2026-10-14; final integration validation belongs to Step12 |
| 12 | Integration and Negative-Path Regression | PostgreSQL/API/UI security, concurrency and lifecycle tests | Identity regression and cross-scope negative paths pass | Owner-approved for progression on 2026-10-14; three-account HTTP smoke not executed, explicitly recorded residual risk |
| 13 | Hardening and Phase Handoff | Review, local regression, final Ubuntu acceptance, documentation | Evidence, migration review, unresolved risks and owner signoff recorded | Owner approved 2026-10-14; macOS + Ubuntu latest checks reported successful; merge/tag pending |

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

## OWNER-APPROVED CORRECTION — CUSTOMER vs TAXPAYER COMMERCIAL OWNERSHIP (2026-10-13)

This correction **supersedes** the earlier incorrect taxpayer-to-Level2 delegation interpretation in Steps 01, 04, 08–11; the 13-step phase numbering remains unchanged. See the [18-phase roadmap](../roadmap/18-phases.md), subsection “Customer-level partner ownership and cash-based settlement”.

**Correct hierarchy:** `Level1 owner -> Level2 partner (optional) -> customer account -> one or more fiscal taxpayer profiles -> fiscal memories/certificates`. The customer's commercial partner is NOT the taxpayer and does not own the private key. Customer identity and taxpayer are different entities. A customer's monthly/annual plan may bind exactly one taxpayer as required by the plan, while other customer or plan types may have more than one taxpayer. The same Level2 partner may manage many customers, and each such customer may own different profiles.

**Acquisition rules:**
1. Customer created by Level1 → owned directly by that Level1; never share Level2 profit.
2. Customer created by Level2 → associated automatically with the creating Level2 partner and its controlling Level1.
3. Public/self-signup customer → owned by configured default Level1; Level2 claiming an existing referred customer submits a request only, with manual Level1 approval required before reassignment. **No self-approval or automatic retroactive claim.**
4. Root always has administration visibility; Level2 has only its customers' data and taxpayers (by live server-side association). Customer self-management/creation of many taxpayers is gated by subscriptions in Phase12 and completed in Phase05. Old taxpayer-to-Level2 grants never authorize access.
5. Commercial ownership is a **single row per customer**, with revision-checked reassignment and append-only previous/new partner, reason, Level1 approver, timestamp history. Existing invoices, credits, subscriptions, company/taxpayer ownership, settled amounts and historical financial allocations cannot be reassigned retroactively.

**Database/code correction:** `CustomerCommercialOwner`, `CustomerCommercialOwnerChange`, nullable `TaxpayerProfile.CustomerUserId` (nullable **only for legacy reconciliation**) and explicit migration `20261013233000_AddCustomerCommercialOwnership`. New taxpayer creation from Level1 requires existing owning customer UUID. New public email/phone and admin-created customer registrations now create a commercial-owner row atomically with identity; Level1 creation selects its own account, Level2 creation selects the creating partner, public selects root default. Root resolution uses configured `Ownership:DefaultLevel1UserId`, or the unique active Level1 as fallback; ambiguity denies new registration (no mistaken ownership). Current database state must be reconciled before applying business financial logic: legacy customers and unlinked taxpayer rows are **not silently reassigned**.

**API correction:** Former `/api/dev/taxpayers/{taxpayerId}/delegates*` grant/revoke/history routes **removed**. The old `taxpayer_delegate_grants` schema is retained temporarily **for historical migration assessment only**, not allowed for new partner access. New `GET /api/dev/customer-owners`, `GET /api/dev/customer-owners/{customerId}`, and `POST /api/dev/customer-owners/{customerId}/transfer` endpoints are introduced. Transfer requires Level1 authenticated session + MFA, customer belongs to that Level1, valid active MFA-enabled Level2 (or null to return the relationship to root), reason, `expectedRevision`, before/after audit and database CAS. Future Level2 referral claim queue is Phase16; until then Level1 can manually document a request/reason.

**UI correction:** Old taxpayer “DelegateConsole” and its Next.js gateway/tests removed; the taxpayer UI now creates a profile **for a selected existing customer account** rather than assigning it to a Level2 partner. The `/account` screen has a new `CustomerOwnershipConsole`: Level1 can list its customer accounts and approve an attribution change; Level2 has read-only list of its owned customers, with taxpayer access derived from them. There are no UI buttons for fake tax-authority token verification or plaintext-key upload.

**Accounting cross-phase nonnegotiables:** Root-direct customer revenue/profit belongs to Level1. Level2 partner customers use independent financial accounting, sale-price/cost snapshots and agreed percent (normally 50/50) of **net profit computed only from verified allocated collected cash**, not the uncollected invoice or contract amount. Tax and eligible expense deductions require Level1 manual approval. Track each company/taxpayer and purchase, first plan sale, plan renewal and invoice/unit package separately, including partial receipts, receivables, reimbursements, pending approval, full/partial settlement and immutable audit trail. Phase12 implements billing/settlement and immutable ledgers; Phase16 administrative referral/expense/settlement approvals; Phase17 security and financial regression.

**Verification blockers:** No schema applied by assistant; `argintaxpayer_test` must be verified before migration. Phase04 Step12 must test new root-created, partner-created and public signups, multiple taxpayers per customer, manually approved referral transfer, former partner losing all access immediately, never taking historic cash allocations, multiple Level1 default ambiguity, old grants having zero authority, concurrency and transaction rollback. Production enablement remains blocked. **Owner acceptance:** Architectural amendment approved, implementation verification pending.

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
**Evidence:** Code commits recorded; build, HTTP, real PostgreSQL duplicates, CI not executed by assistant. Ubuntu runtime deferred to end of phase, per owner cadence. **Owner acceptance:** Approved by owner 2026-10-13. Raw local test logs were not independently available.

### Step 04 — Taxpayer Profile and Lifecycle

**Implementation status:** Committed; no runtime acceptance claim. **Owner acceptance:** Pending.
**Code:** `TaxpayerProfileLifecycleService.cs`, `TaxpayerProfileChange.cs`, `ArginTaxPayerDbContext.cs`, `20261013200000_AddTaxpayerProfileHistory.cs`, EF snapshot, `TaxpayerDevelopmentEndpoints.cs`, `Program.cs`.
**API:** Development/loopback `PUT /api/dev/taxpayers/{taxpayerId}`. Requires a valid opaque bearer, active Level 1 and enabled MFA; no Level 2/Customer modification. Body: `{ "displayName": "New name", "status": 2, "expectedRevision": 1 }`. HTTP 200/400/401/403/404/409 responses. Taxpayer type and identifier intentionally immutable in this operation.
**Concurrency:** In a database transaction, an update compares `id`, `revision`, old status and old name. Successful changes increment revision and insert an immutable change record (actor, before/after, time) in the same transaction. Archived state is terminal.
**Migration:** `20261013200000_AddTaxpayerProfileHistory` creates only the history table and its FKs/indexes. It has NOT been applied. Confirm the database is `argintaxpayer_test` before explicit migration. Do not run migrations automatically at startup.
**Security:** Existing Phase 03 MFA/opaque token authorization reused. No secret exposure or delegate privilege promotion.
**Bug fix — stale Google Authenticator screen:** `apps/web/app/identity/identity-flow.tsx` uses `expiresAtUtc` supplied by `mfa/challenge` (currently server issues a five-minute challenge). The UI displays a second-level countdown, checks actual wall clock on page focus/visibility, blocks expired submissions, discards the challenge/code and returns to login with a clear Persian reason. The 30-second TOTP code cadence is *not* the five-minute challenge expiry. Neither lifetime nor server brute-force limit was extended.
**New tests:** `tests/ArginTaxPayer.Tests/TaxpayerLifecycleTests.cs`, `apps/web/tests/mfa-expiry.test.mjs`.
**macOS verification:**
```bash
zsh scripts/dev-up.zsh
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --filter 'FullyQualifiedName~TaxpayerLifecycleTests|FullyQualifiedName~TaxpayerPersistenceMetadataTests'
(cd apps/web && npm test && npm run typecheck && npm run build)
```
After independent DB check `SELECT current_database();` returns `argintaxpayer_test`, explicitly apply `20261013200000_AddTaxpayerProfileHistory` using the known safe `dotnet ef database update` command. Verify 200 transition, 409 stale revision, 400 illegal archive reactivation, 403 Level 2, 401 unauthenticated, and history row writes. For MFA check auto-return after five minutes without reload and rejection of a stale challenge, as well as successful use before expiry.
**Evidence:** macOS build not executed; targeted tests not executed; migration not applied; PostgreSQL HTTP integration and CI pending. **Ubuntu:** final-phase runtime test deferred (schema migration integrity check required locally).

### Step 05 — Fiscal Memory Persistence

**Status:** Owner approved on 2026-10-13 (owner-reported); migration and focused PostgreSQL evidence not independently observed. This confirmation does not certify live authority integration.
**Commits:** `06767747` (entity), `dbc11bba` (EF mapping), `15441ab3` (migration), `200bae8e` (snapshot), `6e3fa89c` (focused tests).
**Files:**
- `src/ArginTaxPayer.Infrastructure/Persistence/Taxpayers/FiscalMemory.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/ArginTaxPayerDbContext.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261013210000_AddFiscalMemories.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/ArginTaxPayerDbContextModelSnapshot.cs`
- `tests/ArginTaxPayer.Tests/FiscalMemoryPersistenceTests.cs`

**Design:** Fiscal-memory entity contains immutable UUID, required taxpayer FK, authority-issued memory identifier, PendingVerification/Active/Suspended/Retired state, optimistic revision and UTC timestamps. It contains **no private signing keys or certificate secrets**. Restrict FK deletion prevents deleting a taxpayer that still has memories. A globally unique memory identifier prevents associating one identifier with two taxpayers. A PostgreSQL partial unique index on `taxpayer_id WHERE status = 2` enforces a maximum of one **active** fiscal memory per taxpayer, including concurrent attempts. Historical suspended/retired records remain possible. This physical constraint is independent of (and must not be confused with) the Phase 12 per-subscription/entitlement rule. The status transition policy already exists in the Phase 04 domain and is tested at the metadata/pure-policy level; persisted transitions and authorized commands are reserved for Step 06.

**Migration:** `20261013210000_AddFiscalMemories`. Schema adds a new table, FK and indexes; existing taxpayer rows are unchanged. Migration committed but **not applied**. Never auto-migrate.
**Focused macOS testing:**
```bash
git switch phase/04-multi-taxpayer-management
git pull --ff-only origin phase/04-multi-taxpayer-management
zsh scripts/dev-up.zsh
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --filter FullyQualifiedName~FiscalMemoryPersistenceTests
zsh scripts/dev-status.zsh
```
Before applying migration, explicitly verify database identity and prior migration history:
```bash
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
```
Require exact database result `argintaxpayer_test`, verify that prior phase-04 migrations have been applied, then **and only then**:
```bash
dotnet ef database update 20261013210000_AddFiscalMemories --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
```
**Database acceptance:** In a rollback-only transaction, test valid taxpayer FK, missing taxpayer FK rejection, duplicate memory identifier rejection across taxpayers, two active memories for the same taxpayer rejected, and historical suspended/retired coexistence. Test checks must not leave fixture data behind. Compare live schema and EF snapshot after application. These actual PostgreSQL constraint tests have **not been executed by assistant**; the included unit tests check EF metadata and pure lifecycle predicates, not concurrent live-DB inserts.
**macOS status:** Not run by assistant. **Ubuntu:** Full runtime verification deferred to phase end; migration/schema verification is required now on the shared test PostgreSQL. **CI:** Not checked. **Owner acceptance:** Owner approved 2026-10-13; independent execution evidence remains outstanding.

## Owner-approved Phase 04 scope clarification — 2026-10-13 (Step 05)

**Approval:** The owner explicitly approved Step 05 and added the following three non-optional product requirements on 2026-10-13. This is a requirement/acceptance amendment to existing phase ownership, **not** a new Step 05 database feature claim and **not** a step renumbering. The fixed 13 steps are unchanged.

### A. Annual plan — single taxpayer binding, audited manual reset

- Annual plan: **unlimited invoice submissions for one taxpayer only**, within the subscription's paid annual validity. Invoice-count subscription remains a separate model. The choice of taxpayer is stable for the entire annual entitlement unless an exception is approved.
- At initial onboarding, one designated taxpayer/memory pair can be configured. A **confirmed successful authenticated authority connectivity check**, explicitly linked to that taxpayer, memory, subscription and check ID, locks the annual binding. No timeout, unavailable service, invalid token, or inconclusive local self-test may lock it.
- After lock, end users cannot replace the taxpayer, add a second covered taxpayer, or arbitrarily change the active memory. UI affordances are disabled **and** server-side commands reject attempts; direct DB writes must respect transactional invariants.
- Level 2 acting within a current assigned scope, or Level 1, may **request** review and manual reconfiguration with required reason and explanation. **Only Level 1 approves or rejects** by default; no Level 2 self-approval, even when Level 2 requested the change. Approval identity, date, scope, reason, before/after taxpayer and memory IDs, effective time, and decision are persisted in append-only audit history.
- Distinguish (1) replace fiscal memory for the **same** taxpayer from (2) exceptional taxpayer reassignment. The latter requires explicit elevated approval. An approval opens only one controlled rebind, with revision/CAS and independent re-verification. Never silently transfer prior fiscal invoices, tax identifiers, original historical invoice associations, annual expiry, balances, or financial ledger postings. No deletion of former taxpayer/memory relationships.
- Both Level 1/assigned Level 2 and the affected customer may view **redacted, role-scoped** review history (reason, timestamps, statuses and old/new identifiers where authorized), never private keys or global audit details.
- No subscription may span multiple simultaneously covered taxpayer identities under the annual plan. Phase 12 owns enforcement of entitlements, atomic lock/rebind and preservation of remaining validity. Phase 04 persists memory lifecycles and review lifecycle with secure authorization; do not confuse Step 05's one-active-memory-per-taxpayer index with the annual one-taxpayer-per-subscription restriction.

### B. Real tax authority connectivity and token diagnostics

- A visible **Test connection** action must be available at memory details, including after onboarding/when token or signing credentials stop working. It checks real authority authentication/connectivity through the **versioned Phase 10 tax authority adapter** rather than treating a local JWT, network ping or local-only signature check as proof of external connectivity.
- Independently classify: Success, invalid/expired token or authentication credential, key/signature mismatch, authority request timeout/no response, upstream 5xx/server failure, DNS/network/TLS failure, and inconclusive/unsupported check. A transport failure is **not** necessarily a bad token; never mark a stored token invalid based merely on upstream silence.
- Persist a scoped redacted diagnostic result with time, authenticated request actor, memory ID, safe correlation identifier and optional sanitized HTTP status. Apply short network deadlines, rate limits, no invoice issuance, no subscription charge, no automatic disable/rotation or bound-taxpayer change on failure; return a recoverable UI message.
- Step 06 and Steps 10–12 of this phase: create safe API contract and RTL affordance/diagnostic history without asserting actual authority connectivity when adapter is not yet delivered. **Phase 10 implements the live token/authority diagnostic** using officially supported operations; unsupported checking returns Inconclusive/NotConfigured, not fake Success. Phase 11 covers retries and reconciliation; Phase 17 covers outages, invalid token and security tests.

### C. Existing key import (legacy private.txt) alongside key generation

- Offer **Generate new key pair** and **Use existing registered fiscal memory/key** as parallel onboarding choices. For the second route the user already knows the fiscal memory ID and possibly registered public key/certificate; importing legacy `private.txt` content or an uploaded key is supported via a protected encrypted server-side channel, never a public repo, general file store, analytics event, URL, browser localStorage, logs, or ordinary audit content.
- Parse supported PEM/key formats **with strict size and algorithm checks**, including appropriate handling for encrypted keys, and derive the public key from the imported private key. Display/compare only a safe SHA-256 public-key fingerprint. If a public key or certificate was supplied, require cryptographic match. Do not assume the user must generate/re-register a new public key if the existing registered key truly matches.
- Existing registered fiscal memory ID is authority-assigned; verify ownership/binding and key legitimacy with available authoritative evidence, rather than trusting a client assertion. The UI must distinguish **locally valid key pair** from **authority-confirmed registration and successful live connectivity**.
- Store encrypted private key only in restricted custody/HSM/vault architecture with protected custody reference in ordinary DB records and explicit user consent; redact private-key bodies from errors, HTTP logs, request tracing and Level 2 business history. Define safe replacement/rotation and revocation. Imported keys never become accessible through taxpayer read APIs or export endpoints.
- Phase 04 Step 07 defines custody and safe metadata interfaces, Step 10 provides controlled UI designs. **Phase 09 owns actual key import, derivation, validation, signing, generation and rotation**; Phase 10 provides authoritative live connectivity verification; Phase 14 handles sanitized association/migration from legacy accounts without automatically copying plaintext `private.txt` archives; Phase 17 tests security and isolation.

### Implementation boundary and fixed-step acceptance additions

- **Step 05 (completed):** domain-only contracts `FiscalConnectionPolicyContracts.cs` plus pure policy tests `AnnualFiscalBindingPolicyTests.cs` model locking only on confirmed Success, lock after successful connectivity, manual approval and diagnostic categories. No further DB migration was added by this clarification. **The prior Step 05 signoff is preserved.**
- **Step 06:** wire lifecycle API so changes to a locked annual subscription require an approved exception; until Phase 12 entitlement storage exists, the endpoint must fail closed for operations it cannot safely authorize. Do not simulate live authority success.
- **Step 07:** metadata/secure custody reference and import contract; no raw-key persistence in ordinary tables.
- **Steps 08–09:** scoped review request and Level 1-only approval/deny with immutable audit, effective authorization changes and concurrency checks.
- **Steps 10–11:** user-facing connection test, reset request/history, and existing-key onboarding choices with explicit capability states (live authority check and import not available until designated phases); user and delegate see their permitted audit history.
- **Steps 12–13:** exercise invalid token, authority outage, race conditions, cross-tenant access denial, audit immutability, fail-closed unsupported operations and original Phase 03 identity regression. Final Ubuntu runtime verification retains existing schedule.
- **Phases 09/10/12/14/17:** actual key import, live authority test, annual entitlement lock/rebind, legacy migration and hardening respectively remain their owners. No production claim may precede their release gates.

**Step 05 clarification commits:** `dc600665`, `f5ab7b62`. **Tests:** source committed, execution not independently observed. After pull run `dotnet test ArginTaxPayer.sln -c Release --filter FullyQualifiedName~AnnualFiscalBindingPolicyTests` as a focused source regression. No Ubuntu runtime pass or official connectivity success is asserted.

### Step 06 — Fiscal Memory Management API (2026-10-13)

**Status:** Repository implementation committed. Focused tests and owner approval pending. Three owner-approved enhancements recorded in Step 05 clarification remain binding and are **not** claimed fully delivered in this step.

**Implementation:**
- `src/ArginTaxPayer.Infrastructure/Persistence/Taxpayers/FiscalMemoryManagementService.cs`: Level 1 + enrolled MFA + live opaque session required. All taxpayer/memory operations derive actor from Phase 03 server-side identity; no client-supplied role or owner IDs are trusted.
- `src/ArginTaxPayer.Domain/Taxpayers/FiscalMemoryOnboardingPolicy.cs`: side-effect-free normalization and fail-closed transition decision.
- `src/ArginTaxPayer.Api/Taxpayers/TaxpayerDevelopmentEndpoints.cs`: Development+loopback-only routes, `Program.cs` scoped DI, `tests/ArginTaxPayer.Tests/FiscalMemoryManagementPolicyTests.cs` focused unit coverage.
- Commit sequence: `72e47beb`, `7ff965de`, `850f06a9`, `cf9b3fda`, `2475f251`, `7c1c95aa`.

**Implemented endpoints (Development/loopback only):**
| Method | Path | Purpose | Statuses |
| --- | --- | --- | --- |
| POST | `/api/dev/taxpayers/{taxpayerId}/memories` | Register first externally assigned identifier as `PendingVerification` metadata; reject additional records until manual-review capability exists | 201/400/401/403/404/409 |
| GET | `/api/dev/taxpayers/{taxpayerId}/memories` | Role-gated list for a taxpayer | 200/401/403/404 |
| PUT | `/api/dev/taxpayers/{taxpayerId}/memories/{memoryId}/status` | Revision-checked `PendingVerification -> Retired` only; all active-memory changes and activation remain forbidden until authoritative proof and binding review enforcement | 200/400/401/403/404/409 |

Example POST body: `{"memoryIdentifier":"ABC123"}`. Example status body: `{"status":4,"expectedRevision":1}`. Response contains public memory metadata only and never private keys.

**Correctness and scope:** Fiscal-memory IDs are treated as user-supplied *unverified* registry metadata. Creation normalizes uppercase ASCII alphanumeric IDs, ensures an existing active taxpayer, requires Level 1 MFA, uses a PostgreSQL Serializable transaction, denies a second fiscal-memory record for that taxpayer, and relies on database-wide memory identifier uniqueness. This does **not** automatically generate/activate a fiscal memory, call the tax authority or lock an annual entitlement. A stale revision yields 409 and a cross-taxpayer memory URL yields 404. `PendingVerification -> Retired` is the only state change permitted before the server can verify the relevant subscription's annual binding. All other transitions, including `PendingVerification -> Active`, return `authority_verification_not_available` (409) instead of forging connectivity. New operations cannot replace a memory even after retirement until reviewed rebind exists. Level 2 delegation is disabled until Steps 08–09 have server-side assignment records.

**Owner amendments preserved:**
1. Annual plan: one taxpayer, unlimited invoices, lock only on positive real authority connectivity; customer cannot self-switch; Level 2 can request review; Level 1 approves; append-only before/after reason and scoped user/admin history. Phase 12 owns entitlement enforcement and final atomic rebind.
2. Test connection: distinguish token/authentication error, signing mismatch, authority 5xx, timeout and network outage; no fake positive result or invoice-charge side effects. Live connector belongs Phase 10.
3. Existing `private.txt`: supported path alongside new key generation, verify imported key/public fingerprint and secure custody. Cryptographic import belongs Phase 09; no ordinary DB field or API endpoint here can ingest private key material.

**Migration:** None introduced in Step 06; relies on Step 05 `20261013210000_AddFiscalMemories`. Schema verification must occur only against `argintaxpayer_test`; do not auto-migrate.

**Focused macOS verification:**
```bash
git switch phase/04-multi-taxpayer-management
git pull --ff-only origin phase/04-multi-taxpayer-management
zsh scripts/dev-up.zsh
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --filter 'FullyQualifiedName~FiscalMemoryManagementPolicyTests|FullyQualifiedName~FiscalMemoryPersistenceTests|FullyQualifiedName~AnnualFiscalBindingPolicyTests'
zsh scripts/dev-status.zsh
```
**Required API negative tests:** anonymous POST/GET/PUT => 401; Level 2/Customer => 403; unknown taxpayer => 404; invalid identifier => 400; duplicate memory ID => 409; second memory registration same taxpayer => 409 review required; cross-taxpayer URL => 404; illegal or stale transition => 400/409; no activation or memory replacement without review/authority/entitlement enforcement. DB race test must confirm concurrent first-register requests cannot both succeed. **Tests have not been executed by assistant** and no user acceptance is inferred.

**Ubuntu:** Routine runtime verification deferred to end of phase; test-database PostgreSQL migration already owned by Step 05. **CI:** Not checked. **Owner acceptance:** Pending.

### Step 07 — Certificate Metadata and Custody Boundary

**Status:** Source implementation complete, local verification and owner approval pending (2026-10-13). **Step 06 owner acceptance:** Approved 2026-10-13; local execution evidence not independently available.

**Changed files/commits:**
- `src/ArginTaxPayer.Domain/Taxpayers/CertificateCustodyPolicy.cs` (`5aeb8bfa`): metadata validators, explicit origin Generated/ImportedExistingKey, protected vault custody interface; intentionally **no usable plaintext key ingestion implementation**.
- `src/ArginTaxPayer.Infrastructure/Persistence/Taxpayers/FiscalCertificateMetadata.cs` (`63e9b1fc`), `ArginTaxPayerDbContext.cs` (`d9c451b7`), migration `20261013220000_AddFiscalCertificateMetadata` (`81799c2d`), EF snapshot (`f1d39bd4`).
- `FiscalCertificateMetadataService.cs` (`321ba61e`), tests `FiscalCertificateMetadataTests.cs` (`30cde604`), DI in `Program.cs` (`cb32740d`), restricted API in `TaxpayerDevelopmentEndpoints.cs` (`5e785244`).

**Schema and invariants:** Public SHA-256 fingerprint (lowercase 64 hex), optional issuer and UTC validity interval, MetadataOnly/Valid/Expired/Revoked, generated/imported origin, opaque vault custody reference, creation and supersession timestamps, required FK to fiscal memory. Restrict deletes. Unique partial index `fiscal_memory_id WHERE superseded_at_utc IS NULL` allows **one current certificate metadata record** while retaining historic rotations. DB check constraints validate enum ranges, fingerprint, dates. The vault reference itself must not contain actual credentials or key material.

**Protected read API:** Development/loopback `GET /api/dev/taxpayers/{taxpayerId}/memories/{memoryId}/certificates`. Existing opaque bearer + current Level1 role + MFA required; not found for memory outside taxpayer; response excludes custody reference and secret material and computes an effective Expired status from UTC timestamps. Anonymous 401, Level2/Customer 403, wrong scope 404. No write/import/generation route is exposed; issuance and key ingestion remain **blocked** until secure custody in Phase 09 and authority verification in Phase 10.

**Legacy `private.txt` requirement preserved:** ImportExistingPrivateKey is a first-class onboarding origin, not a forced public-key reissue. Later Phase 09 must parse supported private-key formats securely, derive its public key and SHA-256 fingerprint, verify public-key/certificate matching, provision into restricted vault (no plaintext in DB/logs), and allow the existing registered public key when evidence matches. No currently implemented code claims to accept or safely store the legacy plaintext file.

**Cross-phase constraints:** Annual plan locks one taxpayer after confirmed successful real authority connectivity and allows only reviewed manual reassignment; metadata rows never unlock annual binding. Connection diagnostics must distinguish invalid token, signer mismatch, service outage, timeout and network failure; **no live tax authority call** is claimed here.

**Migration:** New `20261013220000_AddFiscalCertificateMetadata` committed; **NOT applied by assistant**. Operator must verify database name exactly `argintaxpayer_test`, confirm earlier migrations `20261013210000_AddFiscalMemories`, and run explicit migration only on test DB. No automatic migration at startup.

**Focused macOS verification after pull:**
```bash
zsh scripts/dev-up.zsh
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --filter 'FullyQualifiedName~FiscalCertificateMetadataTests|FullyQualifiedName~FiscalMemoryManagementPolicyTests'
zsh scripts/dev-status.zsh
```
**Acceptance negatives:** invalid fingerprint rejected, expired/revoked never treated valid, two current metadata versions rejected, historical superseded versions coexist, FK prevents cross/nonexistent memory linking, API never leaks custody reference/secret, Level2 denied and unrelated memory hidden. DB integration, full API HTTP regression and CI were **not executed** by assistant; tests currently assert EF metadata and pure policy/read-projection structure only. **Ubuntu:** routine runtime verification deferred to Step 13, schema verification required now. **Owner acceptance:** Pending.

### Step 08 — Taxpayer Delegate Assignment Model

**Status:** Implementation committed; build, live PostgreSQL, security integration tests and owner signoff are pending. Step 07 was approved by owner 2026-10-13.

**Implementation:** `TaxpayerDelegateGrant`, `TaxpayerDelegateService`, EF configuration/model snapshot, migration `20261013230000_AddTaxpayerDelegateGrants`, scoped DI registration and three Development/loopback-only HTTP endpoints in `TaxpayerDevelopmentEndpoints`. Metadata tests: `TaxpayerDelegatePersistenceTests`. Existing commits `bb1cd935`, `0e711565`, `172cc240`, `d7558fc7`, `9fac23c6`, `b5df1d84`, `cfc8d27a`; review corrections `e18a9df1`, `1eb2d03f`, `e71733bf`.

**Endpoints:**
- `POST /api/dev/taxpayers/{taxpayerId}/delegates` with `{"delegateUserId":"<uuid>"}` grants to an active Level2 user with confirmed MFA, initiated by active Level1 with MFA.
- `POST /api/dev/taxpayers/{taxpayerId}/delegates/{grantId}/revoke` with `{"expectedRevision":1,"reason":"<non-empty reason>"}` conditionally revokes without deletion. Stale revision returns conflict.
- `GET /api/dev/taxpayers/{taxpayerId}/delegates/history` provides Level1-only chronological issue/revoke history.

**Database:** Required FKs to taxpayer, delegate, Level1 grantor, optional revoker; `revoked_at_utc`, required reason for completed revocation, revision, UTC timestamps. Partial unique index on `(taxpayer_id,delegate_user_id) WHERE revoked_at_utc IS NULL` prevents duplicated concurrent active grants while preserving revoked history. Revocation uses conditional update, not a hard delete. Multiple separately granted Level2 users may be assigned the same taxpayer; this does **not** grant a second taxpayer to an annual subscriber.

**Security:** Every operation authenticates Phase03 opaque session and checks live Level1 + MFA. Target delegate requires existing Level2 + MFA; no implicit role grant. No Level2 or customer global history access. Step09 is still responsible for adding live scope checks to every Level2 taxpayer read and mutation. As a result, this step does **not** claim that all future Level2 APIs are scoped yet.

**Migration:** `20261013230000_AddTaxpayerDelegateGrants` committed, not executed by assistant. Verify `SELECT current_database()` is exactly `argintaxpayer_test` before applying and verify Step07 migration history first.

**Targeted macOS validation:**
```bash
git switch phase/04-multi-taxpayer-management
git pull --ff-only origin phase/04-multi-taxpayer-management
zsh scripts/dev-up.zsh
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --filter FullyQualifiedName~TaxpayerDelegatePersistenceTests
zsh scripts/dev-status.zsh
```
PostgreSQL integration acceptance: duplicate/concurrent grants must fail, revoked historical grants must remain, re-grant after revocation must succeed, revision collision must fail, invalid FK must fail. HTTP security acceptance: unauthenticated 401; Level2/Customer 403; wrong taxpayer 404; invalid delegate denied; revoke by Level1 with reason succeeds; stale revoke 409; Level2 history denied. **None of these runtime outcomes has been independently observed.** Ubuntu end-of-phase verification remains planned; DB migration verification must be performed now.

**Owner acceptance:** Pending.

### Step 09 — Server-Side Scoped Query and Command Policies

**Status:** Source implementation committed on 2026-10-13; owner acceptance pending. Step08 owner-approved 2026-10-13 (operator-reported), not independently certified by assistant.

**Implementation:** `TaxpayerScopeService.cs` (`1fa64be5`, `bf64d766`), DI in `Program.cs` (`50a7396a`), routes in `TaxpayerDevelopmentEndpoints.cs` (`3d663f08`), focused SQL-shape test `TaxpayerScopePolicyTests.cs` (`d0d0bbcf`). No schema migration introduced.

**Server-side policy:** Every request authenticates the current opaque Phase03 session; actor must be an active Level1 or Level2 with enabled MFA. Level1 can query all taxpayers. Level2 can query only profiles where a **currently unrevoked** row in `taxpayer_delegate_grants` maps that taxpayer to their server-resolved user ID. For unauthorized or nonexistent resource IDs, detail returns indistinguishable 404 (prevent enumeration); no grant and pending/revoked grants never confer access. List scopes in SQL before materialization and excludes archived profiles. Never trust client-provided role, delegate or taxpayer-owner IDs for scope. Unrecognized role or user is denied by default.

**Development + loopback routes:**
- `GET /api/dev/taxpayers`: authorized scoped list.
- `GET /api/dev/taxpayers/{taxpayerId}`: authorized scoped detail.
- `PATCH /api/dev/taxpayers/{taxpayerId}/display-name`: `{"displayName":"...","expectedRevision":1}`, safe display-name-only scoped command. It does not permit changes to legal identifier, taxpayer status, memory ID, key or annual subscription. Atomic SQL UPDATE predicate includes `revoked_at_utc IS NULL` for Level2, checks current revision, and appends an audited before/after profile-change row in the same transaction. Stale attempts conflict; missing/unassigned IDs not found.
- Prior privileged Step03–08 mutating endpoints retain Level1-only authorization. In particular, scoped Level2 taxpayer access must **not** imply automatic Level2 permission to activate/replace fiscal memory, view custody metadata, grant/revoke other delegates or bypass annual plan review.

**Security acceptance matrix:** Level1 list/detail works; Level2 receives only their assigned records; Customer/Level2 without grant denied; archived filtered in list; cross-delegate guessed UUID returns 404; revoked grant immediately removes read/list/write access in subsequent DB requests; stale revision 409; wrong taxpayer ID 404; Level2 cannot mutate status/identifier/memory/keys via scoped rename; anonymous 401; no caching of grants; privilege removal and MFA disable invalidate subsequent decisions. Exercise parallel revoke/edit with an actual PostgreSQL transaction and verify no unauthorized committed state, plus audit entry created exactly once on successful edit.

**Current tests:** focused `TaxpayerScopePolicyTests` checks generated SQL predicate shape and DTO surface, not actual PostgreSQL authorization, concurrency or HTTP behavior. **Build, tests and CI not executed by assistant.** Complete targeted and negative-path tests locally before owner signoff. Cross-application enforcement for later modules is a continuing mandatory rule; Step12 covers full integration. Production endpoint activation remains intentionally disabled until security gate.

**macOS test:**
```bash
git switch phase/04-multi-taxpayer-management
git pull --ff-only origin phase/04-multi-taxpayer-management
zsh scripts/dev-up.zsh
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --filter 'FullyQualifiedName~TaxpayerScopePolicyTests|FullyQualifiedName~TaxpayerDelegatePersistenceTests|FullyQualifiedName~TaxpayerLifecycleTests'
zsh scripts/dev-status.zsh
```
**Migration:** None in Step09. Requires Step08 `20261013230000_AddTaxpayerDelegateGrants` applied on verified `argintaxpayer_test` before local HTTP integration. **Ubuntu:** defer full runtime verification to Step13 absent platform-specific errors. **Owner acceptance:** pending.

### Step 10 — Taxpayer and Fiscal Memory RTL Management UI

**Status:** Implemented in GitHub, operator/local UI acceptance pending. **Step 09** accepted for continuation by owner on 2026-10-13; full end-to-end acceptance remains explicitly scheduled for Step 12, not falsely marked tested.

**Source commits:**
- `apps/web/app/api/taxpayers/[...path]/route.ts` (`388d3ecb`, `99abe71e`): strict allowlisted backend-for-frontend endpoint, HttpOnly cookie session, server-only bearer forwarding, same-origin write validation, no-store response, payload size limits, sanitized failure responses and Development-only activation. `/api/taxpayers/list` and `/api/taxpayers/new` intentionally map to the backend base path due to Next.js catch-all routing.
- `apps/web/app/taxpayers/page.tsx` (`155605aa`): server-side authenticated role gate for Level1/Level2 with Persian RTL header.
- `apps/web/app/taxpayers/taxpayer-workspace.tsx` (`c80ab8bb`, `145a53bb`): live list/search/detail of authorized taxpayer profiles; safe display-name editing with expectedRevision; Level1 creation of taxpayer and first unverified fiscal memory; memory state display, restricted certificate metadata lookup. Responsive states: loading/empty/busy/conflict/not-found/unauthorized/service outage.
- `apps/web/app/globals.css` (`31e2132d`): responsive full-width desktop layout, RTL controls, mobile breakpoints.
- Account/home navigation: `apps/web/app/account/page.tsx` (`8a6fa261`) and `apps/web/app/page.tsx` (`d244304b`).
- `apps/web/tests/taxpayer-workspace.test.mjs` (`206cfa84`): static regression for role gate, server-only session proxy, allowlist and safe UI scope.

**Available UI:** Start local services and open `http://127.0.0.1:3000/taxpayers` after login. Level1 can see all authorized taxpayers, create taxpayers, register first PendingVerification memory, rename display name and inspect restricted certificate metadata. Level2 can list/detail **only live assigned taxpayers** and rename allowed display names. UI does not accept client-supplied delegate actor, raw session tokens or private keys. The backend SQL policies from Step09 remain authoritative. Level2 cannot register/replace fiscal memory or view the custody metadata endpoint. Taxpayer archive/status transitions remain protected behind earlier Level1 API pending explicitly designed administrative forms.

**Disabled/explicitly postponed:** Level1 grant/revoke UI and separate Level2 workspace are Step11. Real authority token connectivity test and imported `private.txt` key custody belong to roadmap Phases 10 and 09 respectively: the UI explicitly says unavailable, never fabricates a green connection or claims keys have been installed. Annual single-taxpayer subscription lock/review remains a binding Phase12 requirement. No business client or Level2 gets global audit read permissions.

**Acceptance:** Test Level1 login and create a valid sample natural/legal profile, see it in list; open record and observe memory details, create first fiscal memory (only if none exists), and ensure duplicate/second memory is rejected. Test Level2 with active assignment sees only assigned profiles, not other taxpayer by direct UUID and not global memory/cert admin operations. Revoke assignment with Step08 API and refresh or reopen to verify list/detail access disappears. Try a stale revision and verify visible conflict. Test invalid MFA/expired session and 401/403 redirect/message; test network outage/loading/empty and RTL mobile viewport. **Source tests are static guards, not proof of live UI or DB behavior.** Test API requires earlier PostgreSQL migrations through Step08 on `argintaxpayer_test`.

**Mac local commands:**
```bash
git switch phase/04-multi-taxpayer-management
git pull --ff-only origin phase/04-multi-taxpayer-management
zsh scripts/dev-up.zsh
(cd apps/web && npm test && npm run typecheck && npm run build)
dotnet test ArginTaxPayer.sln -c Release --filter 'FullyQualifiedName~TaxpayerScopePolicyTests|FullyQualifiedName~TaxpayerDelegatePersistenceTests'
zsh scripts/dev-status.zsh
```
**Migration:** None in Step10. **Ubuntu:** deferred to Step13 except platform-specific regression. **macOS tests, CI and browser acceptance:** not independently executed by assistant. **Owner acceptance:** Pending.

### Step 11 — Level 1 Assignment Console and Level 2 Workspace

**Status:** Source implemented; macOS test and owner approval pending. Step 10 owner-approved 2026-10-13. No additional EF migration.

**Source/commits:**
- `apps/web/app/taxpayers/delegate-console.tsx` (`f90fba68`, `35e22f9c`): Level1 panel for taxpayer-to-Level2 grant, revocation with mandatory reason and optimistic `expectedRevision`, and historical issue/revocation display. Shows explicit 401/403/404/409 error categories and loads only actual DB-backed responses.
- `apps/web/app/api/delegations/[...path]/route.ts` (`6ac5d941`): restricted dedicated Next.js server gateway to existing Level1-only development delegate API, using HttpOnly access cookie, server-only bearer forwarding, same-origin validation for mutations, a strict UUID/operation allowlist, no-store, timeout, and sanitized upstream error responses. Never exposes session token to client.
- `apps/web/app/taxpayers/taxpayer-workspace.tsx` (`939a5148`): delegate management visible only to Level1; Level2 workspace lists only backend-assigned profiles, allows permitted display-name edit but does not attempt Level1-only memory/certificate endpoints.
- `apps/web/tests/delegate-console.test.mjs` (`a8ec874d`): static regression for panel grant/revoke/revision and gateway session boundary; not a full browser/e2e security test.

**Role matrix:** Level1 can see taxpayer list, inspect Level1 memory/certificate metadata, grant/revoke scoped Level2 access and view full authorized grant history. Level2 only sees active assigned taxpayer profiles via Step09 SQL predicates and permitted display-name edits. Customer cannot access workspace. An active assignment never means annual-plan ownership transfer, fiscal-memory replacement approval, or certificate/key permission.

**Security:** All grant/revoke/history backend routes revalidate Level1, active MFA and persisted role. Revoking sets `revoked_at_utc` without deleting history. Subsequent server reads and scoped mutations for Level2 use live `revoked_at_utc IS NULL` predicates. Already-rendered client data is not itself a source of authorization and must be cleared/refetched as browser navigates; do not claim push-based live UI revocation. No global administrator list or delegated security log is exposed.

**Manual UI verification:** Login as Level1, open `http://127.0.0.1:3000/taxpayers`, select a test taxpayer, enter an existing active MFA-enabled Level2 UUID obtained from the account screen, grant access, verify history; login Level2 in separate browser profile and confirm only assigned taxpayers; revoke by Level1 with a reason, refresh Level2 and verify taxpayer disappears and direct detail request returns 404; regrant and check history preserved; try stale `expectedRevision` to receive 409; verify anonymous, Customer and Level2 mutation attempts are denied. Test SSR role gates, same-origin rejection, no localStorage/token leak, browser mobile RTL and error/loading screens. **These scenarios have not been run by assistant.**

**Local operator commands:**
```bash
git switch phase/04-multi-taxpayer-management
git pull --ff-only origin phase/04-multi-taxpayer-management
zsh scripts/dev-up.zsh
(cd apps/web && npm test && npm run typecheck && npm run build)
dotnet test ArginTaxPayer.sln -c Release --filter 'FullyQualifiedName~TaxpayerScopePolicyTests|FullyQualifiedName~TaxpayerDelegatePersistenceTests'
zsh scripts/dev-status.zsh
```
**Migration:** None. Step08 schema must exist on verified `argintaxpayer_test`. **Ubuntu:** full runtime verification deferred to Step13. **CI:** Not independently checked. **Owner acceptance:** Pending.

## Phase Exit Gate
All 13 steps accepted, focused regression passing, Ubuntu verification once, schema/security review, owner signoff. Merge to main, immutable tag, GitHub Release and production deployment are distinct gated actions and must never be assumed completed.


### Customer-owner correction implementation checkpoint — 2026-10-13

**Repository implementation committed; local build, migration and HTTP/integration tests NOT executed by assistant.** Core commits include domain contract `97947f0f`, owner entities `bd60381f`, EF `fec1887f` / `0a25f642`, migration `11dd126e` / `32e4a24e`, owner service `189b347c`, scope `13cd233e`, removal of unsafe taxpayer-level API `1f52887e`, account provisioning `fba5713d`, public registration `f43c894d` / `559c48f5`, customer creation bound to owner `59e19cd0`, safe owner API `cbbd80ef`, protected Next proxy `a1404fd8`, and account console `fa461ee4` / `fa51207d`. Legacy screens/routes/service were deleted and do not authorize new access. Audit listing API and UI were added in follow-up changes.

**Operational prerequisite:** When more than one active Level1 exists, configure `Ownership__DefaultLevel1UserId=<Level1 UUID>` (mapped to `Ownership:DefaultLevel1UserId` in .NET). Unset uses the unique active Level1 only; invalid or ambiguous mapping blocks public/Level2-created customer acquisition rather than guessing financial owner. Do not include real identity IDs, credentials or customer data in tracked config. Existing pre-correction customers are intentionally excluded from partner attribution until approved legacy reconciliation.

**Schema:** Explicit `20261013233000_AddCustomerCommercialOwnership` must follow Step08 and requires verifying `current_database() = 'argintaxpayer_test'`. It creates separate current ownership and immutable intent history tables plus an optional legacy-compatible `taxpayer_profiles.customer_user_id` FK. No automatic startup migration. **Read-only audit**: `scripts/verify-customer-ownership.sql`, after migration, reports orphaned customers/profiles and deprecated grant counts without altering rows. No guessed mass backfill.

**Required local regression before reapproving Step11:** macOS `zsh scripts/dev-up.zsh`; `dotnet build ArginTaxPayer.sln -c Release`; `dotnet test ArginTaxPayer.sln -c Release --filter 'FullyQualifiedName~CustomerCommercialOwnershipTests|FullyQualifiedName~TaxpayerScopePolicyTests'`; `cd apps/web && npm test && npm run typecheck && npm run build`. Test external email and phone signup default Root, Level1 direct creation, Level2-owned creation, Level1 approved transfer to/from Level2, multi-taxpayer account lookup, old taxpayer grant denied, owner revision conflict, nonexistent customer 404, unauthorized 401/403, immutable historical owner snapshots and immediate former-partner read/edit denial. Then complete Step12 PostgreSQL/HTTP/browser negative-path and Phase12 finance settlement tests. Older automated tests may require contract updates due mandatory `customerUserId` in new taxpayer registration; **do not waive failing tests**.

**Phase12 reservation:** No invoice/collection/partner ledger implementation is claimed in Phase04. Root-direct customers belong financially to Root. Partner customer net realized participation requires verified collected cash *per specific transaction*, approved allocated taxes/expenses and partner rate as immutable deal snapshots. Every initial plan purchase, renewal, bundle sale, company and partial payment has separate tracing/settlement. Reassignment is prospective, never retroactive. Monthly/annual one-taxpayer plan binding is independent from customer→multiple-taxpayers membership.



### Owner-requested customer-first UI correction — 2026-10-14

**Reason:** The screenshot revealed that Step11's top-level `/taxpayers` UI still displayed taxpayers first and forced a customer UUID in the wrong top-level create form, despite the approved customer-to-many-taxpayers commercial ownership correction. This was an incomplete implementation.

**Correct primary UI:** `/users` is now the central **software users management** view. It is guarded by the existing server-side Level1/Level2 session/role gate. It queries `GET /api/dev/customer-owners/accounts`, returning registered Customer identities (mobile/email, active status, registered timestamp, commercial owner, acquisition source and taxpayer count). Level1 sees all registered customers, including old identities without a commercial owner (explicitly marked unresolved rather than fabricating partner attribution); Level2 sees **only accounts currently assigned to it**. Click a customer to load only that customer's taxpayer profiles via `GET /api/dev/customer-owners/{customerId}/taxpayers` and the same live SQL authorization predicates. Former `/taxpayers` bookmarked admin URL redirects to `/users`; account/home navigation displays «مدیریت کاربران نرم‌افزار». User creation is available inside the main users page through existing admin provisioning. Customer partner reassignment remains a Level1-only nested panel. Fiscal taxpayer/memory screens are subordinate to customer detail, never the customer master list.

**Changes:** `CustomerCommercialOwnershipService.ListCustomerAccountsAsync` and `ListCustomerTaxpayersAsync` (including explicit legacy-unassigned root visibility), development-only API, Next BFF allowlist for `accounts` and `{customerId}/taxpayers`, new `apps/web/app/users/page.tsx` and `users-workspace.tsx`; home/account links updated; `apps/web/app/taxpayers/page.tsx` redirects; `apps/web/tests/taxpayer-workspace.test.mjs` updated. Database migration: **none** for this UI correction. The existing customer ownership schema from `20261013233000_AddCustomerCommercialOwnership` is required.

**Subscription distinction:** The customer may have many tax profiles in principle. A monthly/annual plan's allowed active taxpayer count (one vs many, depending on plan) is a **per-entitlement constraint** implemented in Phase12, not the customer's commercial ownership cardinality. Do not display a guessed plan or silently grant unlimited taxable accounts; self-service add-taxpayer UI remains disabled until the entitlement check is enforced server-side. The currently existing Level1 development-only taxpayer registration API is **administrative**, not a plan-enforced public subscription workflow and must not be released as one.

**Acceptance now:** After `git pull`, login Level1 and open `http://127.0.0.1:3000/users`; list rows must represent **users**, showing phone/email and taxpayer counts. Select a user to see its own taxpayers. Level2 must only see customer accounts whose current partner owner is that Level2; no cross-partner guesses; after owner change/revocation a refetch removes old access. Old users without owner must appear for Level1 with «نیازمند تعیین مالکیت», not be silently omitted or assigned. Check `/taxpayers` redirect and corrected top/home/account labels. Use `zsh scripts/dev-up.zsh`, `cd apps/web && npm test && npm run typecheck && npm run build`; additionally run targeted .NET ownership tests. **No tests have been run by assistant.** Do not close this correction until browser acceptance is reported.



### Step 11 corrective completion: paginated software-users table, modal and commercial ownership — 2026-10-14

**Owner requirement clarified:** Customer ownership is a Phase04 Step11 deliverable, NOT deferred to future financial phases. Public/self signup and Level1-provisioned customers go to a real Level1 owner by default; Level2-provisioned customers go automatically to that Level2 and the configured Level1 root; an existing root-owned customer may be transferred to a partner **only by Level1 approval with reason and immutable audit trail**, e.g. after the partner's introduction request. Partner-request submission/approval inbox UX may be extended in Phase16 but **no partner can self-approve or acquire a customer simply by claiming an introduction**. Transaction attribution/settlement belongs to Phase12 and is prospective/snapshot-based.

**UI replacement:** `apps/web/app/users/users-workspace.tsx` was rewritten as a responsive RTL customer table with 10/20/50 page sizes, case-insensitive mobile/email/UUID search, commercial-owner filter (root/partner/unresolved), loading/empty/error states, row actions and a native modal with backdrop, scroll containment, keyboard/Escape closing and accessible labels. User details display identity, source, owner, registration and **nested taxpayer list** (queried only for that customer). The modal has a Level1-only commercial-ownership editor, a safe select of currently MFA-enabled Level2 partners loaded from `GET /api/dev/customer-owners/partners`, a direct-Level1 option, approval reason, compare-and-swap revision and an audited history view. Neither the user table nor the modal fabricates subscription entitlements.

**Legacy resolution:** Old Customer identities with no `customer_commercial_owners` row remain visible to Level1 with `revision=0`; selecting one and submitting an explicit reason creates an initial owner record and a history event `0 -> 1` atomically. Source uses acquisition channel `LegacyReconciledByLevel1 = 5`, not a fabricated public or Level2 acquisition. An additional incremental migration `20261014002000_AllowLegacyCommercialAcquisition` updates the channel check constraint from 1–4 to 1–5. This is intentional incremental migration after `20261013233000_AddCustomerCommercialOwnership`; do NOT manually edit or reapply the earlier migration on a database where it was already applied. Duplicate legacy-resolution attempts return 409 conflict, preserving history. No bulk backfill or automatic guessed matching of fiscal IDs is performed.

**API security:** `ListPartnersAsync` is Level1 + active MFA only; customer-account list/detail remains scoped by live role and commercial partner ownership. Only Level1 can transfer/approve a referred customer. The Next.js BFF allows `/api/customer-owners/partners` with server-only HttpOnly session forwarding. No secret or financial data is included in the customer table. A revoked partner loses access on subsequent requests (and UI closes stale dialogs when ownership list changes). Distinguish already-rendered screen from live backend authorization; the former refreshes on focus.

**Migration and local acceptance (pending):**
1. Verify the session is pointed at `argintaxpayer_test`; check EF migration list and apply up to `20261014002000_AllowLegacyCommercialAcquisition` (which also applies any unapplied earlier migrations). Do not migrate another DB.
2. `zsh scripts/dev-up.zsh`; `dotnet build ArginTaxPayer.sln -c Release`; `dotnet test ArginTaxPayer.sln -c Release --filter FullyQualifiedName~CustomerCommercialOwnershipTests`; `cd apps/web && npm test && npm run typecheck && npm run build`.
3. Browser at `/users`: test full-width RTL table with paging, search and owner filters; modal per user; outside click/Escape; user details and nested taxpayers; partner dropdown; Root assignment with reason, history, 409 on stale revision; public signup owned by configured root; Level2-created customer automatically assigned; Level2 never sees other owners' customers; legacy unowned user manual resolution revision 0 to 1. Browser, PostgreSQL concurrency and end-to-end behavior **have not been independently tested by assistant**.

**Source status:** Implementation is in GitHub; no claim of successful local/Ubuntu build, application or migration. The formal Level2 partner claim inbox is a Phase16 administration UX item, not a prerequisite for Level1's manual approval in Step11. **Step11 owner re-acceptance pending actual browser and database tests.**



### Step 12 — Integration and Negative-Path Regression (2026-10-14)

**Step11 owner approval:** Confirmed 2026-10-14, including customer-first paginated RTL `/users` page, modal and root-approved customer-level commercial partner changes. Acceptance of Step12 requires evidence from runnable tests; source commits are NOT evidence of tests passing. Do not proceed to Phase04 Step13 until critical policy and transactional negative-path checks pass.

**Committed artifacts:**
- `tests/ArginTaxPayer.Tests/Phase04ScopeRegressionTests.cs`: EF PostgreSQL provider SQL-shape and model integrity assertions for live customer owner scoping; ensures deprecated `taxpayer_delegate_grants` are not consulted and owner/history FK consistency.
- `scripts/phase04-step12-smoke.mjs`: **read-only** authenticated HTTP smoke with three independent role sessions (Level1, assigned Level2, unrelated Level2), including 401/403/404, customer detail, nested taxpayer lists and privileged partner directory. All responses are checked for scope leaks. Session tokens must only be in transient environment variables, never committed or printed.
- `scripts/phase04-step12-transfer.mjs`: explicitly opt-in mutation smoke restricted to a dedicated disposable customer, verifying Level1 assignment to partner, revision conflict, scoped read, subsequent revocation to Level1, former partner denied and persistent audit. Refuses to run without `PHASE04_ALLOW_MUTATION=YES`, special fixture IDs and separate valid test sessions. Attempts fixture restoration in `finally` and warns on failure; never run on real customers.
- `scripts/phase04-step12-audit.sql`: transaction `BEGIN READ ONLY`, hard guard for `current_database() = 'argintaxpayer_test'`, checks ownership role/revision invariants, summarizes root/partner ownership, unlinked legacy accounts/taxpayers without revealing identities.
- `scripts/phase04-step12-check.zsh`: guarded quick macOS .NET build, targeted tests, Next.js tests/typecheck/build, and read-only PostgreSQL audit; respects `.env.dev.local`, no schema mutation, no UI login automation.

**One-command owner local test:** With the preexisting AutoSSH tunnel and Development services started via `zsh scripts/dev-up.zsh` (or `zsh scripts/dev-status.zsh` if already up), execute:
```bash
zsh scripts/phase04-step12-check.zsh
```
No new migration is introduced in Step12; the database MUST already contain prior approved migrations `20261013233000_AddCustomerCommercialOwnership` and `20261014002000_AllowLegacyCommercialAcquisition`. Never apply them blindly to a non-test DB. Standalone auth HTTP smoke requires `PHASE04_ROOT_TOKEN`, `PHASE04_PARTNER_TOKEN`, `PHASE04_OTHER_TOKEN`, `PHASE04_CUSTOMER_ID`, and optional `PHASE04_TAXPAYER_ID` / `PHASE04_LEGACY_CUSTOMER_ID`:
```bash
node scripts/phase04-step12-smoke.mjs
```
Fixtures and session tokens are read from the environment (see the script header); do not paste tokens into bug reports or screenshots. For disposable owner-transfer fixture only, separate opt-in:
```bash
PHASE04_ALLOW_MUTATION=YES node scripts/phase04-step12-transfer.mjs
```
Requires separately configured `PHASE04_FIXTURE_CUSTOMER_ID` (Level1 direct customer) and `PHASE04_FIXTURE_PARTNER_ID`. Confirm no pending financial transactions on that fixture. It restores root ownership in `finally`. Do not run for real customers.

**Step12 acceptance matrix:**
1. Existing root account with MFA can list all Customer identities including unreconciled legacy (annotated, not auto-claimed); assigned Level2 sees only its current customer list; unauthenticated 401; unrelated partner's guessed customer UUID and nested taxpayer URL both 404; customer role 403.
2. Every Customer-owned taxpayer belongs to one identity. The same Customer may have multiple taxpayers where future plan entitlements allow it; creating fiscal taxpayer via development API is restricted to Level1 and requires an explicit owner customer ID. Never infer a Level2 or a subscription entitlement from tax identifiers.
3. Public email/mobile registration chooses configured unique root; Level1-created customers stay root-direct; Level2-created customers belong automatically to creating partner. If configured default Level1 is missing/ambiguous, fail closed rather than silently inventing attribution; verify registration does not leave partial identity rows or burn verified OTP.
4. Root manual transfer with reason and revision creates exactly one immutable history entry, cannot self-approve from Level2, stale revision conflicts 409, concurrent initial legacy assignment conflicts not duplicate, reattribution affects subsequent access but never modifies past payments or settlements.
5. Reassigned away Level2 must receive 404 on next customer detail, nested taxpayer list and taxpayer detail/edit; ensure old deprecated taxpayer grant never yields access. Compare assignment and scoped mutation race on live PostgreSQL and verify there is no unauthorized committed change. UI state refetches on focus and modal closes for removed customer.
6. Certificate list still Level1-only, opaque custody reference must not leak; invalid/revoked/mismatched memory lifecycle requests denied; archived taxpayers filtered where promised. Annual plan lock, real tax authority verification and billing settlement remain future phase feature gates, not fake green statuses.
7. Validate RTL browser table pagination and responsive user modal, session expiry and 401/403/409 displays; verify HTTP smoke uses loopback Development endpoints only.
8. Migration audit with only `argintaxpayer_test`, no unreviewed destructive writes. Verify EF latest applied migration and no snapshot/schema drift.

**Explicit limitations:** Only source/test additions were committed by assistant; no live test execution on macOS VPS DB, no browser E2E or SQL transaction race execution. Read-only HTTP smoke and opt-in mutation smoke test complementary cases; neither is equivalent to exhaustive production certification. Report failures before Step12 owner approval; Step13 is final hardening and release handoff, not an excuse to waive the acceptance gate.



### Step 13 — Hardening and Phase Handoff (2026-10-14)

**Owner disposition:** Step12 approved for progression on 2026-10-14. This is an explicit waiver of the **three-account, token-based HTTP smoke not executed**, not an assertion that authorization, concurrency and browser tests passed. The unexecuted smoke and mutation/race test remain tracked open verification items. Step13 implementation is complete in source; final production release is **not approved** until external acceptance checks are supplied.

**Hardening committed:**
- `TaxpayerScopeService.RenameAsync`: PostgreSQL `Serializable` isolation so a delegated Level2 write and an owner transfer cannot both commit under stale access assumptions; database serialization abort maps to `Conflict`, including EF-wrapped failures. The owner must verify the actual race scenario on PostgreSQL.
- `CustomerCommercialOwnershipService.TransferAsync`: PostgreSQL `Serializable` isolation, re-authenticated Level1 session/MFA, target Level2 active-role/MFA recheck within the transaction, CAS `expectedRevision` and append-only owner-change row within the same transaction. Concurrent and legacy conflicting requests fail closed.
- `scripts/phase04-step13-verify.zsh`: safety-gated, **read-only** local release evidence runner. Rejects wrong branch or uncommitted files, invokes Step12 model/tests/UI/DB checks, verifies connected database is `argintaxpayer_test`, then reads `__EFMigrationsHistory` to confirm the four relevant migration IDs. No implicit migration, reassignment, deletion, tag, merge or release.
- Step12 scripts retained for role-based readonly smoke, explicit opt-in disposable-owner mutation smoke and PostgreSQL audit.

**Local verification command:** with AutoSSH and Development environment already running, use:
```bash
git switch phase/04-multi-taxpayer-management
git pull --ff-only origin phase/04-multi-taxpayer-management
zsh scripts/dev-up.zsh
zsh scripts/phase04-step13-verify.zsh
```
If services are already running, skip `dev-up` and check `zsh scripts/dev-status.zsh`. Any failed build, migration audit, model drift or negative-path test blocks final release. An approval to skip the three-token smoke must remain visible as a risk, not changed to PASS.

**Ubuntu final verification, only at the end of the phase:**
```bash
git switch phase/04-multi-taxpayer-management
git pull --ff-only origin phase/04-multi-taxpayer-management
bash scripts/ubuntu-dev.sh verify
```
Ubuntu script uses loopback-only `argintaxpayer_test` and never auto-applies migrations. Do not use production DB or expose Development routes publicly. Confirm Release build, full test suite, Next.js typecheck/build and migration consistency. Only the local operator can attest to results; assistant does not have a dotnet runtime or VPS test execution evidence.

**Schema gate:** Ensure EF history contains these four migration IDs (without re-running them):
1. `20261013220000_AddFiscalCertificateMetadata`
2. `20261013230000_AddTaxpayerDelegateGrants` (deprecated legacy schema retained for audit; cannot authorize new partner access)
3. `20261013233000_AddCustomerCommercialOwnership`
4. `20261014002000_AllowLegacyCommercialAcquisition`

**Cross-phase handoff, not silently changed:** Phase05 can enhance customer record management and taxpayer CRUD based on current customer account ownership. Phase09 handles secure private key/CSR or existing-key import; Phase10 handles actual tax authority connectivity; Phase12 is authoritative for subscription entitlement enforcement, single-taxpayer annual plan binding, invoice/receipt attribution, cash-based net profit sharing, Level1 approved tax/cost deduction and per-deal partner settlement; Phase16 handles formal Level2 referral request/approval workflow and financial administration; Phase17 handles comprehensive pen tests and race/load regression. None of these is marked implemented by Phase04.

**Formal release gates before merge/tag:** (a) successful macOS final script, (b) successful Ubuntu `verify`, (c) no unknown EF schema drift, (d) clean verified branch, (e) manual browser smoke for Level1/Level2 customer table, modal and reassignment, (f) recorded owner acknowledgment of intentionally skipped three-account negative-path smoke, (g) reviewed PR to main. Only after these checks and **explicit final phase release approval** may the operator merge to main and create a single annotated release tag (suggested `v0.4.0`, check existing tags first). A GitHub commit or draft PR is not a finished release.

**Outstanding risks (carry as OPEN until evidenced):**
- Historical unowned customer accounts and unlinked taxpayer rows require explicit supervised reconciliation; do not infer from old deprecated grants or tax IDs.
- Concurrent partner reassignment and a delegated taxpayer edit are now transactionally protected but still require a real PostgreSQL race acceptance test.
- Three-account HTTP isolation smoke and browser end-to-end authorization have not been run.
- Phase03 security gap register remains valid; secrets and taxpayer private keys cannot be exposed in Step04/Phase04 metadata UI.
- No customer invoice-count quota or unlimited annual plan is enforced in the Phase04 UI. Do not enable a public add-taxpayer flow before Phase12 server entitlements.
- No Ubuntu and no local build/test output has been provided to assistant in this step.

**Status:** Source and handoff checklist prepared; **final Step13 operator acceptance, tag, merge, release and Phase04 closure remain pending verified evidence and explicit approval.**


**Review PR:** [#6 — Phase 04 Customer-owned taxpayer management and commercial partner scope](https://github.com/HekmatBeigverdi/ArginTaxPayer/pull/6), opened **DRAFT** on 2026-10-14 against `main`. Not merged, not tagged, not released. Mergeability shown by GitHub does not certify build/test success. Owner must provide local and Ubuntu verification and explicit final phase release approval before making it ready and merging.


### Step 13 Ubuntu full-test regression: 8 failed, remediation committed (2026-10-14)

**Observed:** Operator supplied `bash scripts/ubuntu-dev.sh verify` output: 189 total tests, 181 succeeded and **8 failed**, before Node/Next.js Ubuntu verification. This supersedes any prior expectation of clean Ubuntu acceptance. Two common signatures:
1. PostgreSQL `23503` on `FK_customer_owner_customer` when legacy Identity integration tests cleaned up disposable `identity_users` without deleting their new `customer_commercial_owner_changes` / `customer_commercial_owners` dependents.
2. Legacy registration tests expected `Created` but got `Invalid` because the Phase04 mandatory root attribution rejects ambiguous/missing `Ownership:DefaultLevel1UserId` when PostgreSQL contains several active Level1 accounts.

**Remediation source:** Test-only `tests/ArginTaxPayer.Tests/IdentityTestServices.cs` chooses an existing active Level1 explicitly in integration test configuration, preserving any passed OTP configuration; offline unit tests do not connect to PostgreSQL for test fixture setup. Production ownership remains fail-closed when the default root is ambiguous. The same helper exposes `CleanupCustomerFixturesAsync` to remove only commercial transfer history and owner rows of disposable test identities, then delete the identities inside an EF execution strategy transaction. Six legacy test files were switched to the helper (MFA retry, phone OTP, admin provisioning, MFA onboarding, resource scope, role authorization). Underlying live FKs remain Restrict, not cascade. Stable oldest active Level1 is preferred for fixture attribution. Do not delete unrelated customers, production accounts or weaken schema constraints.

**Evidence:** User-provided Ubuntu result (failed 8/189). **Fixes committed, not yet re-executed**. Next operator action after pull: run `bash scripts/ubuntu-dev.sh verify` again. If an actual active Level1 bootstrap account is unavailable in `argintaxpayer_test`, fix test fixture setup explicitly (do not weaken production registration). Old failed test attempts may have left disposable `@example.invalid` accounts; do not mass-delete them before reviewing dependencies.

**Release decision:** Phase04 PR #6 remains DRAFT and should NOT be merged, tagged or released while Ubuntu full test suite has 8 reported failures or before successful repeat evidence.



### Step 13 Owner Acceptance and Phase 04 Handoff — 2026-10-14

**Owner-confirmed:** After fixing the previously observed Ubuntu regression (8/189 failures), the owner reports that updated Ubuntu VPS and macOS local verifications both passed. This is **owner-reported evidence**, not separately retrieved CI logs or an independent remote command execution. The owner explicitly approved closing Step13. The Phase04 original fixed plan of 13 steps is now owner accepted.

**Signoff:** Step01–Step13 owner accepted for development phase completion. Step12 three-account HTTP smoke was intentionally waived at the owner's request and remains a documented residual validation item; authorization/security promises beyond tested paths must not be interpreted as independently certified. Phase17 formal isolation and penetration-testing gates remain.

**Handoff:** Customer-first identity ownership and delegation stays in Phase04. Plan-specific taxpayer-count entitlements, collection-based commercial partner net-profit and verified tax/expense deduction remain in Phase12. Formal partner referral request queue in Phase16. Private-key custody and authority diagnostics in Phases09/10.

**Release operations:** Pull Request #6 is the integration vehicle to `main`. After its successful GitHub merge and verifying the merged commit, create an annotated `v0.4.0` tag **only if not already present**, and publish an accurately labeled Phase04 development release. Never pretend release or tag exists before checking GitHub. Keep development endpoints private and do not deploy to production as part of phase completion.


### Final integration record — 2026-10-14

- Step 13: **OWNER APPROVED**. Owner reported latest macOS and Ubuntu verification success after test-fixture compatibility fixes.
- Pull Request [#6](https://github.com/HekmatBeigverdi/ArginTaxPayer/pull/6): **MERGED** into `main` using merge commit `422437c49fd153d5f749d1533d0bee3f5d40383e`.
- GitHub Release survey before publication: existing latest Phase03 release `v0.3.0-phase03`; Phase04 tag/release **not yet created**. Suggested consistent tag: `v0.4.0-phase04` (check remote refs first).
- Formal Phase04 steps 01–13 accepted. Separate release/tag publication remains an operator action and should not be marked completed without GitHub confirmation. The Level1/Level2 three-account HTTP smoke remains a knowingly waived security acceptance item for future independent testing.
- Handoff to Phase05 may start from updated `main`. A phase-specific Release is development-stage documentation, not approval to expose dev-only endpoints in production.
