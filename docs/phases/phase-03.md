# ArginTaxPayer — Phase 03: Identity and Authorization

**Status:** Steps 01–08 owner approved (owner-reported; Step 07 residual security limitations documented); Step 09 implementation committed — operator verification and security completion pending; Steps 10–11 planned  
**Branch:** `phase/03-identity-authorization`  
**Baseline:** Phase 02 and `docs/roadmap/18-phases.md`  
**Started:** 2026-10-12

## 1. Objective and Scope Control

Implement user identity, secure login, multi-factor authentication, session management, Level 1 / Level 2 / Customer roles, and server-enforced authorization. Taxpayer records, fiscal memories, and taxpayer-to-delegate assignment belong to Phase 04; Phase 03 provides only the authorization groundwork for them.

**Approved 2026-10-12 plan amendment:** The owner explicitly increased Phase 03 from 10 to **11 fixed implementation steps**, inserting a new Step 09 for phone-first registration, optional SMS/email OTP login and recovery, and the Ghasedak delivery integration. Former Step 09 becomes Step 10; former Step 10 becomes Step 11. Previously completed Steps 01–07 and Step 08 retain their numbering and acceptance status. Further changes still require an explicit dated owner-approved amendment. **This is the only active Phase 03 document**: update `docs/phases/phase-03.md` after every step, rather than creating separate step reports.

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
| 08 | Persian RTL Identity User Interface | Existing email/password login, registration, MFA, guarded account/security pages and safe RTL layout | RTL layout, error states, login/logout, guarded-content tests; original email flow remains functional | Owner Approved (2026-10-12, owner-reported) |
| 09 | Mobile-First Registration, OTP Authentication and Ghasedak Integration | Backwards-compatible mobile/email identity migration, phone ownership verification, optional email/SMS OTP login and password recovery, Ghasedak adapter and RTL workflow updates | Tested OTP expiry/replay/throttling, MFA preservation, no enumeration; mock/provider-gated transport, macOS/PostgreSQL/Ubuntu/UI smoke; no live secrets in repo | Implementation in progress — owner verification pending |
| 10 | Identity and Authorization Integration Tests | API/UI negative-path integration tests, token/recovery replay and role/scope isolation including Step 09 | Local and CI identity/security suites pass; unauthorized access rejected | Planned — formerly Step 09 |
| 11 | Hardening, Verification, and Phase Handoff | Final security and migration review, CI checks, documentation and release readiness; finance-feature handoff | macOS/Ubuntu build/test evidence, security review, explicit deferred billing ledger scope, owner sign-off gate | Planned — formerly Step 10 |

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
AUTOSSH_GATETIME=0 autossh -M 0 -N \
  -o ServerAliveInterval=15 \
  -o ServerAliveCountMax=3 \
  -o ConnectTimeout=10 \
  -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
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
AUTOSSH_GATETIME=0 autossh -M 0 -N \
  -o ServerAliveInterval=15 \
  -o ServerAliveCountMax=3 \
  -o ConnectTimeout=10 \
  -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
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
AUTOSSH_GATETIME=0 autossh -M 0 -N \
  -o ServerAliveInterval=15 \
  -o ServerAliveCountMax=3 \
  -o ConnectTimeout=10 \
  -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156

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
AUTOSSH_GATETIME=0 autossh -M 0 -N \
  -o ServerAliveInterval=15 \
  -o ServerAliveCountMax=3 \
  -o ConnectTimeout=10 \
  -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
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

**Residual risks NOT waived by this status:** The current throttle is process-local, volatile, and Development-only. The current structured redacted security logs are not a queryable, tamper-resistant, persistent security audit trail. These requirements remain **open security hardening blockers before any Production release** and must be resolved and re-tested no later than Step 11. Owner approval of Step 07 does not mean these controls are production-ready or independently tested by the assistant. No Production deployment authorized.

### Step 08 — Persian RTL Identity User Interface (2026-10-12)

**Status:** Owner Approved by explicit owner instruction on 2026-10-12 (results reported by owner; detailed raw terminal and browser logs not independently verified).

**Deliverables:**
- `apps/web/app/identity/page.tsx` and `identity-forms.tsx`: Persian RTL responsive login/registration UI with labeled email/password fields, accessible error messages, 8-character registration minimum, MFA challenge completion using an authenticator TOTP or single-use recovery code, explicit loading and validation states.
- `apps/web/app/account/page.tsx` and `account-controls.tsx`: a server-guarded account/security screen displaying authenticated actor ID and current PostgreSQL roles, own-session listing/revocation, logout, MFA enrollment/confirmation and once-only recovery-code display. The browser must not log or persist secrets; keep provisioning keys and recovery codes away from screenshots and support logs.
- `apps/web/lib/identity.ts`: server-only test-API origin validation and current user lookup; `/account` redirects unauthenticated visitors to `/identity` and identity page redirects already-authenticated users to `/account`. Layout uses Persian `lang="fa" dir="rtl"`.
- `apps/web/app/api/identity/[...path]/route.ts`: a **Development-only** Next.js backend-for-frontend; it forwards a tightly allowlisted set of identity routes to a trusted localhost .NET Development endpoint. Raw access/refresh credentials are held in `HttpOnly`, same-site cookies instead of browser localStorage. Mutation endpoints check same-origin `Origin`; do not expose generic arbitrary forwarding or enable the UI in Production. Successful authentication and refresh replace both cookies; logout revokes the API session and clears cookies.
- `apps/web/app/globals.css`, `app/page.tsx`: Persian desktop/mobile responsive layout and link from the foundation landing to identity UI.
- `apps/web/tests/identity-ui.test.mjs`: contract tests for RTL, guarded pages, MFA recovery workflow, protected account actions and proxy security constraints.

**Security notes and limitations:** The UI is intentionally **not Production-ready**: it uses loopback Development .NET identity endpoints, and is disabled unless `NODE_ENV=development` AND `ARGIN_IDENTITY_UI_ENABLED=true`. Current access-token life is 10 minutes; after expiry, a protected server-side navigation requires fresh login (the explicit refresh operation exists but transparent UI refresh is not yet implemented). Same-origin mutation checks, HttpOnly cookies and server guards reduce risk but are not a substitute for a complete production identity architecture and CSRF/security review in Steps 10–11. No new database migration. Step 07 open persistent-audit / distributed-throttle blockers remain documented above.

**Operator verification (macOS, all commands from repository root):**

