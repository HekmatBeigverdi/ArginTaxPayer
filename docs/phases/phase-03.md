# ArginTaxPayer — Phase 03: Identity and Authorization

**Status:** Step 01 implemented; database verification pending  
**Branch:** `phase/03-identity-authorization`  
**Baseline:** Phase 02 and `docs/roadmap/18-phases.md`  
**Started:** 2026-10-12

## 1. Objective and Scope Control

Implement user identity, secure login, multi-factor authentication, session management, Level 1 / Level 2 / Customer roles, and server-enforced authorization. Taxpayer records, fiscal memories, and taxpayer-to-delegate assignment belong to Phase 04; Phase 03 provides only the authorization groundwork for them.

This phase contains **exactly 10 fixed implementation steps**. Do not silently renumber, split, or add steps. Any material change requires a dated plan amendment and explicit owner approval. **This is the only active Phase 03 document**: update `docs/phases/phase-03.md` after every step, rather than creating separate step reports.

Security invariants:
- Public registration must never grant administrative privileges.
- Level 1 administrators must be provisioned through a controlled bootstrap; only authorized Level 1 administrators may grant Level 2 roles.
- Securely hash passwords; never expose credentials, MFA recovery codes, or tokens in logs.
- Use revocable sessions and single-use, rotating refresh tokens.
- Enforce roles, permissions, and resource boundaries on the server. Never trust tenant IDs supplied by unauthenticated callers.
- Never auto-apply EF Core migrations on API or Worker startup. Apply migrations only against the explicitly verified `argintaxpayer_test` database during this phase.

## 2. Fixed Step Plan and Acceptance Criteria

| Step | English Title | Planned Deliverable | Step-Specific Acceptance | Status |
| --- | --- | --- | --- | --- |
| 01 | Identity Data Model and Bootstrap | Identity entities/contracts, EF Core migration, controlled initial Level 1 admin bootstrap | Identity uniqueness, migration validation against test DB, no public admin creation | Implemented — pending operator verification |
| 02 | Registration, Login, and Password Security | Controlled registration, login, secure password hashing, account validation | Reject invalid credentials, disabled accounts, duplicate registrations | Planned |
| 03 | Token and Session Lifecycle | Short-lived access tokens, rotating refresh tokens, session revocation and logout | Reject reused/expired refresh tokens; revoked sessions cannot renew | Planned |
| 04 | Multi-Factor Authentication and Recovery | MFA enrollment/challenge, single-use recovery codes, secure account recovery | Admin MFA policy enforced; replay and secret disclosure prevented | Planned |
| 05 | Role-Based Authorization and Permissions | Level 1, Level 2, Customer roles, permission policies and safe role grants | Users cannot self-elevate privileges; role-policy tests pass | Planned |
| 06 | Resource-Scoped Authorization | Server-side ownership/scope policies and extension contracts for Phase 04 | Cross-scope access denied with suitable 403/404; UI filtering alone cannot grant access | Planned |
| 07 | Account Protection and Security Audit | Login throttling, security-event audit, sensitive-data redaction, session controls | Brute-force throttling and redaction verified; security events auditable | Planned |
| 08 | Persian RTL Identity User Interface | Login, registration, MFA, account/security screens and guarded routes | RTL layout, correct error states, login/logout flow, protected-content tests | Planned |
| 09 | Identity and Authorization Integration Tests | API/UI negative-path integration tests, token/recovery replay and role/scope isolation | Local and CI identity/security suites pass; unauthorized access rejected | Planned |
| 10 | Hardening, Verification, and Phase Handoff | Final security and migration review, CI checks, documentation and release readiness | macOS/Ubuntu build/test evidence, clean security review, owner sign-off gate | Planned |

## 3. Concise Per-Step Verification

One-time prerequisites: Git, the .NET SDK specified by `global.json`, Node/npm compatible with Phase 02, and a designated PostgreSQL **test** database only for database-dependent steps. Run commands from the repository root. Version-check commands are deliberately omitted.

### macOS

```bash
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
(cd apps/web && npm ci --no-audit --no-fund && npm test && npm run typecheck && npm run build)
```

### Ubuntu VPS

