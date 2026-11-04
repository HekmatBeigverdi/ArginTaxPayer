# ArginTaxPayer — Phase 18 / Step 07: isolated Production PostgreSQL and controlled migrations

**Status: IMPLEMENTED IN SOURCE; UBUNTU OPERATOR ACCEPTANCE PENDING.**
Scope: PostgreSQL 17 in a **new dedicated Docker container and volume**, an exact Production database, TLS VerifyFull, distinct schema migration and runtime app roles, immutable PostgreSQL image digest, reviewed EF migrations, backup and restore. Absolutely no shared Postgres/QA modifications or public service activation in this step.

## Fixed isolation contract

| Concern | Approved Step07 target | Invariant |
|---|---|---|
| Dedicated container | `argintaxpayer-phase18-production-postgres` | New container, never `postgres-postgres-1` |
| Docker Compose project | `argintaxpayer-phase18-production` | Isolated from existing Docker stacks |
| Dedicated named volume | `argintaxpayer-phase18-production_prod_pgdata` | Never reuse shared/QA volumes |
| PostgreSQL | 17, `postgres@sha256:...` | Immutable reviewed local image digest |
| TCP | `127.0.0.1:15434` | No wildcard/public listener |
| Database | `argintaxpayer_prod` | Never the existing `argintaxpayer_test` or `argin_phase18_acceptance` |
| Migrator | `argin_prod_migrator` | NO SUPERUSER / CREATEDB / CREATEROLE |
| Runtime application | `argin_prod_app` | NO schema CREATE; only CONNECT + explicit object DML/sequence grants |
| Bootstrap | `argin_prod_bootstrap` | DBA role only within isolated instance, not for application |
| TCP encryption | Server-generated dedicated private CA, VerifyFull, SCRAM; `hostnossl` rejected | CA and passwords strictly local and private |
| Credentials | `artifacts/phase18-step07/production-private/compose.env` (0600), CA (0700) | Gitignored and not pasted/logged; Step08 hardening/rotation required |
| Recovery | `argin_p18_restore_<UTC>` | Separate new recovery DB on dedicated instance, never overwrite production |
| Public site/API | Existing maintenance and disabled Systemd units | No changes, no tax/SMS/payment operations |

The dedicated instance initially uses Compose `restart: "no"`: do not configure automatic startup/Production traffic until the later app/DB operational release gates. The image must have a locally available, reviewed `postgres:17` registry digest. No floating tag is accepted by the Compose file.

## Stage A — same-SHA source tests on macOS

```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git status --short
git rev-parse HEAD
bash scripts/phase18-step07-source-preflight.sh
```

Store the full SHA. This runs the Node static isolated-DB/10 historical migration guard contract, Bash syntax, and `Phase18LegacyMigrationAdmissionTests` in .NET; no DB is provisioned or migrated.

## Stage B — read-only Ubuntu preflight

```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git status --short
git rev-parse HEAD
sudo -v
PHASE18_EXPECTED_SHA='<FULL_MACOS_SHA>' bash scripts/phase18-step07-source-preflight.sh
```

Expected: source/migration contract PASS, port 15434 FREE, no Production Docker container or named volume, both Step06 services inactive, `database-provisioning NOT_PERFORMED`, `production-go-live BLOCKED`. Do not run create script if preflight does not pass. Keep ports 5432 (existing DB), 15433 (Phase18 QA), 15090/15091 (Step06 reserved) untouched.

## Stage C — explicitly create new Production DB (first writes to NEW resources only)

```bash
PHASE18_EXPECTED_SHA='<FULL_MACOS_SHA>' \
bash scripts/phase18-step07-provision-production-db.sh --provision-isolated-production-db
```

This is not a shared-DB migration. It checks exact SHA, Docker volume/container collisions, missing activation marker, service inactivity, TCP port availability, generates a private CA/server cert/three random passwords, and starts only its own PostgreSQL Compose project. It initializes one new Production database and the two limited application/migration roles and validates migrator TLS VerifyFull. It never modifies existing containers, databases, Nginx or Systemd units. `/usr/bin/node` and existing PostgreSQL client installations are untouched.

If the script refuses because `postgres:17` has no local digest, inspect and separately obtain an authentic official image (`sudo docker image inspect postgres:17` / `sudo docker pull postgres:17` only after checking operational network policy). Do not bypass the image digest safeguard.

## Stage D — exact database-role/TLS audit

```bash
sudo -v
PHASE18_EXPECTED_SHA='<FULL_MACOS_SHA>' \
bash scripts/phase18-step07-production-db-audit.sh
```

Expected checks `migrator-tls-and-role PASS`, `runtime-least-privilege PASS`, `app-tls-login PASS`, `plaintext-tcp-rejected PASS`. The audit independently proves `current_database`, `current_user`, SSL session and role privilege flags; it does not migrate or modify data.