Terminal A — keep the SSH tunnel open:
```bash
AUTOSSH_GATETIME=0 autossh -M 0 -N \
  -o ServerAliveInterval=15 \
  -o ServerAliveCountMax=3 \
  -o ConnectTimeout=10 \
  -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
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

**Evidence:** GitHub code/docs committed; the owner expressly approved Step 08 on 2026-10-12. macOS/browser/PostgreSQL/Ubuntu/CI raw logs were not independently inspected by the assistant. Step 08: **OWNER APPROVED**. Step 09 is now active.

### Scope Amendment Requested — Phone-First Identity, Ghasedak OTP and SMS Credit (2026-10-12)

**Change request:** https://github.com/HekmatBeigverdi/ArginTaxPayer/issues/4. The owner requested a material revision to the Phase 03 identity experience while Step 08 (Persian RTL Identity UI) is still pending verification. Historical note written before the owner-approved eleven-step amendment; preserve existing Steps 01–08 and their approvals. The requested functionality is an **additive scope amendment under design/implementation**, not a retroactive completion claim. Existing email/password, administrator MFA and previous integration tests must remain operational until migration and full acceptance.

**Functional requirement:** Phone number (normalized `+989xxxxxxxxx`) becomes the primary registration identity. Registration requires proving ownership of a reachable channel through SMS OTP or another enabled verified channel. Login accepts either verified email or verified mobile with password, or mobile OTP for a passwordless account. Password recovery supports SMS OTP or email-based proof; no SMS is compulsory when another secure channel is available. No one may register/sign in with an unverified identifier, and the email-only Level 1 administrator must not be locked out.

**SMS provider:** Ghasedak template `TaxPayerActivationCode` with `Param1` OTP parameter; provider-owned content `کد فعالسازی %param1% «نگین حساب» لغو11`. Provider reference: https://github.com/ghasedakapi/GhasedakSms.Framework. Its published SDK targets `net40;net47;net48`, so it must **not** be directly added to this .NET 9 host without explicit compatibility evidence; implement a secret-injected `HttpClient` adapter to provider's documented OTP REST contract and verify the exact payload/result, idempotency and failure semantics against live provider reference. Never commit API keys, OTPs or recovery secrets.

**SMS accounting requirement:** Issue **200,000 toman = 2,000,000 IRR once per end-user** as restricted promotional SMS credit on verified account activation. Use integer IRR exclusively in persistence. Maintain per-provider-panel sending expense ledger and tariff versions; per-user wallet balance and append-only top-up/charge/reversal ledger; atomic reservation and idempotent settlement, charge snapshot and concurrent debits. Provider credit balance is not the same as a user's promotional balance, and promotional funds cannot be cashed out. Pre-registration verification SMS is system-funded and rate-limited, without creating a wallet until activation.

**Security/compatibility:** Purpose-bound hashed OTP, short validity, one-time CAS consume, strict attempt/cooldown budgets and generic failure responses; password resets revoke sessions and preserve administrator MFA. Add a safe, manual test-database migration/backfill before altering the legacy `NormalizedEmail` and `PasswordHash` required fields. No direct changes to production DB. Do not assume sender rates or provider API credentials.

**First non-breaking implementation committed:** `IranianMobileNumber.TryNormalize` in `src/ArginTaxPayer.Infrastructure/Persistence/Identity/IranianMobileNumber.cs` normalizes local/Persian/Arabic and +98/0098 Iranian mobile input to E.164 `+989xxxxxxxxx`. Tests in `tests/ArginTaxPayer.Tests/IranianMobileNumberTests.cs` cover accepted variants and rejection. This does not mark a phone as verified, alter the current login mechanism, create an SMS wallet, or send any messages. Before any wider change, run `dotnet test ArginTaxPayer.sln -c Release --filter FullyQualifiedName~IranianMobileNumberTests` locally and in CI. Build/test outcomes are not independently verified.

**Delivery sequence and acceptance:** Schema and compatibility migration → OTP lifecycle and alternate email/password flows → Ghasedak transport/mock tests → transactional wallet/provider cost accounting → Persian user/admin UI → full macOS, dedicated PostgreSQL, HTTP Smoke, Ubuntu and live-provider operator verification. Step 08 is owner-approved for the original email/MFA UI; the amended phone-first workflows are assigned exclusively to Step 09 and require separate acceptance; the owner has since approved inserting the new Step 09. The technical design, acceptance scenarios and external dependencies are tracked in issue #4. Release/production deployment is blocked until all requirements and Step 07 remaining distributed protection/audit items are resolved.

### Scope Amendment Addendum — Level 1 Scheduled SMS Tariff Versioning and Phase Allocation (2026-10-12)

**Owner decision:** SMS prices must be **scheduled and changed exclusively by Level 1 administrators**, including future-dated increases or decreases (for example, create a new rate today effective two days later). Rates may vary by provider panel and charge category. Editing a future rate must never rewrite the price of a previously reserved, sent or settled SMS. This is an approved requirement and a planned implementation, **not functionality already delivered**.

**Canonical tariff domain contract (integer IRR):**

- `sms_tariff_versions`: `id` (UUID), `provider_panel_id` (FK), `charge_type` (OTP/transactional/other, extensible), `unit_price_irr` (non-negative 64-bit integer), `effective_from_utc` (UTC timestamptz), `effective_until_utc` (derived from the next rate or nullable), `status` (scheduled/active/superseded/cancelled), `created_by_level1_user_id`, `created_at_utc`, optional `superseded_by_version_id`, `change_reason` and concurrency token. Retain full append-only change history, including reschedules and cancellations, with actor, timestamp and before/after data. Do not silently update any tariff used by historical sends.
- For a panel and charge type there is **one effective version for any UTC instant**, with no overlapping validity periods. Every enabled charge category must have an unambiguous effective price before spending; missing tariff blocks a chargeable send with a controlled error, not a zero-price fallback. Rates can be scheduled in advance, including exactly at midnight Tehran time, but user-entered Iran local time must be converted to and stored in UTC, with explicit time-zone display.
- Permission: Only verified **Level 1** may create, approve, reschedule or cancel future tariffs. Level 2 and customers may see the effective cost and their authorized billing history but cannot modify pricing. Enforce this on the API and in database transaction boundaries, never in UI alone.
- A new future-dated tariff must not retroactively affect earlier traffic. Apply the effective tariff **at the point of successful atomic send reservation** (`reserved_at_utc`) and store `tariff_version_id`, `unit_price_irr_snapshot`, `currency='IRR'`, `provider_panel_id` and related idempotency key on the send and wallet ledger. A retry/reconciliation/settlement after a price switch retains the original reservation snapshot. A new send after the effective instant uses the new tariff. If a reservation expires and is reversed, a subsequent genuinely new request obtains a fresh tariff and its own idempotency key.
- For each provider panel, preserve **two distinct price dimensions**: (1) **customer-facing charge tariff** set by Level 1, deducted from the appropriate SMS credit wallet, and (2) **actual provider expense** recorded from the Ghasedak charge/receipt or provider pricing evidence. Track provider expense independently, even if it differs from the customer-facing tariff. Never infer the real Ghasedak cost solely from the customer tariff. Record both in IRR, with provenance and reconciliation status.
- On each send, journal an immutable debit/reservation, a definitive settlement or compensating reversal, and a provider expense/reconciliation entry. Safeguard nonnegative customer balances, concurrent sends, exactly-once promotional issuance (`2,000,000 IRR` per eligible user), top-ups, and request/receipt idempotency. Future price schedules require no periodic job to 'activate' the tariff: use timestamp-based selection in the atomic transaction, so delayed cron tasks do not lead to wrong rates.
- Version history must remain queryable for audit. Never delete or alter versions referenced by a send, wallet journal or settlement. Rescheduling/cancelling a future **unreferenced** rate creates a traceable audit transition and revalidates non-overlap.

**Example (illustrative values only, NOT provider quotes):** Panel A OTP costs **1,000 IRR** through 2026-10-13 23:59:59 Asia/Tehran, then **1,500 IRR** from 2026-10-14 00:00 Asia/Tehran. A send reserved on October 13 is charged 1,000 IRR even if finally settled on October 15; a fresh reservation on October 14 is charged 1,500 IRR. Provider expense can independently be different. In this example, 200,000 toman gift credit = 2,000,000 IRR remains unchanged; only deductions reflect the effective tariff.

**Fixed-phase work allocation (no extra numbered Phase 03 steps):**

| Phase / existing step | Planned work package | Required acceptance |
| --- | --- | --- |
| **03 / Step 08** (verification pending) | Verify previously built Persian RTL email/password, MFA, account and guarded routes without changing accepted core identity behavior. | Existing login, logout, UI smoke and negative-path regression verified. |
| **03 / Step 09** (new) | Build mobile-first identity migration, alternate email/password and phone OTP login/recovery, Ghasedak transport with mock-first validation and updated RTL screens; define financial delivery contracts for downstream phases. | PostgreSQL test DB, expiry/replay/anti-enumeration, admin MFA protection, macOS/Ubuntu/API/UI smoke; live sends explicitly gated. |
| **03 / Step 10** (renumbered former Step 09) | Complete end-to-end identity authorization and OTP integration/regression tests, including multi-user negative paths and provider failure modes. | Integration/CI evidence and compatibility suite all pass before signoff. |
| **03 / Step 11** (renumbered former Step 10) | Final hardening and handoff; durable security/audit review, migration/rollback drill and finance-domain delivery contracts. | Security, compatibility and handoff gate. Do **not** claim paid SMS wallet or scheduled pricing complete. |
| **12 — Subscriptions, Billing and Settlements** | Implement `sms_provider_panels`, versioned/scheduled tariffs, customer SMS credit wallet, immutable ledger/reservations, provider expenses and reconciliation, top-up and once-only promotional 2,000,000 IRR grant. Billing domain owns pricing and financial transactions. | Boundary-time selection, past-price immutability, concurrent debit, no duplicate grants, tariff swap between reserve/settle, reconciliation, replay/idempotency and accounting checks. |
| **16 — Platform & Delegate Administration** | Level 1-only SMS panel administration, future-dated tariff creation/amendment/cancellation, ledger/search screens, top-ups, provider cost vs charge analytics and audit views. Level 2 may view only authorized billing history. | Role/scope denial matrix, future schedule preview and confirmation, no retroactive mutation, operator audit evidence. |
| **17 — Security and Quality Hardening** | Pen-test OTP and tariff endpoints, distributed throttling and durable audit follow-through from Step 07; timezone-boundary, concurrency, failure-injection and provider consistency tests. | No double-charge, price-gap, role escalation, replay or secret leakage. |
| **18 — Production Release** | Secrets/provisioning, provider live compatibility, tariffs seeded by Level 1, migrations/deployment, operational reconciliation/runbooks. | Sign-off required before real sends/charges; no auto-migration or automatically populated guessed tariff. |

**Dependencies and release gates:** Phase 03 may close as **identity groundwork** only if its identity-specific amended acceptance is approved and the deferred financial work is explicitly registered for Phase 12, administration Phase 16, security Phase 17 and release Phase 18. Do not describe SMS wallet or scheduled rates as implemented during Phase 03. Preserve a feature flag / disabled paid-SMS flows until billing and admin work pass acceptance. Update the single-file phase master plan for each later phase before work begins, consistent with the roadmap documentation policy. GitHub issue #4 tracks the multi-phase requirements.

### Operator Checkpoint — Current Step, Test Gates and Ghasedak Secret Provisioning (2026-10-12)

**Current position:** Steps 01–07 = Owner Approved (Step 07 security debt carries into Step 11/17). Step 08 = **Owner Approved** as of 2026-10-12 and the approved phone-first scope amendment. Steps 09, 10 and 11 = Planned (with Step 09 added by owner-approved amendment). The current `/identity` frontend and API still use **email/password**; adding `IranianMobileNumber` did not enable phone registration, SMS dispatch or a wallet. Do not treat the SMS/OTP flow as ready to test merely because a provider key has been configured.

**First verification gate:**
```bash
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet test ArginTaxPayer.sln -c Release --no-build --filter 'FullyQualifiedName~IranianMobileNumberTests'
(cd apps/web && npm ci --no-audit --no-fund && npm test && npm run typecheck && npm run build)
```
Next, use the **owner-proven zsh same-shell PostgreSQL env command** in the section titled `Confirmed macOS Development API Launch Convention`, start API on `127.0.0.1:15089`, and run a separate terminal:
```bash
curl --noproxy '*' -s http://127.0.0.1:15089/health/dev-identity
ARGIN_IDENTITY_UI_ENABLED=true ARGIN_IDENTITY_API_INTERNAL_BASE_URL=http://127.0.0.1:15089 npm --prefix apps/web run dev -- --port 3000
```
While API and web run in dedicated shells, in another shell from repository root:
```bash
python3 scripts/phase-03-step-08-ui-smoke.py
ARGINTAXPAYER_API_BASE=http://127.0.0.1:15089 python3 scripts/phase-03-step-06-smoke.py
ARGINTAXPAYER_API_BASE=http://127.0.0.1:15089 python3 scripts/phase-03-step-07-smoke.py
```
Check `http://127.0.0.1:3000/identity`, `/account`, registration/login, logout, forbidden direct account access, MFA and recovery with the **previously configured** Identity encryption key; use disposable users only in the isolated `argintaxpayer_test` PostgreSQL database. Then run all matching build/test commands on Ubuntu without publishing development API/web ports.

