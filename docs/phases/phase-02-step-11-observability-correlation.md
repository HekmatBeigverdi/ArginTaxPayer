# Phase 02 — Step 11: Structured Logging and Request Correlation

Date: 2026-10-11
Branch: `phase/02-foundation`
Status: IMPLEMENTED IN GITHUB; MAC/VPS RUNTIME ACCEPTANCE PENDING

## Scope

This step establishes the observability baseline for API and background Worker hosts.

- Both hosts use built-in .NET JSON console logging with scopes enabled.
- API middleware assigns an `X-Correlation-ID` to each request and echoes it on the response.
- A supplied correlation ID is accepted only if 1–64 characters are ASCII letters, numbers, hyphens or underscores; invalid or repeated headers are replaced with a cryptographically random 128-bit hex ID.
- The middleware emits structured fields `Method`, `Path` (without query string), `StatusCode`, `ElapsedMilliseconds`, `CorrelationId` and a per-request correlation scope.
- Existing `/`, `/health/live`, `/health/ready` API behavior is unchanged.
- Automated integration tests cover ID propagation, rejected unsafe IDs, generated IDs and compatibility with healthy/unhealthy health endpoints.
- No database migration, table, or external library/Serilog package is needed.

## Boundaries and safeguards

The access logger does **not** intentionally log request query strings, body, authentication headers, cookies, database passwords, invoices or taxpayer secrets. This is **not yet full redaction for all framework/provider/application logs**. Exception logging and database-provider command logging must be governed by a separately audited policy before customer data is handled. Never enable EF Core sensitive-data logging in production. Other middleware or frameworks can log additional fields independently; review deployed logging configuration and sinks.

The public correlation header is **not** identity, authorization, tenant scope, cryptographic proof, or an idempotency key. A caller can provide a valid ID, so correlation is for diagnostics only. Later phases must propagate trusted trace context across internal jobs/outbox while ensuring per-tenant log access restrictions.

Logging to stdout may expose diagnostic metadata to anyone who can read container logs. Do not publish stdout publicly or give Level 2 delegates access to global security logs. Log retention, access controls and deletion policy are separate deployment concerns.

## macOS prerequisites and test steps

Only Git and the .NET SDK compatible with `global.json` (`9.0.202` / latestPatch in that feature band) are required for the automated tests. PostgreSQL, Adminer, Docker and SSH tunneling are NOT required for this step's focused tests. Do not export a connection string for the tests.

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
  --filter FullyQualifiedName~ApiCorrelationTests \
  --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Debug --no-build
```

Manually run API on a fixed loopback port (first Mac terminal):

```bash
cd ~/Projects/ArginTaxPayer
unset ConnectionStrings__ArginTaxPayerDb
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/ArginTaxPayer.Api --urls http://127.0.0.1:5088
```

Then in a second Mac terminal:

```bash
curl -i -H 'X-Correlation-ID: dev-check-123' http://127.0.0.1:5088/
curl -i -H 'X-Correlation-ID: bad/value' \
  'http://127.0.0.1:5088/health/live?secret=do-not-log'
curl -i http://127.0.0.1:5088/health/ready
```

Expected:
- Bootstrap returns HTTP 200 and response `X-Correlation-ID: dev-check-123`.
- Invalid `bad/value` is replaced by a generated lowercase hex correlation ID. Response 200 on liveness.
- Without database configuration in Development, `/health/ready` responds HTTP 503 and has a generated `X-Correlation-ID`.
- In the API stdout, look for JSON entries with `Method`, `Path`, `StatusCode`, `ElapsedMilliseconds`, `CorrelationId`. The exact message layout can vary.
- In the structured request completion entry, `Path` is `/health/live`; the string `do-not-log` should not appear in access log messages. Framework defaults still require configuration review before production.
- `Ctrl+C` to stop the API.

## VPS Ubuntu prerequisites and test steps

No Docker/DB connectivity is required to run the Step 11 automated tests or unconfigured Development API smoke test. PostgreSQL/Adminer may remain running from prior steps. Do not publish API port 5088 or open firewall rules.

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
  --filter FullyQualifiedName~ApiCorrelationTests \
  --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Release --no-build
```

First Ubuntu terminal:

```bash
unset ConnectionStrings__ArginTaxPayerDb
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/ArginTaxPayer.Api --urls http://127.0.0.1:5088
```

Second Ubuntu terminal:

```bash
curl -i -H 'X-Correlation-ID: vps-check-123' http://127.0.0.1:5088/
curl -i -H 'X-Correlation-ID: bad/value' \
  'http://127.0.0.1:5088/health/live?secret=do-not-log'
curl -i http://127.0.0.1:5088/health/ready
```

Expect same outputs as macOS, with supplied valid ID echoed and invalid one replaced. Stop the API afterward with `Ctrl+C`.

Worker JSON logger can be smoke-tested from a separate terminal after stopping API (no database string required in Development):

```bash
DOTNET_ENVIRONMENT=Development timeout 5s dotnet run \
  --project src/ArginTaxPayer.Workers
```

On Linux, `timeout` exits 124 when the 5-second time limit is reached; that is expected for the deliberately long-running worker. Confirm process start JSON logs and absence of sensitive content. On macOS, start the worker with `dotnet run --project src/ArginTaxPayer.Workers` and stop manually using `Ctrl+C`.

## Optional VPS PostgreSQL/Adminer checks (no new migrations)

If you wish to verify previously running containers on Ubuntu:

```bash
sudo docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
PG_ID=$(sudo docker ps -q --filter label=com.docker.compose.service=postgres | head -n 1)
test -n "$PG_ID" || { echo "Postgres container unavailable"; exit 1; }
sudo docker exec "$PG_ID" psql -U argintaxpayer -d argintaxpayer_test \
  -tAc "SELECT current_database();"
```

Optional Adminer: confirm `argintaxpayer-adminer` is running on VPS loopback port 8081. On macOS, an SSH tunnel `-L 127.0.0.1:18081:127.0.0.1:8081` permits viewing `http://127.0.0.1:18081`. In Adminer select System PostgreSQL, Server `postgres`, user `argintaxpayer`, database `argintaxpayer_test` and the existing secret. No migrations or schema writes are required for Step 11.

## Acceptance

1. Targeted `ApiCorrelationTests` and all regression tests pass on macOS and Ubuntu.
2. Correlation header is returned for all smoke-tested endpoints.
3. Valid IDs survive intact; unsafe IDs are replaced.
4. Request access entries are structured JSON and contain no query/body/authorization/cookie details.
5. Bootstrap and health behavior remain unchanged.
6. Worker starts with JSON console logs.
7. No database migration or change, no main-branch merge, no release or Step 12 work.

GitHub tool execution cannot run tests in the user's macOS/VPS environment. Acceptance remains pending until terminal output is provided.
