# ArginTaxPayer — Phase 03: Identity and Authorization

**Status:** Steps 01–04 owner approved (operator-reported acceptance); Step 05 planned  
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
| 01 | Identity Data Model and Bootstrap | Identity entities/contracts, EF Core migration, controlled initial Level 1 admin bootstrap | Identity uniqueness, migration validation against test DB, no public admin creation | Owner approved (2026-10-12) |
| 02 | Registration, Login, and Password Security | Controlled registration, login, secure password hashing, account validation | Reject invalid credentials, disabled accounts, duplicate registrations | Owner approved (2026-10-12) |
| 03 | Token and Session Lifecycle | Short-lived access tokens, rotating refresh tokens, session revocation and logout | Reject reused/expired refresh tokens; revoked sessions cannot renew | Owner approved (2026-10-12) |
| 04 | Multi-Factor Authentication and Recovery | MFA enrollment/challenge, single-use recovery codes, secure account recovery | Admin MFA policy enforced; replay and secret disclosure prevented | Owner approved (2026-10-12) |
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

### Step 01 — Complete macOS-to-Ubuntu Tunnel and Migration Acceptance Runbook

**Execution order is mandatory**; do not omit the SSH tunnel or PostgreSQL verification. The Ubuntu host from the Phase 02 runbook is `argindev@85.133.244.156`; the Docker Compose PostgreSQL service binds the VPS loopback port `5432`. These commands are for an operator with an existing SSH login and the test PostgreSQL password.

**A. macOS terminal 1: keep SSH tunnel open**

```bash
ssh -N -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
```

**B. Ubuntu: verify or create the dedicated test database (once)**

```bash
ssh argindev@85.133.244.156
PG_ID=$(sudo docker ps -q --filter label=com.docker.compose.service=postgres | head -n 1)
test -n "$PG_ID" || { echo "PostgreSQL container missing"; exit 1; }
sudo docker exec "$PG_ID" psql -U argintaxpayer -d postgres -tAc \
  "SELECT datname FROM pg_database WHERE datname='argintaxpayer_test';"
# ONLY IF THE PREVIOUS QUERY RETURNED NO DATABASE:
sudo docker exec "$PG_ID" createdb -U argintaxpayer argintaxpayer_test
exit
```

**C. macOS terminal 2: pull, build, and connect via tunnel**

```bash
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
read -rs "PGPASS?PostgreSQL test password: "; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
```

Note: `read -rs "PGPASS?..."` is for macOS's default zsh shell. The connection string above is an Npgsql/EF Core string; do **not** pass it directly to `psql`, which uses libpq connection syntax. In a separate psql invocation, specify host, port, database, and username:

```bash
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
```

**Require output `argintaxpayer_test` before mutating schema.** The password is entered at the psql prompt. For installed EF Core CLI:

```bash
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
dotnet ef database update 20261012010000_AddIdentityFoundation --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
```

**D. macOS: verify tables, migration history and real DB constraints**

```bash
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c '\d identity_users'
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c '\d identity_user_roles'
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --filter FullyQualifiedName~IdentityPostgresIntegrationTests
```

Expected: applied `20261012010000_AddIdentityFoundation`; both identity tables visible; email unique index and role key / FK exist; PostgreSQL integration test passes (test transaction rolled back). Database update must be performed only once, from macOS; Ubuntu does not need to reapply it.

**E. Ubuntu code verification (no second schema mutation)**

```bash
ssh argindev@85.133.244.156
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
exit
```

Keep the tunnel open until all macOS DB tests finish. Optional: repeat the opt-in PostgreSQL integration test on Ubuntu only after explicitly setting a safe local test connection. Never expose PostgreSQL to the public network. Stop the tunnel with Ctrl+C when finished. Do not commit or paste database credentials in issue comments.

**Verification evidence:** macOS: pending; Ubuntu: pending; migration applied: pending; schema inspected: pending; PostgreSQL integration: pending; owner approval: pending.

### Step 02 — Registration, Login, and Password Security

**Implementation:** `IdentityPasswordHasher` verifies Phase 01's PBKDF2-SHA256 hashes using constant-time comparison, and generates random-salted new hashes. `IdentityCredentialService` persists new Customer identities (no client-selectable administrative role), handles duplicate emails, rejects invalid/disabled credentials, and does not issue sessions. API endpoints are available **only in Development**, restricted to **loopback connections**; Customer registration additionally requires opt-in `Identity__AllowDevelopmentRegistration=true`. No token, session or user authority is issued until subsequent steps. Production exposure is intentionally disabled.

