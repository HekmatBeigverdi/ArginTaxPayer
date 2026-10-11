# Phase 02 — Step 08: Persistence & Unit of Work

Date: 2026-10-11
Branch: `phase/02-foundation`
Status: IMPLEMENTED IN SOURCE — LOCAL/VPS VERIFICATION PENDING

## Scope

This step adds an Application-owned `IUnitOfWork` abstraction and a scoped EF Core / Npgsql adapter. It does **not** add taxpayer business entities, tenant schemas, automatic startup migrations, business repositories, or implicit database creation.

- `src/ArginTaxPayer.Application/Abstractions/Persistence/IUnitOfWork.cs`: application boundary exposing `SaveChangesAsync` and `ExecuteInTransactionAsync`.
- `src/ArginTaxPayer.Infrastructure/Persistence/EfUnitOfWork.cs`: implementation using the scoped `ArginTaxPayerDbContext`.
- `PersistenceRegistration.cs`: registers `IUnitOfWork` scoped **only** when database configuration is present; preserves no-database development bootstrap and production fail-closed behavior.
- `UnitOfWorkRegistrationTests.cs`: registration, scope and backward-compatibility tests.
- `UnitOfWorkPostgresIntegrationTests.cs`: opt-in database smoke test; only allowed on the explicitly named `argintaxpayer_test` database.

## Transaction semantics

`SaveChangesAsync` persists current tracked changes using EF defaults. `ExecuteInTransactionAsync` creates an EF execution strategy, starts an explicit transaction inside its delegate, invokes the caller's database-only callback, saves pending changes, and commits on success. Exceptions propagate, and disposal rolls back uncommitted transactions.

**Critical:** Npgsql transient retries can replay the entire operation callback. Only database operations safe to replay may occur inside it. Do not perform gateway payments, email/Telegram sends, HTTP requests, file operations or publish non-transactional events in this callback. Future integration uses an idempotent transactional outbox, explicit unique constraints, request idempotency keys and recovery for unknown-commit outcomes. This generic primitive is not itself an idempotency guarantee.

Use a single DI scope per logical request/job. `DbContext` and `IUnitOfWork` are not thread-safe; do not use them concurrently or retain them in singleton services. An explicit transaction does not make multi-tenant authorization safe; tenant scoping and financial invariants are separate follow-on work.

## Preconditions

### macOS
- .NET 9 SDK matching repository `global.json` (9.0.202 / permitted same-band patch), VS Code, Git.
- Clone and checkout `phase/02-foundation`.
- VPS PostgreSQL and (for DB integration testing) an SSH tunnel on macOS from local 15432 to VPS loopback 5432.
- The dedicated `argintaxpayer_test` database from Step 07.
- Adminer is **optional** and for visual inspection only; it must not be used as an application dependency.
- `dotnet-ef` CLI 9.0.4 is only required for Step 07 migrations, **not** for Step 08 unit tests.

### VPS
- Ubuntu + .NET SDK compatible with 9.0.202, Git, running PostgreSQL 17 Docker container.
- The dedicated `argintaxpayer_test` database; do not run tests against `argintaxpayer_dev`.
- Adminer (if installed) is accessible via its local port 8081; do not publish it publicly.
- Docker Compose commands require `ARGIN_POSTGRES_PASSWORD` even for `logs` and `exec`, due to environment interpolation. Direct `docker exec` does not.

## macOS validation (without a database)

```bash
cd ~/Projects/ArginTaxPayer
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
dotnet --version
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Debug --no-restore
dotnet test ArginTaxPayer.sln -c Debug --no-build
```

The integration smoke test is inert by default. Build and unit tests must not require a tunnel or Adminer.

## macOS PostgreSQL smoke test (optional but required for full operational acceptance)

Keep the SSH port forward active in another macOS terminal:

```bash
ssh -N -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  -L 127.0.0.1:18081:127.0.0.1:8081 \
  argindev@YOUR_VPS_IP
```

In a new macOS zsh terminal:

```bash
cd ~/Projects/ArginTaxPayer
read -rs "PGPASS?PostgreSQL password: "; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
"$(brew --prefix libpq)/bin/psql" -h 127.0.0.1 -p 15432 \
  -U argintaxpayer -d argintaxpayer_test -W \
  -tAc "SELECT current_database();"
```

The `psql` command must return exactly `argintaxpayer_test`. It does **not** accept Npgsql's semicolon-delimited connection string. Then:

```bash
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln \
  -c Debug --no-build --filter FullyQualifiedName~UnitOfWorkPostgresIntegrationTests
```

Unset the connection when finished:

```bash
unset ConnectionStrings__ArginTaxPayerDb
```

## Ubuntu VPS validation

```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
dotnet --version
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build
```

Optional PostgreSQL integration test on VPS, in **bash**:

```bash
read -rsp 'PostgreSQL test password: ' PGPASS; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=5432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
RUN_ARGINTAXPAYER_PG_INTEGRATION=1 dotnet test ArginTaxPayer.sln \
  -c Release --no-build --filter FullyQualifiedName~UnitOfWorkPostgresIntegrationTests
unset ConnectionStrings__ArginTaxPayerDb
```

Do not run the macOS and VPS integration tests concurrently during initial verification. No need to reapply the Step 07 migration for this step.

## Adminer verification

Adminer is optional for Step 08. If needed, keep the macOS 18081 SSH forward open, browse to `http://127.0.0.1:18081`, and sign in with PostgreSQL, server `postgres`, user `argintaxpayer`, target database `argintaxpayer_test`. Confirm the `__EFMigrationsHistory` baseline from Step 07. Step 08 intentionally adds no tables, no columns, and no migrations. Check the target database carefully before writing SQL.

## Acceptance and limits

To mark this step verified, record:
1. macOS `dotnet build` and `dotnet test` results.
2. VPS `dotnet build` and `dotnet test` results.
3. At least one opt-in integration run against `argintaxpayer_test`, with no credentials in logs.
4. No schema changes beyond Step 07 baseline.

These operations have **not** been executed by the GitHub editing tool; source commits alone do not prove the code builds or that the remote database is available. Do not merge to main, create release tags, or start Step 09 before the user confirms.
