# ArginTaxPayer — Phase 03: Identity and Authorization

**Status:** Steps 01–07 owner approved (operator-reported acceptance; Step 07 residual security limitations recorded); Step 08 implemented — verification pending  
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
| 05 | Role-Based Authorization and Permissions | Level 1, Level 2, Customer roles, permission policies and safe role grants | Users cannot self-elevate privileges; role-policy tests pass | Owner approved (2026-10-12) |
| 06 | Resource-Scoped Authorization | Server-side ownership/scope policies and extension contracts for Phase 04 | Cross-scope access denied with suitable 403/404; UI filtering alone cannot grant access | Owner approved (2026-10-12) |
| 07 | Account Protection and Security Audit | Login throttling, security-event audit, sensitive-data redaction, session controls | Brute-force throttling and redaction verified; security events auditable | Owner approved with documented residual risks (2026-10-12) |
| 08 | Persian RTL Identity User Interface | Login, registration, MFA, account/security screens and guarded routes | RTL layout, correct error states, login/logout flow, protected-content tests | Implemented — verification pending |
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

### Step 05 — Role-Based Authorization and Permissions

**Delivery:** Code committed to `phase/03-identity-authorization`; operator build/test verification and owner approval pending. Do not mark this step complete based only on committed source. Phase 03 remains a fixed ten-step plan.

**Implementation:**
- `IdentityPermissions` is a central deny-by-default matrix. `Level1` can read identity, role directory, administration and customer areas, and grant `Level2`. `Level2` may access administrative/customer read areas but **cannot** grant roles. `Customer` receives customer/self access only. Unknown roles and permissions never grant privileges.
- `IdentityAuthorizationService` resolves the current user from the hashed, revocable bearer session using `IdentitySessionService`, then fetches **current roles from PostgreSQL on each authorization check**. Never trust roles, permissions, tenant identifiers or actor IDs from request input.
- `GrantLevel2Async` accepts only a target user ID. Current, active, MFA-enabled `Level1` actor required. Actor cannot grant a role to self. Target must be active, already have `Customer`, have confirmed MFA **before promotion**, and must not already be `Level1` or `Level2`. Successful grant is transactional with PostgreSQL SERIALIZABLE isolation, rechecks eligibility and revokes all target sessions; a new MFA login is required. Unique role key and conflict handling protect against duplicate grants.
- `Program.cs` injects authorization service into Development only. New **loopback Development-only** endpoints under `/api/dev/identity`: `GET /authorization/me`, `GET /authorization/admin`, `GET /authorization/customer`, `POST /roles/level2/grant` with body `{"targetUserId":"<uuid>"}`. No public role-management endpoint is enabled in Production. Existing registration, login, MFA, token and health endpoint behavior is preserved.
- Tests: `IdentityRolePolicyTests` validate the role-to-permission matrix and invalid token rejection. `IdentityRolePostgresTests` exercises live PostgreSQL role escalation denial, MFA prerequisite, valid `Level1` grant, target-session revocation, repeated grant conflict and `Level2` privilege restrictions. `scripts/phase-03-step-05-smoke.py` exercises anonymous 401, Customer 403, customer 200 and self-escalation denial over localhost HTTP.

**Migration impact: NONE.** Reuse `identity_users`, `identity_user_roles`, `identity_mfa_states` and `identity_sessions` from Steps 01–04. **Do not execute `dotnet ef database update` for Step 05.** Resource ownership and taxpayer membership remain deferred to Step 06 / Phase 04 as planned.

**Ordered operator runbook — macOS, SSH and PostgreSQL**

**1. Terminal A — SSH tunnel, remain connected throughout DB tests:**
```bash
ssh -N -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
```

**2. Terminal B — macOS repository root, inspect local modifications before switching:**
```bash
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet build tools/ArginTaxPayer.IdentityOperator/ArginTaxPayer.IdentityOperator.csproj -c Release
```

**3. Terminal B — explicitly configure the test connection and verify exact database:**
```bash
read -rs "PGPASS?Test PostgreSQL password: "; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
```
**Stop unless** the output is exactly `argintaxpayer_test`. This is an Npgsql connection string, not a `psql` libpq DSN. Only read/check existing schema; there is no new migration:
```bash
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c '\d identity_user_roles'
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c '\d identity_mfa_states'
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
```
Expected: Step 01–04 migrations including `20261012040000_AddIdentityMfa` already applied and identity role composite key preserved.

