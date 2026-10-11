# Phase 02 — Step 13: Scoped Idempotency Reservation Foundation

Date: 2026-10-11
Branch: `phase/02-foundation`
Status: IMPLEMENTED IN GITHUB — MACOS / UBUNTU VPS VERIFICATION REQUIRED
Precondition: Step 12 approved by project owner.

## Purpose

Establish a tenant-scoped **database uniqueness boundary** for future replay-safe financial, fiscal and subscription workflows. This step introduces a transaction-compatible reservation record and an Application-owned write contract. It is **not yet an end-to-end idempotent request executor**, a request replay handler, a payment callback handler, or an invoice submission service.

## New code

- `Application/Abstractions/Idempotency/IIdempotencyReservationWriter.cs`: reservation write contract.
- `Infrastructure/Persistence/Idempotency/IdempotencyReservation.cs`: PostgreSQL persistence entity.
- `Infrastructure/Persistence/Idempotency/EfIdempotencyReservationWriter.cs`: stages reservations on the current scoped EF DbContext. Requires an initialized, previously **authorized** taxpayer scope; does not connect, commit, send messages or create transactions.
- `Persistence/ArginTaxPayerDbContext.cs`: `DbSet`, model mapping and unique index.
- `Persistence/PersistenceRegistration.cs`: scoped DI registration only when a database connection is configured.
- `Persistence/Migrations/20261011180000_AddIdempotencyReservations.cs`: forward/backward migration.
- `Persistence/Migrations/ArginTaxPayerDbContextModelSnapshot.cs`: updated model.
- `IdempotencyFoundationTests`: mapping, migration discovery, staged writes, negative tenant scoping and invalid fingerprint rejection.
- `IdempotencyPostgresIntegrationTests`: opt-in real PostgreSQL unique-key test, with rollback-only transaction.

## Database contract

`idempotency_reservations` schema:
- `id` UUID primary key;
- `taxpayer_id` UUID non-null;
- `operation` varchar(100) non-null;
- `idempotency_key` varchar(200) non-null;
- `request_fingerprint` varchar(64) non-null (SHA-256 hex digest representation; canonical hashing algorithm is a later domain decision);
- `created_at_utc` timestamptz non-null.

The unique index `ux_idempotency_taxpayer_operation_key` on `(taxpayer_id, operation, idempotency_key)` ensures a repeated key for the **same taxpayer and operation** triggers a PostgreSQL unique-violation error. It does not prevent a different key for an identical payload, compare conflicting fingerprints automatically, cache prior results, or resolve a commit outcome lost in transit.

A complete future application-layer protocol must compare fingerprints, define in-flight/pending/completed states, atomicity with domain and outbox changes, replayable result storage, recovery from unknown commit, TTL/retention, rate limits, and domain-specific idempotency (payment settlement, fiscal submission). Never treat a valid correlation ID as an idempotency key. Never persist credentials, fiscal signing keys, or sensitive payloads in the request fingerprint; store only a digest.

## Safety and deployment

No migration is auto-applied by API or Workers. Test the migration only on **`argintaxpayer_test`**; leave `argintaxpayer_dev` and Production untouched pending explicit review. This migration adds one table and one unique index and does not modify `outbox_messages`.

## macOS prerequisites

- Git and .NET SDK compatible with repository `global.json` (`9.0.202` plus supported patch).
- An existing checkout `~/Projects/ArginTaxPayer` (adjust for custom path).
- For unit tests: NO PostgreSQL, Adminer, Docker, SSH tunnel or `psql` required.
- For database integration: VPS PostgreSQL 17 running; separate `argintaxpayer_test` from previous steps; SSH tunnel forwarding Mac `15432` to VPS loopback `5432`; `dotnet-ef` version 9.0.x; `libpq` client installed via Homebrew. Adminer is optional.

### macOS commands: checkout, compile, tests

```bash
cd ~/Projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
dotnet --version
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Debug --no-restore
dotnet test ArginTaxPayer.sln -c Debug --no-build \
  --filter FullyQualifiedName~IdempotencyFoundationTests \
  --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Debug --no-build
```

### macOS commands: dedicated database & migration

On **a separate Mac Terminal**, keep SSH tunneling active:

```bash
ssh -N -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  -L 127.0.0.1:18081:127.0.0.1:8081 \
  argindev@YOUR_VPS_IP
```

On a **second Mac Terminal**, confirm the target database via libpq-format arguments (not the Npgsql semicolon connection string):

```bash
cd ~/Projects/ArginTaxPayer
"$(brew --prefix libpq)/bin/psql" \
  -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test \
  -W -tAc "SELECT current_database();"
```

