# Phase 02 — Step 09: Taxpayer Scope Foundation

Date: 2026-10-11
Branch: `phase/02-foundation`
Status: SOURCE COMMITTED — MAC/VPS BUILD AND TEST VERIFICATION PENDING
Depends on: Step 08 accepted by owner.

## Purpose and boundary

Prepare the modular-monolith dependency-injection boundary for a **single pre-authorized taxpayer** per API request or background-job scope. This is infrastructure for later tenant-isolated EF Core access, not a claim that tenant authorization is implemented. Authentication, user-to-taxpayer assignment, ownership verification and server-enforced EF query scoping belong to later phases. Never accept a taxpayer ID from an HTTP header or request body without checking actor grants.

This step intentionally adds **no database entities, migrations, migrations execution, authorization middleware, new endpoints, or startup database requirements**. Previous API and worker bootstrap behavior remains unchanged.

## Files

- `src/ArginTaxPayer.Application/Abstractions/Tenancy/ITaxpayerScope.cs` — read-only taxpayer contract plus a trusted initialization interface.
- `src/ArginTaxPayer.Application/Tenancy/TaxpayerScope.cs` — fail-closed read, single-shot initialization, rejects empty GUID and reinitialization.
- `src/ArginTaxPayer.Application/DependencyInjection.cs` — scoped registration of one shared object under two interfaces, respecting existing TryAdd behavior.
- `tests/ArginTaxPayer.Tests/TaxpayerScopeTests.cs` — uninitialized failure, immutable selection, independent request/job scopes.

## Semantic constraints

1. Resolve taxpayer ID with `ITaxpayerScope.RequireTaxpayerId()` only *after* a trusted upstream entry point verified user grants. Uninitialized access throws.
2. Only trusted code should resolve `ITaxpayerScopeInitializer`; registration alone does not enforce trusted callers. Future phase authorization infrastructure must own it.
3. One DI scope = one selected taxpayer. A new job or request gets a new scope; do not retain this state in singletons or static fields.
4. Root/Level 1 operations spanning several taxpayers must use a separately authorized administrative workflow, not mutate or bypass the single-taxpayer scope.
5. This step **does not isolate PostgreSQL rows**: no tenant FK/index, DbContext global query filter or Row-Level Security exists yet. Add structural negative isolation tests before exposing taxpayer data.
6. Never infer Level 2 access merely from this scope having a valid GUID.

## Verification prerequisites

### macOS
- Git and compatible .NET 9 SDK (repository global.json pins 9.0.202 + latestPatch).
- Existing checkout at `~/Projects/ArginTaxPayer` (adjust if different).
- PostgreSQL, SSH tunnel and Adminer are NOT needed to run Step 09 unit tests; no DB writes occur.

### Ubuntu VPS
- Same compatible .NET SDK and Git.
- Same branch checkout at `~/projects/ArginTaxPayer`.
- Docker/PostgreSQL/Adminer may remain running from Steps 07 and 08; they are NOT needed for this step's source tests.

## macOS steps

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
  --filter FullyQualifiedName~TaxpayerScopeTests --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Debug --no-build
```

Verify API bootstrap remains operational (run in one macOS terminal, curl in another):

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/ArginTaxPayer.Api
# A separate terminal: use the actual port displayed by the running API
curl -i http://127.0.0.1:5000/
```

Do not claim port 5000 unless the application's output confirms it.

## Ubuntu VPS steps

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
  --filter FullyQualifiedName~TaxpayerScopeTests --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Release --no-build
```

Do not open API/database ports publicly to run this test.

## Optional PostgreSQL/Adminer health checks (unchanged from Step 08)

PostgreSQL is used by future tenant row-scoping steps, **not Step 09**. If the operator wishes to confirm the remote DB is still available:

```bash
sudo docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
PG_ID=$(sudo docker ps -q --filter label=com.docker.compose.service=postgres | head -n 1)
test -n "$PG_ID" || { echo "PostgreSQL container is not running"; exit 1; }
sudo docker exec "$PG_ID" psql -U argintaxpayer -d argintaxpayer_test \
  -tAc "SELECT current_database();"
```

Adminer, if already installed, is optional. Keep both PostgreSQL/8081 ports loopback-only on VPS. A Mac-local SSH tunnel can forward 18081 to VPS 8081 and show `http://localhost:18081`. Use the test DB `argintaxpayer_test`. Expect no schema change and only the Phase 02 Step 07 initial migration in `__EFMigrationsHistory`.

## Acceptance

- Unit tests pass on macOS and Ubuntu, including `TaxpayerScopeTests`.
- An uninitialized taxpayer scope fails closed; a scope cannot be reassigned; two DI scopes stay independent.
- Existing bootstrap API is not affected.
- No migrations, DB schema changes, or Adminer steps are required for this step.
- Source review alone is not runtime acceptance; operator must return test results.
- Do not proceed to Step 10, merge main or create a release until explicit signoff.