**4. Terminal B — real PostgreSQL authorization tests (test-only dedicated database):**
```bash
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~IdentityRolePostgresTests
```
Expected: only MFA-confirmed Level 1 can grant Level 2; duplicate grants rejected; Customer and Level 2 denied; prior sessions revoked. The integration test creates disposable test identities and attempts cleanup in `finally`; do not use production credentials or database.

**5. Terminal B — start loopback Development API.** Keep the configured test DB string and **previously saved** `Identity__MfaEncryptionKey` present if testing an existing enrolled account. For the disposable Customer-only RBAC smoke, the API requires no MFA key; if a key is configured, do not replace it with a fresh key for existing enrolled accounts.
```bash
ASPNETCORE_ENVIRONMENT=Development Identity__AllowDevelopmentRegistration=true \
  dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15088
```

**6. Terminal C — HTTP smoke from macOS repository root:**
```bash
python3 scripts/phase-03-step-05-smoke.py
```
Expected `STEP 05 RBAC SMOKE PASSED`. Script does not output raw credentials or tokens; creates one disposable Customer row in `argintaxpayer_test`. Stop the API with Ctrl+C after it passes.

**7. Ubuntu VPS — same branch, no database migration:**
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

**Acceptance requirements:** macOS build and full regression suite green, test PostgreSQL identity confirmed, existing role/MFA schema verified (no migration applied), PostgreSQL permission/elevation tests pass, HTTP smoke passes, Ubuntu build/test and operator project build pass, and existing Phase 03 Step 04 MFA tests remain green. Raw terminal or CI evidence has not been provided to the assistant; owner sign-off pending.

**Security limitations:** This phase introduces role-level permission infrastructure, **not** taxpayer/resource ownership authorization. Those checks are Step 06 and Phase 04 deliverables. These API routes remain loopback Development-only; do not treat them as a production authentication/authorization deployment. No new admin account or additional Level 1 provisioning is needed for the automated database test.

**Owner acceptance record (2026-10-12):** The project owner expressly confirmed successful completion of Step 05 macOS Build and Unit Tests, SSH tunnel connectivity, PostgreSQL `IdentityRolePostgresTests`, HTTP RBAC smoke tests, and Ubuntu build/tests. Step 05 is **Owner Approved**. All results are **operator-reported PASS**; the assistant did not independently execute the tests or verify raw terminal/CI logs. There is **no Step 05 database migration**. Approval is limited to Step 05, and does not authorize a merge to `main`, release/tag or production deployment. Step 06 — Resource-Scoped Authorization — remains Planned.

### Step 06 — Resource-Scoped Authorization

**Status:** Owner Approved (2026-10-12), based on explicit operator-reported successful completion of the acceptance tests. Source commits alone were not treated as test evidence.

**Deliverables and security boundary:**
- `IdentityResourceKind` and `IIdentityResourceOwnershipResolver` define a typed extension contract. `IdentityResourceOwnershipResolver` resolves **active IdentityUser** and **IdentitySession** ownership directly in PostgreSQL. `IdentityResourceAuthorizationService` combines current bearer session, current RBAC permission and storage-derived ownership. No user-controlled owner ID, tenant ID or client-supplied role is trusted.
- Owners can read their own identity resource and identify/revoke their own database-backed sessions. A missing ID or a foreign user/session ID produces the same **404**; absent/invalid session returns **401**; valid session lacking role permission returns **403**. Scope enforcement is server-side and independent of the UI.
- The revocation query filters by both session ID and authenticated user ID at the point of mutation. Session revocation invalidates both access and refresh credentials, without affecting other users. Authorization is fail-closed for unknown resource kinds.
- Development-only, loopback-only HTTP probes: `GET /api/dev/identity/resources/users/{userId}`, `GET /api/dev/identity/resources/sessions`, `GET /api/dev/identity/resources/sessions/{sessionId}`, `POST /api/dev/identity/resources/sessions/{sessionId}/revoke`. These return only resource identifiers/statuses; never raw session tokens or password/MFA secrets.
- The contract **does not invent taxpayer-company memberships** or assume that Level 1/Level 2 globally own taxpayer data. Mapping a user to a taxpayer/company and the delegated role for that taxpayer remain Phase 04 responsibilities. Future taxpayer resource resolvers must use authoritative stored memberships and deny unlinked user-resource combinations. Existing Phase 03 role checks and endpoints remain intact.
- Focused tests: `IdentityResourceScopeUnitTests` cover anonymous/unknown-resource denial without DB I/O. `IdentityResourceScopePostgresTests` creates two disposable Customer identities in the dedicated test DB, checks own and foreign identity/session boundaries, confirms own-session revocation, rejects cross-user revoke, and verifies no effect on the other account. `scripts/phase-03-step-06-smoke.py` repeats the core negative paths via HTTP.

