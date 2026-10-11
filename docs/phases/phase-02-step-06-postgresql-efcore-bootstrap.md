# Phase 02 — Step 06: PostgreSQL & EF Core Bootstrap

Date: 2026-10-11
Status: SOURCE COMMITTED — BUILD, DATABASE CONNECTIVITY AND TEST EXECUTION NOT YET VERIFIED
Branch: `phase/02-foundation`

## Scope and architecture

The `ArginTaxPayer.Infrastructure` project owns `ArginTaxPayerDbContext` and the PostgreSQL provider. Both API and Worker composition roots call `AddArginTaxPayerPersistence(configuration, isProduction)`. The context is registered with **scoped lifetime**, not as singleton; future hosted jobs will create scopes. Domain and Application remain free of EF Core and Npgsql dependencies.

No entities are mapped in this step. **No migration, production schema, database seed, table, or automatic `EnsureCreated` / `Migrate` execution is performed.** The controlled migration pipeline and first migration are Step 07 responsibilities.

## Runtime behavior

- `ConnectionStrings:ArginTaxPayerDb` is read from the standard .NET configuration chain; export it as `ConnectionStrings__ArginTaxPayerDb`.
- In Development/Test, an absent connection string skips DbContext registration and preserves the prior API and Worker bootstrap behavior. Requests requiring a DbContext will not work until configured.
- In Production, a missing connection string fails startup. Malformed strings, missing Host/Database/Username, and explicitly non-TLS SSL modes (Disable, Allow, Prefer) are rejected.
- TLS is mandatory in Production. Operators should prefer `SSL Mode=VerifyFull` and configure trusted server certificates appropriate to their PostgreSQL deployment. `SSL Mode=Require` provides encryption but is not equivalent to full hostname verification.
- The provider uses a 30-second SQL command timeout and transient-failure retries (three attempts). This does not substitute for application idempotency or explicit transaction policy.
- Never log, commit or include database credentials in exception messages; application-level connection failures should be redacted.

## Local PostgreSQL provisioning (Docker)

Requires Docker Desktop or Docker Engine with Compose. The Compose service binds only to the local loopback interface and persists data in a named volume.

```bash
export ARGIN_POSTGRES_PASSWORD='YOUR_STRONG_LOCAL_PASSWORD'
docker compose -f infra/postgres/compose.yaml up -d
docker compose -f infra/postgres/compose.yaml ps

export ConnectionStrings__ArginTaxPayerDb="Host=localhost;Port=5432;Database=argintaxpayer_dev;Username=argintaxpayer;Password=${ARGIN_POSTGRES_PASSWORD};SSL Mode=Prefer"
```

The local Docker image is not configured for TLS. `SSL Mode=Prefer` is permitted **only** in Development/Test. The `ARGİN_POSTGRES_PASSWORD` (ASCII spelling `ARGIN_POSTGRES_PASSWORD`) environment variable is used by Compose; keep it secret. Do not add credentials to committed JSON files or a public GitHub issue.

Stop without deleting the volume:

```bash
docker compose -f infra/postgres/compose.yaml down
```

Volume deletion is destructive and is not part of this step.

## Build and tests

Use the repository-pinned .NET SDK from `global.json` (9.0.202 or compatible latest patch).

```bash
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation

dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~PostgreSqlBootstrapTests
dotnet test ArginTaxPayer.sln -c Release --no-build
```

The tests confirm that missing optional development configuration preserves the bootstrap, Production requires connection settings and rejects non-TLS configurations, and a valid setting registers a scoped Npgsql DbContext without opening a connection.

These tests **do not** verify a live PostgreSQL handshake, credential correctness, TLS certificate verification, database creation, tenant isolation, or migrations. Verify connectivity separately on a developer machine or CI environment with a provisioned test database before claiming operational readiness.

## Change list

- `src/ArginTaxPayer.Infrastructure/ArginTaxPayer.Infrastructure.csproj` — Npgsql EF Core provider.
- `src/ArginTaxPayer.Infrastructure/Persistence/ArginTaxPayerDbContext.cs` — empty EF model.
- `src/ArginTaxPayer.Infrastructure/Persistence/PersistenceRegistration.cs` — provider registration and validation.
- `src/ArginTaxPayer.Infrastructure/Persistence/InfrastructurePersistenceExtensions.cs` — host integration method.
- `src/ArginTaxPayer.Api/Program.cs` and `src/ArginTaxPayer.Workers/Program.cs` — composition.
- `infra/postgres/compose.yaml` — local-only database service.
- `.env.example` — safe placeholder instructions.
- `tests/ArginTaxPayer.Tests/PostgreSqlBootstrapTests.cs` — smoke validation without a live database.

## Acceptance record

- [x] EF Core DbContext and PostgreSQL provider source committed.
- [x] API and Worker composition roots connected.
- [x] Existing Development/Test startup behavior protected.
- [x] Production connection requirements and TLS guard coded.
- [x] Local PostgreSQL Compose and tests committed.
- [x] No Phase 01 modification, automatic migrations, main merge, or release.
- [ ] SDK build and xUnit run independently verified.
- [ ] Live PostgreSQL connectivity and deployment TLS verified.
- [ ] Product-owner approval of Step 06.

Step 07 is intentionally not started.
