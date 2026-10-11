# Phase 02 — Step 14: .NET CI Pipeline (Ubuntu and macOS)

Date: 2026-10-11
Branch: `phase/02-foundation`
Status: WORKFLOW COMMITTED; REMOTE ACTIONS RUN AND LOCAL/VPS VERIFICATION PENDING
Prerequisite: Step 13 explicitly approved.

## Goal and implementation

Add the first GitHub Actions .NET quality gate without changing the existing API, Worker, PostgreSQL model or migrations.

New file: `.github/workflows/phase-02-dotnet-ci.yml`.

The workflow:
- Runs when pushing to `phase/02-foundation` and on pull requests targeting `main` or the phase branch.
- Can also be manually dispatched when GitHub exposes the workflow in its Actions UI (for some configurations, manual dispatch is available only after the workflow exists on the default branch).
- Builds a matrix on `ubuntu-24.04` and `macos-15`, with `fail-fast: false` so a single OS failure cannot hide another.
- Uses `actions/setup-dotnet@v4` with `global-json-file: global.json` (SDK pinned to 9.0.202 / latestPatch).
- Runs `dotnet restore`, `dotnet build -c Release --no-restore`, and `dotnet test -c Release --no-build`.
- Captures test reports as TRX and uploads per-OS artifacts retained for 7 days.
- Limits workflow token permissions to `contents: read`, and checkout with `persist-credentials: false`.
- Explicitly disables opt-in PostgreSQL integration tests and supplies no database connection, password, signing key or fiscal API credentials.
- Avoids Docker, VPS and actual PostgreSQL connections in this step.

## Security and limitations

This is a **source/build/unit test CI gate**, not a production or real database integration test. Existing tests that return early when `RUN_ARGINTAXPAYER_PG_INTEGRATION` is not `1` will show normal framework success even though their real DB scenario was not executed; manual PostgreSQL tests remain mandatory when DB models change. No migrations run; no schema updates, API deployment or Worker jobs start. GitHub-hosted runners are external ephemeral machines and cannot reach a loopback-bound PostgreSQL service on the VPS (this is intentional).

Later: formal PR branch protection, NuGet lockfiles and restore locking, frontend checks, integration environment, dependency/security scanning, release artifacts, and concurrency/quality gates for other languages. Nothing in this step implies these are finished.

The workflow has no deployment privileges and no production secrets. Review third-party action version pinning and the GitHub Actions permissions/settings before treating this as a production-security boundary.

## Prerequisites

Common:
- The `phase/02-foundation` branch exists in `HekmatBeigverdi/ArginTaxPayer`.
- .NET SDK compatible with `global.json` is installed on both macOS and Ubuntu VPS.
- Internet access to NuGet for restore and GitHub for checkout/push.
- No database connection, SSH tunnel, Adminer, Docker or `psql` needed for Step 14.

GitHub:
- GitHub Actions are enabled for the repository and allowed to use `actions/checkout`, `actions/setup-dotnet`, and `actions/upload-artifact`.
- The repository Actions page is accessible to the account.
- GitHub token in Workflow has `contents: read`; no configured Secrets are required.

## macOS: validate the exact CI pipeline

Run in macOS Terminal:

```bash
cd ~/Projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
dotnet --version
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build \
  --logger "trx;LogFileName=phase-02-tests.trx" \
  --results-directory "./artifacts/test-results"
```

Check the generated TRX files:

```bash
find ./artifacts/test-results -name '*.trx' -print
```

Do **not** commit test reports; generated local reports must remain outside commits or be ignored. For this reason, preferably use a temporary directory (`/tmp`) or remove generated files before committing if the Git ignore rules do not cover `artifacts/`. No database environment variable is necessary; if a previously exported `RUN_ARGINTAXPAYER_PG_INTEGRATION` is still `1`, unset it before running the offline suite:

```bash
unset RUN_ARGINTAXPAYER_PG_INTEGRATION
unset ConnectionStrings__ArginTaxPayerDb
```

## Ubuntu VPS: validate independently

Run on Ubuntu:

```bash
cd ~/projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/02-foundation
git pull --ff-only origin phase/02-foundation
dotnet --version
unset RUN_ARGINTAXPAYER_PG_INTEGRATION
unset ConnectionStrings__ArginTaxPayerDb
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln -c Release --no-restore
dotnet test ArginTaxPayer.sln -c Release --no-build \
  --logger "trx;LogFileName=phase-02-tests.trx" \
  --results-directory "./artifacts/test-results"
find ./artifacts/test-results -name '*.trx' -print
```

Do **not** run Docker Compose, migrations or open ports for Step 14. The VPS PostgreSQL can remain running untouched. No need to deploy the application to prove CI.

## GitHub Actions: verify the remote pipeline

Open:
`https://github.com/HekmatBeigverdi/ArginTaxPayer/actions`

Find `Phase 02 .NET CI`, select the workflow run corresponding to the latest `phase/02-foundation` commit, and inspect BOTH jobs:
- `.NET 9 | ubuntu-24.04`
- `.NET 9 | macos-15`

Verify Restore, Compile Release, Test Release and Publish test results steps. The expected conclusion is `Success` for both jobs. Download the TRX artifacts if investigation is needed; they are temporary (retention 7 days).

If no run exists, check GitHub Actions repository policy, whether a push event was emitted after the workflow file was added, and whether workflow runs were disabled. A manual dispatch may require the workflow file to be on the default branch; do not merge just to force it. Ask for operator approval before changing Actions policy or branch protection.

## Optional Adminer/PostgreSQL health (not a dependency)

On Ubuntu, only if the operator wants to independently check earlier services:

```bash
sudo docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
PG_ID=$(sudo docker ps -q --filter label=com.docker.compose.service=postgres | head -n 1)
test -n "$PG_ID" || { echo 'PostgreSQL container unavailable'; exit 1; }
sudo docker exec "$PG_ID" psql -U argintaxpayer \
  -d argintaxpayer_test -tAc "SELECT current_database();"
```

Adminer is optional; existing VPS loopback `8081` can be reached from macOS using SSH `-L 127.0.0.1:18081:127.0.0.1:8081`; browse to `http://127.0.0.1:18081`. Select PostgreSQL server `postgres` and DB `argintaxpayer_test`. There is **no** Step 14 migration to inspect or apply.

## Acceptance record

1. macOS Release restore/build/full unit tests pass.
2. Ubuntu VPS Release restore/build/full unit tests pass.
3. GitHub Actions shows success in both OS matrix jobs for the phase branch.
4. TRX artifacts are accessible and do not contain secrets.
5. No migrations, PostgreSQL schema changes or external calls occurred.
6. No merge to main, deployment, release or next phase step before explicit approval.

Status remains **pending runtime verification** until the developer/operator confirms results; a committed YAML file is not equivalent to a successful workflow run.