**Migration impact: NONE.** Reuses `identity_users` and `identity_sessions` from Steps 01–03. Do **not** run EF Core schema update for Step 06. A new persisted taxpayer membership model must be designed and explicitly migrated in Phase 04; this delivery is deliberately limited to identity-owned resources and a future extension contract.

**macOS test runbook (repository root):**

Terminal 1 — open and keep the PostgreSQL SSH tunnel alive:
```bash
ssh -N -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
```

Terminal 2 — pull source and run regression tests:
```bash
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
```

Terminal 2 — export a dedicated **test** PostgreSQL connection string only if not already configured:
```bash
read -rs "PGPASS?PostgreSQL test password: "; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
```
Proceed **only** if output is exactly `argintaxpayer_test`. Verify existing resource tables without schema changes:
```bash
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c '\d identity_users'
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -c '\d identity_sessions'
```

Terminal 2 — run focused PostgreSQL test and prior Step 04/05 integration regressions:
```bash
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --no-build --filter 'FullyQualifiedName~IdentityResourceScopePostgresTests'
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --no-build --filter 'FullyQualifiedName~IdentityMfaRetryStrategyPostgresTests|FullyQualifiedName~IdentityRolePostgresTests'
```

Terminal 2 — run the API on loopback Development mode (keep running):
```bash
ASPNETCORE_ENVIRONMENT=Development Identity__AllowDevelopmentRegistration=true \
dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15088
```

Terminal 3 — HTTP smoke test:
```bash
python3 scripts/phase-03-step-06-smoke.py
```
Expected final marker: `STEP 06 RESOURCE SCOPE SMOKE PASSED`. The HTTP smoke creates disposable `Customer` records in `argintaxpayer_test`; it does not delete them automatically. Stop the API with Ctrl+C after the test.

**Ubuntu VPS build and test:**
```bash
ssh argindev@85.133.244.156
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
```
The Ubuntu default tests do not require a configured PostgreSQL URL. Running the opt-in PostgreSQL tests on Ubuntu is allowed only with an **explicit** connection string pointing to `argintaxpayer_test`; do not point it to any production service and do not apply migrations.

**Acceptance (owner-reported PASS, 2026-10-12):** macOS and Ubuntu builds/tests, dedicated PostgreSQL connectivity, `IdentityResourceScopePostgresTests`, the Step 04/05 regression tests, and the Step 06 HTTP smoke (including 401/404 cross-scope isolation and own-session revocation) all succeeded. **CI status not independently verified. Owner Approved.** No Step 06 migration was necessary.

### Step 06 — HTTP 404 Diagnostic Patch (2026-10-12)

The owner reported `404` from both `GET /api/dev/identity/authorization/me` (expected unauthenticated `401`) and the Step 06 customer registration smoke, even after confirming the standard Development and test connection settings. This means the route was either not registered or the loopback guard returned NotFound; the HTTP status alone does not distinguish those causes.

**Patch:** Hardened the loopback guard to recognize IPv4-mapped IPv6 loopback addresses (`::ffff:127.0.0.1`), which some local host stacks present. Added Development-only, localhost-only `GET /health/dev-identity`, registered independently of the identity route condition, returning only booleans: `development`, `connectionConfigured`, `identityEndpointsRegistered`, `registrationEnabled` and `loopbackRecognized`. No connection string, token, secret, password, address, or environment variable contents are returned. Step 06 HTTP smoke now checks this endpoint before creating test users and provides a configuration failure diagnostic if identity routes are disabled. If the endpoint remains `404`, investigate the exact process/binary bound to port 15088, its actual runtime environment or the remote-loopback guard. This change **does not establish the user's root cause without the diagnostic response**.

