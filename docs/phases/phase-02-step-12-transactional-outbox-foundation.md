# Phase 02 — Step 12: Transactional Outbox Foundation

Date: 2026-10-11
Branch: `phase/02-foundation`
Status: SOURCE COMMITTED — LOCAL/VPS BUILD AND DB ACCEPTANCE PENDING
Previous step: Step 11 approved by owner.

## Purpose and scope

Add a migration-managed, taxpayer-scoped durable outbox table and a write-side application abstraction. The outbox is intended for later reliable event dispatch with the **same EF DbContext and IUnitOfWork transaction** as business state updates.

**In this step there is NO background dispatcher, no sends to the tax authority, no payment fulfillment, no retries, no delivery acknowledgement and no promise of exactly-once delivery.** Consumers, leasing/claim concurrency, exponential backoff, dead-letter policy, event schemas and downstream idempotency are deferred to later approved phases. This foundation alone does NOT fully solve duplicate payment processing or cross-tenant access control.

## Files

- `Application/Abstractions/Messaging/IOutboxWriter.cs`: write-only contract; caller supplies a stable `messageId`, event type, serialized payload and deduplication key.
- `Infrastructure/Persistence/Outbox/OutboxMessage.cs`: persistence record.
- `Infrastructure/Persistence/Outbox/EfOutboxWriter.cs`: attaches a message to the shared DbContext; requires a previously validated `ITaxpayerScope`; **does not call SaveChanges**.
- `Persistence/ArginTaxPayerDbContext.cs`: mapping for `outbox_messages` and indexes.
- `Persistence/PersistenceRegistration.cs`: scopes `IOutboxWriter` when PostgreSQL is configured.
- `Persistence/Migrations/20261011120000_AddOutboxMessages.cs`: new reviewed migration.
- `Persistence/Migrations/ArginTaxPayerDbContextModelSnapshot.cs`: updated snapshot.
- `OutboxFoundationTests`: model, metadata, authorization-scope precondition and tracked-but-unsaved behavior.
- `OutboxPostgresIntegrationTests`: explicit opt-in database uniqueness test using a rollback-only transaction.

## Database changes

New `outbox_messages` table:
- `id` UUID PK
- `taxpayer_id` UUID NOT NULL
- `event_type` varchar(200) NOT NULL
- `payload_json` text NOT NULL
- `deduplication_key` varchar(200) NOT NULL
- `created_at_utc` timestamptz NOT NULL
- `processed_at_utc` timestamptz NULL
- `attempt_count` int NOT NULL default 0

Indexes: unique (`taxpayer_id`, `deduplication_key`) plus pending lookup index (`processed_at_utc`, `created_at_utc`).

This design assumes a later domain-level decision for immutable event payload schemas, replay semantics, encryption/redaction and retention. **Do not put taxpayer private keys, financial gateway secrets, sensitive certificate material or unnecessary PII in payloads.** The table is not a cross-tenant authorization mechanism; generic DbContext access must remain Infrastructure-internal and regulated by later authorization/query-scoping implementation.

Duplicate keys **raise a database exception**, not silently return an existing event. Callers must use stable keys and handle conflicts intentionally. For financial requests, independent end-to-end idempotency and atomic ledger invariants are still mandatory.

## Correct transaction boundary

Queue a message as part of the same scoped DbContext as its associated business update, then `IUnitOfWork.SaveChangesAsync`, or wrap database-only operations in `IUnitOfWork.ExecuteInTransactionAsync`. The writer itself only adds a tracked entity. Never send network requests or trigger external fiscal submissions inside a retryable transaction callback.

## Prerequisites

- macOS: .NET SDK compatible with `global.json` (9.0.202/allowed same-band patch), Git and an existing project checkout.
- Ubuntu VPS: same SDK, Git, Docker Engine, running PostgreSQL 17 and separate `argintaxpayer_test` database created in Step 07.
- Database integration requires a working password, and on macOS an SSH tunnel local 15432 -> VPS 5432.
- `dotnet-ef` CLI version 9.0.4 or a compatible 9.0.x release to inspect/apply migrations.
- Adminer is **optional** for visual table/unique-index inspection. It is not needed by code, build or automated tests.
- Run database update on **argintaxpayer_test ONLY**. Do not apply to `argintaxpayer_dev` or Production until reviewed and expressly authorized.
- Take an appropriate test DB snapshot/backup when necessary before altering shared test environments.

