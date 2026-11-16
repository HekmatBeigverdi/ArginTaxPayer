# Phase 18 database backup and restore

The backup controller runs inside the existing `ArginTaxPayer.Workers` host. The same release bundle promotes the API, web shell and backup controller together. The controller binds only to `127.0.0.1:15092`; the web server reaches it through the same-origin BFF at `/api/backup-control/*`.

## Host provisioning

1. Install the reviewed deployment helper and the backup controller systemd unit with `sudo bash scripts/phase18-install-deploy-helper.sh --install <reviewed-source-sha>`. This installs a root-owned helper with fixed API service actions and does not start the controller or enable backups.
2. Provision the PostgreSQL connection in `/etc/argintaxpayer/api.env`, readable only by `argintaxsvc` and root. The worker uses the same application connection as the API.
3. Provision `/etc/argintaxpayer/database-backup.key` as two lines: a key version, then 64 hexadecimal characters (32 random bytes). Set owner `argintaxsvc:argintaxsvc`, mode `0600`. Keep an independently encrypted offline escrow copy. When rotating keys, retain prior versions as `/etc/argintaxpayer/database-backup-keys/<version>.key` (same two-line format and permissions) until all backups encrypted with that version expire. A missing or incorrectly permissioned key disables backup and restore.
4. For restore, create a separate PostgreSQL role with only the rights needed to create isolated databases and set the application role as database/object owner (grant membership in the application role), and provision its connection string as `ConnectionStrings__DatabaseBackupRestore` in `/etc/argintaxpayer/backup-restore.env`, owner `root:argintaxsvc`, mode `0640`. Do not grant this role superuser, server-file, or program-execution privileges. The restore endpoint refuses to run without this separate credential.
5. Ensure `/var/lib/argintaxpayer/database-backups` is on a monitored volume with enough free space. The controller reserves at least 512 MiB plus the current database size before creating a local backup.
6. Provision the FTPS password through the Level 1 backup settings page. It is stored at `/var/lib/argintaxpayer/backup-controller/ftps.json` with mode `0600`; it is never returned by the API. Rotate credentials shared in development or chat before configuring the production endpoint.
7. Review `/etc/sudoers.d/argintaxpayer-backup` and the unit files. The worker can call only the root-owned allowlisted helper for API start/stop and recovery database selection. No FTP port is exposed by the web server or firewall.

After prerequisites are present, deploy with the existing `scripts/phase18-manual-build-deploy.sh`. It publishes the Worker and packages `api.tar.gz`, `web.tar.gz`, and `backup.tar.gz` into one checksummed release. Promotion restarts the three systemd units together and probes the loopback controller health endpoint. The systemd activation marker and existing manual SHA confirmation remain required.

## Schedule and retention

Level 1 users with MFA can enable a UTC daily schedule or an interval from 1 to 24 hours. Scheduled backups keep the API online. Local encrypted artifacts older than seven days are removed after a new artifact passes archive validation. Remote FTPS transfer uses explicit TLS on port 21, TLS-encrypted passive data connections, platform certificate and hostname validation, a temporary remote name, and server-side rename. There is no plaintext fallback. Remote retention keeps the rolling seven-day set and the newest point for each configured monthly retention period (default 12).

The admin page shows the local storage path, artifact time, encrypted size, archive check and checksum check. Remote storage shows file metadata only; no file contents are downloaded by browsing.

## Manual backup and restore

A manual backup takes the operation lock, durably records that restart is required, stops `argintaxpayer-api.service`, creates and verifies the encrypted archive, and leaves the API stopped even if backup fails. The restart requirement remains visible even if later scheduled operations replace the latest-operation status. The web shell and backup controller remain available. A Level 1 administrator must explicitly start the API from the backup page.

Restore downloads or reads the selected encrypted artifact, validates its manifest and checksum, checks database and schema identity, then restores it into a uniquely named isolated PostgreSQL database. The live database is not changed during staging. Activation first creates a verified backup of the current database, stops the API, writes only the non-secret database selection to `/etc/argintaxpayer/backup-db.env`, schedules the controller restart against the selected database, then starts the API with the recovery marker still active. Business routes return `503 recovery_mode`; health and identity sign-in remain available so Level 1 can authenticate and review the restored data. The application remains unavailable for business work until the explicit re-enable action. The former database is retained. A second Level 1+MFA action with the explicit `ENABLE_BUSINESS_OPERATIONS` confirmation first verifies the restored business EF migration set matches the deployed application (the backup feature’s own constraint-only migration is metadata-only), starts/checks the API, and requires PostgreSQL readiness before clearing recovery-only mode. Any mismatch or failed readiness keeps business routes blocked.

If activation or startup fails, leave the API stopped and preserve both databases and the encrypted pre-restore backup. Investigate controller operation status and systemd journal before retrying. Do not drop the previous database or remove the recovery marker until a reviewed recovery has succeeded.

## Operational checks and limitations