**Secret file convention for FUTURE Ghasedak adapter tests (no production SMS in current Step 08):** On macOS use an **untracked** project-root `.env.ghasedak.local` file (covered by root `.gitignore` rule `.env.*`). Do not create it from actual credentials until the adapter tests require it. The intended format is:
```sh
# .env.ghasedak.local (NEVER commit or paste this file in chat)
Ghasedak__ApiKey='REPLACE_LOCALLY_WITH_REAL_API_KEY'
Ghasedak__TemplateName='TaxPayerActivationCode'
Ghasedak__EnableLiveSending='false'
# Add account/panel identifier and tested provider mapping when confirmed.
```
The API currently has **no Ghasedak registration or file-loading mechanism**, so these values are **planned**, not presently wired configuration. For future test runs, load the restricted local file into **the same process shell that launches .NET** using `set -a; source ./.env.ghasedak.local; set +a` (after inspecting the file, ensuring it is owner-readable only via `chmod 600 .env.ghasedak.local`). Do not run `git add -f` on secrets or expose an API key through `NEXT_PUBLIC_*` environment variables. Never log secrets.

**Additional operator inputs at the relevant live-provider gate:** one Ghasedak panel/account identifier and a secret API key supplied in the file only; account-specific confirmation that `TaxPayerActivationCode` exists and `Param1` is correct; a permitted test recipient mobile entered privately into a local test configuration; provider response/receipt/cost metadata and sender restrictions. The actual provider expense **must be established from source evidence**. Customer-facing scheduled charges require Level 1 tariff versions in Phase 12/16, not a hard-coded Ghasedak unit price.

**Provider compatibility clarification:** The `GhasedakSms.Framework` repository README documents `SendOtpWithParamsInput`, while the current C# method signature accepts `SendOldOtpInput` for `SendOtpWithParams` and submits to `https://gateway.ghasedak.me/rest/api/v1/WebService/SendOtpWithParams`. Verify the request DTO against actual provider behavior before implementing a .NET 9 typed HttpClient adapter; do not add the .NET Framework-only SDK directly to .NET 9. Test with mock transport first and default live sending OFF.

### Step 08 — Owner Signoff (2026-10-12)

**Decision: OWNER APPROVED**, per explicit owner statement: `گام ۸ تایید هست گام ۹ را کامل کن`. This signoff applies to the prior Persian RTL email/password, MFA and guarded identity screens. The new phone-first identity feature belongs to newly inserted Step 09 and remains subject to its own acceptance gate. No production deployment, main merge or release authorized.

### Owner-Approved Phase Plan Amendment — Expand to 11 Steps (2026-10-12)

**Decision and scope:** The owner explicitly instructed that phone-first registration and optional Ghasedak SMS OTP must be a **separate next step**, after the existing Step 08, and that Phase 03 shall **close in 11 steps**. This supersedes all earlier notes in this file saying that the phase must remain at ten steps. Earlier text documenting the original ten-step plan is retained as historical context only, not the active plan. Existing Steps 01–08 retain their numbers; the original Step 09 is renumbered Step 10 and the original Step 10 is renumbered Step 11. Step 08 was subsequently owner-approved by explicit instruction on 2026-10-12; the new Step 09 remains unapproved pending security and test evidence.

**New Step 09 — Mobile-First Registration, OTP Authentication and Ghasedak Integration**

- Schema: additive, reviewed EF Core migrations for normalized and verified mobile identity, optional verified email, password-optional accounts, OTP challenge/delivery/audit entities; no destructive change to existing Level 1 email/password/MFA records. PostgreSQL migration is **manual against the verified `argintaxpayer_test` DB only** before wider rollout.
- Registration: primary phone number, verified ownership before activation; alternative verified email identity supported when SMS is unavailable; no automatic activation from an unverified identifier. Phone representation canonicalized to `+989xxxxxxxxx` using existing tested `IranianMobileNumber`.
- Login: a normalized verified phone **or** verified email with password, or phone OTP for eligible passwordless account; existing email/password users keep working. Password recovery: SMS OTP or verified email mechanism; any successful password change revokes existing sessions and refresh credentials. Existing administrative MFA must not be bypassed by a phone OTP.
- OTP: purpose-specific, account/phone-bound cryptographic hashes, short TTL, one-time atomic consumption, retry/resend limits, multi-instance protection where applicable, generic responses to prevent user enumeration, secure event audit and no raw OTP in logs.
- Provider: modern .NET 9 Ghasedak REST adapter, mocked delivery tests first; template `TaxPayerActivationCode` and single `Param1`; local untracked secret `.env.ghasedak.local` only when the adapter supports these variables. Live send remains **disabled by default** until permitted test number, verified template, provider key and explicit operator approval are supplied. An SMS service disruption must not disable other safe authentication/recovery channels.
- RTL UI: registration by phone, choice of email/password or eligible mobile OTP login, SMS/email recovery, countdown/errors and ownership verification; retain guarded account page and same-origin HttpOnly session handling.
- **Financial boundary:** Step 09 may define SMS delivery cost and event contracts but does **not** claim that 200,000 toman promotional credit, paid SMS deductions, future tariff scheduling or provider expense settlement are implemented. These remain Phase 12 (ledger, wallet and scheduled pricing) and Phase 16 (Level 1 admin controls), tested in Phase 17 and released in Phase 18. Authentication verification must not be blocked by an unimplemented customer SMS wallet.
- **Acceptance:** macOS Build and all existing identity regressions, PostgreSQL integration against `argintaxpayer_test`, OTP one-use/expiry/replay/negative path, email fallback, admin MFA and session invalidation, HTTP API+Next.js smoke, Ubuntu Build/tests, controlled provider mock validation and an operator-authorized live test where available. Record separate evidence and do not invent pass results. At least one viable non-SMS authentication/recovery route must remain. No CI/release/merge without independent evidence and owner approval.

