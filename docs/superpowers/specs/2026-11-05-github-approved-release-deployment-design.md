# GitHub-approved release deployment design

**Date:** 2026-11-05  
**Project:** ArginTaxPayer  
**Status:** Draft for owner approval  
**Target:** Automate app-only releases from reviewed changes merged to `main`, using GitHub Actions and the existing immutable release scripts.

## Intent

A developer works on a feature branch, pushes it, and opens a Pull Request to `main`. GitHub Actions verifies the exact commit and packages the API and web application. After the Pull Request is approved and merged, the deployment job waits for approval in GitHub's protected `production` environment, then transfers and activates that exact artifact on the Ubuntu VPS. Routine deployment must not require copying commands manually.

## Approaches considered

1. **GitHub Actions builds, protected-environment approval deploys (recommended and selected):** deterministic commit-addressed artifact, approval record, existing stage/promote and rollback model. Requires a narrowly scoped server deployment entry point.
2. **Ubuntu pulls source and builds in place:** fewer GitHub configuration steps, but build output depends on VPS state, source checkout and runtime toolchain; rollback and artifact provenance are weaker.
3. **Self-hosted GitHub runner on the production VPS:** removes artifact transfer but places CI execution on the production host and expands its attack surface and maintenance burden.

## Goals

- Make `main` the only production-release branch; feature branches enter by Pull Request.
- Require passing CI and review before merge.
- Build once from the merged SHA and deploy that same artifact; never rebuild during deployment.
- Run the deploy job automatically after a `main` merge, pausing for a required reviewer in GitHub Environment `production`.
- Transfer artifacts over SSH with host-key pinning, verify SHA-256 and embedded source SHA, stage an immutable release under `/opt/argintaxpayer/releases/<SHA>`, then atomically promote `/opt/argintaxpayer/current`.
- Restart only the dedicated API and web services, perform loopback health checks and automatically restore the previous application release on failure.
- Keep local Development/test configuration, production config files, cryptographic keys, provider credentials, database credentials, customer records and tax payloads out of GitHub artifacts and workflow logs.
- Preserve Sandbox capability and the persisted per-fiscal-memory authority-environment contract. Deployment must not turn on live tax sending, SMS, identity registration, or other feature switches.
- Make a release SHA and deploy result easy to identify, and preserve a small number of prior releases for rollback.

## Database and migration boundaries

- Application deployment does not run EF migrations, alter data, or change database roles.
- Database schema changes use a separate reviewed process: generate SQL from the reviewed commit, inspect it, take and verify a backup/restore point, apply with the designated migration identity to the explicitly verified target database, and record the result.
- Use expand/contract migrations so the previously active application release remains compatible during the app-only rollback window.
- Never give GitHub Actions a production database connection string or database password. Do not run migrations against the local/shared development database from the deployment job.
- Preserve the existing split between local test database, VPS test database and the isolated Production database. The current developer AutoSSH tunnel is not part of application deployment.

## Deployment identity and host boundary

- Use a dedicated non-login or restricted deployment identity and a dedicated SSH key held only as a GitHub Environment secret. Pin the VPS host key in `ARGIN_DEPLOY_KNOWN_HOSTS`; never disable host verification.
- Do not grant general passwordless sudo. The current stage/promote scripts call `sudo` interactively, so unattended CI needs a small root-owned deployment wrapper with strict SHA/path validation and only the fixed staging, symlink-switch, service-restart and health-check operations; or an equally narrow sudo policy reviewed for exact command and argument constraints.
- The wrapper must not accept arbitrary shell, service names, file destinations, environment changes or database commands. Keep root-owned unit files and private `/etc/argintaxpayer/*.env` files untouched by application artifacts.
- SSH secrets: `ARGIN_DEPLOY_HOST`, `ARGIN_DEPLOY_USER`, `ARGIN_DEPLOY_SSH_KEY`, `ARGIN_DEPLOY_KNOWN_HOSTS`. Do not store database, signing, tax-authority, SMS or payment-provider secrets in GitHub.

## Workflow

1. Developer branches from current `main`, implements and locally verifies changes, then pushes the branch.
2. Pull Request to `main` runs required .NET/API and web tests, typecheck, production build, release packaging, checksum generation and secret-file exclusion checks.
3. GitHub branch protection blocks merge until required checks pass and a reviewer approves.
4. Merge to `main` builds one immutable release artifact named with the full Git SHA.
5. Deploy job waits at the protected `production` environment review gate.
6. On approval, job verifies the artifact SHA/checksums, uploads it to a SHA-specific incoming directory, invokes the restricted server deployment wrapper, stages and promotes it, then reports health/rollback result and SHA.
7. If deploy approval is rejected or times out, production remains unchanged. If health fails, only the application symlink is rolled back; database rollback is never implicit.
8. Migrations, Production authority activation and any live invoice acceptance remain separate explicitly approved operations.

## Non-goals

- Automatically migrate databases or modify server environment files.
- Automatically enable real tax authority submission, live SMS, payment gateways or identity onboarding.
- Treat a green CI build or HTTP 200 as proof of tenant/security acceptance or tax-authority integration.
- Expose API, database or development ports publicly.
- Add a second long-lived `deploy` branch unless a later operational need justifies it.

## Acceptance criteria for implementation

- Pull Request checks run against the PR SHA and block merge when failing.
- Merge to `main` produces exactly one artifact with matching full SHA and valid checksums.
- Production deploy job is protected by a required reviewer and cannot run for feature branches.
- A successful approved deploy stages/promotes that exact artifact and records the SHA.
- A simulated application health failure returns to the prior symlink and leaves the database and server secrets untouched.
- Missing/invalid SHA, checksum, host key, environment approval or server activation preconditions cause a fail-closed deployment.
- No general `NOPASSWD:ALL`, embedded credential, automatic migration or authority-send path is introduced.
- The workflow and runbook explain local Development/test config, production secrets, database changes, normal releases and rollback.

## Implementation calls

- Use GitHub Actions, GitHub protected environments, an artifact-per-SHA release and the existing release directories/scripts.
- Use `main` as the only release branch.
- After merge, deploy automatically up to a separate required reviewer gate (rather than silently deploying without a production approval).
- The production Environment and server wrapper require one-time operator setup; source changes alone cannot create host secrets or safely change root policy.
- Reuse the existing `phase18-release-stage.sh` and `phase18-release-promote.sh` after adapting their privilege boundary for automation.
