# Ubuntu release helper upgrade — 2026-11-12

The schema-aware helper was installed on the Ubuntu host using a clean, isolated Git checkout and the official installer with the previously installed helper SHA256 pinned. No application release promotion or EF migration was executed.

- Reviewed helper source commit: `e1eed18e5f3e641e531fe9500c97c7f44a532307` on local isolated branch `ops/schema-helper-upgrade-20261112`.
- Installed helper SHA256: `38769631a846f515c6260e6f9585a2210b6d1aceb2de8dd5856deda074217bf6`.
- Capability: `schema-before-promote-v1`, accessible to `argin-deploy` without exposing private configuration.
- Active application release retained: `55272353811a17b90a777c0c0f5564217a835996`.
- Production database retains 83 migrations, through `20261109010000_AllowPartialSalesReturns`.

## Migrator preparation

The existing dedicated migrator had no usable credential configuration and no access to restored application tables. All 109 public application tables were still owned by the restore superuser. Application schema objects (154 tables/functions/etc.) and the dedicated production database were transferred to `argin_prod_migrator`, matching the intended restricted ownership model. Existing runtime per-table SELECT/INSERT/UPDATE/DELETE grants were compared before and after and remained identical. Default grants for new migrator-owned public tables/sequences were configured per the original production initialization design. No unrelated schema ownership or bootstrap superuser membership was granted.

The dedicated migrator credential was rotated and stored only in root-owned mode-0600 `/etc/argintaxpayer/migrations.env`. The old authenticator and ownership rollback instructions are encrypted under `/var/backups/argintaxpayer-helper`. The application runtime credential was not changed.

The public CA was provisioned at `/usr/local/share/argintaxpayer-migration/ca.pem` inside `argintaxpayer-production-postgres`, readable by the nonowner migration client UID/GID 65534. If the database container is recreated, provision this public certificate again. The private backup encryption key is root-only `/etc/argintaxpayer/migration-backup.key`; retain it securely for backup recovery.

The database also contains unrelated leftover test schemas. The release backup intentionally covers the complete application `public` schema and its data; those other schemas were not modified or added to the migrator's privileges.

## Verification performed on Ubuntu

- 31 helper tests passed against synthetic temporary files; their rollback messages were test output, not application rollback.
- Installed helper authenticated as `argin_prod_migrator` to `argintaxpayer_prod` over VerifyFull TLS, using PostgreSQL client 17.11 as UID/GID 65534.
- Restricted psql mode accepted a read-only probe.
- Real public-schema backup encrypted, decrypted and archive-listed successfully. No restore into any database was performed.
- Migrator CREATE TABLE and runtime default DML grants were tested in a transaction that was explicitly rolled back. The probe table was confirmed absent afterward.
- Runtime role remains unable to CREATE in public schema.
- API/Web remained active; normal loopback readiness/login/unauthenticated identity probes passed.

Root-only verification metadata is saved at `/var/backups/argintaxpayer-helper/upgrade-verification.json`. No passwords or key contents are recorded in this document.

The next application release must include the prepared workflow/manual-build changes that export `api/schema/migrations.json` and `migrations.sql`. The installed helper will refuse old artifacts missing this schema contract rather than activating an application without its migrations. Remaining application changes in the main workspace have not been committed or published by this helper-only upgrade.