**Updated completion sequence:** Step 08 verify and owner signoff → Step 09 implementation and owner signoff → Step 10 Identity and Authorization Integration Tests → Step 11 Hardening, Verification, and Phase Handoff. The finished Phase 03 deliverable is identity/OTP groundwork; the commercial SMS credit and configurable tariff system is tracked as downstream work.

### Step 09 — Mobile-First Registration, OTP Authentication and Ghasedak Integration (Implementation checkpoint, 2026-10-12)

**Status:** Core implementation and a local-only practical OTP file sender committed; **not yet Owner Approved**. The assistant cannot run `.NET` Build/PostgreSQL on the GitHub-hosted repository in this environment, and no CI result is available. Do **not** merge, deploy, enable real SMS or treat this as a production-ready account recovery mechanism until tests and the explicit remaining gaps below are resolved.

**Implemented artifacts:**
- `IdentityUser` now has nullable legacy email/password plus a unique optional `NormalizedMobile` and `MobileVerifiedAtUtc`; existing email/password records are left intact. A new `IdentityPhoneChallenge` entity and `ArginTaxPayerDbContext` mapping store purpose, keyed digest, expiry, attempts, delivery status and consumption timestamp.
- Additive EF Core migration `20261012090000_AddMobileIdentityOtp` and updated model snapshot. The migration does **not** auto-apply, and its downgrade fails safely if newly created phone-only/passwordless accounts exist.
- `IdentityPhoneService`: verified Iranian mobile registration with optional password, password login using mobile/email, passwordless mobile OTP login for Customer accounts without a password, SMS proof-based password reset, and immediate revocation of prior sessions on reset. Administrative identities are excluded from phone passwordless login and phone reset; their established MFA rules remain in effect.
- OTP code generation with `RandomNumberGenerator`, HMAC-SHA256 bound to challenge ID, normalized phone and purpose; stored values cannot be used as raw OTP; three-minute expiry, one-use DB CAS claim, up to five attempts, basic mobile send cooldown. Secret `Identity__OtpPepper` must be supplied privately (32+ characters). No OTP response contains plaintext codes.
- `IIdentityOtpSender` and `GhasedakOtpSender`: typed .NET 9 HttpClient, official `SendOtpWithParams` URL and PascalCase JSON contract, `TaxPayerActivationCode` with exactly `Param1`, client reference ID, `ApiKey` header. **Actual SMS sending disabled by default**. The real API key is never committed or logged. Provider receipts/charges have not been integrated into a financial ledger yet (Phase 12).
- Development/loopback API routes: `POST /api/dev/identity/phone/request`, `/phone/register/complete`, `/phone/login/complete`, `/phone/reset/complete`. Start-request acknowledgement is intentionally generic (`202`) to avoid account enumeration. Local `/health/dev-identity` now reports only safe booleans `smsSendingEnabled` and `otpPepperConfigured`.
- Next.js local BFF allowlists corresponding actions; Persian RTL `phone-forms.tsx` offers registration, eligible passwordless login and SMS reset, while maintaining prior email/password forms and protected account/session UI.
- Tests: `IdentityPhoneOtpTests` for code binding and disposable PostgreSQL registration/login/replay/reset (with in-memory fake sender), `GhasedakOtpSenderTests` for disabled-by-default and template payload mock, `IranianMobileNumberTests` for canonical formats, and `scripts/phase-03-step-09-smoke.py` for phone endpoint rejection and no-live-SMS safety preflight. The Python script **refuses to run if the API does not explicitly report `smsSendingEnabled=false`**.

**Post-implementation security correction:** The previously existing MFA enrollment flow has been guarded against passwordless phone-only account lockout; mobile/password identities now also resolve in `IdentityMfaService.BeginLoginAsync`, so password-enabled mobile accounts retain MFA. The correction was committed as `4e366ce4d3773b63f322b17d244eb222542c8751`. MFA and all identity paths require owner/CI regression validation; no results have been independently observed.

**Unresolved acceptance items (must not be described as passed):**
1. Existing email/password authentication remains; however, email *ownership verification and email-based forgotten-password recovery* are **not yet implemented**. A secure email-delivery and verified-email recovery contract must be implemented and tested to fully meet the owner's requirement that SMS not be mandatory for registration, login and recovery.
2. The SMS sender's response parsing is based on the public Framework contract but has not been validated with a real Ghasedak test panel; live send and provider cost reconciliation are disabled. Do not provide an API key in this chat; store it in an untracked restricted local file only when explicitly conducting a live test.
3. OTP cooldown is currently persisted but not a complete atomic distributed send budget or IP/device/provider abuse policy; durable, redacted audit/security tracking and provider timeout reconciliation are not yet complete. These are security blockers for production, not optional enhancements.
4. Frontend browser, full macOS/Ubuntu Build, isolated PostgreSQL migration/integration, and HTTP/UI regression test evidence have not yet been supplied. CI not independently verified. These are required before Owner Approved.
5. Per-user 200,000 toman promotional SMS wallet, customer/provider cost ledgers and future-dated Level 1 tariff management remain explicitly deferred to Phases 12 and 16 under the approved roadmap. The lack of a live SMS wallet cannot silently be treated as a zero-price charge.

**Verification command sequence, macOS, repository root:**

```bash
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
dotnet test ArginTaxPayer.sln -c Release --no-build --filter 'FullyQualifiedName~IranianMobileNumberTests|FullyQualifiedName~GhasedakOtpSenderTests|FullyQualifiedName~IdentityPhoneOtpTests'
(cd apps/web && npm ci --no-audit --no-fund && npm test && npm run typecheck && npm run build)
```

**Test DB / migration gate:** Keep the existing SSH tunnel open in a separate terminal:
```bash
AUTOSSH_GATETIME=0 autossh -M 0 -N \
  -o ServerAliveInterval=15 \
  -o ServerAliveCountMax=3 \
  -o ConnectTimeout=10 \
  -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
```
Use the **owner-verified macOS zsh command** in the same shell that will run `.NET` API/tests:
```zsh
read -rs 'db_password?Test database password: '; print
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${db_password};SSL Mode=Prefer"
unset db_password
export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_ENVIRONMENT=Development
export Identity__AllowDevelopmentRegistration=true
export Identity__OtpPepper="$(openssl rand -hex 32)"
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
```
**Stop unless the result is exactly `argintaxpayer_test`.** Inspect migrations before applying; do not apply to any other database:
```bash
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
dotnet ef database update 20261012090000_AddMobileIdentityOtp --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --no-build --filter 'FullyQualifiedName~IdentityPhoneOtpTests|FullyQualifiedName~IdentityResourceScopePostgresTests|FullyQualifiedName~IdentityMfaRetryStrategyPostgresTests'
```
Do not automatically roll back the migration after creating phone-only accounts; the manual Down migration intentionally blocks unsafe downgrade.

**Start .NET API with the same configured shell (Development only):**
```bash
export Ghasedak__TemplateName=TaxPayerActivationCode
export Ghasedak__EnableLiveSending=false
dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15089
```
Leave the API running. In another terminal, from repo root:
```bash
curl --noproxy '*' -s http://127.0.0.1:15089/health/dev-identity
ARGINTAXPAYER_API_BASE=http://127.0.0.1:15089 python3 scripts/phase-03-step-09-smoke.py
ARGINTAXPAYER_API_BASE=http://127.0.0.1:15089 python3 scripts/phase-03-step-06-smoke.py
ARGINTAXPAYER_API_BASE=http://127.0.0.1:15089 python3 scripts/phase-03-step-07-smoke.py
python3 scripts/phase-03-step-08-ui-smoke.py
```
**Note:** The last command requires Next.js to be running in a **separate** terminal with `ARGIN_IDENTITY_UI_ENABLED=true ARGIN_IDENTITY_API_INTERNAL_BASE_URL=http://127.0.0.1:15089 npm --prefix apps/web run dev -- --port 3000`. Expected marker for Step 09 negative smoke: `STEP 09 PHONE OTP NEGATIVE SMOKE PASSED`. The positive phone OTP flow is verified with `IdentityPhoneOtpTests` using the fake sender; do **not** turn on live Ghasedak just to make the negative smoke pass.

