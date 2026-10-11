# Phase 02 — Step 07: Controlled EF Core Migration Infrastructure

Date: 2026-10-11
Status: COMMITTED / EXECUTION ON MAC AND VPS REQUIRES OPERATOR VERIFICATION
Branch: `phase/02-foundation`

## Intent and changes

Establish an explicit Npgsql/EF Core 9 design-time context factory, a reproducible initial (empty) migration, a model snapshot, and metadata tests. No commercial, taxpayer, subscription, or settlement tables are invented before their schema reviews.

- `ArginTaxPayerDesignTimeDbContextFactory` requires `ConnectionStrings__ArginTaxPayerDb` to be set explicitly. The connection value is not stored or logged.
- `20261011000000_InitialBaseline` has intentionally empty Up/Down methods. Running it creates/updates the EF migrations history ledger, not any tenant data tables.
- Context and snapshot remain empty until a reviewed physical model is introduced. Migration execution is **manual only**: neither API nor Workers calls `Database.Migrate()` or `EnsureCreated()`.
- The Infrastructure and API startup projects reference EF Design 9.0.4 while retaining the existing Npgsql 9.0.4 provider; use a compatible `dotnet-ef` CLI from the 9.0.x train.
- Smoke tests inspect migration metadata without opening a PostgreSQL connection.

## Mandatory separated test database

Use `argintaxpayer_test` as a distinct PostgreSQL database. Do not run this step's `database update` command against `argintaxpayer_dev`, let alone a production database.

On the VPS, locate the running PostgreSQL container without Compose interpolation:

```bash
PG_ID=$(sudo docker ps -q --filter label=com.docker.compose.service=postgres | head -n 1)
test -n "$PG_ID" || { echo 'Postgres container unavailable'; exit 1; }
sudo docker exec "$PG_ID" psql -U argintaxpayer -d postgres -tAc \
  "SELECT 1 FROM pg_database WHERE datname='argintaxpayer_test';"
```

If no row is returned, create the **empty test database** once:

```bash
sudo docker exec "$PG_ID" createdb -U argintaxpayer argintaxpayer_test
```

Database creation does not remove existing data or volumes. Keep test and development databases distinct. For hardened setups, later introduce dedicated non-superuser roles and least-privilege migrations.

## macOS workflow using the already-established SSH tunnel

In one macOS terminal, keep the tunnel open:

```bash
ssh -N -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  argindev@YOUR_VPS_IP
```

In another terminal, from the project root:

```bash
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build
```

Install a compatible EF Core CLI tool if absent:

```bash
dotnet tool install --global dotnet-ef --version 9.0.4
# Or: dotnet tool update --global dotnet-ef --version 9.0.4
dotnet ef --version
```

Enter the known local PostgreSQL password without putting it in Git:

```bash
read -rs "PGPASS?PostgreSQL test password: "; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
```

Inspect the targeted database and pending migrations:

```bash
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
```

Only after explicitly confirming the database name is `argintaxpayer_test`:

```bash
dotnet ef database update --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --context ArginTaxPayerDbContext
```

Then verify in Adminer (`argintaxpayer_test`) or with `psql`:

```sql
SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
```

Expect exactly the baseline migration ID on a new test database; no business tables. Re-running `database update` should do nothing when already applied.

## VPS workflow

Use `dotnet restore/build/test` on the Ubuntu host as for previous steps. For EF commands on VPS, set `ConnectionStrings__ArginTaxPayerDb` to `Host=127.0.0.1;Port=5432;Database=argintaxpayer_test;...` in the **same terminal**, after sourcing the database secret securely. Running manual migration from either macOS or VPS is sufficient; do not run both concurrently.

## New migrations and safe review

When physical models are approved in later steps, create a new migration:

```bash
dotnet ef migrations add MeaningfulMigrationName \
  --project src/ArginTaxPayer.Infrastructure \
  --startup-project src/ArginTaxPayer.Api \
  --context ArginTaxPayerDbContext
```

Review the generated Up, Down and snapshot changes before applying anything. Never blindly accept destructive operations such as DropTable, DropColumn or data loss. For production, deploy reviewed SQL migration scripts/bundles with a backup and change approval rather than letting API startup modify schema.

## Validation boundaries

Unit/metadata tests and source checks do not prove real PostgreSQL integration or successful migration application. Human-operated test DB execution is required for those claims. No automatic migration on `argintaxpayer_dev` or production. No Git merge, release or Step 08 work.