**macOS recovery checklist:**
```bash
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
lsof -nP -iTCP:15088 -sTCP:LISTEN
# Stop the existing 15088 API cleanly (Ctrl+C in its owning terminal)
# In the API terminal that has the exact argintaxpayer_test DB connection set:
export DOTNET_ENVIRONMENT=Development
export ASPNETCORE_ENVIRONMENT=Development
export Identity__AllowDevelopmentRegistration=true
dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15088
# Another terminal:
curl -i http://127.0.0.1:15088/health/dev-identity
curl -i http://127.0.0.1:15088/api/dev/identity/authorization/me
python3 scripts/phase-03-step-06-smoke.py
```
Expected diagnostic all booleans `true`, unauthenticated authorization `401` and `STEP 06 RESOURCE SCOPE SMOKE PASSED`. If diagnostics show `connectionConfigured=false`, restore the test connection string **in the API process's terminal**. If diagnostics show `registrationEnabled=false`, set `Identity__AllowDevelopmentRegistration=true` **before starting the API**. An unrecognized diagnostic URL (404) after pulling may mean a stale listener; inspect the owning process and effective environment. No migration required. **Historical diagnostic note:** This was the status before the owner subsequently reported all Step 06 tests passing and approved the step (2026-10-12).

### Step 06 — Isolated-port recovery for stale Kestrel listener (2026-10-12)

The owner confirmed `curl --noproxy '*'` receives a local Kestrel `200` on `127.0.0.1:15088` while the safe diagnostic reports `connectionConfigured=false` and `identityEndpointsRegistered=false`; `lsof -nP -iTCP:15088 -sTCP:LISTEN` did not show a listener. This proves the HTTP request is direct but does **not** identify the listener's process or why local socket inspection was empty. Environment exports in a separate shell cannot alter a previously started process. No evidence of a code-level RBAC or resource-scope failure.

To avoid colliding with the unidentified listener, Step 06 smoke now supports a configurable *loopback HTTP origin*, `ARGINTAXPAYER_API_BASE` (default remains `http://127.0.0.1:15088`); non-local destinations are refused. Execute from the repository root:

```bash
git pull --ff-only origin phase/03-identity-authorization
# In the SAME terminal with an exported ConnectionStrings__ArginTaxPayerDb targeting argintaxpayer_test:
export ASPNETCORE_ENVIRONMENT=Development DOTNET_ENVIRONMENT=Development
export Identity__AllowDevelopmentRegistration=true
dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15089
# Keep this API terminal open. In a second terminal:
curl --noproxy '*' -i http://127.0.0.1:15089/health/dev-identity
curl --noproxy '*' -i http://127.0.0.1:15089/api/dev/identity/authorization/me
ARGINTAXPAYER_API_BASE=http://127.0.0.1:15089 python3 scripts/phase-03-step-06-smoke.py
```

Expected: diagnostic five booleans `true`, anonymous `authorization/me` returns `401`, smoke ends `STEP 06 RESOURCE SCOPE SMOKE PASSED`. SSH tunnel `127.0.0.1:15432 -> Ubuntu:5432` must remain active in its own shell. Never print the database password or MFA secret. This diagnostic patch does not require a migration or alter production endpoint exposure. **Historical diagnostic note:** Step 06 was awaiting verification when this workaround was written. The owner subsequently confirmed all Step 06 tests passed (2026-10-12).

### Step 06 — Owner Acceptance and Handoff (2026-10-12)

**Final decision: OWNER APPROVED.** The owner expressly confirmed that the following were successful after the local HTTP 404 troubleshooting: macOS build/unit suite, SSH tunnel and dedicated `argintaxpayer_test` PostgreSQL checks, Resource Scope PostgreSQL integration tests, HTTP scope smoke, Step 04/05 regression tests, and Ubuntu build/tests. These results are **operator-reported PASS**, not independently executed or verified against raw terminal/CI logs by the assistant.

