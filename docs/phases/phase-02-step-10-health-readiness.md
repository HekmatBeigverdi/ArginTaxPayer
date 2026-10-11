# Phase 02 — Step 10: Health, Liveness and PostgreSQL Readiness

Date: 2026-10-11
Branch: `phase/02-foundation`
Status: SOURCE COMMITTED; OPERATOR MAC/VPS ACCEPTANCE PENDING

## Scope

Adds two public minimal-health endpoints to the existing ASP.NET Core API while keeping `GET /` behavior unchanged:

- `GET /health/live`: cheap process-only health, HTTP 200 with `{"status":"Healthy"}`; **never** depends on PostgreSQL.
- `GET /health/ready`: checks PostgreSQL reachability through the registered EF Core context. HTTP 200 when reachable, HTTP 503 when missing/unavailable. Returns **only** `{"status":"Healthy"}` or `{"status":"Unhealthy"}` without exception/connection details.
- `DatabaseReadinessHealthCheck`: scoped check using EF Core `CanConnectAsync`; handles ordinary database failures as unhealthy. It does not create schema, apply migrations, or modify data.
- `ApiHealthEndpointTests`: in-process ASP.NET Core integration tests via Microsoft.AspNetCore.Mvc.Testing. Verify liveness, fail-closed readiness without configuration and bootstrap compatibility.

Production requires a configured PostgreSQL connection at service startup according to the Step 06 policy. For cluster deployments, do not expose diagnostics beyond the proxy's trusted monitoring boundary; consider adding access restrictions and rate limiting at the infrastructure perimeter.

### Key behavior

Readiness answers **connectivity only**, not database schema/migration freshness, fiscal authority availability, queues, signing key custody, tenant authorization, or financial correctness. It does not replace dedicated migration/read-model checks. Liveness should not restart healthy API processes merely because the DB becomes unavailable.

## Preconditions and safety

- .NET SDK compatible with repository `global.json` (9.0.202/latestPatch).
- Restore will fetch `Microsoft.AspNetCore.Mvc.Testing` 9.0.4 for in-memory API tests.
- macOS: Git, existing project checkout, optional SSH tunnel to VPS PostgreSQL for manual readiness (not needed for unit tests).
- VPS: Git, .NET SDK, running PostgreSQL 17 Docker container for manual readiness.
- Adminer is *optional*, used only for visual database inspection. A missing Adminer does not fail any Step 10 test.
- Keep database port 5432 and Adminer port 8081 bound to VPS loopback only. Use the established macOS SSH tunnel.
- Never publish connection passwords in repo, committed files, console transcript or issue trackers.
- The readiness check uses `CanConnectAsync` and does not modify `argintaxpayer_dev` or `argintaxpayer_test`.

## macOS validation: source and in-memory API tests

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
  --filter FullyQualifiedName~ApiHealthEndpointTests \
  --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Debug --no-build
```

If the package download fails, resolve the NuGet network/mirror issue; do not disable tests.

### macOS: live readiness against VPS database

Keep SSH tunnel open in a separate macOS terminal:

```bash
ssh -N -o ExitOnForwardFailure=yes -o ServerAliveInterval=30 \
  -L 127.0.0.1:15432:127.0.0.1:5432 \
  -L 127.0.0.1:18081:127.0.0.1:8081 \
  argindev@YOUR_VPS_IP
```

In a second macOS terminal verify connectivity, then configure only the currently active shell session:

```bash
nc -vz 127.0.0.1 15432
read -rs "PGPASS?PostgreSQL password: "; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=15432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src/ArginTaxPayer.Api --urls http://127.0.0.1:5088
```

In **a third macOS terminal**:

```bash
curl -i http://127.0.0.1:5088/
curl -i http://127.0.0.1:5088/health/live
curl -i http://127.0.0.1:5088/health/ready
```

Expected HTTP 200 for all three when PostgreSQL is reachable. If the DB is unavailable, `/health/live` stays HTTP 200 while `/health/ready` becomes HTTP 503. Terminate the API with Ctrl+C, then `unset ConnectionStrings__ArginTaxPayerDb` in the launching shell.

## Ubuntu VPS validation

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
  --filter FullyQualifiedName~ApiHealthEndpointTests \
  --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Release --no-build
```

Check the running PostgreSQL container without Compose's password interpolation:

```bash
sudo docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
PG_ID=$(sudo docker ps -q --filter label=com.docker.compose.service=postgres | head -n 1)
test -n "$PG_ID" || { echo "Postgres container missing"; exit 1; }
sudo docker exec "$PG_ID" psql -U argintaxpayer -d argintaxpayer_test -tAc "SELECT current_database();"
```

Start the API on Ubuntu **only on loopback** (no public port):

```bash
read -rsp "PostgreSQL password: " PGPASS; echo
export ConnectionStrings__ArginTaxPayerDb="Host=127.0.0.1;Port=5432;Database=argintaxpayer_test;Username=argintaxpayer;Password=${PGPASS};SSL Mode=Prefer"
unset PGPASS
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/ArginTaxPayer.Api --urls http://127.0.0.1:5088
```

In a **second VPS terminal**:

```bash
curl -i http://127.0.0.1:5088/
curl -i http://127.0.0.1:5088/health/live
curl -i http://127.0.0.1:5088/health/ready
```

Stop the API with Ctrl+C. The above commands do not create migrations or alter the DB.

## Adminer (optional)

If previously installed, on VPS:

```bash
sudo docker ps --filter name=argintaxpayer-adminer
```

If stopped:

```bash
sudo docker start argintaxpayer-adminer
```

On Mac, with the SSH 18081 tunnel open, visit `http://127.0.0.1:18081`. Use System PostgreSQL, Server `postgres`, User `argintaxpayer`, Database `argintaxpayer_test`, and the existing database password. The only expected initial schema artifact is the Step 07 `__EFMigrationsHistory` table; this step creates none.

## Acceptance

- In-process health endpoint tests pass on macOS and Ubuntu.
- Existing bootstrap endpoint remains HTTP 200.
- With reachable configured PostgreSQL, `/health/live` and `/health/ready` return HTTP 200.
- Without PostgreSQL configuration in Development, liveness is 200 but readiness is 503.
- No connection secrets/exceptions in HTTP health bodies; no schema changes.
- No automatic merge to main, release tag or advance to Step 11 until user approval.

**GitHub source commits do not prove local or VPS execution**; collect terminal output for operational acceptance.
