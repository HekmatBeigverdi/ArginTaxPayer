# Phase 02 — Step 18: Reproducible Frontend CI, Lockfile and TypeScript Gate

Date: 2026-10-11
Branch: `phase/02-foundation`
Status: SOURCE COMMITTED; MAC/VPS/GITHUB WORKFLOW EXECUTION AND SECURITY REMEDIATION PENDING
Previous gate: Step 17 owner-approved.

## Scope

- Verified that `apps/web/package-lock.json` (npm lockfile v3) was **already present** in the phase branch, with declared direct dependencies matching `apps/web/package.json`; it is retained unchanged.
- Switched GitHub Actions frontend dependency installation from `npm install` to `npm ci --no-audit --no-fund`. In contrast to `npm install`, `npm ci` requires a consistent committed lockfile and does not rewrite it.
- Added `npm run typecheck` script (`tsc --noEmit`) and a dedicated CI step before `next build`.
- Added Node.js built-in tests `apps/web/tests/dependency-integrity.test.mjs` to verify the top-level manifest/lockfile match, dependency package lock digests, and no install-time secret handling script.
- Added a non-blocking `npm audit --omit=dev --audit-level=high` visibility step to CI. **Non-blocking audit is a temporary diagnostic, NOT a security acceptance gate.** Any high/critical findings are blockers for real customer-facing deployment and require a separately reviewed upgrade of package.json + package-lock.json plus retests.
- No backend code, DB migrations, API routes, VPS ports, or deployment settings changed.

## Known security issue: action required before production

The currently committed lockfile marks **Next.js 15.5.0 as deprecated due to a security vulnerability**. This step intentionally DOES NOT upgrade that version without a verified compatible regenerated lockfile and full build validation. `npm audit` can report additional current advisories. The project is still developer-only; do not publish this frontend to the public internet or treat security as approved until a patched version is selected and dependencies are fully tested. The workflow audit step currently uses `continue-on-error` solely to make findings visible without blocking infrastructure iteration. A future step must remediate vulnerabilities and turn the audit into a strict deployment gate.

## Requirements

- Node.js 22.x on both macOS and Ubuntu VPS; npm >= 10.
- Git checkout of `phase/02-foundation`.
- npm registry network access for `npm ci` (or compatible configured trusted proxy).
- .NET SDK from `global.json` only for optional backend regression.
- PostgreSQL, Docker, Adminer, SSH DB tunnel, `psql`, EF tools and actual secrets **NOT required**.
- Mac and VPS working trees must be clean or have changes inspected/stashed/committed BEFORE switching/pulling. Never run `git reset --hard` or `git clean -fd` to fix an uncommitted-change warning.

## macOS exact commands

```bash
cd ~/Projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
node --version
npm --version
cd apps/web
npm ci --no-audit --no-fund
npm test
npm run typecheck
npm run build
npm audit --omit=dev --audit-level=high
```

The last audit command may return nonzero due to known dependency advisories. It is a finding to record, **not** permission to ignore it in production. Report package, affected version, and patched release; no real credentials are needed. To check deterministic lockfiles without changing them:

```bash
git diff --exit-code -- apps/web/package.json apps/web/package-lock.json
```

Run on Mac loopback in terminal 1:

```bash
cd ~/Projects/ArginTaxPayer/apps/web
npm run dev
```

Terminal 2:

```bash
curl -I http://127.0.0.1:3000/
curl -s http://127.0.0.1:3000/ | grep -o 'dir="rtl"' | head -n 1
```

Expected HTTP 200 and `dir="rtl"`. Stop using Ctrl+C.

Optional backend regression (from project root; no DB):

```bash
cd ~/Projects/ArginTaxPayer
unset RUN_ARGINTAXPAYER_PG_INTEGRATION
unset ConnectionStrings__ArginTaxPayerDb
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Debug --no-restore
dotnet test ArginTaxPayer.sln -c Debug --no-build
```

## Ubuntu VPS exact commands

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
node --version
npm --version
cd apps/web
npm ci --no-audit --no-fund
npm test
npm run typecheck
npm run build
npm audit --omit=dev --audit-level=high
```

Before pull, the VPS has previously shown staged/unstaged changes. If `git status --short` is not empty, **stop** and review `git diff --stat`, `git diff --cached --stat`; if safe to preserve them temporarily, `git stash push -u -m "VPS before step 18"`. Do not blindly restore the stash after pulling because generated lockfiles or earlier local edits can conflict. `npm ci` replaces `node_modules` but does not delete your tracked source edits.

Use two VPS shells to test loopback frontend:

```bash
cd ~/projects/ArginTaxPayer/apps/web
npm run dev
```

```bash
curl -I http://127.0.0.1:3000/
curl -s http://127.0.0.1:3000/ | grep -o 'dir="rtl"' | head -n 1
```

For VPS-hosted browser view from Mac, SSH local forward 13000 -> VPS loopback 3000:

```bash
ssh -N -o ExitOnForwardFailure=yes \
  -L 127.0.0.1:13000:127.0.0.1:3000 \
  argindev@YOUR_VPS_IP
```

Visit Mac `http://127.0.0.1:13000`. Do not expose port 3000 publicly. Stop frontend and tunnel after testing.

Optional backend regression on Ubuntu (from repo root):

```bash
unset RUN_ARGINTAXPAYER_PG_INTEGRATION
unset ConnectionStrings__ArginTaxPayerDb
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build
```

## GitHub Actions review

Navigate to `https://github.com/HekmatBeigverdi/ArginTaxPayer/actions`. The existing workflow `Phase 02 .NET CI` runs three jobs:
1. `.NET 9 | ubuntu-24.04`
2. `.NET 9 | macos-15`
3. `Next.js RTL | ubuntu-24.04` — Verify `npm ci`, `npm test`, `npm run typecheck`, `npm run build`; inspect dependency audit warnings. `npm audit` is currently non-blocking, so a green overall job **does not mean dependency security is clean**.

Do not merge this phase to `main` or deploy automatically.

## PostgreSQL/Adminer

No migrations or changes. The existing `argintaxpayer_test` DB and VPS PostgreSQL Docker stack are not prerequisites. Adminer stays as configured with loopback access only. Do **not** run `dotnet ef database update` for Step 18.

## Acceptance checklist

- macOS npm ci succeeds without changing the lockfile.
- macOS npm test, typecheck and build succeed; RTL smoke test passes.
- Ubuntu VPS npm ci, npm test, typecheck and build succeed; loopback RTL smoke test passes.
- Backend regressions pass on both environments if run.
- GitHub Actions all three jobs pass and audit findings are reviewed and recorded.
- Security caveat and patched Next.js upgrade remain explicit action items; Step 18 does not certify production readiness.
- No schema changes, secret disclosures, main merge, release or automatic deployment.