**Migration impact: None.** Reuse the Step 01 identity migration already applied on the VPS test database. No additional migration or Ubuntu schema change is needed.

**Mac testing — connect first:**
1. Open and keep running, in macOS Terminal 1:
```bash
ssh -N -o ExitOnForwardFailure=yes -L 127.0.0.1:15432:127.0.0.1:5432 argindev@85.133.244.156
```
2. In macOS Terminal 2, from the repository root:
```bash
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
read -rs "PGPASS?Test PostgreSQL password: "; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~IdentityCredentialPostgresTests
```
Expected database: `argintaxpayer_test`, and Step 01 migration already applied. `psql` uses libpq flags, not the Npgsql connection string syntax. Integration test transactions roll back.

3. Run the development API in Terminal 2 (keep the exported test database connection):
```bash
ASPNETCORE_ENVIRONMENT=Development Identity__AllowDevelopmentRegistration=true dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15088
```
4. In macOS Terminal 3, test using a **throwaway** identity (not real credentials):
```bash
curl -i http://127.0.0.1:15088/api/dev/identity/register -H 'Content-Type: application/json' -d '{"email":"phase03-demo@example.invalid","password":"Demo-Only-Password-2026!"}'
curl -i http://127.0.0.1:15088/api/dev/identity/register -H 'Content-Type: application/json' -d '{"email":"PHASE03-DEMO@example.invalid","password":"Demo-Only-Password-2026!"}'
curl -i http://127.0.0.1:15088/api/dev/identity/verify-password -H 'Content-Type: application/json' -d '{"email":"phase03-demo@example.invalid","password":"Demo-Only-Password-2026!"}'
curl -i http://127.0.0.1:15088/api/dev/identity/verify-password -H 'Content-Type: application/json' -d '{"email":"phase03-demo@example.invalid","password":"wrong"}'
```
Expected: `201`, `409`, `200` with `sessionIssued:false`, then `401`. Since user rows persist, choose a fresh email on every rerun, or use the isolated transaction integration test for repeatable validation. Verify the row has **only Customer** role, and no plaintext password in the database. Stop the API and tunnel after testing.

**Ubuntu build and regression tests (no second migration):**
```bash
ssh argindev@85.133.244.156
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
```

**Security and acceptance:** The development-only password verification response is not an authenticated login and MUST NOT be used to grant access. Rate-limiting, MFA, token/session issuance, production registration policy, and authorization are future steps. Never expose this development endpoint outside loopback. Verify strong hashing, duplicate registration, valid/invalid credentials, inactive users, Customer-only role, normal API regressions, and both local/Ubuntu test results.

**Acceptance record (2026-10-12):** The project owner explicitly reported that all Step 02 tests completed successfully on macOS and Ubuntu, including the prescribed PostgreSQL integration, registration/duplicate/credential scenarios, and required database checks; Step 02 is **Owner approved**. This is **operator-reported PASS**, not an independent assistant execution or an attached terminal transcript. The owner confirmed tests against the existing `argintaxpayer_test` schema; no Step 02 migration was required or applied. Implementation commits: `48e3a037`, `0e9731d8`, `5606d756`; documentation before sign-off: `9500846`. No phase merge, tag, release, or production deployment is implied. Step 03 remains Planned.

### Step 03 — Token and Session Lifecycle

**Status:** Implemented in branch; macOS, PostgreSQL, Ubuntu and owner acceptance pending.

**Changes:** Added `IdentitySession`, `IdentitySessionService`, mapping and migration `20261012030000_AddIdentitySessions`. Opaque random 256-bit access/refresh tokens are returned to clients; only SHA-256 digests are stored. Access validity: 10 minutes. Refresh validity: 7 days, renewed on rotation. Atomic compare-and-swap rotation replaces both tokens. Reuse of the immediately previous refresh token revokes that session. Expired, revoked, inactive-account, and unknown tokens are rejected. Logout revokes the current session. Administrative account sessions are blocked until MFA is implemented.

**Security boundary:** `/api/dev/identity/login`, `/refresh`, `/me`, `/logout` are mapped only in Development and accept loopback clients. They are **not production authentication endpoints** and must never be exposed publicly. Customer registration still requires `Identity__AllowDevelopmentRegistration=true`. No roles or taxpayer scope are selected by the caller.

**Database:** One new migration creates `identity_sessions` with user FK, unique access and refresh digest indexes, replay-detection index, expiry, revocation and rotation fields. Neither API nor Worker automatically migrates. Apply **once from macOS to the existing Ubuntu PostgreSQL test DB**.

