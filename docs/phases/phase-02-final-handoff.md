# ArginTaxPayer — Phase 02 Final Handoff and Acceptance Record

Date: 2026-10-12
Owner's instruction: Document and hand over the completed Phase 02 foundation, without arbitrary new numbered implementation steps.
Branch: `phase/02-foundation`
Scope reference: [18-Phase Roadmap](../roadmap/18-phases.md), Phase 02 “Foundation and developer platform”.
Delivery state: **IMPLEMENTATION HANDOFF COMPLETE; INDEPENDENT FINAL VERIFICATION / MAIN MERGE / TAG / RELEASE NOT EXECUTED OR ATTESTED.**

## 1. Scope control

The correct planning boundary is the Phase 02 roadmap outcome, not an automatically expanding list of steps. The work performed through Step 20 is consolidated into this single phase handoff; there are **no newly invented Steps 21–24**. Do not reinterpret later-phase requirements as Phase 02 failures or mark incomplete future business modules as implemented.

Functional baseline delivered:
1. ASP.NET Core 9 API and Worker host project, Application, Domain and Infrastructure layers, xUnit solution.
2. Configuration validation, safe bootstrapping, structured JSON logging and correlation identifiers.
3. PostgreSQL/EF Core persistence design and separately controlled migrations; health readiness checks.
4. Tenant-scoped Application contracts; fail-closed scope until authorization by future identity workflows.
5. EF Unit of Work, outbox persistence and tenant-scoped idempotency reservation foundations.
6. Development-only OpenAPI endpoint; production route protection.
7. Redacted secret provider abstraction with environment-variable development adapter.
8. Next.js 15 App Router with TypeScript, Persian RTL responsive developer shell and planned-module placeholders.
9. npm lockfile, `npm ci`, TypeScript validation, and blocking production-dependency `npm audit --audit-level=high` checks; the vulnerability fix was merged into this phase branch.
10. Server-side, development-only loopback API health bridge with bounded probes and no user/business data.
11. GitHub Actions matrix for .NET macOS and Ubuntu; Next.js Ubuntu build/tests/audit; automated Ubuntu local API-to-Next.js integration smoke.

## 2. Representative evidence locations

- `ArginTaxPayer.sln`, `global.json`, `src/ArginTaxPayer.Api`, `src/ArginTaxPayer.Workers`, `src/ArginTaxPayer.Application`, `src/ArginTaxPayer.Domain`, `src/ArginTaxPayer.Infrastructure`, `tests/ArginTaxPayer.Tests`.
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/` holds the initial baseline, outbox and idempotency migrations.
- `apps/web/` holds Next.js code, Node tests and the lockfile.
- `scripts/phase-02-integration-smoke.mjs` for automated loopback smoke.
- `.github/workflows/phase-02-dotnet-ci.yml` for 4 CI jobs.
- `docs/phases/phase-02-step-12-outbox-foundation.md`, `phase-02-step-13-idempotency-reservation-foundation.md`, `phase-02-step-14-dotnet-ci-pipeline.md`, `phase-02-step-15-openapi-contract-foundation.md`, `phase-02-step-16-secrets-abstraction.md`, `phase-02-step-17-nextjs-rtl-shell.md`, `phase-02-step-18-reproducible-frontend-ci.md`, `phase-02-step-19-local-api-health-bridge.md`, `phase-02-step-20-automated-integration-smoke.md`.
- Earlier Phase 02 deliverables are represented by the committed source, migration, and test trees; do not invent missing historic step titles or overwrite their original approval record.

## 3. Acceptance criteria and evidence status

| Gate | Status | Evidence / next action |
| --- | --- | --- |
| Source and scope handoff | Complete | Phase branch source, step documents and this final record |
| Developer API/Worker, persistence, observability | Implemented | Source + existing .NET tests |
| Next.js Persian RTL, OpenAPI, health bridge | Implemented | Source + Node and .NET tests |
| npm lockfile and patched dependency baseline | Implemented | `next@15.5.27`, `react@19.1.7`, `react-dom@19.1.7`; `postcss@8.5.29`, `sharp@0.35.5` in the merged lockfile |
| Blocking npm production dependency audit | Implemented | CI command `npm audit --omit=dev --audit-level=high` without `continue-on-error` |
| Mac and VPS verification steps 1–19 | User approved in conversation | Earlier step acceptance reported by operator |
| Step 20 macOS integration smoke | Not independently verified by assistant | Operator must run and archive exit code/output |
| Step 20 Ubuntu VPS integration smoke | Not independently verified by assistant | Operator must run and archive exit code/output |
| Four green jobs **on final handoff commit** | Not independently verified | Verify latest push workflow run; earlier successful jobs on prior commits do not prove this revision |
| PostgreSQL test DB migration state / optional real DB integration | Not revalidated at handoff | Verify `argintaxpayer_test` only if release gate demands; do not auto-migrate |
| `main` merge | Not performed | Requires explicit owner authorization |
| Version tag / GitHub release | Not performed | Requires explicit owner authorization and release notes |

This record distinguishes **phase implementation complete** from verified release readiness. Do not claim the complete product is live, secure for customer data, or production-deployable. No requirement implies that GitHub file creation proves a successful build.

## 4. macOS: final verification

Prerequisites: Git; SDK compatible with `global.json`; Node.js 22; npm >= 10; access to NuGet/npm; loopback ports 15088/13019 free. No secrets or DB required.

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

Expected smoke success includes the lines:
```text
PASS: API 200 / 503; Next.js 200; RTL and independent health states.
PASS: Frontend remains available when API is stopped.
STEP 20 INTEGRATION SMOKE PASSED
```

Do not run this against unrelated services bound to the same test ports.

## 5. Ubuntu VPS: final verification

Prerequisites: the same SDK, Node.js 22/npm >=10, Git, package sources and free loopback ports. No sudo, publicly opened frontend port or DB secrets needed for the offline smoke. Prior Ubuntu working-tree modifications must be inspected before pull.

```bash
ssh argindev@85.133.244.156
cd ~/projects/ArginTaxPayer
git status --short
git diff --stat
git diff --cached --stat
# Preserve any existing local changes before switching branches or pulling.
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