Inspect/preserve local modifications before switching branches or pulling. No public test ports or production credentials are needed.

```bash
cd ~/projects/ArginTaxPayer
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
(cd apps/web && npm ci --no-audit --no-fund && npm test && npm run typecheck && npm run build)
```

For every step, add focused automated tests covering that step's acceptance criteria and report the actual macOS and Ubuntu results. When a migration is delivered, append the exact safe migration/test commands to its step record after verifying the test DB identity. Coding or committing a test is not evidence it passed.

## 4. Step Completion Record (Update This File Only)

Append a completion entry for each executed step below, in **English**:

- **Step number and English title:**
- **Implementation summary and important files:**
- **Commit SHA:**
- **Migration impact:** None / Name and safe test execution
- **macOS build/tests:** Pass / Fail / Not run; output or evidence
- **Ubuntu build/tests:** Pass / Fail / Not run; output or evidence
- **Step-specific security and functional tests:** results
- **CI:** Link and result / Not run
- **Owner acceptance:** Pending / Approved (with date)
- **Status:** Planned / Implemented / Verified / Owner approved / Blocked

### Step 01 — Identity Data Model and Bootstrap

**Status:** Implemented — operator macOS/Ubuntu tests and approval pending.

**Implementation:** Added global identity users with normalized unique email, password hash, active status and timestamp; role assignments with composite key; explicit one-time Level 1 administrator provisioning service (not exposed via HTTP or auto-run); PostgreSQL migration `20261012010000_AddIdentityFoundation` and updated EF snapshot; metadata and password hashing tests.

**Security note:** Bootstrap is deliberately a restricted programmatic operation, not a public endpoint or automatic startup action. A privileged provisioning CLI with a concurrency lock and MFA enrollment flow is still needed before any real administrator is provisioned. No administrator credentials are stored in the repository.

**Database:** Migration is committed, not executed on any DB. Apply *only* to confirmed `argintaxpayer_test`, using configured development/test connection, e.g. `dotnet ef database update --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext` after independently confirming the target; do not run on production.

**Validation:** Build and tests on macOS and Ubuntu not run by the assistant. Run the commands in Section 3, and specifically `dotnet test ArginTaxPayer.sln -c Release --filter FullyQualifiedName~IdentityFoundationTests`. Record results below after execution.

**Commit:** See GitHub commit containing this Step 01 update. Owner acceptance: pending.

### Step 01 — Required Migration Test (macOS and Ubuntu)

The database migration must be applied and verified **during Step 01**, before Step 02. Run these commands from the repository root on each test machine, only after independently confirming that the configured `ConnectionStrings__ArginTaxPayerDb` targets the dedicated `argintaxpayer_test` database (never production). The environment connection is required by the existing design-time DbContext factory.

```bash
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
dotnet ef database update 20261012010000_AddIdentityFoundation --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --filter FullyQualifiedName~IdentityPostgresIntegrationTests
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
```

Expected: `20261012010000_AddIdentityFoundation` is applied, `identity_users` and `identity_user_roles` exist, and PostgreSQL rejects a duplicate normalized email. The integration test rolls back its inserts. This is an opt-in real DB test; no connectivity means **not verified**, even if unit tests pass. Capture the results separately for macOS and Ubuntu. Do not create administrator accounts or run migrations during application startup.

**Verification record:** macOS migration: Not run; Ubuntu migration: Not run; integration test: Not run; owner acceptance: Pending.

## 5. Communication Convention

The **repository phase plan and all step completion records are written in English only**. When implementing or reporting on a step in the chat, present its title in **both Persian and English** (for example: `گام ۱ — زیرساخت مدل هویت و ایجاد مدیر اولیه | Step 01 — Identity Data Model and Bootstrap`). Explain execution and acceptance in concise, right-to-left Persian, while keeping code and shell commands left-to-right. Do not change the planned English step title without an approved plan amendment.

## 6. Phase Closure

After Step 10 is independently verified and owner-approved, record final test evidence, CI, migration and security review in this file. Merge, immutable tag, GitHub Release, and deployment remain separate, explicitly authorized gates. Phase 04 begins under its own single-file plan.
