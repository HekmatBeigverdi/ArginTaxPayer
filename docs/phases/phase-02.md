# ArginTaxPayer — Phase 02 Master Plan and Delivery Record

Document type: **single authoritative phase file**
Branch: `phase/02-foundation`
Phase: **Foundation and Developer Platform**
Status: **Phase 02 implementation and automated CI accepted; final main merge/tag/release tracked below**
Last updated: 2026-10-12

> This document consolidates work already implemented through Step 20. Steps 01–11 below are a **retrospective reconstruction of the delivered foundation capabilities**, not a claim to reproduce a previously approved verbatim step-by-step plan. Original titles should be restored from original records if found. Steps 12–20 follow their committed deliverables. No further steps are added without a versioned plan amendment and owner approval.

## 1. Phase objective and acceptance

Deliver the developer platform defined by [the 18-phase product roadmap](../roadmap/18-phases.md): ASP.NET Core 9 API and background Worker foundation, Domain/Application/Infrastructure projects, PostgreSQL/EF Core migrations, Next.js TypeScript Persian RTL frontend scaffold, CI, configuration and observability.

Acceptance is based on independently run builds/tests, migrations tested only against `argintaxpayer_test`, four GitHub Actions jobs green on the final branch revision, scope audit and owner sign-off. Phase 02 completion means a **development foundation**, not customer-facing production readiness or later-phase business features.

## 2. Change control (mandatory for this and all future phases)

1. At phase start, create **one** `docs/phases/phase-XX.md` with immutable Step IDs, planned title, goal, deliverables, dependencies, exact verification commands, and acceptance conditions.
2. Before implementation, confirm the active step and read its pre-agreed acceptance criteria.
3. At each step, **edit that same file**: add implementation summary, code references, commit SHA, migration impact, macOS and Ubuntu test evidence, CI result, status and owner approval. Do not add `phase-XX-step-NN-*.md` files for ordinary progress.
4. Never silently renumber, split or increase the number of steps. Material changes use a separate “Plan amendment” entry within this file with date, impact, and explicit owner authorization.
5. At phase close, add the final handoff and release-gate result to this document; release/merge/tag require their own explicit authorization.
6. Historical step documents from Phase 02 remain as **read-only archival evidence**; the master file is authoritative going forward.

Status vocabulary: `Planned`, `Implemented`, `Verified`, `Owner approved`, `Blocked`, `Deferred`. A source commit alone must never be described as successful runtime testing.

## 3. Step ledger (20 delivered implementation steps)

| Step | Area / scope | Evidence | Status |
| ---: | --- | --- | --- |
| 01 | Solution and .NET platform baseline *(retrospective label)* | `ArginTaxPayer.sln`, `global.json` | Previously accepted by owner |
| 02 | API bootstrap and host wiring *(retrospective)* | `src/ArginTaxPayer.Api` | Previously accepted |
| 03 | Application/Domain/Infrastructure layering *(retrospective)* | `src/ArginTaxPayer.{Application,Domain,Infrastructure}` | Previously accepted |
| 04 | Worker composition root *(retrospective)* | `src/ArginTaxPayer.Workers` | Previously accepted |
| 05 | Configuration contract foundation *(retrospective)* | Infrastructure configuration code | Previously accepted |
| 06 | PostgreSQL connectivity wiring *(retrospective)* | Persistence DI / Npgsql | Previously accepted |
| 07 | EF Core migrations and DbContext *(retrospective)* | `Persistence/Migrations` | Previously accepted |
| 08 | Health check endpoints *(retrospective)* | `/health/live`, `/health/ready` | Previously accepted |
| 09 | Scoped unit of work / consistency *(retrospective)* | `EfUnitOfWork` | Previously accepted |
| 10 | Fail-closed taxpayer scope *(retrospective)* | `ITaxpayerScope`, initializer | Previously accepted |
| 11 | Observability/correlation *(retrospective)* | JSON logging, CorrelationId middleware | Previously accepted |
| 12 | Tenant-aware transactional outbox foundation | `outbox_messages`, `IOutboxWriter` | Previously accepted |
| 13 | Tenant-aware idempotency reservations | `idempotency_reservations`, `IIdempotencyReservationWriter` | Previously accepted |
| 14 | .NET CI on macOS/Ubuntu | `.github/workflows/phase-02-dotnet-ci.yml` | Owner approved |
| 15 | Development-only OpenAPI | `/openapi/v1.json`, OpenAPI tests | Owner approved |
| 16 | Development secrets abstraction | `ISecretProvider`, `EnvironmentSecretProvider` | Owner approved |
| 17 | Next.js + TypeScript + Persian RTL shell | `apps/web` | Owner approved |
| 18 | Lockfile / npm ci / frontend security and typecheck | `apps/web/package-lock.json`; `npm audit` CI gate | Owner approved; security remediation merged |
| 19 | Server-side local API health bridge | `apps/web/lib/api-health.ts` | Owner approved |
| 20 | Automated API+Next.js end-to-end smoke | `scripts/phase-02-integration-smoke.mjs` and fourth CI job | Implemented; final independent execution evidence pending |

The reconstruction above is **not** an attempt to retroactively reapprove each reconstructed title. Always prefer original Git history if an original 01–11 step plan is located.

## 4. Architecture, security and database contracts

