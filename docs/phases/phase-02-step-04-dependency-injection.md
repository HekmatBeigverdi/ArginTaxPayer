# Phase 02 — Step 04: Dependency Injection and Composition Roots

Date: 2026-10-11
Status: SOURCE COMMITTED; RUNTIME BUILD/TEST EXECUTION NOT VERIFIED
Branch: `phase/02-foundation`

## Intent

Establish explicit, predictable service-registration entry points for the API and Workers, keeping business rules independent of transport, database and process hosting. This step does not deliver production adapters or permission enforcement.

## Implemented

- `AddArginTaxPayerApplication(IServiceCollection)` is declared in Application. It registers the 13 Phase 01 module descriptors as singleton **metadata** using `TryAddEnumerable`. Calling it twice must not add duplicate descriptors.
- `AddArginTaxPayerInfrastructure(IServiceCollection)` is declared in Infrastructure. It registers a harmless assembly marker with `TryAddSingleton` so host wiring can be verified. No services access databases, secrets, payment gateways, fiscal SDKs or background queues.
- Both `ArginTaxPayer.Api` and `ArginTaxPayer.Workers` call the application and infrastructure registration extensions before building their hosts.
- `Microsoft.Extensions.DependencyInjection.Abstractions` version 9.0.0 is referenced by the Application project; the independent test project uses `Microsoft.Extensions.DependencyInjection` version 9.0.0, matching the repository's `net9.0` and SDK 9.0.202 baseline. Domain still has **no** service-container dependency.

## Lifetime and dependency rules

- Module descriptors and the infrastructure marker are stateless singleton objects. **Do not** use singleton state for mutable fiscal signing, tenant context, payment execution or EF Core DbContext.
- Future per-request context and EF Core DbContext must be scoped, and hosted workers must create an explicit scope per job.
- Application ports remain interfaces owned by Application; Infrastructure supplies implementations. Cross-module calls must be explicit and audited.
- A `WorkspaceId`/module registration is never a substitute for authorization. Root/Delegate permission, customer allocation, approved costs and key-custody policies remain enforced in later implementation steps.
- No reflection-based scanning or wildcard auto-registration; all registrations are explicit and reviewable.

## Verification

```bash
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore -c Release
dotnet test ArginTaxPayer.sln --no-build -c Release --filter FullyQualifiedName~DependencyInjectionTests
dotnet test ArginTaxPayer.sln --no-build -c Release
dotnet run --project src/ArginTaxPayer.Api
```

API bootstrap `/` should retain its original response, with no new endpoints. The Worker should start and stop without registering jobs. Tests check service resolution, exact module count, repeat registration, application-only composition and null argument behavior. Tests must be executed on an environment with the .NET 9 SDK pinned in `global.json` and access to NuGet feeds; GitHub source inspection alone cannot certify a passing build.

## Acceptance

- [x] Application and Infrastructure extension methods and explicit composition-root calls committed.
- [x] Existing Bootstrap API and Worker process behavior preserved.
- [x] No new business workflows, database tables, migrations or external side effects.
- [x] Dependency-injection test source added.
- [ ] `dotnet restore`, `dotnet build` and `dotnet test` observed successful on an executable environment.
- [ ] Owner approval for Step 04.

## Completion record

Only the Phase 02 branch is changed. No merge to `main`, tag/release, or Step 05 environment configuration is performed.
