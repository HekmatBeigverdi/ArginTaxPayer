# Reviewed Restored Database No-Op Deploy

## Intent

Allow the already reviewed restored database to receive releases that make no migration change, so web-only fixes can ship while the active database selection remains in place.

## Goals and non-goals

- Permit deploy only when `recoveryMode` is false, the active database selection matches both recovery state and staged restore metadata, and the candidate migration manifest and SQL are byte-identical to the current release.
- Keep all schema-changing releases blocked while a restored database is selected.
- Keep `backup-db.env` in place; do not switch or delete the selected database.
- Continue using the existing normal migration and backup path when no restored database selection exists.

## Approaches

- Keep the unconditional refusal: safest but prevents even web-only releases after an administrator has reviewed and enabled the restored database.
- Allow a strict no-migration deploy after verifying the recovery state and comparing both migration artifacts with the active release: selected because it leaves the database untouched and only accepts an explicitly reviewed, schema-identical release.
- Migrate the restored database during deployment: rejected because it expands this fix into database writes and requires separate credentials and migration review.

## Guard conditions

The root-owned preflight accepts an existing selection only when it is a regular root-owned file containing exactly one valid `Backup__ActiveDatabase=recovery_<12 hex>` entry. The recovery and restore-stage files must be regular, service-owned private files; recovery mode must be false; and the selected database and stage identifiers must agree. The release helper repeats these checks, then permits the no-migration path only when the candidate and current release migration manifest and SQL bytes match exactly. Any missing, malformed, stale, or inconsistent state remains a refusal.

## Verification

Regression tests cover approved matching state, recovery still active, mismatched database or stage, malformed/symlinked files, and changed migration manifest or SQL. Existing production preflight and release tests must remain green.
