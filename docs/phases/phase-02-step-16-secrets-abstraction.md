# Phase 02 — Step 16: Secrets Abstraction and Safe Bootstrap Adapter

Date: 2026-10-11
Branch: `phase/02-foundation`
Status: SOURCE COMMITTED — macOS / Ubuntu / GitHub CI acceptance pending
Prerequisite: Step 15 explicitly approved.

## Goal and scope

Establish an Application-owned `ISecretProvider` abstraction, a redacted `SecretValue` wrapper, and a safe bootstrap-only environment-variable implementation in Infrastructure. Register the provider in the shared Infrastructure composition root used by both API and Worker.

Files:
- `src/ArginTaxPayer.Application/Abstractions/Security/ISecretProvider.cs`
- `src/ArginTaxPayer.Infrastructure/Security/EnvironmentSecretProvider.cs`
- `src/ArginTaxPayer.Infrastructure/DependencyInjection.cs`
- `tests/ArginTaxPayer.Tests/SecretProviderTests.cs`

The adapter maps a logical name `TEST_KEY` to environment variable `ARGIN_SECRET_TEST_KEY`. Only names consisting of uppercase ASCII letters, digits and underscore (1-64 chars) are accepted. `GetRequired` fails closed with a generic message if unset; `GetOptional` returns null. `SecretValue.ToString()` is always `[REDACTED]`. `Reveal()` is only for vetted integration boundaries and **is not a security boundary**: the plaintext value remains a managed .NET string. This is an extensibility/testability baseline, NOT a production vault, HSM, key management system, rotation service, fiscal signing key store, or complete logging redaction guarantee.

There are no real secrets in committed code, no secret test environment mutation, no database or migration changes, no new endpoints, and no API/Worker behavioral changes.

## Safety limits

- Do not store real secrets in `appsettings.json`, GitHub commits, tickets, screen recordings or transcripts.
- Environment variables are visible to sufficiently privileged local users and may appear in diagnostic or process inspection tools. Use an authenticated secret manager/vault with explicit custody, authorization, audit, rotation and secure deployment in later phases.
- Do not use `SecretValue.ToString()` for cryptographic inputs; `Reveal()` must not be passed to logs, structured telemetry or errors.
- This abstraction does not automatically cover the existing `ConnectionStrings__ArginTaxPayerDb` configuration; integration and rotation require a separately reviewed change.
- As with Step 11, per-provider default logging still requires audit before production.
- GitHub Actions should run without injecting real secrets; unit tests use a fake lookup delegate.

## macOS prerequisites and isolated validation

Requirements: Git and .NET SDK matching `global.json` (`9.0.202` or allowed same-feature-band patch); NuGet reachability for restore. **No PostgreSQL, SSH tunnel, Adminer, Docker, dotnet-ef or psql needed.**

In macOS Terminal:

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
  --filter FullyQualifiedName~SecretProviderTests \
  --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Debug --no-build
```

Optional API bootstrap regression in Terminal 1:

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/ArginTaxPayer.Api --urls http://127.0.0.1:5088
```

Terminal 2:

```bash
curl -i http://127.0.0.1:5088/
curl -i http://127.0.0.1:5088/health/live
curl -i http://127.0.0.1:5088/health/ready
curl -i http://127.0.0.1:5088/openapi/v1.json
```

Expected codes: 200, 200, 503 (no DB connection configured), 200. Stop API via Ctrl+C.

## Ubuntu 24.04 VPS prerequisites and isolated validation

Requirements: same Git/.NET SDK; NuGet access. No Docker/PostgreSQL running is required for Step 16 tests. On Ubuntu VPS:

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
  --filter FullyQualifiedName~SecretProviderTests \
  --logger "console;verbosity=normal"
dotnet test ArginTaxPayer.sln -c Release --no-build
```

Optional API test, run on loopback ONLY (two VPS shells):

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/ArginTaxPayer.Api --urls http://127.0.0.1:5088
```

```bash
curl -i http://127.0.0.1:5088/
curl -i http://127.0.0.1:5088/health/live
curl -i http://127.0.0.1:5088/health/ready
curl -i http://127.0.0.1:5088/openapi/v1.json
```

Expected 200, 200, 503, 200. Stop with Ctrl+C. No firewall rules or public ports needed.

## GitHub Actions CI

Open `https://github.com/HekmatBeigverdi/ArginTaxPayer/actions`, find `Phase 02 .NET CI` for the Step 16 commit, and verify both macOS 15 and Ubuntu 24.04 jobs succeed. The tests do not require any repository secrets or the VPS database. A committed test is not an executed/validated test; acceptance is pending until runner outcomes are known.

## PostgreSQL and Adminer (optional existing-infrastructure checks)

This step requires **no migrations**, and no SQL changes should be applied to `argintaxpayer_dev` or `argintaxpayer_test`.

If desired on Ubuntu:

```bash
sudo docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
PG_ID=$(sudo docker ps -q --filter label=com.docker.compose.service=postgres | head -n 1)
test -n "$PG_ID" || { echo "Postgres unavailable"; exit 1; }
sudo docker exec "$PG_ID" psql -U argintaxpayer -d argintaxpayer_test \
  -tAc "SELECT current_database();"
```

Adminer remains optional. If already running on VPS loopback port 8081, use a Mac-side SSH forward local 18081 to VPS 8081 and browse `http://127.0.0.1:18081`. Use System PostgreSQL, Server `postgres`, username `argintaxpayer`, DB `argintaxpayer_test`. There is no new table to inspect.

## Step 16 acceptance

1. Focused `SecretProviderTests` and full regression tests pass on macOS.
2. Focused `SecretProviderTests` and full regression tests pass on Ubuntu VPS.
3. API bootstrap/health/OpenAPI regression checks retain previous responses.
4. CI green on both runners.
5. No literal credentials committed; no new secrets required in GitHub settings; no migrations/schema changes.
6. No main branch merge, release, or start of Step 17 until owner confirmation.
