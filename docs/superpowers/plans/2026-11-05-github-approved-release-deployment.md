# GitHub-approved application release deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deploy the exact application artifact for a reviewed commit merged to `main` after approval in GitHub's protected production environment, with fail-closed checks and app-only rollback.

**Architecture:** GitHub Actions runs the existing .NET and web quality gates, packages once by full commit SHA, and pauses the deployment job for the `production` Environment reviewer. A restricted SSH identity uploads the bundle; a fixed root-owned helper validates and stages it, promotes the immutable release using the current symlink, restarts only the dedicated API/Web units, and rolls back the symlink on failed health checks. Migration remains a separate operator-approved process.

**Tech Stack:** GitHub Actions, GitHub Environment protection, OpenSSH, Bash, Node.js contract tests, ASP.NET Core 9, Next.js 15, systemd, Ubuntu 24.04.

**Spec:** `docs/superpowers/specs/2026-11-05-github-approved-release-deployment-design.md`

## Global Constraints

- `main` is the single production release branch; feature changes enter through Pull Requests.
- Application deployment never executes EF migrations or modifies database state.
- Secrets and private config remain on the server under `/etc/argintaxpayer`; no database/provider credentials enter GitHub artifacts.
- The deployment preserves Sandbox behavior and does not enable Production tax sending, SMS, identity onboarding, or other feature flags.
- Only loopback-bound application services are restarted; do not alter Nginx, PostgreSQL, DNS, or unrelated services.
- Every artifact and release directory is bound to the exact 40-character Git SHA and SHA-256 checksums.
- Never grant root SSH or general `NOPASSWD:ALL`; use a fixed root-owned deployment helper with strict argument validation.
- The existing AutoSSH database tunnel is for local development and is not used by deployment.
- Schema changes use an independently reviewed and approved migration procedure.

## Review Focus

- **PR or feature-branch event accidentally deploys:** test that deploy only runs for a `push` to `main`, after the protected Environment gate.
- **Artifact SHA/checksum mismatch or path traversal:** test that the server helper rejects malformed SHA, wrong metadata/checksum, symlinked archives, and unsafe archive paths before changing `current`.
- **Missing SSH host verification or untrusted deploy identity:** test strict host-key verification and document exact Environment secrets and restricted server identity.
- **Database/schema change bundled with app rollout:** test that the release workflow has no migration command or database secret and document migration as a separate approved operation.
- **Health failure after promotion:** preserve and verify previous-release rollback while leaving database/configuration untouched.

---

### Task 1: Merge-triggered CI artifact and protected deployment gate

**Files:**
- Modify: `.github/workflows/phase18-production-release.yml`
- Modify: `apps/web/tests/phase18-fast-release-contract.test.mjs`
- Create: `apps/web/tests/phase18-main-release-workflow.test.mjs`

**Interfaces:**
- Consumes: existing test/build/package steps and artifact contract (`api.tar.gz`, `web.tar.gz`, `SHA256SUMS`, `build-info.txt`, deploy scripts).
- Produces: package job on Pull Requests to `main` and pushes to `main`; deploy job only on a push to `main`, protected by the `production` Environment, consuming the same run's artifact.

- [ ] **Step 1: Add workflow contract tests** for PR-only verification, main-only deployment, required `production` Environment, same-SHA artifact, strict SSH host verification, and absence of migrations/secrets.
- [ ] **Step 2: Run the focused web contract test** and confirm it fails against the current `workflow_dispatch`-only deployment condition.
- [ ] **Step 3: Update the workflow** to run package checks on PRs to `main` and pushes to `main`; make deploy conditional only on the successful main push, and rely on required reviewers configured on the `production` Environment. Keep deployment disabled for feature-branch and PR events.
- [ ] **Step 4: Keep artifact production single-build**: artifact includes full `GITHUB_SHA`, archives, checksums, and the reviewed deployment helper payload; deploy job downloads that artifact and must not rebuild.
- [ ] **Step 5: Run the focused workflow contract test** and confirm it passes.

### Task 2: Restricted server deployment entry point

**Files:**
- Create: `ops/phase18-deploy/phase18-release-root.sh`
- Create: `ops/phase18-deploy/release-core.sh`
- Create: `scripts/phase18-install-deploy-helper.sh`
- Modify: `scripts/phase18-release-stage.sh`
- Modify: `scripts/phase18-release-promote.sh`
- Create: `apps/web/tests/phase18-deploy-helper-contract.test.mjs`

