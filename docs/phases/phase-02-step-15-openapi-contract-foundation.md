# Phase 02 — Step 15: OpenAPI Contract Foundation

Date: 2026-10-11
Branch: `phase/02-foundation`
Status: SOURCE COMMITTED — MAC/VPS BUILD, TEST AND CI VERIFICATION PENDING
Prerequisite: Step 14 owner-approved.

## Scope

Add ASP.NET Core 9 OpenAPI document generation to the existing API as a developer contract baseline.

- `Microsoft.AspNetCore.OpenApi` v9.0.4 in the API project.
- `builder.Services.AddOpenApi()` with default document name `v1`.
- `app.MapOpenApi()` **only when** `app.Environment.IsDevelopment()`, at `GET /openapi/v1.json`.
- The existing bootstrap `GET /` is named/tagged for future client generation.
- Existing `GET /health/live` and `GET /health/ready` routes remain unchanged.
- `ApiOpenApiTests` checks Development document, Production absence (404), and bootstrap/liveness regression.
- No Swagger UI, no auth-bypassing demo endpoints, no migrations, no DB changes and no production publication of the API contract.

This baseline is not yet a versioned complete taxpayer-invoice schema or a substitute for authorization, endpoint policy review or explicit fiscal API specifications.

## Safety boundaries

The OpenAPI route is hidden in Production by default. Do **not** switch a public production server to Development simply to view documentation. No secrets, certificate keys, payment gateway values or actual taxpayer records belong in examples. Development environments exposed publicly require additional network/access controls; the endpoint's environment check alone is not an authentication control.

## Prerequisites

Common: Git, SDK compatible with `global.json` (currently SDK 9.0.202 with latestPatch), internet/NuGet access to restore `Microsoft.AspNetCore.OpenApi` 9.0.4, checkout of `phase/02-foundation`.

For build and `ApiOpenApiTests` **no PostgreSQL, SSH tunnel, Adminer, Docker, dotnet-ef or psql are required**. In-memory Production test uses only a syntactically valid fake PostgreSQL connection string and must not connect.

## macOS build, tests and manual endpoint check

Run in Terminal on Mac (adjust repo path as needed):

```bash
cd ~/Projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
dotnet --version
unset RUN_ARGINTAXPAYER_PG_INTEGRATION
unset ConnectionStrings__ArginTaxPayerDb
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Debug --no-restore
dotnet test ArginTaxPayer.sln -c Debug --no-build \
  --filter FullyQualifiedName~ApiOpenApiTests \
  --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Debug --no-build
```

Terminal 1:

```bash
cd ~/Projects/ArginTaxPayer
unset ConnectionStrings__ArginTaxPayerDb
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/ArginTaxPayer.Api --urls http://127.0.0.1:5088
```

Terminal 2:

```bash
curl -i http://127.0.0.1:5088/openapi/v1.json
curl -i http://127.0.0.1:5088/
curl -i http://127.0.0.1:5088/health/live
curl -i http://127.0.0.1:5088/health/ready
```

Expected status codes respectively `200`, `200`, `200`, `503` with no DB configured. The OpenAPI JSON must include an `openapi` version and `paths`. Use `curl -s http://127.0.0.1:5088/openapi/v1.json | python3 -m json.tool | head -n 40` for readable output. Terminate the API via Ctrl+C.

Production route protection is verified automatically by `ApiOpenApiTests`. Do not run a Production-mode API manually against test database just to verify the 404.

## Ubuntu VPS independent validation

After SSH to VPS, run:

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
dotnet --version
unset RUN_ARGINTAXPAYER_PG_INTEGRATION
unset ConnectionStrings__ArginTaxPayerDb
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build \
  --filter FullyQualifiedName~ApiOpenApiTests \
  --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Release --no-build
```

VPS Terminal 1 (loopback only; never open a public firewall port for this test):

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/ArginTaxPayer.Api --urls http://127.0.0.1:5088
```

VPS Terminal 2:

```bash
curl -i http://127.0.0.1:5088/openapi/v1.json
curl -i http://127.0.0.1:5088/
curl -i http://127.0.0.1:5088/health/live
curl -i http://127.0.0.1:5088/health/ready
```

Expect the same 200/200/200/503 responses as macOS. Stop the API with Ctrl+C.

## GitHub Actions

The Step 14 workflow `Phase 02 .NET CI` runs for every push to the phase branch. Open the Actions page and verify **both** Ubuntu 24.04 and macOS 15 jobs succeed on the new Step 15 commit. CI executes normal .NET tests only; opt-in real PostgreSQL tests are deliberately disabled.

## PostgreSQL/Adminer prerequisites: NOT REQUIRED

This step adds no entity, migration or schema change. Running `dotnet ef database update` is not required. Existing PostgreSQL 17 Docker, Adminer and `argintaxpayer_test` can remain untouched.

For an optional previous-infrastructure check on VPS:

```bash
sudo docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
PG_ID=$(sudo docker ps -q --filter label=com.docker.compose.service=postgres | head -n 1)
test -n "$PG_ID" || { echo "PostgreSQL container not running"; exit 1; }
sudo docker exec "$PG_ID" psql -U argintaxpayer -d argintaxpayer_test \
  -tAc "SELECT current_database();"
```

If Adminer is already running on VPS loopback 8081, the Mac can view it over SSH with local `18081` forwarding at `http://127.0.0.1:18081`; credentials and database selection are unchanged. Adminer is optional and there is nothing new to inspect for Step 15.

## Acceptance checklist

1. Restore, build and targeted `ApiOpenApiTests` pass on macOS.
2. Full regression suite passes on macOS.
3. Restore, build and targeted `ApiOpenApiTests` pass on Ubuntu VPS.
4. Full regression suite passes on VPS.
5. Local Development `/openapi/v1.json` returns HTTP 200 and valid document, without PostgreSQL.
6. Production route returns 404 as checked by automated integration test.
7. Bootstrap/health endpoints retain prior behavior.
8. GitHub Actions is green for both OS jobs.
9. No migration or database change.
10. No merge into main, release, deploy, or Step 16 until explicit user approval.

Source commits alone do not verify the actual environments. Record test output and CI status before marking this step operationally accepted.
