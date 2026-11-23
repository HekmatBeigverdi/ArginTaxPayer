# Recovery Release Migration Allowlist Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow the reviewed restored database to receive exactly the two migrations in release `5efe44c6` while preserving refusal for every other schema delta.

**Architecture:** Replace the stale recovery migration IDs in the root-owned deploy helper with the exact current release delta. Add regression coverage for the two approved IDs and refusal for extra or reordered migrations. Update the deployment runbook with the database/SHA approval and backup requirements.

**Tech Stack:** Python 3.11, `unittest`, GitHub Actions deployment workflow, PostgreSQL EF migration bundle.

**Spec:** `docs/deployment-schema-and-hosted-sandbox.md` and `docs/superpowers/specs/2026-11-17-reviewed-restored-db-noop-deploy-design.md`.

## Global Constraints

- Only `20261123100000_AddInvoiceBusinessSerials` and `20261123133602_AddCatalogVatRatePercent` may be admitted as this release's recovery migration delta.
- Recovery mode stays disabled and selection, staged restore, and recovery state must agree.
- The exact recovery database and merged release SHA must be approved in the protected production environment and root-owned approval file.
- The deploy helper must back up and verify the selected database before applying migration SQL.
- No application promotion occurs unless migration history and application health checks pass.

## Review Focus

- Extra, missing, reordered, or unknown migration IDs continue to refuse before database access.
- Wrong recovery database or release SHA continues to refuse before mutation.
- A failed backup or migration restarts the current application and does not advance the release pointer.
- The active recovery database must already match the current release migration baseline before applying the two new IDs.
- A deployment with no recovery selection retains the normal migrator/backup path.

---

### Task 1: Pin the exact release delta

**Files:**
- Modify: `ops/phase18-deploy/argin-taxpayer-release.py`
- Test: `tests/ops/test_schema_deployment.py`

- [x] Add a test expecting the exact two new migration IDs to pass with matching database/SHA approval.
- [x] Add assertions that an extra migration is refused before DB calls or service stop.
- [x] Run the focused test and observe the expected guard refusal.
- [x] Change the recovery allowlist to the two IDs above; preserve exact-baseline and approval checks.
- [x] Re-run focused tests and full deployment helper tests (50 operations tests passed).

### Task 2: Document the reviewed recovery deployment

**Files:**
- Modify: `docs/deployment-schema-and-hosted-sandbox.md`

- [x] Describe the exact migration delta, schema effects, approval file format, encrypted backup, post-migration verification, and rollback limitation.
- [x] Confirm the documented release SHA is the merged release SHA supplied at deployment time, not hardcoded to this run.

### Task 3: Verify and publish

- [x] Run full Python deploy tests, shell syntax checks, and whitespace validation.
- [ ] Push the fix on `feature/next-software-updates` and create a PR to `main`.
- [ ] Merge only after checks pass; verify the resulting production deployment and both migration history rows.
- [ ] Do not modify the restored database selection or bypass protected environment/recovery approval gates.
