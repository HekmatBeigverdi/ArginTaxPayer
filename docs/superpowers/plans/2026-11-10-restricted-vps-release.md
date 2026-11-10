# Restricted VPS Release Implementation Plan

**Goal:** Complete the already selected GitHub-built, environment-approved application deployment on the replacement VPS.

**Spec:** `docs/superpowers/specs/2026-11-05-github-approved-release-deployment-design.md`

**Architecture:** GitHub uploads a SHA-addressed bundle. A root-owned helper accepts only `--deploy <40 lowercase hex characters>`, snapshots and validates uploads, installs dependencies without root privileges, publishes immutable code, and switches the current symlink with health-based application rollback.

**Constraints:** Preserve `/etc/argintaxpayer`, systemd units and database contents. No general passwordless sudo. Never execute uploaded scripts as root. Keep host-key verification and the existing production environment gate. Migrations are a separate reviewed operation.

## Task 1 — Privilege boundary

- [x] Add meaningful filesystem tests: valid extraction, traversal/link/device rejection, checksum/source mismatch, root-only file snapshots, safe final copy, and rollback after service/health failure.
- [x] Observe failure before implementing the helper.
- [x] Implement `ops/phase18-deploy/argin-taxpayer-release.py` using only the Python standard library.
- [x] Verify tests; perform separate security review of every root file operation.

## Task 2 — Operator installation and workflow

- [x] Add idempotent explicit installer for root-owned helper and health probe; require the existing dedicated accounts and fixed runtime paths.
- [x] Update workflow: deploy main pushes through production approval, serialize deployments, SHA-specific upload, reject stale main, call only the fixed helper via noninteractive sudo.
- [x] Add helper checks to CI before existing .NET/web checks.
- [x] Verify shell syntax and workflow structure; document operator commands and limitations.

## Review focus

- Writable upload ancestors, source symlinks/FIFOs and TOCTOU: pin directory/file descriptors, copy to a private root-owned snapshot before validation.
- Malicious tar paths, metadata and resource exhaustion: extract only regular files/directories, reject traversal/links/secrets, bound sizes/counts, and never trust archive ownership/modes.
- Dependencies: npm runs as argin-deploy, with scripts/config disabled and a cleared environment; copy results through no-follow descriptors into a root-only immutable tree.
- Failed deployment: serialize, retain previous release, rollback on service/probe failure; no implicit database rollback.
- Old approved run: verify current main immediately before upload; existing immutable release is never overwritten.

## Execution record

- Source base: e4571cea0ef168da4a8e894909f9188004b80930.
- Operator already created the account, pinned host key, configured secrets and installed the helper-only sudoers rule.
- Work stays on a dedicated branch; installing on the VPS and exercising real SSH/deployment remain operator steps.
- Final independent review found a post-switch signal window, unbounded tar extension metadata, and lexical symlink ancestors omitted by resolve-first trust checks. Each was reproduced by a failing regression and fixed; helper suite 22/22 passes.
- Tests use real temporary files/archives and atomic symlinks; trust tests normalize UID metadata because the local sandbox remaps filesystem root ownership. No real root/service/SSH deployment is claimed.
- Ruling: dependency installation remains on the VPS as the existing stage script requires, but runs under argin-deploy with lifecycle scripts disabled; application build output is reused unchanged.
- Ruling: immutable releases are reused only when stored source/checksum manifests match the newly validated bundle. Legacy releases without the manifest are not overwritten.
- Application .NET/web code is unchanged; complete application regressions run in the PR workflow, not this partial local snapshot.