**Scope closure:** Resource ownership is enforced server-side for existing identity and session resources; cross-owner access is hidden and foreign session revocation is denied. A typed resource-ownership contract is ready for Phase 04 taxpayer membership implementation. **No Step 06 database migration.** Preserve the ten-step plan: Steps 01–06 are owner-approved; Step 07 — Account Protection and Security Audit — is next, not yet started or approved. No merge to `main`, release, tag, or production deployment is authorized by this acceptance.

### Step 07 — Account Protection and Security Audit (Implementation in progress, 2026-10-12)

**Scope implemented so far:**
- `IdentityProtectionService` provides an account-keyed, 5-failure/15-minute login throttle for the **Development-only identity API**, using a per-process random HMAC key and canonicalized emails as map keys. The failure map never stores raw usernames, passwords or tokens. Successful authentication clears the account's failure bucket.
- The same throttle applies to `/login`, `/verify-password`, and `/mfa/challenge` in the loopback-only Development API. Locked callers receive `429 Too Many Requests`. Incorrect credentials continue to receive generic `401`.
- Structured security signals record only operation names and coarse outcomes (`rejected`, `throttled`, `accepted`), with no user identifier, password, raw bearer token, MFA recovery code or secret in log arguments. Existing Step 03 revocable sessions, rotating refresh tokens and Step 06 own-session revocation remain in place.
- `IdentityProtectionTests` cover canonicalized account buckets, the failure threshold, lock window and successful reset. `scripts/phase-03-step-07-smoke.py` runs a disposable test-only account through five failed logins and checks `429` on subsequent valid credentials and password verification.

**Important limitations before production acceptance:** This is a **single-process** development-mode throttle. It resets upon application restart, is not cross-instance, and is not suitable as the final distributed lockout enforcement. Application JSON logs are redacted structured event signals, **not** an immutable durable security-audit event store. Authentication history retention, security event correlation and durable auditing require an explicit persistent implementation and additional acceptance tests. MFA completion retains its earlier five-attempt per-challenge counter. Therefore this implementation **cannot yet be certified as meeting every Step 07 hardening criterion**, and owner approval remains pending. No new database migration is included in this increment. Never deploy this Development API as the production identity service.

**macOS prerequisites and Build:**
```bash
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter 'FullyQualifiedName~IdentityProtectionTests'
dotnet test ArginTaxPayer.sln -c Release --no-build
```

**macOS to PostgreSQL SSH tunnel (Terminal A):**
```bash
ssh -N -o ExitOnForwardFailure=yes -L 127.0.0.1:15432:127.0.0.1:5432 argindev@85.133.244.156
```

**PostgreSQL test identity verification (Terminal B):**
```bash
read -rs "PGPASS?Test PostgreSQL password: "; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --no-build --filter 'FullyQualifiedName~IdentityMfaRetryStrategyPostgresTests|FullyQualifiedName~IdentityRolePostgresTests|FullyQualifiedName~IdentityResourceScopePostgresTests'
```
**Stop unless database identity is exactly `argintaxpayer_test`.** Step 07 currently contains **no migration**.

**Run Development API (Terminal B; test connection must be present in that same shell):**
```bash
export DOTNET_ENVIRONMENT=Development ASPNETCORE_ENVIRONMENT=Development Identity__AllowDevelopmentRegistration=true
dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15089
```

**HTTP smoke (Terminal C):**
```bash
curl --noproxy '*' -s http://127.0.0.1:15089/health/dev-identity
ARGINTAXPAYER_API_BASE=http://127.0.0.1:15089 python3 scripts/phase-03-step-07-smoke.py
```
Expected `STEP 07 ACCOUNT PROTECTION SMOKE PASSED`. Smoke generates one disposable Customer row in the `argintaxpayer_test` database. Do not run it against other environments.

**Ubuntu VPS Build/tests:**
```bash
ssh argindev@85.133.244.156
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
```

**Verification record:** Source committed, CI not independently checked, macOS tests not run by assistant, PostgreSQL tests not run by assistant, HTTP smoke not run by assistant, Ubuntu tests not run by assistant. Owner acceptance Pending. **Remaining work:** durable cross-process throttle and queryable/redacted persistent security audit must be completed before final Step 07 acceptance.