## Stage E — generate and REVIEW all EF SQL, with no DB connection

```bash
bash scripts/phase18-step07-migration-sql-review.sh
```

Generated private files under `artifacts/phase18-step07/sql-review/<SHA>/<UTC>/`:
- `migrations.sql`: full EF idempotent SQL based on current Git source (offline only).
- `migrations.sha256`: exact SHA-256 of reviewed SQL.
- `ddl-review-hits.txt`: candidate DDL destructive/locking operations for attention.
- `manifest.tsv`: review mandatory and migration blocked.

**Operator must inspect the SQL, especially 10 legacy Phase09/10 security-restricted migrations, lock behavior, type changes, default data inserts, `Down` limitations, and any `DROP`/unbounded lock operations.** Merely generating SQL does not authorize running it. If unexpected destructive operations or incompatible schema exist, STOP and patch source + repeat cross-platform tests. Don't disable a migration guard to make it pass.

## Stage F — pre-migration backup + separate recovery drill

Only while the Production database contains **no actual customer identities or live data**:

```bash
sudo -v
PHASE18_EXPECTED_SHA='<FULL_MACOS_SHA>' \
PHASE18_RESTORE_DRILL_APPROVED=YES \
bash scripts/phase18-step07-backup-restore-drill.sh
```

This exports a private, permission-restricted custom-format backup using the PostgreSQL 17 tools **inside the dedicated container** (no host `pg_dump` upgrade), checks archive integrity, creates a **distinct recovery database** inside that isolated instance (never restores to source), restores, and compares table counts and EF migration history counts. The recovery database and backup are retained for investigation. There is no `DROP DATABASE`, `docker compose down`, volume deletion, or destructive rollback.

**Security restriction:** These Step07 acceptance backups are not yet externally encrypted and refuse to back up a database with populated `identity_users`; use this procedure only before users/accounts/tax payloads exist. Before any live traffic Step12 must provide encrypted backups, off-host retention, tested restoration and key recovery. Do not upload backup or certificate material.

## Stage G — initial Production Migration: separate explicit gate (do NOT run without review)

Use ONLY after Stage E reviewed SQL and Stage F restored manifest both PASS, and **after explicitly approving the reviewed schema**. The apply script requires all of:
- `PHASE18_EXPECTED_SHA` exact macOS=Ubuntu Git SHA;
- `PHASE18_OWNER_APPROVED_INITIAL_MIGRATION=YES`;
- `PHASE18_REVIEWED_SQL_PATH` pointing to the private generated `migrations.sql` for this SHA;
- `PHASE18_REVIEWED_SQL_SHA256` exact checksum from `migrations.sha256`;
- `PHASE18_RESTORE_MANIFEST` pointing to the matching pre-migration restore `manifest.tsv`.

It re-generates SQL from the exact source, byte-for-byte compares hashes, validates exact isolated DB, role and VerifyFull, and refuses anything other than an **empty first-time** schema. Only then:

```bash
PHASE18_EXPECTED_SHA='<FULL_MACOS_SHA>' \
PHASE18_OWNER_APPROVED_INITIAL_MIGRATION=YES \
PHASE18_REVIEWED_SQL_PATH='<ABSOLUTE_PRIVATE_MIGRATIONS_SQL_PATH>' \
PHASE18_REVIEWED_SQL_SHA256='<64_HEX_SHA256>' \
PHASE18_RESTORE_MANIFEST='<ABSOLUTE_PRE_MIGRATION_RESTORE_MANIFEST>' \
bash scripts/phase18-step07-apply-reviewed-migration.sh --apply-reviewed-initial-schema
```

Check `initial_migration PASS`, `ef_history_count > 0` and then run **Stage F again** to prove a populated *schema* can also be restored. If migration fails or any checks fail, do **not** retry against shared/QA, do **not** reset/recreate the Production DB, do **not** invoke `dotnet ef database drop` or use destructive Down migrations. Retain logs/database for diagnosis.

## Step07 acceptance requirements

Owner must supply summarized PASS evidence of same Git SHA on macOS and Ubuntu; read-only Ubuntu preflight; isolated new instance create; strict DB role/TLS audit and rejection of plaintext; offline reviewed SQL checksum and explicit review; pre-migration backup/restore; approved one-time migration completed and EF history; second post-migration backup/restore; application role read validation without schema modification; and explicit no public go-live. Include all private manifest paths, **not** secret contents. Additional post-migration runtime tests and encrypted operational backup/restart recovery are Step08/12/13 acceptance gates.

**State:** Source implemented and committed, NOT operator-tested on the Ubuntu VPS in Step07 yet. No permission to claim Production database/migrations PASS or to enable services. Keep Step06 units `disabled/inactive` and tax dispatch off. This step does not constitute a release or merge.