Monitor the backup controller's systemd status, local disk capacity, last operation status, and remote FTPS listing. Test restoration against a disposable PostgreSQL instance before production activation and retain the encryption key escrow separately from the VPS. This source change has not run against the production FTP endpoint, production VPS, or a live database. PostgreSQL restore-role privileges, FTPS server compatibility, systemd installation, and a full disposable-database restore drill still require environment-specific acceptance.

## ZIP download and upload

Root administrators can download each verified local artifact as a ZIP containing exactly `<artifact-id>.argonbackup` and `<artifact-id>.manifest.json`. Encryption keys are never included. The console displays the source database name from the manifest.

Upload a ZIP using the new upload control. The browser reports actual bytes sent; after transfer, the queued controller operation reports structural/checksum verification, key and full PostgreSQL archive validation, and publication. Upload does not execute SQL or activate a database. Select **prepare restore** on the imported local artifact to use the existing isolated restore workflow, then explicitly activate and review it before re-enabling business operations.

The controller accepts raw `application/zip` at `POST /api/backup/artifacts/upload` and streams downloads at `GET /api/backup/artifacts/download/{id}`. Both require root authentication. The same-origin Next.js proxy exposes these under `/api/backup-control/`. The default compressed and encrypted archive size limit is 2 GiB (`BackupController__MaximumBundleBytes`); the supplied Nginx backup-control location uses `client_max_body_size 2g`, disables proxy buffering and permits long transfers. Apply/review that location separately on Ubuntu; this change does not deploy Nginx. Storage reserve is checked before receive, validation and publication. ZIPs with extra entries, paths, symlinks, duplicate IDs, invalid manifests, checksum mismatch, missing/wrong keys or invalid archives are rejected; existing artifacts are preserved.

For the Mac test artifact created on 2026-11-16, the database is `argintaxpayer_test`, schema identity `phase18`, key version `dev-mac-1`. To restore it on Ubuntu, securely provision the matching key as an escrowed historical key at `/etc/argintaxpayer/database-backup-keys/dev-mac-1.key` (owner-only 0600), without replacing the server's current key. The version label alone is insufficient; key material must match. Do not place keys in ZIPs or browser uploads. Restore preparation requires `ConnectionStrings__DatabaseBackupRestore` and the privileges described above. Local macOS helper does not implement database activation; the UI and API explicitly block activation in local development mode. Ubuntu activation still requires its installed host helper and service configuration.


## Ubuntu release acceptance (2026-11-17)

The reviewed installer now installs the API database-selection drop-in as well as the backup helper/unit. Worker archives live at `/var/lib/argintaxpayer/database-backups`, separately from the root-private `/var/backups/argintaxpayer` migration archives. Do not reuse the old nested worker path. Existing saved settings using that path require operator review and a writable path update.

The controller runs as `argintaxsvc`, with no ambient capabilities. Its bounded capabilities allow `sudo -n` to run only the root-owned allowlisted helper. `NoNewPrivileges` and sandbox directives that implicitly force it are disabled on this unit; API/web hardening remains independent. `/etc/argintaxpayer` is writable in the controller mount namespace so the root helper can atomically publish `backup-db.env`; Unix ownership prevents direct writes by the worker account. Do not add a seccomp hardening directive without repeating the sandbox helper preflight. Reference: https://www.freedesktop.org/software/systemd/man/latest/systemd.exec.html

From a clean checkout of the reviewed merged main SHA:

1. Install/upgrade the helper with `sudo bash scripts/phase18-install-deploy-helper.sh --install <full-main-sha>` (supply the reviewed existing deployment-helper hash if the installer requests the documented replacement flag).
2. Provision PostgreSQL clients, private current encryption key, separate restore role/environment and the operator-reviewed Nginx ZIP location. Never replace an existing key with a new random key. Preserve the key escrow.
3. Run `sudo bash scripts/phase18-backup-preflight.sh`. It only inspects prerequisites and invokes the real helper's API status check inside a transient sandbox with the controller's effective permissions. API must currently be active. The manual build/deploy entry point also invokes this check before building.
4. Run the existing `bash scripts/phase18-manual-build-deploy.sh` with its normal SHA confirmation gates. After successful initial deployment, enable `argintaxpayer-backup.service` for boot as a separate reviewed operator action.
5. On an isolated Ubuntu test deployment, run manual backup, verify the file and explicitly restart API. Download ZIP, test re-import into a separate test storage context (same-ID uploads are intentionally rejected), stage isolated restore, activate, verify readiness/business recovery blocking and explicitly enable business operations. Confirm data and preserve the previous database. Do not use `argintaxpayer_test` as a production archive.

Activation starts the API before scheduling the controller restart through a separate manager-owned 60-second timer; the helper is not killed by its own immediate cgroup restart. Restore selection accepts the original configured database identity and generated recovery database names, so older archives remain usable after activation.

Deployment deliberately refuses if `/etc/argintaxpayer/backup-db.env` exists: the migration helper currently targets `argintaxpayer_prod`. A subsequent release after restore requires an explicit migration-target review; do not delete the selection file to bypass the guard. This PR has local build/test evidence, not a completed Ubuntu restore drill.