**Step 03 test commands (ordered):**

macOS Terminal A — keep SSH tunnel active:
```bash
ssh -N -o ExitOnForwardFailure=yes -L 127.0.0.1:15432:127.0.0.1:5432 argindev@85.133.244.156
```

macOS Terminal B — from project root, after preserving any uncommitted changes:
```bash
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
read -rs "PGPASS?Test DB password: "; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
```

Stop if the database name is not exactly `argintaxpayer_test`. Then in the same macOS terminal:
```bash
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
dotnet ef database update 20261012030000_AddIdentitySessions --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c '\d identity_sessions'
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~IdentitySessionPostgresTests
```

Expected: identity session migration applied, FK and unique token-digest indexes present; real PostgreSQL integration tests pass for login, rotation, replay revocation, logout, inactive and expired accounts/tokens. Unit/metadata tests are included in `dotnet test`.

Run local API from Terminal B (connection must remain exported):
```bash
ASPNETCORE_ENVIRONMENT=Development Identity__AllowDevelopmentRegistration=true dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15088
```

Terminal C, project root:
```bash
python3 scripts/phase-03-step-03-smoke.py
```
Expected: `STEP 03 SESSION SMOKE PASSED`. This smoke creates a disposable Customer user in the test database. Tokens are not printed. Close API and SSH tunnel after completion.

Ubuntu VPS — do **not** apply the migration again:
```bash
ssh argindev@85.133.244.156
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
```

**Acceptance:** macOS build/tests, verified test database, migration/table/indexes, opt-in PostgreSQL integration, real HTTP lifecycle smoke, Ubuntu build/tests, previous behavior preserved. Record actual output before acceptance.

**Acceptance record (2026-10-12):** The project owner explicitly reported successful execution of all Step 03 acceptance tests: macOS build and tests; SSH tunnel to the Ubuntu-hosted `argintaxpayer_test` database; successful application and verification of migration `20261012030000_AddIdentitySessions`; real PostgreSQL integration tests; HTTP session lifecycle smoke; and Ubuntu build/tests. **Status: Owner approved.** Results are operator-reported; raw terminal output and independent CI run evidence were not supplied or independently verified by the assistant. No additional database migration was run on Ubuntu. Approval closes Step 03 only; it does not authorize main merge, tag, release, or production deployment. Step 04 remains Planned.

### Step 04 — Multi-Factor Authentication and Recovery

**Implementation status:** Source committed. Owner macOS/PostgreSQL/Ubuntu validation, CI execution, and sign-off pending. This is not a production authentication release.

**Code and security model**
- `IdentityTotp`: standard RFC 6238 SHA-1, six digits, 30-second time steps and ±1 step clock tolerance; consumed time steps cannot be used again.
- `IdentityMfaCrypto`: AES-256-GCM authenticated encryption of TOTP secrets at rest. The 32-byte key is injected solely through `Identity__MfaEncryptionKey`, never committed. Retain the SAME key across restarts; loss of the key makes enrolled secrets unrecoverable.
- `IdentityMfaService`: authenticated enrollment and TOTP confirmation; ten high-entropy one-time recovery codes displayed once and stored as SHA-256 digests; five-minute challenge tokens and a five-attempt budget; atomic PostgreSQL consumption of TOTP steps, recovery codes and challenges; challenge-based creation of a new session. Prior sessions are revoked when MFA is enabled.
- `IdentitySessionService`: password-only login rejected once MFA is enabled. Administrative roles (`Level1`, `Level2`) never receive password-only sessions and must complete MFA challenge before receiving a bearer session. Refresh and authentication check the MFA state of admin sessions.
- Loopback Development-only routes: `POST /api/dev/identity/mfa/enroll`, `/mfa/confirm`, `/mfa/challenge`, `/mfa/complete`. They are not public production endpoints. Admin enrollment is performed by the dedicated `tools/ArginTaxPayer.IdentityOperator` local operator CLI; there is intentionally no public or anonymous admin enrollment route. The operator project is built separately from `ArginTaxPayer.sln`.
- Tests: `IdentityMfaUnitTests` (RFC vector, encryption tampering, schema), `IdentityMfaPostgresTests` (enrollment, password-only blocking, TOTP replay, recovery reuse, challenge replay, admin requirement, disabled account), and `scripts/phase-03-step-04-smoke.py` (end-to-end loopback HTTP).
- Password-reset-by-email, production identity verification, lost-all-factors administrator recovery, audit/event monitoring and distributed rate limiting are NOT yet offered by this step. Recovering a lost authenticator while retaining the password is supported through single-use recovery codes.