**Interfaces:**
- Workflow invocation: `sudo /usr/local/sbin/argin-taxpayer-release --sha <40-lowercase-hex>`.
- Fixed incoming bundle: `/var/lib/argintaxpayer-deploy/incoming/<SHA>/`; no caller-controlled arbitrary paths, shell strings, systemd units, or environment variables.
- Installer: `sudo bash scripts/phase18-install-deploy-helper.sh --install`; idempotent, installs root-owned code/config, and does not start services, alter DB, or create activation approval.
- Root helper: validates fixed bundle path, SHA, `build-info.txt`, checksums, safe archive paths and expected API/Web files; stages immutable release, atomically promotes `current`, restarts only the two named units, invokes existing health probe, and restores the prior symlink on health failure.
- Operator scripts remain available for manual stage/promote use and use the same shared validation/health logic.

- [ ] **Step 1: Add helper contract tests** for valid SHA + fixed path, malformed SHA, wrong bundle SHA, checksum mismatch, symlink/path-traversal rejection, no arbitrary command argument, and no database/migration operation.
- [ ] **Step 2: Run the focused helper contract test** and confirm it fails before helper implementation.
- [ ] **Step 3: Implement the shared release core** in `release-core.sh` so both current operator scripts and the root helper use the same checksum, archive-validation, SHA and health/rollback primitives.
- [ ] **Step 4: Implement the fixed root helper** with the one-command SHA interface and hardcoded paths/units; ensure it never reads or prints private env values and never executes caller-supplied shell.
- [ ] **Step 5: Add the one-time installer** to install root-owned helper files and a sudoers rule limited to the fixed helper; run `visudo -cf` before installing the rule. Refuse unsafe pre-existing files, broad sudo grants, root SSH, and changes to unrelated services.
- [ ] **Step 6: Preserve manual stage/promote behavior** by delegating to the shared core and retaining their existing SHA, activation-marker, health and app-only rollback checks.
- [ ] **Step 7: Run shell syntax checks and focused Node contract tests** for installer, helper, stage and promote scripts.

### Task 3: Operator setup and release/migration runbook

**Files:**
- Modify: `docs/phases/phase-18-fast-release.md`
- Create: `docs/phases/phase-18-github-release-operations.md`
- Modify: `docs/phases/phase-18.md`

- [ ] **Step 1: Document GitHub setup**: protect `main`, require Pull Request review and named CI checks, create `production` Environment with required reviewer(s), and configure `ARGIN_DEPLOY_HOST`, `ARGIN_DEPLOY_USER`, `ARGIN_DEPLOY_SSH_KEY`, `ARGIN_DEPLOY_KNOWN_HOSTS` as Environment secrets.
- [ ] **Step 2: Document one-time VPS preparation** using a separate restricted deployment identity, fixed incoming directory ownership, pinned SSH host key, root helper installation, required server activation/config preconditions, and safe secret-file permissions.
- [ ] **Step 3: Document the developer path** from feature branch and local/test-database verification through PR, required CI, merge, Environment approval, exact-SHA deployment, health result and rollback.
- [ ] **Step 4: Document database handling separately**: no migrations in the app deployment workflow; reviewed SQL, verified target DB identity, backup/restore evidence, explicit operator approval and compatibility/rollback plan for schema changes.
- [ ] **Step 5: Document the operational boundary**: no AutoSSH tunnel in CI, no Production database secret in GitHub, no automatic authority sends or flag changes, and Sandbox remains available by persisted fiscal-memory environment.

### Task 4: Same-SHA verification and rollout readiness

**Files:**
- Modify: `.github/workflows/phase18-production-release.yml`
- Modify: `docs/phases/phase-18-github-release-operations.md`

- [ ] **Step 1: Verify workflow conditions** for PR, feature push, main merge and deployment approval; deploy must be impossible for PR/feature refs.
- [ ] **Step 2: Verify server helper refusals** for invalid SHA, missing activation preconditions, malformed artifact and unavailable previous/current state; no rejection may modify the active symlink.
- [ ] **Step 3: Run workflow tests, helper contract tests, existing web tests/typecheck/build, and API Release tests on the same commit SHA.**
- [ ] **Step 4: Keep live taxpayer submissions and database migrations out of automated acceptance; record operator-only setup tasks and evidence still required before first automated deploy.**
- [ ] **Step 5: Commit implementation in reviewable increments and report exact SHA, passed checks, and server setup still pending.**
