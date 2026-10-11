# Phase 02 — Step 20: Automated API/Next.js Integration Smoke

Branch: `phase/02-foundation`
Status: SOURCE COMMITTED — REAL BUILD/TEST & GITHUB ACTIONS CONFIRMATION REQUIRED
Prerequisite: Step 19 accepted by project owner.

## Scope
Step 19 provided a server-side Development loopback API health bridge but required manual smoke testing. Step 20 adds an executable Node.js test script at `scripts/phase-02-integration-smoke.mjs` and a fourth independent job to `.github/workflows/phase-02-dotnet-ci.yml`.

The script launches built Release ASP.NET Core API on `127.0.0.1:15088`, then launches Next.js dev server on `127.0.0.1:13019`. It verifies:
- `GET /health/live` returns HTTP 200 and `Healthy`;
- `GET /health/ready` returns HTTP 503 and `Unhealthy` with no database configured;
- Next.js returns HTTP 200 and HTML `lang="fa" dir="rtl"`;
- server-side Next.js health labels show API = "سالم", DB = "در دسترس نیست";
- after stopping the ASP.NET process, Next.js still serves HTTP 200 with API and DB statuses = "در دسترس نیست".
The test checks that neither port is in use before starting, applies bounded HTTP/boot timeouts, logs only sanitized status messages by default, and attempts to stop child processes on completion/failure.

**It does not test** credentials, CORS, remote API deployment, taxpayer isolation/authorization, persistence integration, signed fiscal invoices, payment callbacks or a production health dashboard.

## Prerequisites (both operating systems)
- Git checkout of `phase/02-foundation` with a clean/inspected working tree.
- .NET SDK compatible with `global.json` (9.0.202 latestPatch); Node.js 22 and npm >= 10.
- Internet / configured trusted NuGet and npm sources for dependency restore.
- Ports **15088** (API) and **13019** (Next.js) must be free on local loopback.
- Do not keep the Step 19 API/Next.js smoke instances running on these same test ports.
- PostgreSQL, Docker, Adminer, database SSH tunnel, dotnet-ef, credentials and migrations are **not necessary** for this smoke.
- The test sets `ASPNETCORE_ENVIRONMENT=Development` only on loopback-bound test process and forcibly sets empty DB connection in that child. Never run these developer servers on public addresses.

## macOS — complete command sequence

In macOS Terminal (use your real repository path, which may differ from `~/Projects/ArginTaxPayer`):

```bash
cd ~/Projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation

node --version
npm --version
dotnet --version

unset RUN_ARGINTAXPAYER_PG_INTEGRATION
unset ConnectionStrings__ArginTaxPayerDb

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

Expect:
```text
PASS: API 200 / 503; Next.js 200; RTL and independent health states.
PASS: Frontend remains available when API is stopped.
STEP 20 INTEGRATION SMOKE PASSED
```

The script terminates its child processes. If it reports a port conflict, inspect the port first instead of killing an unrelated service:

```bash
lsof -nP -iTCP:15088 -sTCP:LISTEN
lsof -nP -iTCP:13019 -sTCP:LISTEN
```

Check the command exit status immediately after running with `echo $?`. Success is 0.

## Ubuntu VPS — complete command sequence

From macOS first connect over SSH:

```bash
ssh argindev@85.133.244.156
```

Inside Ubuntu (important: the VPS previously had staged/unstaged changes):

```bash
cd ~/projects/ArginTaxPayer
git status --short
git diff --stat
git diff --cached --stat
```

If there are uncommitted changes, inspect and preserve them before checkout/pull. Do not use `git reset --hard` or `git clean -fd` as a workaround.

With a clean working tree:

```bash
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation

node --version
npm --version
dotnet --version

unset RUN_ARGINTAXPAYER_PG_INTEGRATION
unset ConnectionStrings__ArginTaxPayerDb

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

Expected messages same as macOS. API and Next.js are launched only on VPS loopback addresses and stopped automatically. Do not open firewall ports or expose developer servers publicly.

For Ubuntu port conflict investigation:
```bash
ss -ltnp | grep -E ':(15088|13019)\\b' || true
```

## CI execution

The Step 14-19 workflow still has the three original jobs. Step 20 adds:
`API + Next.js | Ubuntu loopback smoke`.
The new job installs the pinned .NET SDK and Node 22, restores/builds ASP.NET Core Release, runs `npm ci`, then runs `node scripts/phase-02-integration-smoke.mjs`. It does not connect to any real database. All four jobs must be green for Step 20 acceptance.

GitHub Actions: https://github.com/HekmatBeigverdi/ArginTaxPayer/actions

## PostgreSQL / Adminer

No EF migration or schema change. `argintaxpayer_dev` and `argintaxpayer_test` must remain unchanged. The 503 readiness response is **expected** because the smoke process deliberately supplies an empty connection string. Do not modify PostgreSQL readiness logic to force a 200 in this step. Adminer and Docker are not needed.

## Acceptance

1. Backend restore/build/tests and frontend npm ci/tests/typecheck/build/audit succeed on Mac.
2. Step 20 executable prints BOTH PASS messages and exits zero on macOS.
3. Same successful commands on Ubuntu VPS independently.
4. GitHub Actions shows four successful jobs, including the loopback integration smoke.
5. API shutdown does not break Next.js rendering, and the test leaves no listening test processes.
6. No real credentials, external API calls, migrations, production deployments or merge to `main`.
7. Step 21 starts only with explicit owner approval.

Status is **not operationally accepted** until runtime results are reported; GitHub file creation does not prove tests passed.
