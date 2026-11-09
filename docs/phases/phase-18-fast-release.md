# Phase 18 — Fast deployment and routine updates

**Status:** GitHub Actions release automation and a restricted server helper are implemented in source. GitHub settings, deploy secrets, one-time Ubuntu helper installation, and production runtime approval are operator setup items. No new VPS deployment or production activation is claimed here.

## Routine release path

1. Developer creates a feature branch, makes the change, and runs local checks against the local/test database. Local secrets and database credentials stay outside Git.
2. Push the feature branch and open a Pull Request to `main`. The GitHub workflow runs .NET restore/tests/publish and web install/tests/typecheck/build, installs the locked production Node dependencies in CI, then produces an artifact bound to the exact commit SHA. A PR cannot deploy.
3. Required CI checks and a human review must pass. Merge the PR to `main`.
4. The `main` push workflow builds the merged SHA once and uploads its immutable artifact. The production deployment job waits at GitHub's `production` Environment approval gate.
5. After an authorized reviewer approves, the job verifies the artifact SHA and checksums, transfers it over host-key-pinned SSH to `/var/lib/argin-taxpayer-deploy/incoming/<SHA>/`, and invokes only `sudo -n /usr/local/sbin/argin-taxpayer-release --sha <SHA>`.
6. The fixed helper moves the upload into root-owned private processing storage, revalidates the bundle, stages `/opt/argintaxpayer/releases/<SHA>`, atomically promotes the symlink, restarts only the named API and Web units, checks loopback health, and restores the prior application release on health failure. It does not install dependencies from the network, run migrations, or change application/database/provider secrets.

## Required one-time setup

Complete [Phase 18 GitHub release operations](phase-18-github-release-operations.md) before expecting unattended deployment. In particular, protect `main`, require a review and CI checks, configure a required reviewer on the `production` Environment, set its four deploy secrets, provision the restricted `argin-deploy` SSH identity, and install the root-owned helper on Ubuntu.

The host helper still refuses promotion until the already reviewed server activation marker and private config preconditions are satisfied. This CI/CD setup does not authorize public application activation, tax authority sends, SMS, or payment operations. Existing sandbox behavior remains governed by the application's persisted fiscal-memory environment.

## Database and migration boundary

Application release deployment never runs EF migrations and has no production DB credential. A schema change is a separate reviewed operation: produce and review SQL, assert the exact target database, verify a fresh backup and isolated restore, approve and apply the migration under the migration role, then deploy a backward-compatible application release. Application rollback does not reverse a schema migration.

The AutoSSH development database tunnel is not used in GitHub Actions and must not be repurposed as a production migration channel.

## Current Phase 18 boundary

Automating application release does not resolve the existing Phase 18 production readiness gates. Production identity routes and tax-authority routing remain separately gated; this workflow never changes those flags. In particular, do not infer that successful CI or an HTTPS health check proves Sandbox/live invoice acceptance or authorizes a real taxpayer submission.