Read the scripts' process exit codes. API readiness must be 503 **in this deliberately database-disconnected test**; never falsify DB connectivity to force HTTP 200.

## 6. PostgreSQL / Adminer boundary

Phase 02 includes EF Core migrations already authored in prior steps, but **the final handoff adds no migration**. To confirm database-related acceptance, use only the dedicated `argintaxpayer_test` and the prior opt-in PostgreSQL integration test runbooks. Adminer is optional via existing SSH tunnel to loopback; no public exposure. Never run production or dev migrations as part of this closeout, and never commit actual DB passwords.

## 7. GitHub Actions and release decision

At `https://github.com/HekmatBeigverdi/ArginTaxPayer/actions` verify all **four** jobs for the latest commit:
- `.NET 9 | macos-15`
- `.NET 9 | ubuntu-24.04`
- `Next.js RTL | ubuntu-24.04`
- `API + Next.js | Ubuntu loopback smoke`

Do not confuse the job `Next.js RTL` with real cross-system integration; only the fourth job launches both services.

Recommended subsequent workflow after evidence is green and owner explicitly authorizes a release:
1. Review migration scope / security caveats.
2. Approve exact merge target and release version.
3. Open PR `phase/02-foundation` -> `main`, review and merge.
4. Create an immutable phase-specific tag and GitHub release notes.
5. Preserve a rollback ref and operator acceptance logs.

**This handoff does not merge, tag, publish a release, or deploy.** Those actions are separate explicit approvals.

## 8. Remaining scope in later phases (not Phase 02 blockers)

- Phase 03: login/MFA, validated user identity and server-side authorization.
- Phase 04: taxpayer records and per-Level-2-admin assignments.
- Phases 07–11: fiscal schema/rules, invoice workflow, signing, official tax authority transport, asynchronous outcomes.
- Phase 12: subscription entitlements, payment collection, independent deal-level settlement, net collected-profit split with Root-approved expenses.
- Phase 17: end-to-end pen testing, load, recovery and audit.
- Phase 18: actual production release and operational documentation.

## 9. Architectural handoff invariants

- Never use the unauthenticated `ITaxpayerScope` initializer as a general public tenant selector.
- Outbox/idempotency abstractions are reservation/persistence foundations, not complete replay-safe external transaction pipelines.
- Health endpoints are not permission or readiness-to-deploy proofs.
- Keep fiscal private keys and payment credentials out of code and configuration commits.
- Preserve bilingual UI conventions: Persian right-to-left and code left-to-right.
- Maintain a fixed phase scope and require project owner approval for material amendments.

## 10. Phase 02 sign-off

Owner explicitly requested documentation and completion handoff. The deliverable is packaged on the phase branch as **implementation complete**; runtime/CI evidence and release actions remain transparent outstanding acceptance gates. Do not misrepresent the latter as accomplished.