### Confirmed macOS Development API Launch Convention (Owner feedback, 2026-10-12)

The project owner confirmed that the following **zsh** startup sequence resolved the Step 07 HTTP smoke preflight error. Use this exact convention in subsequent Phase 03 steps when running the Development API against the SSH-tunneled PostgreSQL test database. Run it in the **same terminal that launches the API**; do not assume exported variables in a different shell will be inherited. Keep the separate SSH tunnel `127.0.0.1:15432` open.

```bash
read -rs 'db_password?Test database password: '; print

export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${db_password};SSL Mode=Prefer"
unset db_password

export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_ENVIRONMENT=Development
export Identity__AllowDevelopmentRegistration=true

dotnet run --no-launch-profile \
  --project src/ArginTaxPayer.Api \
  --urls http://127.0.0.1:15089
```

The `read -rs 'var?prompt: '` syntax and `print` command are **zsh-specific** and suitable for the owner's macOS terminal; do not copy them into bash on Ubuntu unchanged. Start the API only after all exports are set, and keep the process running. In another terminal, confirm `GET /health/dev-identity` reports `connectionConfigured=true`, `identityEndpointsRegistered=true`, and `registrationEnabled=true`. Do not print or commit the database password. This is a Development/test-only process, never a production deployment instruction.

### Step 07 — Owner Acceptance and Explicit Security Carry-Forward (2026-10-12)

**Owner decision:** The owner explicitly accepted Step 07 and requested implementation of Step 08. Record as **Owner Approved**, based on owner instruction. The reported resolution of the HTTP smoke preflight was achieved using the owner-confirmed zsh launch convention.

**Residual risks NOT waived by this status:** The current throttle is process-local, volatile, and Development-only. The current structured redacted security logs are not a queryable, tamper-resistant, persistent security audit trail. These requirements remain **open security hardening blockers before any Production release** and must be resolved and re-tested no later than Step 10. Owner approval of Step 07 does not mean these controls are production-ready or independently tested by the assistant. No Production deployment authorized.

### Step 08 — Persian RTL Identity User Interface (2026-10-12)

**Status:** Implemented on `phase/03-identity-authorization`, awaiting operator macOS/Ubuntu tests, browser inspection and owner sign-off.

**Deliverables:**
- `apps/web/app/identity/page.tsx` and `identity-forms.tsx`: Persian RTL responsive login/registration UI with labeled email/password fields, accessible error messages, 8-character registration minimum, MFA challenge completion using an authenticator TOTP or single-use recovery code, explicit loading and validation states.
- `apps/web/app/account/page.tsx` and `account-controls.tsx`: a server-guarded account/security screen displaying authenticated actor ID and current PostgreSQL roles, own-session listing/revocation, logout, MFA enrollment/confirmation and once-only recovery-code display. The browser must not log or persist secrets; keep provisioning keys and recovery codes away from screenshots and support logs.
- `apps/web/lib/identity.ts`: server-only test-API origin validation and current user lookup; `/account` redirects unauthenticated visitors to `/identity` and identity page redirects already-authenticated users to `/account`. Layout uses Persian `lang="fa" dir="rtl"`.
- `apps/web/app/api/identity/[...path]/route.ts`: a **Development-only** Next.js backend-for-frontend; it forwards a tightly allowlisted set of identity routes to a trusted localhost .NET Development endpoint. Raw access/refresh credentials are held in `HttpOnly`, same-site cookies instead of browser localStorage. Mutation endpoints check same-origin `Origin`; do not expose generic arbitrary forwarding or enable the UI in Production. Successful authentication and refresh replace both cookies; logout revokes the API session and clears cookies.
- `apps/web/app/globals.css`, `app/page.tsx`: Persian desktop/mobile responsive layout and link from the foundation landing to identity UI.
- `apps/web/tests/identity-ui.test.mjs`: contract tests for RTL, guarded pages, MFA recovery workflow, protected account actions and proxy security constraints.