Require exactly `argintaxpayer_test`. Then enter password in zsh without printing it:

```bash
read -rs "PGPASS?PostgreSQL test password: "; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
```

Inspect migration status:

```bash
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure \
  --startup-project src/ArginTaxPayer.Api \
  --context ArginTaxPayerDbContext
```

Expected: `20261011000000_InitialBaseline`, `20261011120000_AddOutboxMessages` applied, and `20261011180000_AddIdempotencyReservations` pending.

**Only after database name confirmed**:

```bash
dotnet ef database update --project src/ArginTaxPayer.Infrastructure \
  --startup-project src/ArginTaxPayer.Api \
  --context ArginTaxPayerDbContext
```

Then run opt-in PostgreSQL uniqueness check:

```bash
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln \
  -c Debug --no-build \
  --filter FullyQualifiedName~IdempotencyPostgresIntegrationTests \
  --logger "console;verbosity=normal"
```

Check applied migration and structure using `psql`:

```bash
"$(brew --prefix libpq)/bin/psql" \
  -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W \
  -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
"$(brew --prefix libpq)/bin/psql" \
  -h 127.0.0.1 -p 15432 -U argintaxpayer -d argintaxpayer_test -W \
  -c '\d idempotency_reservations'
```

Expect new migration applied, table and unique index present, and no test rows persisted after rollback. Unset connection string on completion:

```bash
unset ConnectionStrings__ArginTaxPayerDb
```

## Ubuntu VPS prerequisites

- Git; .NET SDK compatible with `9.0.202` in repo `global.json`.
- PostgreSQL 17 Docker container from earlier steps, bound to VPS localhost:5432.
- Existing `argintaxpayer_test`, previously migrated through Step 12.
- A separate Ubuntu shell for direct API/testing when needed.
- Adminer optional. `psql` installed on Ubuntu or use `docker exec ... psql`.
- Do NOT open PostgreSQL, API or Adminer ports publicly.

### Ubuntu: checkout, build, tests

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
dotnet --version
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build \
  --filter FullyQualifiedName~IdempotencyFoundationTests \
  --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Release --no-build
```

### Ubuntu: verify migrated database without Compose interpolation

```bash
sudo docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
PG_ID=$(sudo docker ps -q --filter label=com.docker.compose.service=postgres | head -n 1)
test -n "$PG_ID" || { echo "PostgreSQL container missing"; exit 1; }
sudo docker exec "$PG_ID" psql -U argintaxpayer \
  -d argintaxpayer_test -tAc "SELECT current_database();"
sudo docker exec "$PG_ID" psql -U argintaxpayer \
  -d argintaxpayer_test -c '\d idempotency_reservations'
```

After the Mac runs the migration, optionally execute the opt-in VPS integration test in Bash:

```bash
read -rsp "PostgreSQL test password: " PGPASS; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=5432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln \
  -c Release --no-build \
  --filter FullyQualifiedName~IdempotencyPostgresIntegrationTests \
  --logger "console;verbosity=normal"
unset ConnectionStrings__ArginTaxPayerDb
```

Do not concurrently run Mac and VPS integration tests during the initial check, or apply the same migration twice.

## Adminer (optional)

On VPS check the existing container:

```bash
sudo docker ps --filter name=argintaxpayer-adminer
```

If stopped, `sudo docker start argintaxpayer-adminer`. On Mac, keep the SSH forward on local port `18081`; visit `http://localhost:18081`.

Login: System PostgreSQL; Server `postgres`; username `argintaxpayer`; database `argintaxpayer_test`; password is the existing PostgreSQL secret.

Run these read-only queries:

```sql
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
SELECT indexname, indexdef FROM pg_indexes
WHERE tablename = 'idempotency_reservations';
SELECT COUNT(*) FROM idempotency_reservations;
```

The migration should appear, a composite unique index should exist, and on a fresh test database the count should be zero. Do not create customer data manually for this test.

## Acceptance and limitations

- Targeted and entire regression suites pass on macOS and VPS.
- Migration applies only to `argintaxpayer_test`.
- Opt-in PostgreSQL uniqueness test succeeds and rolls back.
- Unique scope is taxpayer + operation + key; no cross-tenant access or replay guarantee is claimed.
- Previous Step 12 outbox and health/bootstrap behaviors remain intact.
- No worker side effects, external payments, fiscal submissions, main merge or release tagging.
- Source changes are committed but no local/VPS runtime claims can be made until the operator returns outputs.