**Ubuntu VPS build and tests (no public API port):**
```bash
ssh argindev@85.133.244.156
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
(cd apps/web && npm ci --no-audit --no-fund && npm test && npm run typecheck && npm run build)
```
The Ubuntu unit/build gate does not imply that database migrations or real SMS were performed on the VPS.

**Ghasedak configuration convention (when a subsequent live-provider verification is explicitly authorized):** Place the real `Ghasedak__ApiKey` and verified `Ghasedak__TemplateName='TaxPayerActivationCode'` in the repository-root untracked `.env.ghasedak.local`, set file permissions `chmod 600`, load it with `set -a; source ./.env.ghasedak.local; set +a` in the API shell, and never commit it or print it to logs. **Keep `Ghasedak__EnableLiveSending=false` at this stage**. The OTP pepper is a distinct durable secret, not the Ghasedak API key; store it in the secret environment for any non-disposable deployment.

### Project-Wide macOS-to-Ubuntu PostgreSQL Tunnel Standard — Owner Approved (2026-10-12)

**MANDATORY for all future ArginTaxPayer phase runbooks and local development tests:** The owner replaces previous plain `ssh -N` PostgreSQL port-forward commands with persistent `autossh` (AutoSSH must be installed and SSH-key login should be configured). Use this exact command from macOS in a **dedicated foreground terminal**, keeping the same PostgreSQL test-only local port:

```bash
AUTOSSH_GATETIME=0 autossh -M 0 -N \
  -o ServerAliveInterval=15 \
  -o ServerAliveCountMax=3 \
  -o ConnectTimeout=10 \
  -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
```

Do not introduce an old `ssh -N -L` variant in new phase instructions. AutoSSH repairs dropped SSH tunnels, but it does **not** restart the .NET API, restart a crashed PostgreSQL service, or make an incorrectly configured database safe. This tunnel is for local testing against PostgreSQL via Ubuntu only; distinguish it from the ordinary `ssh argindev@85.133.244.156` command used to open an Ubuntu remote shell. Keep `argintaxpayer_test` database identity verification as a hard gate before migrations/tests. Do not insert non-breaking-space (NBSP) characters from chat formatting into terminal commands.

### Step 09 — Practical Browser Acceptance: Local OTP Without Ghasedak Cost (2026-10-12)

**Delivery mode:** The operator requested practical end-to-end testing with the actual Next.js panel. A new `LocalFileOtpSender` is registered **only** when the .NET host runs in Development and `Identity__OtpDeliveryMode=LocalFile`; never enable this mode in Production, on an exposed public API, or on a shared server. When enabled, `IdentityPhoneService` permits test OTP delivery while `Ghasedak__EnableLiveSending=false`. A freshly generated OTP is written to an operator-selected **absolute** local path with owner-only Unix mode, never to HTTP/JSON logs or GitHub. The file contains sensitive one-time data: remove it immediately after testing. The local file sender is only for a disposable test account; no live provider SMS costs occur.

**Step 09 operator acceptance sequence (macOS zsh; repository root):**

**Terminal A — owner-approved AutoSSH, replaces legacy `ssh -N`:**
```bash
AUTOSSH_GATETIME=0 autossh -M 0 -N \
  -o ServerAliveInterval=15 \
  -o ServerAliveCountMax=3 \
  -o ConnectTimeout=10 \
  -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
```
AutoSSH prerequisite: `brew install autossh` if needed. Keep this foreground terminal open.

**Terminal B — retrieve and build, then verify test DB before migration:**
```zsh
git fetch origin
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
(cd apps/web && npm ci --no-audit --no-fund && npm test && npm run typecheck && npm run build)
read -rs 'db_password?Test database password: '; print
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${db_password};SSL Mode=Prefer"
unset db_password
export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_ENVIRONMENT=Development
export Identity__AllowDevelopmentRegistration=true
export Identity__OtpPepper="$(openssl rand -hex 32)"
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
```
Stop on any build/test failure or if the query is not **exactly** `argintaxpayer_test`. If and only if the migration is unapplied, execute against the verified test DB:
```bash
dotnet ef database update 20261012090000_AddMobileIdentityOtp --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --no-build --filter 'FullyQualifiedName~IdentityPhoneOtpTests|FullyQualifiedName~IdentityResourceScopePostgresTests|FullyQualifiedName~IdentityMfaRetryStrategyPostgresTests'
```

**Terminal B — run .NET API with local-only OTP file** (same shell; do not expose a non-loopback listener):
```zsh
mkdir -p "$HOME/.argintaxpayer-test"
chmod 700 "$HOME/.argintaxpayer-test"
export Identity__OtpLocalFile="$HOME/.argintaxpayer-test/step09-otp.txt"
export Identity__OtpDeliveryMode=LocalFile
export Ghasedak__TemplateName=TaxPayerActivationCode
export Ghasedak__EnableLiveSending=false
dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15089
```

**Terminal C — start Next.js** (another shell, repository root):
```bash
ARGIN_IDENTITY_UI_ENABLED=true ARGIN_IDENTITY_API_INTERNAL_BASE_URL=http://127.0.0.1:15089 npm --prefix apps/web run dev -- --port 3000
```

**Terminal D — preflight** (another shell, repository root):
```bash
curl --noproxy '*' -s http://127.0.0.1:15089/health/dev-identity
ARGINTAXPAYER_API_BASE=http://127.0.0.1:15089 python3 scripts/phase-03-step-09-smoke.py
python3 scripts/phase-03-step-08-ui-smoke.py
```
Require `identityEndpointsRegistered=true`, `registrationEnabled=true`, `connectionConfigured=true`, `otpPepperConfigured=true`, `localOtpTestMode=true`, and **`smsSendingEnabled=false`**. If any differ, stop and correct configuration.

**Interactive browser at `http://127.0.0.1:3000/identity`:**
1. **New passwordless customer:** choose `ثبت‌نام با شماره همراه`, use a genuinely disposable Iranian-formatted mobile number that is **not an existing real user's mobile** (avoid sending any real message; local file transport only), click `دریافت کد تأیید`; open `cat "$HOME/.argintaxpayer-test/step09-otp.txt"` in Terminal D (do not paste its contents into a chat). Enter its 6-digit Code; leave password empty; confirm. Expect account creation and no automatic admin role.
2. **Passwordless login:** in the same browser sign-in switch to `ورود بدون رمز با پیامک` with that phone. Respect the 60-second persisted cooldown after the previous request; if it has not passed, the generic acknowledgement is displayed but no new file OTP is written. Check that the OTP file has a new Code and Delivery ID (the current file does not include purpose), enter the new Code; expect redirect to `/account`, server-backed Customer role and own sessions visible. Log out.
3. **Wrong/reused/expired:** request another OTP after 60 seconds; enter a wrong code and confirm access is rejected; the right code should succeed once only and replay should fail. An OTP is valid for three minutes; requesting a fresh one invalidates the previous only after expiry or consumption under the current service, so explicitly verify the most recent proof is chosen. No code should appear in browser localStorage or application JSON logs. Note that the UI might not provide direct retry for a consumed code; use the request flow after cooldown.
4. **Password recovery:** only after a valid passwordless customer exists, choose `بازیابی رمز با پیامک`, request a code, respect cooldown, inspect the local file, set a new 8+-character password, and confirm. Existing sessions must be revoked. Now use the **email/password tab** (its identifier field accepts phone) to log in with mobile + new password. Passwordless OTP login is intentionally not available for password-bearing accounts under the current conservative security policy.
5. **Existing account regression:** register a separate test account through the existing email form, sign in and out normally; verify /account is unavailable anonymously, MFA enrollment/authentication preserves the existing admin restrictions; verify the system denies selecting a foreign session.
6. **Browser presentation:** RTL, mobile width, clear errors, missing/invalid code, loading states; verify only HttpOnly session cookies exist, not raw bearer tokens in localStorage/sessionStorage.
7. **Cleanup:** stop API and Next.js; remove sensitive OTP file with `rm -f "$HOME/.argintaxpayer-test/step09-otp.txt"`. Test database records are disposable but will persist for future local regression unless removed using a targeted, reviewed test-only cleanup; never run a broad delete.