**Database migration:** `20261012040000_AddIdentityMfa` creates `identity_mfa_states`, `identity_recovery_codes`, and `identity_mfa_challenges`, with user foreign keys, hashed recovery/challenge data, encrypted secret, challenge lifetime, attempts and replay metadata. Previous tables and migrations are retained. No application-startup migration.

**Mandatory ordered execution (on macOS, from project root)**

Terminal A — keep the SSH tunnel open until all macOS DB/API testing is finished:
```bash
ssh -N -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
```

Terminal B — checkout and regression tests:
```bash
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
```

Terminal B — configure the existing PostgreSQL test password (never check secrets into git):
```bash
read -rs "PGPASS?PostgreSQL test password: "; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
```

**Hard stop** unless the query reports exactly `argintaxpayer_test`. `psql` uses libpq flags, NOT the EF Core/Npgsql connection-string syntax. Review existing migration history before applying the new schema:
```bash
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
dotnet ef database update 20261012040000_AddIdentityMfa --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c '\dt identity_*'
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c '\d identity_mfa_states'
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c '\d identity_recovery_codes'
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c '\d identity_mfa_challenges'
```

Expected: migration `20261012040000_AddIdentityMfa` applied; all three new MFA tables and constraints present. **Do not apply the same migration from Ubuntu**; the tunnel already reaches the Ubuntu-hosted PostgreSQL instance.

Terminal B — opt-in database integration tests (transaction rollback):
```bash
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~IdentityMfaPostgresTests
```

Terminal B — before launching the API, configure a dedicated TEST-ONLY MFA encryption key, generated once per disposable test environment. It must be preserved securely for subsequent runs using the same enrolled accounts (do not echo or commit the key). Example for a one-off smoke against disposable accounts only:
```bash
export Identity__MfaEncryptionKey="$(openssl rand -base64 32)"
ASPNETCORE_ENVIRONMENT=Development Identity__AllowDevelopmentRegistration=true \
  dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15088
```

Terminal C, repository root:
```bash
python3 scripts/phase-03-step-04-smoke.py
```
Expected `STEP 04 MFA SMOKE PASSED`. The script creates a disposable Customer account in the test DB; it does not display TOTP secrets, recovery codes or bearer tokens. Stop API and SSH tunnel with Ctrl+C after the test.

**Trusted Level 1 bootstrap and MFA enrollment on the TEST database only (optional operator exercise):**

Run this from macOS Terminal B with the SSH tunnel still active. `Identity__MfaEncryptionKey` must be the **same persistent key** used by the test API for these users; do not generate a new key after enrollment. The console reads administrator credentials without echo and requires explicit confirmation before creating the first Level 1 account:
```bash
dotnet build tools/ArginTaxPayer.IdentityOperator/ArginTaxPayer.IdentityOperator.csproj -c Release
dotnet run --project tools/ArginTaxPayer.IdentityOperator -- bootstrap-level1-test
```
For an existing Level 1 test account requiring first-time MFA enrollment, substitute `enroll-level1-test`. The CLI refuses databases other than `argintaxpayer_test`, checks the required MFA migration, verifies existing Level 1 credentials when applicable, and displays the OTP provisioning URI and one-time recovery codes only to the trusted operator. The terminal session must not be recorded or screen-shared. This is not a production administrator provisioning workflow.

**Ubuntu VPS (build/regression only):**
```bash
ssh argindev@85.133.244.156
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet build tools/ArginTaxPayer.IdentityOperator/ArginTaxPayer.IdentityOperator.csproj -c Release
```
Database migrations are not repeated on Ubuntu. Integration tests run against the DB from macOS through the tunnel. Verify standard Phase 02 and Steps 01–03 regression suites remain green.

**Owner acceptance gates:** macOS build/unit tests pass; tunnel and test DB verified; migration tables/FKs/indexes verified; TOTP unit tests and encrypted-secret tamper test pass; PostgreSQL integration passes (TOTP and recovery replay, challenge expiration and attempt limit, mandatory admin MFA); end-to-end smoke passes; Ubuntu build/tests pass; no tokens/secrets in output. Store operator-reported results and approval here only after completion.

