# Phase 02 — Step 05: Configuration & Environment Management

Date: 2026-10-11
Status: IMPLEMENTED IN SOURCE — BUILD/TEST PENDING
Branch: `phase/02-foundation`

## Scope

Shared typed non-secret configuration for API and Workers, with environment overrides and production startup validation. Never commit fiscal signing keys, payment credentials, taxpayer PII or database passwords.

## Configuration precedence

Default host application settings: appsettings.json < appsettings.{Environment}.json < environment variables < command line. Double underscores delimit nested sections in environment variable keys.

Supported deployment labels: Development, Test and Production are conventional host environment names, not a hard-coded allowlist. For API set ASPNETCORE_ENVIRONMENT; for Worker set DOTNET_ENVIRONMENT. If both are set, keep them identical to avoid differing host precedence. The root .env.example is a reference file; .NET does not automatically load .env files.

## Typed options

- Section `ArginTaxPayer`: `DeploymentLabel` (1–64 characters) and optional `PublicBaseUrl`.
- PublicBaseUrl requires an absolute HTTP(S) URL without credentials, query, or fragment.
- Production requires a nonempty HTTPS PublicBaseUrl.
- Registration through `AddArginTaxPayerConfiguration(configuration, isProduction)` uses `IOptions<PlatformOptions>`, an `IValidateOptions<PlatformOptions>` validator, and `ValidateOnStart()`.
- API and Workers use the same extension. PublicBaseUrl is metadata only, not proof of tenant authorization or a network security control.

## Local verification (macOS)

```bash
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~PlatformConfigurationTests
dotnet test ArginTaxPayer.sln -c Release --no-build
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src/ArginTaxPayer.Api
```

For the separate worker terminal:

```bash
export DOTNET_ENVIRONMENT=Development
dotnet run --project src/ArginTaxPayer.Workers
```

Negative startup test: set Production without ArginTaxPayer__PublicBaseUrl and start the host; it should fail with an options validation error. Do not use production credentials for testing.

## Security

- appsettings.Production.json contains no secrets or actual production URLs.
- .env.example is a reference only; real .env files remain ignored by Git.
- Do not print IConfiguration or full process environment in logs.
- Actual secret store, connection strings and per-service credentials are deliberately deferred to later steps.
- PostgreSQL provisioning begins in Step 06; no migration or connection is created here.

## Acceptance checklist

- [x] Shared typed options registered for API and Worker.
- [x] Environment-specific non-secret JSON overlays added to both hosts.
- [x] Validation source and unit test source added.
- [x] Documentation and environment example added.
- [ ] Build and test results confirmed on a .NET 9 SDK environment.
- [ ] Owner approval.

## Completion record

No modifications to Phase 01 design, production API behavior, authentication, key custody, payment logic or databases. No merge, release or Step 06 implementation.
