# Reviewed Restored Database No-Op Deploy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Allow an explicitly reviewed restored database to receive a release only when the candidate migration artifacts are identical to the currently deployed release.

**Architecture:** Add one root-owned validation command to the deployment helper. The Ubuntu preflight calls it to verify restore selection/state consistency; the release path calls the same function and skips database access only when both migration artifacts match the current release exactly. All other cases retain the refusal.

**Tech Stack:** Python 3.11 standard library, Bash, Python `unittest`.

**Spec:** `docs/superpowers/specs/2026-11-17-reviewed-restored-db-noop-deploy-design.md`

## Global Constraints

- Never remove or rewrite `backup-db.env` as a deploy workaround.
- Never run migrations against a restored database in this change.
- Require `recoveryMode=false` and matching selected database, recovery state, and staged restore metadata.
- Require byte-for-byte equality of `migrations.json` and `migrations.sql` before skipping migrations.
- Preserve the existing migration backup/apply/restart flow when no restored database selection exists.

## Review Focus

- Recovery review still active: must refuse before database access; tested in Task 1.
- Selection points to a different recovery database: must refuse; tested in Task 1.
- Restore-stage ID or database is stale: must refuse; tested in Task 1.
- Candidate SQL changes while the ordered migration manifest stays the same: must refuse; tested in Task 2.
- Candidate changes schema while no restore selection exists: existing migration flow and tests must remain unchanged; tested in Task 2.

---

### Task 1: Validate approved recovery selection

**Files:**
- Modify: `ops/phase18-deploy/argin-taxpayer-release.py`
- Modify: `scripts/phase18-backup-preflight.sh`
- Test: `tests/ops/test_schema_deployment.py`

**Interfaces:**
- Produce `reviewed_recovery_selection() -> str | None`; returns the selected recovery database when state and stage agree, returns `None` when no selection exists, and raises `Refused` for any invalid or unreviewed selection.
- Expose root-only helper mode `--check-recovery-selection` for the preflight script.

- [x] Write tests for no selection, matching reviewed selection, `recoveryMode=true`, mismatched selected database, mismatched stage ID/database, malformed env content, and symlinked files.
- [x] Run `python3 -m unittest tests/ops/test_schema_deployment.py -v` and confirm the new cases fail for the expected missing behavior.
- [x] Implement bounded no-follow reads and exact identity checks in the release helper.
- [x] Replace the unconditional shell preflight rejection with the helper validation command.
- [x] Run the focused test module and shell syntax check; confirm the new cases pass and old cases remain green.

### Task 2: Allow only schema-identical deployment on reviewed recovery DB

**Files:**
- Modify: `ops/phase18-deploy/argin-taxpayer-release.py`
- Modify: `tests/ops/test_schema_deployment.py`

**Interfaces:**
- `migrate_release` receives the resolved current release when a current symlink exists.
- With a reviewed selection, it compares current and candidate `api/schema/migrations.json` and `api/schema/migrations.sql`; exact matches skip DB access, and any difference raises `Refused` before services stop.
- Without a selection, existing behavior is unchanged.

- [x] Write tests that prove identical migration artifacts skip database calls and that a changed manifest or SQL refuses before stop/backup/apply.
- [x] Run the focused test module and confirm the new tests fail first.
- [x] Implement the strict comparison and no-migration path.
- [x] Run focused deployment tests, the complete `tests/ops` deployment suite, and `bash -n scripts/phase18-backup-preflight.sh`.
- [x] Run `git diff --check` and inspect the final diff for unchanged legacy migration behavior.

### Task 3: Publish the reviewed host guard, then release

**Files:**
- Modify: `scripts/phase18-install-deploy-helper.sh` only if the existing installer cannot safely replace both reviewed helpers.

- [ ] Obtain independent review of the guard changes and fix any high-priority findings.
- [ ] Run CI for the reviewed branch and install the exact reviewed helper/preflight SHA using the guarded installer before merge, so the automatic main release can use the updated guard.
- [ ] Verify the installed helper hashes, full read-only preflight, and `--check-recovery-selection` result.
- [ ] Merge the reviewed change; allow the main push to start the production release.
- [ ] Confirm migration artifacts match exactly, no DB migration ran, and the release pointer advances only after health passes.
- [ ] Verify API readiness and public web response.
