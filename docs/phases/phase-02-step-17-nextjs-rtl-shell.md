# Phase 02 — Step 17: Next.js TypeScript Persian RTL Shell

Date: 2026-10-11
Branch: `phase/02-foundation`
Status: SOURCE COMMITTED — MAC/VPS/CI VALIDATION AND LOCKFILE ACCEPTANCE PENDING
Depends on Step 16 signoff.

## Scope

This step creates the first Next.js App Router TypeScript frontend, at `apps/web`, with a Farsi (`lang=fa`), right-to-left (`dir=rtl`) document layout, responsive welcome shell, planned-module placeholders and metadata with robots indexing disabled. The shell explicitly labels modules as **not implemented**: no authentication, taxpayer access, payment, tax submission, database access or mock privileged screens.

Files:
- `apps/web/package.json` (Next.js 15.5.0, React 19.1.1, TypeScript 5.9.x, npm scripts)
- `apps/web/next.config.ts`, `tsconfig.json`, `next-env.d.ts`
- `apps/web/app/layout.tsx`, `app/page.tsx`, `app/globals.css`
- `apps/web/tests/foundation.test.mjs` (Node.js built-in test runner)
- `apps/web/.gitignore`, `.env.example` (no actual secrets)
- `.github/workflows/phase-02-dotnet-ci.yml` adds an Ubuntu Node.js 22 frontend job, alongside the pre-existing macOS and Ubuntu .NET jobs.

### Explicit limitations

1. **No `package-lock.json` is committed yet.** Build dependencies cannot be downloaded in the GitHub editing environment due to network DNS limitations. The workflow currently uses `npm install`, which is not as reproducible as `npm ci`. After the operator confirms `npm install` succeeds, generate and commit a lockfile (see below) and then change CI to `npm ci`. This is an acceptance blocker for fully reproducible frontend CI; do not claim production-ready dependency provenance.
2. No end-to-end rendered DOM/browser accessibility suite is provided: basic Node tests verify static contract text and RTL markup, and `next build` validates TypeScript/Next runtime compilation. Future UI steps need Playwright / accessibility tests.
3. No API requests or Next.js-to-API connectivity are attempted. API, Worker, PostgreSQL and admin permissions remain unaffected.
4. No deployment, shared backend cookies, real customer information, external fonts or private fiscal credentials are added.
5. TypeScript and Next dependency versions must undergo security/upstream compatibility review before public rollout; arbitrary `npm install` upgrades must not be merged without tests.
6. This step does not add frontend build to the macOS GitHub runner; the Next.js job runs on Ubuntu, while macOS manual verification is required.

## Common prerequisites

- Node.js 22.x LTS-compatible runtime and npm 10+ on both macOS and VPS.
- Git and internet access from both machines to npm registry.
- `phase/02-foundation` source checkout.
- Existing .NET SDK compatible with repo `global.json` only for the separate backend regression checks.
- PostgreSQL, Docker, Adminer, `psql`, SSH tunnel and any secrets **are not required for the frontend**.

## macOS instructions

If `node` is missing, install Node.js 22 using Homebrew (`brew install node@22`), then ensure the Homebrew opt path is first in PATH according to `brew info node@22`. Verify `node --version` is major 22 and `npm --version` is 10 or later.

```bash
cd ~/Projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation

cd apps/web
node --version
npm --version
npm install --no-audit --no-fund
npm test
npm run build
```

To test the UI, run in macOS Terminal 1:

```bash
cd ~/Projects/ArginTaxPayer/apps/web
npm run dev
```

In Terminal 2:

```bash
curl -I http://127.0.0.1:3000/
curl -s http://127.0.0.1:3000/ | grep -o 'dir="rtl"' | head -n 1
```

Visit `http://127.0.0.1:3000/` on the **Mac local browser**: Farsi page, right-to-left layout, responsive cards, a development badge, and no real login. Stop via Ctrl+C.

Optional backend regression, from project root:

```bash
cd ~/Projects/ArginTaxPayer
unset RUN_ARGINTAXPAYER_PG_INTEGRATION
unset ConnectionStrings__ArginTaxPayerDb
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Debug --no-restore
dotnet test ArginTaxPayer.sln -c Debug --no-build
```

## Ubuntu 24.04 VPS instructions

Node.js 22 and npm must be installed before testing; use a trusted OS vendor package manager or `nvm`, not `sudo npm install` inside project. Check `node --version` and `npm --version`; stop if Node major is not 22.

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation

cd apps/web
node --version
npm --version
npm install --no-audit --no-fund
npm test
npm run build
```

Run the frontend on loopback only:

```bash
cd ~/projects/ArginTaxPayer/apps/web
npm run dev
```

In a separate VPS terminal:

```bash
curl -I http://127.0.0.1:3000/
curl -s http://127.0.0.1:3000/ | grep -o 'dir="rtl"' | head -n 1
```

If the operator wants to see the **VPS-hosted frontend in the Mac browser**, keep it loopback-bound and open an SSH tunnel from macOS (in a separate local terminal):

```bash
ssh -N -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:13000:127.0.0.1:3000 \
  argindev@YOUR_VPS_IP
```

Then browse to `http://127.0.0.1:13000` on Mac. Never open a public port 3000 for this test. Stop frontend with Ctrl+C. Do not run local Mac and VPS dev frontends simultaneously on the same forwarded port.

Optional backend regression on VPS (from repo root):

```bash
unset RUN_ARGINTAXPAYER_PG_INTEGRATION
unset ConnectionStrings__ArginTaxPayerDb
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build
```

## GitHub Actions review

Open `https://github.com/HekmatBeigverdi/ArginTaxPayer/actions` and find `Phase 02 .NET CI` for the Step 17 commit. It now has three jobs: .NET Ubuntu, .NET macOS, Next.js RTL Ubuntu. All must succeed for CI acceptance. Note: frontend dependency locking remains outstanding.

## Lockfile follow-up (required before final Step 17 acceptance)

After `npm install` succeeds on Mac, `apps/web/package-lock.json` should exist. Commit the reviewed lockfile to the current branch from macOS, without accidentally committing generated folders:

```bash
cd ~/Projects/ArginTaxPayer
git status --short
git add apps/web/package-lock.json
git commit -m "build(phase-02): lock Next.js frontend dependencies"
git push origin phase/02-foundation
```

Then change the frontend GitHub Actions `Install frontend dependencies` command from `npm install --no-audit --no-fund` to `npm ci --no-audit --no-fund` in a separate reviewed change, ensuring Ubuntu/macOS reproducibility. Do not commit `node_modules`, `.next`, `.env`, credentials or personal data.

## Adminer/PostgreSQL

Step 17 has no new migrations, entities, tables or SQL. No Docker, PostgreSQL, Adminer or secret configuration required. Existing VPS PostgreSQL/8081 Adminer containers should remain untouched. If inspecting previous setup, only use the established SSH forward, never public endpoints.

## Acceptance gate

- Mac Node/npm versions verified; `npm test` and `npm run build` pass and page is visually RTL.
- VPS Node/npm versions verified; `npm test` and `npm run build` pass, loopback serving checked.
- Existing .NET suite still passes independently on macOS and VPS.
- All three GitHub Actions jobs are green.
- Lockfile is reviewed and committed; CI switched to `npm ci` before claiming fully reproducible frontend CI.
- No customer data, private credentials, API access, DB migrations, public port or production deployment.
- No main merge, release or Step 18 until explicit user acceptance.