**Acceptance record (2026-10-12):** The project owner explicitly reported that all Step 04 final verification succeeded after fixes: macOS build and unit/regression tests, SSH tunnel and dedicated PostgreSQL test database, MFA migration and schema verification, `IdentityMfaRetryStrategyPostgresTests`, `IdentityMfaPostgresTests`, real HTTP MFA smoke, Level 1 administrator MFA enrollment, and Ubuntu build/tests. **Step 04 status: Owner approved.** This evidence is **operator-reported PASS**, not independently executed by the assistant; terminal transcripts and CI logs were not supplied. No new migration was needed for subsequent HTTP-500, administrator diagnostic, or password-policy fixes. Approval closes Step 04 only; it does not authorize release, merge to main, tag, or production deployment.

### Step 04 — MFA Enrollment HTTP 500 Regression Fix (2026-10-12)

**Operator issue:** `scripts/phase-03-step-04-smoke.py` passed customer registration, first login and TOTP enrollment, but `POST /api/dev/identity/mfa/confirm` with an invalid code returned HTTP 500 rather than 401.

**Root cause:** In production-style API registration, Npgsql EF Core uses `EnableRetryOnFailure`. `IdentityMfaService.ConfirmEnrollmentAsync` began a user transaction directly, outside `DbContext.Database.CreateExecutionStrategy()`, which is incompatible with the configured retry strategy. The same flaw affected the valid TOTP confirmation and challenge completion paths.

**Correction:** Run each confirmation/completion transaction **inside** EF Core's execution strategy. Respect existing caller-owned transactions in the test suite. Invalid codes and replay return null (HTTP 401); successful operations keep atomic enrollment/recovery/session writes and transaction commit. Added `IdentityMfaRetryStrategyPostgresTests` using `EnableRetryOnFailure(maxRetryCount: 3)` to exercise invalid and valid enrollment, invalid and valid challenge completion, and replay under real PostgreSQL.

**Migration impact:** None. No EF migration or schema change for this fix. Do NOT execute `dotnet ef database update` again merely for this correction.

**macOS regression retest order:** Keep the existing SSH tunnel to `127.0.0.1:15432` open (or restart it). Stop the running Development API with Ctrl+C. From project root, in the terminal with `ConnectionStrings__ArginTaxPayerDb` configured for `argintaxpayer_test`:
```bash
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~IdentityMfaRetryStrategyPostgresTests
```
Then relaunch the Development API in that same connection-configured terminal; keep the existing MFA encryption key for existing accounts, or use a disposable smoke-only key for the new unique smoke account:
```bash
ASPNETCORE_ENVIRONMENT=Development Identity__AllowDevelopmentRegistration=true dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15088
```
From another macOS terminal at the project root:
```bash
python3 scripts/phase-03-step-04-smoke.py
```
Expected: `PASS invalid enrollment code` and final `STEP 04 MFA SMOKE PASSED`. If the regression still returns 500, capture the **exception type, message and stack frames**, removing credentials, tokens, TOTP secrets and personally identifying data before sharing.

**Ubuntu:** Pull this fix, run `dotnet build ArginTaxPayer.sln -c Release` and `dotnet test ArginTaxPayer.sln -c Release --no-build`. No repeated migration is required.

**Verification record:** Fix committed; macOS/operator retest Pending; PostgreSQL retry regression Pending; Ubuntu Pending; Step 04 approval Pending.

### Step 04 — Initial Level 1 Operator Diagnostic (2026-10-12)

**Operator report:** `bootstrap-level1-test` returned `Initial Level 1 bootstrap rejected`, and `enroll-level1-test` returned `Invalid administrator identity, password, or role` for an entered email. These messages do not prove a database or migration failure. The bootstrap rejects creation if another Level 1 exists, if the normalized email is already assigned, or if the password fails the minimum length policy. Enrollment only accepts an existing active Level 1 with the correct password.

**Change:** Added the read-only `inspect-level1-test` command to `tools/ArginTaxPayer.IdentityOperator`. It requires an explicitly verified `argintaxpayer_test` connection and reports Level 1 email(s) and active flag, without displaying password hashes or MFA secrets. The CLI also now displays the safe original bootstrap validation reason. No migration or data mutation is necessary for diagnosis.