**Critical accuracy note:** The sample test case for an OTP sent to an arbitrary phone uses `LocalFile` delivery, so it does not prove actual phone ownership, provider delivery, real SMS costs or Ghasedak functionality; it only proves local code lifecycle and UI integration. Never activate this mechanism outside the test-only loopback Development environment. Real Ghasedak live acceptance needs a deliberate second gate, one personally controlled test recipient, an untracked secret file, validated provider template/API response, explicit permission to incur SMS charges, and verification of a receipt. Do not send the actual OTP or API key to the assistant.

**Ubuntu verification** remains `dotnet build`, `dotnet test`, `npm test`, `npm run typecheck`, and `npm run build` on the Phase 03 branch; **do not** run the development OTP test sender publicly or open API/Next.js ports.

### Step 09 — UX Revision and Operator-Gated Ghasedak Live-Test Procedure (2026-10-12)

**UX change:** The former stacked mobile widget + legacy email tabs have been replaced on `/identity` by the new `identity-flow.tsx`: exactly two primary tabs **Login / Registration** in Persian RTL. Login has password-vs-SMS-OTP secondary choices; registration defaults to mobile, with an explicit **Development-only email** alternative for backward compatibility. Forgotten-password is an inline subflow, not a third primary tab. MFA is handled in the same compact card. Responsive spacing, validation, one-time-code fields, status messages and accessible controls are retained. Historical `identity-forms.tsx` and `phone-forms.tsx` are no longer rendered; do not run old UI assumptions as acceptance criteria. `apps/web/tests/identity-ui.test.mjs` now verifies the unified interface.

**Connection state — IMPORTANT:** A .NET 9 `GhasedakOtpSender` that POSTs the `TaxPayerActivationCode` template and `Param1` to the provider endpoint exists, **but no live account success is independently confirmed**. `Ghasedak__EnableLiveSending` defaults to false; `Identity__OtpDeliveryMode=LocalFile` disables real provider transport. A live provider test now requires an explicit *single operator-owned test recipient* (`Ghasedak__TestRecipientMobile`) as well as API key and template; all other recipients are rejected at the adapter. There is no production activation in Step 09.

**Safe diagnostics:** `GET /health/dev-identity` (loopback Development only) returns booleans `ghasedakApiKeyConfigured`, `ghasedakTemplateConfigured`, `ghasedakTestRecipientConfigured`, `smsSendingEnabled`, `localOtpTestMode`, `otpPepperConfigured`, plus existing identity flags; it never returns secret values or the recipient number. .NET JSON console emits safe categorized reasons:
`OTP_SMS_CONFIG_INVALID reason=api_key_missing`,
`OTP_SMS_CONFIG_INVALID reason=template_mismatch`,
`OTP_SMS_REJECTED reason=test_recipient_not_allowed`,
`OTP_SMS_PROVIDER_REJECTED httpStatus=...`,
`OTP_SMS_PROVIDER_REJECTED reason=response_not_success`,
`OTP_SMS_PROVIDER_REJECTED reason=invalid_json`,
`OTP_SMS_TRANSPORT_FAILED reason=network|timeout`,
`OTP_SMS_PROVIDER_ACCEPTED`, and `OTP_REQUEST_DELIVERED` or `OTP_REQUEST_NOT_DELIVERED`. No raw response payload, API key, mobile or OTP is logged by these diagnostic events. `/phone/request` deliberately responds `202` even when not sent, to avoid account enumeration; **HTTP 202 alone is not proof of delivery**.

**Operator test plan, macOS / real Ghasedak (perform after Build, test-DB migration and no-cost local-file browser tests succeed):**

A. Keep the project-standard AutoSSH tunnel in an independent macOS terminal:
```bash
AUTOSSH_GATETIME=0 autossh -M 0 -N \
  -o ServerAliveInterval=15 \
  -o ServerAliveCountMax=3 \
  -o ConnectTimeout=10 \
  -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@85.133.244.156
```
Never paste chat-formatted nonbreaking spaces into shell. This is a tunnel to the **test** PostgreSQL only, not public API hosting.

B. Ensure `dotnet build ArginTaxPayer.sln -c Release`, `dotnet test ArginTaxPayer.sln -c Release --no-build`, `(cd apps/web && npm ci && npm test && npm run typecheck && npm run build)` pass. In a new zsh API shell set the **owner-confirmed** Postgres env:
```zsh
read -rs 'db_password?Test database password: '; print
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${db_password};SSL Mode=Prefer"
unset db_password
export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_ENVIRONMENT=Development
export Identity__AllowDevelopmentRegistration=true
export Identity__OtpPepper="$(openssl rand -hex 32)"
psql -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W -tAc 'SELECT current_database();'
```
Stop unless database reports exactly `argintaxpayer_test`. Inspect/apply migration `20261012090000_AddMobileIdentityOtp` only if pending, according to the Step 09 migration procedure above.

C. Create an **untracked** root `.env.ghasedak.local` with `chmod 600`; the shell must never echo the API key. Use a private editor:
```sh
Ghasedak__ApiKey='PASTE_YOUR_REAL_KEY_LOCALLY'
Ghasedak__TemplateName='TaxPayerActivationCode'
Ghasedak__TestRecipientMobile='YOUR_OWN_TEST_NUMBER_09XXXXXXXXX'
Ghasedak__EnableLiveSending='false'
Identity__OtpDeliveryMode='Ghasedak'
```
The owner must explicitly choose to change `Ghasedak__EnableLiveSending` to `true` **only** for a short, supervised single-recipient live smoke test; SMS costs may be incurred. The file is ignored by Git; `chmod 600 .env.ghasedak.local`. Do not provide the key, recipient, OTP or phone logs to ChatGPT.

D. In the **same API startup shell**, load the secret file and avoid accidentally selecting LocalFile:
```zsh
set -a
source ./.env.ghasedak.local
set +a
unset Identity__OtpLocalFile
dotnet run --no-launch-profile --project src/ArginTaxPayer.Api --urls http://127.0.0.1:15089
```
In another shell verify `curl --noproxy '*' -s http://127.0.0.1:15089/health/dev-identity` has `ghasedakApiKeyConfigured=true`, `ghasedakTemplateConfigured=true`, `ghasedakTestRecipientConfigured=true`, `otpPepperConfigured=true`, `localOtpTestMode=false`. For a live test, `smsSendingEnabled=true` must also be present. If any flag is false, stop before sending. A test recipient must be under operator control.

E. Start web in another shell:
```bash
ARGIN_IDENTITY_UI_ENABLED=true ARGIN_IDENTITY_API_INTERNAL_BASE_URL=http://127.0.0.1:15089 npm --prefix apps/web run dev -- --port 3000
```
Open `http://127.0.0.1:3000/identity`. Use the two tabs to check: registration with the operator's own number (if never registered); OTP login **only** for a verified **passwordless Customer**; recovery for a registered Customer with phone; password login with verified phone or email; existing admin MFA. Respect the 60-second SMS cooldown and 3-minute code expiry. An already registered phone cannot be registered again. For a fresh account that should test OTP login, leave optional password **blank** when completing registration. For password reset, use a passworded Customer. This is **not** a test of email verification or email recovery, which remain incomplete.

F. Check the API terminal's sanitized `OTP_SMS_...` / `OTP_REQUEST_...` events and compare with actual receipt in the mobile inbox and provider control panel. Distinguish `OTP_SMS_PROVIDER_ACCEPTED` (provider accepted request) from actual handset delivery and cost; gateway acceptance does **not** establish handset delivery. On any failure, return only nonsecret diagnostic codes and HTTP status to the assistant. After live test stop the API, restore `Ghasedak__EnableLiveSending='false'`, do not expose ports publicly, and keep the API key in the untracked owner-only file.

