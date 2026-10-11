# Phase 02 — Step 19: Safe Development-Only Next.js/API Health Bridge

Branch: `phase/02-foundation`
Status: CODE COMMITTED; MAC/VPS/CI VERIFICATION PENDING
Depends on accepted Step 18 and reviewed security fix.

## Scope

Add a minimal, non-sensitive backend/frontend integration boundary to the Next.js RTL development shell:
- New `apps/web/lib/api-health.ts`: server-only health probe of existing API `/health/live` and `/health/ready`.
- New `apps/web/tests/api-health.test.mjs`: structural boundary tests.
- Update `apps/web/app/page.tsx`: show Persian liveness/readiness labels. The page renders dynamically and stays usable when API is absent.
- Update responsive styling and `apps/web/.env.example`.
- No new API routes, database entities, fiscal records, CORS configuration, credentials or public proxy endpoints.

## Threat boundaries and known limitations

The environment variable `ARGIN_API_INTERNAL_BASE_URL` is read **on the Next.js server**, not exposed to browser JavaScript. The probe works only when `NODE_ENV=development`, the configured URL uses plaintext `http:`, and the host is `127.0.0.1`, `localhost`, or `::1` loopback without path, query, credentials or fragment. This intentionally does not support remote API servers or production ingress. Do not connect public internet listeners or use Development mode in public production.

Fetches are GET, no-cache, limited to 1500ms each, run concurrently, and return only three predefined states: healthy, unavailable, not-configured. Health endpoint details and exceptions are not sent to browser output. There is no auth, tenant selection, payment or user data.

Note: HTML health status is not an authorization check, an uptime monitor or a security dashboard. The probe also does not verify that a remote API instance is production-safe, and an application that binds 127.0.0.1 does not automatically authenticate local users. Automated Node tests are static safeguards; full functional checks need the smoke procedure below.

## Common prerequisites

macOS and Ubuntu 24.04:
- Git; .NET SDK compatible with `global.json` (9.0.202 + latestPatch).
- Node.js 22 and npm 10 or newer.
- npm/NuGet access.
- Both API and Next.js are run on the SAME host for the smoke test.
- No PostgreSQL, Adminer, Docker, SSH DB tunnel or EF migration is required for a basic offline run.
- Never commit actual `.env.local` or database secrets; the sample is a loopback address only.

## macOS instructions (distinct environment)

Mac Terminal A, repository checkout:

```bash
cd ~/Projects/ArginTaxPayer
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
dotnet build ArginTaxPayer.sln -c Debug --no-restore
dotnet test ArginTaxPayer.sln -c Debug --no-build

cd apps/web
npm ci --no-audit --no-fund
npm test
npm run typecheck
npm run build
npm audit --omit=dev --audit-level=high
```

For the smoke test, Mac Terminal A, at project root:

```bash
cd ~/Projects/ArginTaxPayer
unset ConnectionStrings__ArginTaxPayerDb
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/ArginTaxPayer.Api --urls http://127.0.0.1:5088
```

Mac Terminal B, same machine:

```bash
cd ~/Projects/ArginTaxPayer/apps/web
ARGIN_API_INTERNAL_BASE_URL=http://127.0.0.1:5088 npm run dev
```

Mac Terminal C:

```bash
curl -i http://127.0.0.1:5088/health/live
curl -i http://127.0.0.1:5088/health/ready
curl -I http://127.0.0.1:3000/
curl -s http://127.0.0.1:3000/ | grep -o 'اجرای API:\|آمادگی دیتابیس:\|سالم\|در دسترس نیست' | head
```

Expect API liveness=200, readiness=503 (no PostgreSQL configured), web=200; UI liveness 'سالم', readiness 'در دسترس نیست'. Open `http://127.0.0.1:3000/`. Terminate both dev servers via Ctrl+C. Repeat web with API stopped to confirm page renders 'در دسترس نیست' instead of an error. With env var unset, both show 'تنظیم نشده'.

## Ubuntu VPS instructions (distinct environment)

SSH to VPS, ensure local uncommitted edits are inspected before switch/pull:

```bash
cd ~/projects/ArginTaxPayer
git status --short
git diff --stat
git diff --cached --stat
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
```

On VPS shell A:

```bash
cd ~/projects/ArginTaxPayer
unset ConnectionStrings__ArginTaxPayerDb
ASPNETCORE_ENVIRONMENT=Development dotnet run \
  --project src/ArginTaxPayer.Api --urls http://127.0.0.1:5088
```

On VPS shell B:

```bash
cd ~/projects/ArginTaxPayer/apps/web
ARGIN_API_INTERNAL_BASE_URL=http://127.0.0.1:5088 npm run dev
```

On VPS shell C:

```bash
curl -i http://127.0.0.1:5088/health/live
curl -i http://127.0.0.1:5088/health/ready
curl -I http://127.0.0.1:3000/
```

To see the VPS UI from the Mac browser, open a tunnel in a separate **Mac** terminal:

```bash
ssh -N -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:13000:127.0.0.1:3000 \
  argindev@85.133.244.156
```

Then visit `http://127.0.0.1:13000/` on Mac. The API health probe happens on the VPS server-side Next.js process, not in the Mac browser; do not open public ports 3000 or 5088. Stop services and SSH tunnel after testing.

## PostgreSQL/Adminer relevance

No migration or new table. If the API has no DB connection string, readiness 503 is EXPECTED and is a useful distinction from liveness 200. If independently testing database readiness with the existing `argintaxpayer_test`, explicitly supply only that test DB connection and never run migrations on production; this is optional and not required in this Step. Adminer and Docker are not dependencies for this smoke test.

## GitHub Actions

The previously configured `Phase 02 .NET CI` workflow automatically tests .NET on macOS and Ubuntu and frontend on Ubuntu via `npm ci`, Node tests, TypeScript, blocking high-severity npm audit and Next build. The workflow does not run an API server, so HTML-to-API integration is **manual** on Mac and VPS. Review three jobs on the Step 19 commit at `https://github.com/HekmatBeigverdi/ArginTaxPayer/actions`; commit does not prove they passed.

## Acceptance

1. Mac .NET build/tests + npm test/typecheck/build/audit pass.
2. VPS .NET build/tests + npm test/typecheck/build/audit pass.
3. API on loopback: live=200; ready=503 without DB; Next page displays healthy/unavailable independently.
4. With API absent, UI stays usable; with no env var, UI shows not-configured.
5. Next.js does not make client/browser requests to the API and does not leak internal errors.
6. Three GitHub Actions jobs green.
7. No migrations, external calls, public exposure, merge to main or next step without user approval.

Await operator verification; no claims of local execution by GitHub connector.