**Operator instructions:** Keep the macOS-to-Ubuntu SSH tunnel open; export the test connection string in the exact terminal running the operator. Pull the branch, then:
```bash
git pull --ff-only origin phase/03-identity-authorization
dotnet run --project tools/ArginTaxPayer.IdentityOperator -- inspect-level1-test
```
Separately, use a read-only `psql` query for the target email and existing Level 1 roles (the CLI inspection does not validate the entered email or password):
```sql
SELECT u.normalized_email, u.is_active, COALESCE(string_agg(r.role, ', '), 'No role') AS roles
FROM identity_users u
LEFT JOIN identity_user_roles r ON r.user_id = u.id
WHERE u.normalized_email = upper('TARGET_EMAIL') OR r.role = 'Level1'
GROUP BY u.id, u.normalized_email, u.is_active
ORDER BY u.normalized_email;
```

**Do not** delete/change users or manually elevate Customer to Level 1 to circumvent bootstrap. Existing Level 1 identity ownership and trusted operator authority must be verified; later role grant/recovery procedures remain separate work. Store and reuse the exact test MFA encryption key after the first successful enrollment, not a newly generated key on every run.

**Evidence:** Diagnostic source committed; operator verification pending; Step 04 remains unapproved.

### Step 04 — Password Policy Adjustment and Closed SSH Tunnel (2026-10-12)

**Owner-requested policy change:** The previous 16-character password minimum was relaxed consistently to **8 characters** (maximum remains 1,024). Applies to initial Level 1 administrator bootstrap and customer registration through shared `IdentityPasswordHasher.Validate`. Existing PBKDF2-SHA256 hashing, salts, credential verification, mandatory administrator MFA, and role boundaries remain unchanged. Regression tests cover eight-character acceptance and short-password rejection. No database migration is required.

**Operator error:** `SocketException (61): Connection refused` on `127.0.0.1:15432` means that the macOS SSH tunnel to Ubuntu PostgreSQL is not currently accepting connections. This is unrelated to the password minimum. The operator CLI now reports a concise tunnel/connectivity diagnostic instead of an unhandled PostgreSQL connection stack trace.

**Verification sequence (macOS):**
```bash
# Terminal 1: keep open until after all DB-dependent checks
ssh -N -o ExitOnForwardFailure=yes -L 127.0.0.1:15432:127.0.0.1:5432 argindev@85.133.244.156

# Terminal 2: from repo root, in a shell that has the test DB connection and persistent MFA key
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
lsof -nP -iTCP:15432 -sTCP:LISTEN
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~IdentityCredentialTests
dotnet run --project tools/ArginTaxPayer.IdentityOperator -- inspect-level1-test
```
If the same-shell `ConnectionStrings__ArginTaxPayerDb` variable is absent, restore it with `Database=argintaxpayer_test` via the established runbook; do not pass the Npgsql string to `psql`. For a new Level 1 account in the dedicated test DB, run `bootstrap-level1-test`; for an existing Level 1, `enroll-level1-test`. The email must not already belong to a different account. Do not regenerate the AES-GCM MFA key after enrollment. **No migration** for this change.

**Ubuntu regression:** Pull branch, `dotnet build ArginTaxPayer.sln -c Release`, and `dotnet test ArginTaxPayer.sln -c Release --no-build`. Step 04 remains pending operator acceptance.

### Step 04 — Owner Sign-off (2026-10-12)

**Decision:** Approved by project owner after successful macOS, Ubuntu, PostgreSQL retry regression, MFA database integration, HTTP smoke and Level 1 administrator MFA tests. Step 05 — Role-Based Authorization and Permissions — is the next planned step of the unchanged ten-step Phase 03.

**Security follow-up:** An authenticator provisioning URI containing an MFA secret was previously shared in a chat transcript. Treat that particular secret as exposed. Confirm that the enrolled Level 1 authenticator uses a **new, uncompromised** TOTP secret; if the exposed secret remains active, invalidate and securely re-enroll it through an authorized, audited administrative recovery path before any non-test use. Existing CLI intentionally refuses overwriting enabled MFA. Preserve the actual encryption key securely; do not create a fresh random key over an existing enrolled identity. This security note does not represent independently verified rotation.

## 5. Communication Convention

The **repository phase plan and all step completion records are written in English only**. When implementing or reporting on a step in the chat, present its title in **both Persian and English** (for example: `گام ۱ — زیرساخت مدل هویت و ایجاد مدیر اولیه | Step 01 — Identity Data Model and Bootstrap`). Explain execution and acceptance in concise, right-to-left Persian, while keeping code and shell commands left-to-right. Do not change the planned English step title without an approved plan amendment.

## 6. Phase Closure

After Step 10 is independently verified and owner-approved, record final test evidence, CI, migration and security review in this file. Merge, immutable tag, GitHub Release, and deployment remain separate, explicitly authorized gates. Phase 04 begins under its own single-file plan.