**Known constraints to resolve before closing Step 09:** Verified-email recovery and confirmation still need implementation; real Ghasedak compatibility, request/response mapping and provider-level cost must be verified using an operator-approved test; distributed OTP throttling and persistent security audit hardening remain incomplete; provider acceptance is not equivalent to confirmed delivery. The Phase 12 wallet and tariff work stays deferred. Build, CI, PostgreSQL, macOS and Ubuntu tests remain owner-executed; no pass results are inferred.

### Step 09 — Mac API Config-Loading Fix / Old Process Diagnostics (2026-10-13)

**User-reported symptom:** `/health/dev-identity` responds with `development=true`, `identityEndpointsRegistered=true`, `connectionConfigured=true`, but all of `smsSendingEnabled`, `ghasedakApiKeyConfigured`, `ghasedakTemplateConfigured`, `ghasedakTestRecipientConfigured` and `otpPepperConfigured` are `false` even after following manual environment instructions. This establishes that the **running API process** does not see those values; it does not establish any Ghasedak API failure. Possible causes: an old listener or variables sourced in the wrong shell. Do not send OTP/API keys in chat; do not reapply migrations to fix process configuration.

**Committed remedy:** `scripts/phase-03-step-09-run-api.zsh`, a single entrypoint that checks no existing TCP listener on port `15089`, prompts privately for `argintaxpayer_test` DB password if not exported, loads an existing stable OTP pepper from `~/.argintaxpayer-test/otp-pepper`, sources the untracked owner-only repository-root `.env.ghasedak.local`, prints booleans only (never keys, passwords or mobile numbers), and `exec`-starts `.NET` in the very same environment. It refuses missing/mismatched configuration and the LocalFile test mode. **This script does not generate credentials, run migrations, enable live SMS, or overwrite the private config.**

**Operator actions (macOS):**
```bash
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
lsof -nP -iTCP:15089 -sTCP:LISTEN
```
If a process is listening, stop it using `Ctrl+C` in its original shell. Confirm `lsof` no longer shows the listener. In another macOS terminal keep the owner-mandated `autossh` tunnel open. From the repository root:
```zsh
test -s "$HOME/.argintaxpayer-test/otp-pepper" || {
  mkdir -p "$HOME/.argintaxpayer-test"; chmod 700 "$HOME/.argintaxpayer-test";
  openssl rand -hex 32 > "$HOME/.argintaxpayer-test/otp-pepper";
  chmod 600 "$HOME/.argintaxpayer-test/otp-pepper";
}
# Edit untracked .env.ghasedak.local with real operator-owned credentials and
# Ghasedak__EnableLiveSending=false for preflight; test-recipient only.
zsh scripts/phase-03-step-09-run-api.zsh
```
Then run:
```bash
curl --noproxy '*' -s http://127.0.0.1:15089/health/dev-identity
curl --noproxy '*' -i http://127.0.0.1:15089/health/ready
```
The first endpoint must show `otpPepperConfigured=true`, `ghasedakApiKeyConfigured=true`, `ghasedakTemplateConfigured=true`, `ghasedakTestRecipientConfigured=true`, `localOtpTestMode=false`, `smsSendingEnabled=false` for **configuration-only preflight**. The second must respond healthy `200` before any DB test. Only after verified preflight and an explicit operator decision to incur limited real-SMS charges should they edit the secret file's `Ghasedak__EnableLiveSending=true`, stop/restart the API with the same launcher, and request a single OTP to the configured private test recipient. The health endpoint should then show `smsSendingEnabled=true`. Restore false immediately after testing. If config is present but provider rejects or times out, consult existing **redacted** `OTP_SMS_...` logs; never paste raw provider responses/credentials.

**No acceptance claim:** Runtime success, SMS receipt and provider delivery are still unverified; Step 09 remains open until actual operator tests and remaining email recovery/security requirements are satisfied.

### Step 09 — Live SMS Is Configured but Not Arriving: Decision-Point Diagnostics (2026-10-13)

**Observed operator evidence:** `/health/dev-identity` now reports all necessary Ghasedak config booleans `true`, `localOtpTestMode=false`, and `smsSendingEnabled=true`. These flags only prove configuration has been read; they do not prove that an OTP request passed eligibility/cooldown checks, reached the Ghasedak REST endpoint, or was delivered to a handset.

**Root-cause instrumentation change:** `IdentityPhoneService.RequestAsync` now logs redacted `OTP_REQUEST_SKIPPED reason=...` for `invalid_mobile`, `invalid_purpose`, `delivery_disabled`, `rate_limit_15m`, `cooldown_60s`, `mobile_already_registered`, `no_verified_account`, `password_login_required`, `administrator_mfa_required`. It logs `OTP_REQUEST_DISPATCH_STARTED purpose=register|login|reset` only when code generation, challenge persistence and sender dispatch occur, and `OTP_REQUEST_DISPATCH_FAILED` if the sender rejects. The transport still logs `OTP_SMS_CONFIG_INVALID`, `OTP_SMS_REJECTED`, `OTP_SMS_PROVIDER_REJECTED`, `OTP_SMS_TRANSPORT_FAILED`, `OTP_SMS_PROVIDER_ACCEPTED`. These diagnostic events do **not** contain the user mobile, provider key or OTP. The generic HTTP 202 response remains unchanged to prevent user enumeration.

**Verify after pulling new commits and restart .NET API** (old process must be stopped):
1. `dotnet build ArginTaxPayer.sln -c Release` and `dotnet test ArginTaxPayer.sln -c Release --no-build --filter 'FullyQualifiedName~IdentityPhoneOtpTests|FullyQualifiedName~GhasedakOtpSenderTests'`.
2. Confirm the API still targets `argintaxpayer_test` through the owner-approved AutoSSH tunnel and `/health/ready` returns 200.
3. Verify `/health/dev-identity`: `smsSendingEnabled=true`, `ghasedakApiKeyConfigured=true`, `ghasedakTemplateConfigured=true`, `ghasedakTestRecipientConfigured=true`, `otpPepperConfigured=true`, `localOtpTestMode=false`. Only test the operator-owned `Ghasedak__TestRecipientMobile` entered in the private file.
4. In the browser `http://127.0.0.1:3000/identity`, select **Registration** for a **not-yet-registered** permitted mobile. If the account is already registered, use **Login with OTP** only if it is a passwordless Customer, or **Password reset** for an existing verified Customer. For existing users registered exclusively by email, `normalized_mobile` is null and the SMS reset option cannot work until a separate verified-mobile linking workflow is implemented. Avoid trying the wrong purpose.
5. Make one request; read the .NET terminal, not the browser's generic `202`. If `OTP_REQUEST_SKIPPED`, fix the indicated prerequisite; if `OTP_REQUEST_DISPATCH_STARTED` followed by `OTP_SMS_PROVIDER_REJECTED`, inspect provider dashboard/API rights/template; if `OTP_SMS_TRANSPORT_FAILED`, inspect TLS/DNS/network; if `OTP_SMS_PROVIDER_ACCEPTED`, confirm provider delivery and handset inbox separately. Do **not** automatically retry immediately because sending can incur charges and a provider timeout does not prove it did not submit.
6. If results still disagree, share only sanitized log event names/reason tokens and HTTP status, never API key, OTP, full mobile or raw provider response.

**No assertion of successful delivery has been made.** Remain on Step 09 until operator evidence, email recovery and security hardening acceptance are complete.


### Step 09 — Customer OTP Login Regardless of Password (2026-10-13)

**Owner-approved behavior:** Verified Customer users may sign in using either password or SMS OTP even after creating or resetting a password. Admins (Level 1/2) and MFA-enabled users must not bypass MFA using SMS OTP. Both OTP issuance and session creation enforce role/MFA restrictions. No database migration is required for this change.

**Implementation:** The password-null restriction was removed from `IdentityPhoneService.RequestAsync` and `IdentitySessionService.CreatePhoneSessionAsync`; the OTP request also rejects MFA-enabled accounts. `IdentityPhoneOtpTests` now exercises OTP and password access for a password-bearing Customer, one-time replay and MFA denial. Existing email/password and administrative MFA behavior remain unchanged.

**Acceptance:** Pull the branch, build, run `dotnet test ArginTaxPayer.sln -c Release --filter FullyQualifiedName~IdentityPhoneOtpTests`, then run the same suite with `RUN_ARGINTAXPAYER_PG_INTEGRATION=1` against the confirmed `argintaxpayer_test` PostgreSQL database. Test the two login methods from the unified two-tab browser UI. Do not enable live Ghasedak except for the operator-owned test recipient. Step 09 remains verification-pending.