## macOS: checkout, build and unit tests (database not needed)

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
  --filter FullyQualifiedName~OutboxFoundationTests \
  --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Debug --no-build
```

The opt-in real DB test returns early without `RUN_ARGINTAXPAYER_PG_INTEGRATION=1`. An ordinary passing suite is **not** evidence the PostgreSQL outbox migration has been tested.

## macOS: database migration & integration

Keep a dedicated terminal on macOS running the SSH tunnel:

```bash
ssh -N -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  -L 127.0.0.1:18081:127.0.0.1:8081 \
  argindev@YOUR_VPS_IP
```

In a second macOS zsh terminal:

```bash
cd ~/Projects/ArginTaxPayer
"$(brew --prefix libpq)/bin/psql" -h 127.0.0.1 -p 15432 \
  -U argintaxpayer -d argintaxpayer_test -W \
  -tAc 'SELECT current_database();'
```

Require output **exactly** `argintaxpayer_test`. Set the Npgsql connection string in this shell:

```bash
read -rs "PGPASS?PostgreSQL test password: "; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
```

Inspect pending migrations:

```bash
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure \
  --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
```

After verifying the target database again, apply the new migration **once, from macOS**:

```bash
dotnet ef database update --project src/ArginTaxPayer.Infrastructure \
  --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
```

Run the opt-in DB integration test (unique index, transaction rollback):

```bash
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln \
  -c Debug --no-build \
  --filter FullyQualifiedName~OutboxPostgresIntegrationTests \
  --logger "console;verbosity=normal"
```

Verify tables/migration through psql:

```bash
"$(brew --prefix libpq)/bin/psql" -h 127.0.0.1 -p 15432 \
  -U argintaxpayer -d argintaxpayer_test -W \
  -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
```

Expect `20261011000000_InitialBaseline` and `20261011120000_AddOutboxMessages`.

```bash
unset ConnectionStrings__ArginTaxPayerDb
```

## VPS: checkout, build and unit tests

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
  --filter FullyQualifiedName~OutboxFoundationTests \
  --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Release --no-build
```

PostgreSQL container check without Compose secret interpolation:

```bash
sudo docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
PG_ID=$(sudo docker ps -q --filter label=com.docker.compose.service=postgres | head -n 1)
test -n "$PG_ID" || { echo "Postgres is not running"; exit 1; }
sudo docker exec "$PG_ID" psql -U argintaxpayer -d argintaxpayer_test \
  -tAc 'SELECT current_database();'
```

After applying the migration on macOS, optionally rerun the DB integration test from the VPS with a **separate shell** Npgsql connection pointing at `127.0.0.1:5432;Database=argintaxpayer_test` (no SSH needed from within VPS). Do **not** apply the same migration concurrently from both computers.

## Adminer

On Ubuntu check `sudo docker ps --filter name=argintaxpayer-adminer` and if stopped run `sudo docker start argintaxpayer-adminer`. With SSH forwarding from macOS, open `http://localhost:18081`, choose PostgreSQL, server `postgres`, username `argintaxpayer`, database `argintaxpayer_test`. Verify:
1. `__EFMigrationsHistory` contains new migration ID;
2. `outbox_messages` exists with unique index `ux_outbox_taxpayer_deduplication`;
3. no business tables were invented; this step does not dispatch any events.

## Acceptance gate

Require operator confirmation of:
1. macOS build and focused/regression tests passing.
2. VPS build and focused/regression tests passing.
3. `argintaxpayer_test` migration successful, with no changes to dev/prod.
4. opt-in PostgreSQL uniqueness test passing.
5. Adminer or psql inspection of outbox table/index.
6. No external fiscal gateway calls or Worker dispatch.

Do not merge to `main`, publish a release or start Step 13 before approval. Source commits alone are not proof of runtime acceptance.
