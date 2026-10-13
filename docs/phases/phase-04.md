# ArginTaxPayer — Phase 04: Multi-Taxpayer Management

**Status:** Planned; Step 01 ready, not implemented or accepted
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
| 01 | Domain Contracts and Lifecycle Matrix | Taxpayer/memory/assignment state models, invariants, API contracts, migration plan | Identity, lifecycle and authorization matrix reviewed; no unapproved mutation | Ready |
| 02 | Taxpayer Persistence and Migration | Natural/legal taxpayer entities, normalized identifiers, constraints, EF migration | Safe migration and duplicate/constraint tests against test DB | Planned |
| 03 | Taxpayer Creation and Validation | Level 1 controlled taxpayer registration, type-specific validation | Invalid/duplicate inputs rejected; authorization enforced | Planned |
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
**Status:** Ready for implementation. **Acceptance:** Pending. **Implementation:** Not started.
**Required output:** documented domain contracts, approved state-transition matrix, permission matrix, and migration implications before Step 02.
**macOS:** Not run. **Ubuntu:** Deferred per approved cadence. **Commit:** Pending.

## Phase Exit Gate
All 13 steps accepted, focused regression passing, Ubuntu verification once, schema/security review, owner signoff. Merge to main, immutable tag, GitHub Release and production deployment are distinct gated actions and must never be assumed completed.