**Security notes and limitations:** The UI is intentionally **not Production-ready**: it uses loopback Development .NET identity endpoints, and is disabled unless `NODE_ENV=development` AND `ARGIN_IDENTITY_UI_ENABLED=true`. Current access-token life is 10 minutes; after expiry, a protected server-side navigation requires fresh login (the explicit refresh operation exists but transparent UI refresh is not yet implemented). Same-origin mutation checks, HttpOnly cookies and server guards reduce risk but are not a substitute for a complete production identity architecture and CSRF/security review in Steps 09–10. No new database migration. Step 07 open persistent-audit / distributed-throttle blockers remain documented above.

**Operator verification (macOS, all commands from repository root):**

Terminal A — keep the SSH tunnel open:
```bash
ssh -N -o ExitOnForwardFailure=yes -L 127.0.0.1:15432:127.0.0.1:5432 argindev@85.133.244.156
```

Terminal B — use the **owner-confirmed zsh** command in the SAME shell as the API:
```zsh
read -rs 'db_password?Test database password: '; print
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${db_password};SSL Mode=Prefer"
unset db_password
export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_ENVIRONMENT=Development
export Identity__AllowDevelopmentRegistration=true
# Set the SAVED Identity__MfaEncryptionKey before startup only when testing MFA.
dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15089
```

Terminal C — API preflight (direct, no proxy):
```bash
curl --noproxy '*' -s http://127.0.0.1:15089/health/dev-identity
curl --noproxy '*' -i http://127.0.0.1:15089/api/dev/identity/authorization/me
```
Require `connectionConfigured=true`, `identityEndpointsRegistered=true`, `registrationEnabled=true` and anonymous `401`. Stop on any mismatch.

Terminal D — Next.js frontend; no client-visible API secrets:
```bash
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
cd apps/web
npm ci --no-audit --no-fund
npm test
npm run typecheck
npm run build
ARGIN_IDENTITY_UI_ENABLED=true ARGIN_IDENTITY_API_INTERNAL_BASE_URL=http://127.0.0.1:15089 npm run dev -- --port 3000
```

**Additional HTTP UI smoke from the repository root (Next.js on port 3000):**
```bash
python3 scripts/phase-03-step-08-ui-smoke.py
```
Expected marker: `STEP 08 PERSIAN IDENTITY UI SMOKE PASSED`. It asserts server-guarded `/account`, rendered Persian RTL markup, and a CSRF-origin rejection from the BFF; it does not submit secrets, persist accounts or replace interactive browser MFA testing.

**Browser acceptance at `http://127.0.0.1:3000`:** `/identity` opens in Persian RTL, registration of a disposable Customer succeeds, login reaches `/account`, an unauthenticated direct request to `/account` redirects to `/identity`, account page shows only own identity/sessions, log out clears identity and returns to the login page; incorrect password, duplicate registration and wrong MFA code display safe localized messages; MFA TOTP/recovery login succeeds when the stored application key is provided to .NET API; narrow mobile layout (<=780px) is usable. Inspect browser storage: no plaintext access/refresh tokens in localStorage/sessionStorage and cookies have HttpOnly and SameSite Strict. Avoid copying MFA provisioning or recovery secrets to console/logs. Browser results are **not yet verified**.

**PostgreSQL and HTTP regression:** Test DB must be explicitly `argintaxpayer_test`:
```bash
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --no-build --filter 'FullyQualifiedName~IdentityMfaRetryStrategyPostgresTests|FullyQualifiedName~IdentityRolePostgresTests|FullyQualifiedName~IdentityResourceScopePostgresTests'
ARGINTAXPAYER_API_BASE=http://127.0.0.1:15089 python3 scripts/phase-03-step-06-smoke.py
ARGINTAXPAYER_API_BASE=http://127.0.0.1:15089 python3 scripts/phase-03-step-07-smoke.py
```
Run the Python commands from repo root in a separate terminal while the .NET API stays running. These create disposable customer records in the test DB and are not to be run on Production.

**Ubuntu VPS test runbook:**
```bash
ssh argindev@85.133.244.156
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
cd apps/web
npm ci --no-audit --no-fund
npm test
npm run typecheck
npm run build
```
No API port needs to be exposed publicly; no migration is performed. This validates builds and static routes, not an Ubuntu-hosted production UI.

