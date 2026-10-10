# Phase 02 — Step 02: Solution & Project Structure

Date: 2026-10-11
Status: IMPLEMENTED IN BRANCH; LOCAL SDK BUILD/TEST VERIFICATION PENDING
Branch: `phase/02-foundation`

## Goal

Establish a compilable ASP.NET Core modular-monolith host layout without prematurely implementing tenancy, accounting, fiscal APIs, database tables, authorization or background jobs.

## Added projects

| Project | Role | Allowed project references |
|---|---|---|
| `ArginTaxPayer.Domain` | Core domain rules and value types | None |
| `ArginTaxPayer.Application` | Use cases, domain ports, service contracts | Domain |
| `ArginTaxPayer.Infrastructure` | Adapters and persistence implementation | Application (transitively Domain) |
| `ArginTaxPayer.Api` | ASP.NET Core composition host | Application, Infrastructure |
| `ArginTaxPayer.Workers` | Background process host | Application, Infrastructure |
| `ArginTaxPayer.Tests` | Independent smoke and future unit tests | Domain, Application, Infrastructure |

Target .NET framework: `net10.0` (SDK must be installed by the developer). The API and Worker processes are deliberately distinct. A placeholder API root route returns non-sensitive bootstrap metadata; it is not a health readiness signal or production API contract. Worker host registers no jobs.

## Boundary constraints

- Domain must not depend on Application, Infrastructure, HTTP endpoints, EF Core or UI.
- Application depends on Domain only; adapters implement future application ports.
- Infrastructure is referenced by composition hosts, not directly by domain business services.
- Module-specific folders and shared contracts will be created in Step 03, following Phase 01 bounded contexts.
- Tenant authorization, ledger invariants, key custody, notifications and outbox are **not** implemented by this structural bootstrap.

## Local prerequisites and verification (macOS or Windows)

Install official .NET 10 SDK before running commands.

```bash
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
dotnet --info
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore -c Release
dotnet test ArginTaxPayer.sln --no-build -c Release
dotnet run --project src/ArginTaxPayer.Api
```

In another terminal, `curl http://localhost:5000/` only if the API terminal explicitly reports that listening URL, or use the displayed bound URL. The Worker can be launched using `dotnet run --project src/ArginTaxPayer.Workers` and stopped with Ctrl+C.

These commands are an executable verification plan, **not** a claim that they have already run. The current execution container does not have the `dotnet` CLI or GitHub network resolution, so local build/test results remain unverified.

## Tests included

Three xUnit smoke assertions load the Domain, Application and Infrastructure assemblies. They are structural smoke checks only, **not** authentication, tenancy, fiscal, security or financial correctness tests. Those are later phase gates.

## Acceptance checklist

- [x] Solution, project files, two process hosts and smoke test source committed on Phase 02 branch.
- [x] Reference direction and layer responsibilities explicitly documented.
- [x] Phase 01 design artifacts remain unchanged.
- [ ] `dotnet restore`, `dotnet build`, and `dotnet test` succeed on developer/CI machine.
- [ ] Product-owner approval of Step 02.
- [ ] Step 03 modular boundaries implementation — not started.

## Completion record

No merge to `main`, tag, release, production credential, PostgreSQL migration, frontend project or CI configuration has been performed. Those are separate scheduled steps. Preserve the Phase 01 design handoff and outstanding verification gates.