- The API and Worker share Application/Infrastructure registration; configuration is environment-aware.
- EF Core schema changes are explicit migrations, never applied automatically during API/Worker startup.
- API liveness does not require PostgreSQL; readiness returns 503 when PostgreSQL is absent.
- Scoped tenant infrastructure fails closed. Authentication/authorization and taxpayer assignment enforcement are **Phase 03/04**, not complete in Phase 02.
- Outbox and idempotency provide storage/reservation, not full delivery/replay/payment/tax APIs.
- Development-only OpenAPI and server-side local health probes are not publicly exposed production endpoints.
- The secret provider is a development abstraction, not a secure vault or private-key custody.
- Frontend dependencies are locked and CI audits production dependencies for high/critical findings; review newer advisories before deployment.
- No taxpayer or financial production data should be stored in a public repository.

## 5. Build and verification — macOS

Prerequisites: Git, .NET 9 SDK compatible with `global.json`, Node.js 22, npm 10+, NuGet/npm registry access, free local ports 15088 and 13019. Database and Adminer not required for offline smoke.

Run from your actual clone path:

```bash
cd /path/to/ArginTaxPayer
git status --short
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation

node --version
npm --version
dotnet --version

unset ConnectionStrings__ArginTaxPayerDb
unset RUN_ARGINTAXPAYER_PG_INTEGRATION
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build

cd apps/web
npm ci --no-audit --no-fund
npm test
npm run typecheck
npm run build
npm audit --omit=dev --audit-level=high
cd ../..

node scripts/phase-02-integration-smoke.mjs
```

Expected final output: `STEP 20 INTEGRATION SMOKE PASSED`, exit 0. Store the actual run/CI link in Section 7 before release signoff.

## 6. Build and verification — Ubuntu VPS

Prerequisites: the same tools and available loopback test ports; Ubuntu VPS checkout at `~/projects/ArginTaxPayer`. Inspect previously reported local modifications before pull. No public network ports or actual credentials are required.

```bash
ssh argindev@85.133.244.156
cd ~/projects/ArginTaxPayer
git status --short
git diff --stat
git diff --cached --stat
# If modified, preserve changes before switching or pulling.
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation

node --version
npm --version
dotnet --version

unset ConnectionStrings__ArginTaxPayerDb
unset RUN_ARGINTAXPAYER_PG_INTEGRATION
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build

cd apps/web
npm ci --no-audit --no-fund
npm test
npm run typecheck
npm run build
npm audit --omit=dev --audit-level=high
cd ../..

node scripts/phase-02-integration-smoke.mjs
```

Do not alter the PostgreSQL setup, expose development ports, or apply migrations for the offline test. Test-DB migration and actual PostgreSQL integration have separate runbooks and require explicitly selected `argintaxpayer_test`.

## 7. Final acceptance and release gate

| Gate | Result / evidence |
| --- | --- |
| API, Worker, persistence, frontend, docs source | Implemented in `phase/02-foundation` |
| Historical step acceptances | Reported by owner for steps 1–19 |
| Step 20 macOS smoke | Owner reported local testing accepted; separate command log not attached |
| Step 20 Ubuntu VPS smoke | Owner reported testing accepted; separate command log not attached |
| Four CI jobs on pre-release phase commit | **Passed**: [run 38174376963](https://github.com/HekmatBeigverdi/ArginTaxPayer/actions/runs/38174376963), head `c3b2c1c0457c86d81487e34d33b90233a311b5c6`. All four jobs succeeded. A documentation-only closeout commit will have its own CI run. |
| Database migration state and opt-in PostgreSQL test | Previous work; not reverified in this handoff |
| Security risk / unresolved vulnerabilities | Production-dependency audit gate enabled; no claim of complete security certification |
| Main merge | Not done |
| Phase tag / GitHub release | Not done |
| Production deployment | Not done |

**Delivery conclusion:** Phase 02 development and four-job CI acceptance are confirmed on the cited tested source commit. User explicitly authorized formal Phase 02 closeout and release work on 2026-10-12. The merge and immutable tag/release must still be verified as separate actions and recorded accurately. This foundation is not a production tax-invoicing service.

## 8. Deferred work (not part of this phase)

Later phases implement real identity/MFA and delegated authorization, fiscal memory and private-key custody, invoice rules, signing and tax authority integration, audited subscription/payment/settlement, isolation penetration testing, and production deployment. Do not extend Phase 02 by silently adding those workflows.

## 9. Archival notes

Existing `docs/phases/phase-02-step-*.md` documents and the prior `phase-02-final-handoff.md` are historical evidence only. **New progress, plan amendments, results and the final release record are written here**, not in new per-step files.

## 10. Formal closure decision and release record (2026-10-12)

- Project owner expressly authorized completing the Phase 02 closeout, preparing for a new chat to plan Phase 03.
- Proven CI: all four jobs succeeded for `c3b2c1c0457c86d81487e34d33b90233a311b5c6`, run [38174376963](https://github.com/HekmatBeigverdi/ArginTaxPayer/actions/runs/38174376963).
- Phase 02 milestone: **FOUNDATION IMPLEMENTATION COMPLETE**; no extra numbered implementation steps beyond 20.
- API-to-Next.js loopback smoke and frontend security audit are enforced in CI.
- Release prerequisites: review last documentation-only commit and PR checks, merge to `main`, then create and verify an immutable `v0.2.0-phase02` Git tag and GitHub Release.
- Git tag, GitHub Release, production deployment and independent operator test logs are not claimed complete until specifically verified. No application should be deployed publicly under this milestone.
- Phase 03 begins with an *approved, fixed* single-file step plan at `docs/phases/phase-03.md`; this phase file is the sole active Phase 02 plan reference.