**Evidence:** GitHub code/docs committed; macOS build, Browser flows, HTTP regression, PostgreSQL integration, Ubuntu build and CI results are not independently executed by the assistant. Owner approval of Step 08 **pending**. Next is Step 09 only after acceptance.

### Scope Amendment Requested — Phone-First Identity, Ghasedak OTP and SMS Credit (2026-10-12)

**Change request:** https://github.com/HekmatBeigverdi/ArginTaxPayer/issues/4. The owner requested a material revision to the Phase 03 identity experience while Step 08 (Persian RTL Identity UI) is still pending verification. Preserve the original **ten-step numbering** and historical approvals. The requested functionality is an **additive scope amendment under design/implementation**, not a retroactive completion claim. Existing email/password, administrator MFA and previous integration tests must remain operational until migration and full acceptance.

**Functional requirement:** Phone number (normalized `+989xxxxxxxxx`) becomes the primary registration identity. Registration requires proving ownership of a reachable channel through SMS OTP or another enabled verified channel. Login accepts either verified email or verified mobile with password, or mobile OTP for a passwordless account. Password recovery supports SMS OTP or email-based proof; no SMS is compulsory when another secure channel is available. No one may register/sign in with an unverified identifier, and the email-only Level 1 administrator must not be locked out.

**SMS provider:** Ghasedak template `TaxPayerActivationCode` with `Param1` OTP parameter; provider-owned content `کد فعالسازی %param1% «نگین حساب» لغو11`. Provider reference: https://github.com/ghasedakapi/GhasedakSms.Framework. Its published SDK targets `net40;net47;net48`, so it must **not** be directly added to this .NET 9 host without explicit compatibility evidence; implement a secret-injected `HttpClient` adapter to provider's documented OTP REST contract and verify the exact payload/result, idempotency and failure semantics against live provider reference. Never commit API keys, OTPs or recovery secrets.

**SMS accounting requirement:** Issue **200,000 toman = 2,000,000 IRR once per end-user** as restricted promotional SMS credit on verified account activation. Use integer IRR exclusively in persistence. Maintain per-provider-panel sending expense ledger and tariff versions; per-user wallet balance and append-only top-up/charge/reversal ledger; atomic reservation and idempotent settlement, charge snapshot and concurrent debits. Provider credit balance is not the same as a user's promotional balance, and promotional funds cannot be cashed out. Pre-registration verification SMS is system-funded and rate-limited, without creating a wallet until activation.

**Security/compatibility:** Purpose-bound hashed OTP, short validity, one-time CAS consume, strict attempt/cooldown budgets and generic failure responses; password resets revoke sessions and preserve administrator MFA. Add a safe, manual test-database migration/backfill before altering the legacy `NormalizedEmail` and `PasswordHash` required fields. No direct changes to production DB. Do not assume sender rates or provider API credentials.

**Delivery sequence and acceptance:** Schema and compatibility migration → OTP lifecycle and alternate email/password flows → Ghasedak transport/mock tests → transactional wallet/provider cost accounting → Persian user/admin UI → full macOS, dedicated PostgreSQL, HTTP Smoke, Ubuntu and live-provider operator verification. The current Step 08 status remains `Implemented — Verification Pending` until the amended workflows are built and verified; no new step number is created. The technical design, acceptance scenarios and external dependencies are tracked in issue #4. Release/production deployment is blocked until all requirements and Step 07 remaining distributed protection/audit items are resolved.

## 5. Communication Convention

The **repository phase plan and all step completion records are written in English only**. When implementing or reporting on a step in the chat, present its title in **both Persian and English** (for example: `گام ۱ — زیرساخت مدل هویت و ایجاد مدیر اولیه | Step 01 — Identity Data Model and Bootstrap`). Explain execution and acceptance in concise, right-to-left Persian, while keeping code and shell commands left-to-right. Do not change the planned English step title without an approved plan amendment.

## 6. Phase Closure

After Step 10 is independently verified and owner-approved, record final test evidence, CI, migration and security review in this file. Merge, immutable tag, GitHub Release, and deployment remain separate, explicitly authorized gates. Phase 04 begins under its own single-file plan.