### Step 09 — UX, Administrator Customer Onboarding, and Reusable Development Runner (2026-10-13)

**Owner-requested UI fix:** On successful password/OTP/MFA login, the unified `identity-flow.tsx` keeps the submit button disabled and displays `در حال ورود به پنل...` through the Next.js `router.replace("/account")` transition. The form cannot be double-submitted in the small interval after successful API authentication but before the protected dashboard renders. On error the button becomes available again. Code and frontend regression test committed; browser timing still requires operator verification.

**Level 1/Level 2 manual account onboarding:** A new **administrative Customer creation** workflow accepts required normalized Iranian phone and password, with optional email. It does **not** require SMS OTP. Access requires an authenticated, active Level 1 or Level 2 administrator with enabled TOTP MFA. Authorization is enforced server-side with current database roles and MFA, not merely by hiding the form. The API `POST /api/dev/identity/admin/customers` accepts only `Mobile`, `Email`, `Password`, never a requested role. It always creates the Customer role; unique mobile/email conflicts and validation fail without privilege elevation. The same-origin Next.js BFF route `POST /api/identity/admin/customers` forwards a protected HttpOnly-cookie authenticated request, and the form is shown on `/account` only for Level 1/2. The operator must check real-world phone ownership out-of-band: admin-created mobile is **trusted as verified** without SMS, which is an intentional privileged bypass, not proof of actual SIM possession. Do not introduce a public unauthenticated bypass. The current workflow creates Customer users, **not** administrative Level 1/2 accounts; promotion to Level 2 still requires the existing Level 1 grant and MFA safeguards. Manual creation logs a non-sensitive event, but a durable operator/taxpayer audit trail must be completed before Production. No schema migration for this route.

**Shared local developer commands (macOS, all subsequent phases):**

First-time setup:
```bash
cp .env.dev.local.example .env.dev.local
chmod 600 .env.dev.local
# Edit .env.dev.local with the private dedicated test Postgres ConnectionStrings__ArginTaxPayerDb value.
# Keep .env.ghasedak.local with Ghasedak__ApiKey, TaxPayerActivationCode,
# Ghasedak__TestRecipientMobile and explicit Ghasedak__EnableLiveSending=false/true.
# Set stable Identity__MfaEncryptionKey in .env.dev.local when testing admin MFA.
```
Do not print or commit the credential files. The project root `.gitignore` ignores `.env.*`.

Every day/step after setup, from repo root:
```bash
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
zsh scripts/dev-down.zsh
```
The launcher starts the exact approved `AUTOSSH_GATETIME=0 autossh -M 0 -N` tunnel at local `127.0.0.1:15432` (with ServerAliveInterval 15, ServerAliveCountMax 3, ConnectTimeout 10, ExitOnForwardFailure, host `argindev@85.133.244.156`), followed by .NET API `127.0.0.1:15089`, Next.js `127.0.0.1:3000`. It loads `.env.dev.local` and `.env.ghasedak.local` in the very same child process environment, preserves `~/.argintaxpayer-test/otp-pepper`, preflights dedicated test DB and identity readiness, checks Ghasedak configuration booleans, identifies whether **real SMS is enabled**, and stores local logs in `~/.argintaxpayer-test/` with owner-only directory access. It never creates migrations, charges customers or triggers an SMS by itself. **AutoSSH in this runner uses BatchMode=yes, so SSH key-based authentication must be set up**; otherwise run the user's foreground AutoSSH command and then relaunch the runner. Do not run this development stack publicly or in Production.

**Operator smoke checklist:** (a) correct Build; (b) `dotnet test` baseline; (c) PostgreSQL tests with `RUN_ARGINTAXPAYER_PG_INTEGRATION=1` only after checking the exact DB name; (d) Login button stays busy/disabled until navigation; (e) Level 1/2 with MFA sees Customer creation form; (f) required phone + password, optional email creates a Customer without sending SMS; (g) normal Customer cannot call the endpoint, return 403; (h) invalid/duplicate phone/email denied; (i) previously existing email/phone login, Customer SMS OTP, TOTP admin MFA and role policies remain intact; (j) dev-up, dev-status and dev-down verify usable workflow.

**Status:** Code committed but no real macOS/Ubuntu Build, browser acceptance or positive Level 1/2 provisioning integration evidence has been independently observed. Pending Step 09 acceptance/security debt remains unchanged. A five-day completion target for the entire roadmap is an owner schedule objective, **not an evidenced commitment that all 18 phases or production-hardening gates can be safely completed in that period**. Prioritize gating on runnable tests and correctly scoped deliverables, not skipping validations.

### Step 09 — Duplicate Registration and Precise OTP Retry UX (2026-10-13)

**Owner-reported defects and implemented fix:**
1. For a mobile already present in `identity_users`, the Development/loopback OTP registration request returns `409 Conflict` with `mobile_already_registered` instead of falsely returning 202 and showing the code-entry screen. The Persian RTL UI displays `این شماره همراه قبلاً ثبت‌نام شده است. از تب ورود استفاده کنید.`. Note: this explicit existence disclosure is **Development-only**; future public registration must be evaluated for account-enumeration resistance before production release.
2. If the operator requests another code to the same mobile after a delivered OTP (including after entering an incorrect code and returning to edit the phone), the API returns `429` with calculated `retryAfterSeconds`. The cooldown is now **three minutes from the last provider-accepted OTP request**, with dynamic remaining time, e.g. `03:00`, then `02:30`. The Next.js same-origin BFF passes the bounded retry field without exposing secrets; the UI remains on the phone-entry step, renders `کاربر گرامی، لطفاً پس از MM:SS مجدداً تلاش کنید.` and disables the request button while the countdown runs. Editing the identifier resets the local timer; the server independently enforces the actual cooldown.
3. Failed provider dispatch returns `503` and **does not create a successful-send cooldown**. Every attempt still counts toward the existing 5-attempts/15-minute abuse budget; the UI never navigates to code entry on 409/429/503. An HTTP 202 now represents accepted dispatch by the local SMS adapter, not verified handset delivery.

**Regression evidence prepared, not claimed executed:** `IdentityPhoneOtpTests` covers registered-number conflict, immediate 180-second cooldown, approximately 150 seconds after a simulated 30-second passage, valid/invalid/replayed codes and reset/login. `apps/web/tests/identity-ui.test.mjs` guards 409/429 propagation and the countdown. The no-live-SMS HTTP smoke now correctly expects 503, not the older misleading 202. The macOS/Ubuntu builds, PostgreSQL integration tests, and manual browser scenarios require operator verification.

**Local acceptance (same shared developer runner):**
```bash
git switch phase/03-identity-authorization
git pull --ff-only origin phase/03-identity-authorization
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
(cd apps/web && npm test && npm run typecheck && npm run build)
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
```
Confirm `current_database() = 'argintaxpayer_test'` before DB integration; use `RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~IdentityPhoneOtpTests`. Test a registered Customer's mobile in the registration tab (should show conflict, no code entry and no SMS), and one personally owned, unregistered test mobile (first accepted OTP, then wrong code, back to phone, immediate retry yields countdown; after 30 seconds it should decrease). Real SMS only with explicit restricted private Ghasedak configuration; it may incur provider charges. No database migration required for this fix.

**Scope caveat:** The existing 15-minute request budget remains; if five attempts are recorded, the displayed remaining time can exceed three minutes based on that longer control. OTP expiration is three minutes independently of the resend cooldown. Do not mark Phase 03 Step 09 Owner Approved until live/negative UI and security tests are verified.

## 5. Communication Convention

The **repository phase plan and all step completion records are written in English only**. When implementing or reporting on a step in the chat, present its title in **both Persian and English** (for example: `گام ۱ — زیرساخت مدل هویت و ایجاد مدیر اولیه | Step 01 — Identity Data Model and Bootstrap`). Explain execution and acceptance in concise, right-to-left Persian, while keeping code and shell commands left-to-right. Do not change the planned English step title without an approved plan amendment.

## 6. Phase Closure

After Step 11 is independently verified and owner-approved, record final test evidence, CI, migration and security review in this file. Merge, immutable tag, GitHub Release, and deployment remain separate, explicitly authorized gates. Phase 04 begins under its own single-file plan.
