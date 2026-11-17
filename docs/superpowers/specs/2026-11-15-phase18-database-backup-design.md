# Phase 18 Database Backup and Restore Service

## Intent

Provide a production-grade, Level 1 controlled backup and recovery feature for the real PostgreSQL database. The web console must configure schedules, show storage locations and backup files, start manual backups, and guide safe restore and reactivation. This implements the Phase 18 daily-backup, tested-restore, key-recovery, monitoring, and rollback obligations; the existing Step07 drill is only a pre-production acceptance exercise.

## Goals

- Keep one local backup set on the VPS with a rolling seven-day retention window. Support daily or every-N-hours schedules and manual backups.
- Send encrypted backups to the supplied explicit-FTPS endpoint on port 21. Never put the password in source, database settings, logs, or this document; provision it through a protected secret channel on the VPS. The console shows endpoint, username, and whether a password is configured, never the password.
- Retain remote monthly recovery points in addition to the rolling remote set. Default: retain 12 monthly points; make this retention policy explicit in settings.
- Show the configured absolute local path, remote directory, file size, timestamps, checksum/integrity state, and transfer/restore status. Present files in a browsable list.
- Allow Level 1 with MFA to read/update settings and request backup/restore. Enforce authorization in the API, not only in the UI.
- Serialize backup/restore operations, record an audit trail, report failures, verify archive readability and checksum, and avoid incomplete files appearing as usable backups.
- Restore only after validating an encrypted backup in an isolated recovery database. Require a second explicit Level 1 confirmation before activating it. After activation, keep business features disabled until a Level 1 explicitly re-enables them.
- Preserve encrypted database backups and the independent recovery key needed to decrypt them. Document secure key escrow and recovery; a backup without its key is not recoverable.

## Non-goals

- No automatic deployment, schema migration, or release rollback as part of database restore.
- No backup of unrelated application files, uploaded assets, OS state, provider credentials, or signing keys in the database archive. Key escrow remains an independent, documented recovery item.
- No plaintext FTP transfer or unencrypted dump files.

## Constraints and current architecture

- Production uses PostgreSQL 17 on the VPS, with separate systemd API and Next.js web services. API runs as the unprivileged `argintaxsvc` account; systemd units use hardening and explicit activation markers.
- Current hosted background work lives inside the API. Backup scheduling and systemd control therefore belong in a separate worker/controller service, with a narrowly scoped host helper for the exact lifecycle actions it needs.
- Admin settings already use a Level 1 console route; backend settings patterns persist configuration in `application_settings`. FTP passwords and backup encryption keys must not be stored there in plaintext.
- The owner supplied an explicit-FTPS endpoint on port 21 and credentials. Do not include the password in source control, generated artifacts, tool output, or audit logs. Rotate the supplied password before production use because it was shared in the conversation.
- Phase 18 requires encrypted off-host DB backup and independent key escrow. Step07's dump/restore script refuses real identity rows and cannot be reused as a live-user backup system.

## Approaches

1. **Selected: console plus separate backup controller.** The app console manages policy and requests; a dedicated service handles schedules, encryption, FTPS, filesystem operations, restore staging, and a minimal maintenance/recovery control path. A restricted host helper mediates systemd and PostgreSQL operations. This meets the requested browser workflow while keeping privileged host actions out of the API process.
2. **Host timers and scripts only.** Safer and simpler operationally, but it cannot provide the requested file browser, manual restore flow, or Level 1 lifecycle controls.
3. **Run all jobs in the API.** Easiest to wire into existing settings, but API restarts interrupt work, it couples privileged service control to public request handling, and restore can remove its own control plane.

## Proposed behavior (builder call for implementation details)

- A single scheduler uses UTC and a durable operation lock. Level 1 chooses daily or an interval from 1 to 24 hours. Local backups use rolling seven-day retention; the service removes expired artifacts only after the new artifact is verified. Manual backups follow the same retention class unless explicitly tagged as a monthly point.
- Each dump is a PostgreSQL custom-format archive, streamed through authenticated encryption to a temporary file. The finished file is atomically renamed only after pg_dump, encryption, checksum generation, and archive validation succeed. The backup-specific encryption key is stored in root-only host configuration and escrowed separately from the VPS; the console never reads or displays it.
- A manually requested backup first durably records the operation, stops the business API and background processing services, then performs the dump/encryption/verification/transfer. On completion, they remain stopped until Level 1 explicitly starts them again from the backup control console. The management shell and backup controller remain available only for backup status and service recovery while business services are stopped. An automatic scheduled backup runs online and does not stop services; it uses PostgreSQL's consistent custom-format dump and reports the result through the controller without service interruption.
- For remote backups, upload the encrypted artifact and manifest over explicit FTPS with TLS certificate/hostname validation, passive mode, retries, and temporary remote name followed by rename. Never silently fall back to plaintext FTP. Local and remote manifests record artifact ID, UTC creation time, SHA-256, encryption key version, database/schema identity, and operation result; never include secrets or row data.
- Remote retention keeps the same recent rolling set plus one monthly recovery point per month for the configured retention period (proposed default: 12 months). Monthly points are protected from rolling cleanup. Show actual local and remote paths in the console.
- Restore has two confirmations: first select a backup and stage/verify it in a separately named recovery database; then Level 1 reviews metadata and explicitly approves activation. Before activation, stop the API/web services and preserve the current database as a recovery point. Switch the application to the staged database using a controlled database-name/configuration switch and restart into a recovery-only mode: business routes remain unavailable while Level 1 can log in to inspect health and explicitly re-enable normal operation. If validation fails, retain both databases and leave the business application disabled. Never drop or overwrite the only known-good database.
- Persist operation lifecycle outside the database being restored so a restore cannot erase its own status. Keep an append-only host-side operation log with actor, action, backup ID, timestamps, checks, and outcomes. UI history can be rebuilt from this record.
- Admin settings can update schedule and FTPS endpoint fields and submit a new password through HTTPS. The API forwards the password over a permission-restricted local socket to the backup controller, which stores it in a controller-owned 0600 secret file outside PostgreSQL. Password reads return only configured/not-configured state. Changes require Level 1 with MFA. Path fields are informational and are not arbitrary filesystem input.
- Fail closed when the local disk lacks configured free-space headroom, the encryption key is unavailable, the destination TLS check fails, or the selected archive fails integrity checks. Surface concise errors in the console and detailed redacted logs in the host journal.

## Open product decision for owner review

Owner-approved behavior: manual backups stop business API/background services and leave them stopped until Level 1 explicitly starts them in the backup control console; the control console remains available. Scheduled automatic backups do not stop services. After restore, the system starts in recovery-only mode with business routes disabled and a Level 1 re-enable action available. Retain 12 monthly remote recovery points by default; make the retention duration configurable.

## Acceptance evidence

- Tests cover schedule bounds, retention rollover, atomic local/remote publication, FTPS TLS failure, missing key, serialized execution, Level 1/MFA authorization, backup list redaction, restore staging, double confirmation, restore failure safety, post-restore disabled mode, explicit reactivation, and audit records outside the restored DB.
- Verify actual backup and isolated restore against a disposable production-like PostgreSQL instance. Verify a real FTP endpoint only with explicit operator authorization and never print credentials.
- Document VPS package/service install, owner-only secret provisioning, encryption-key escrow/recovery, disk monitoring, restore runbook, and failure recovery.
- Do not run the feature against the live production database, stop production services, upload a real database dump, restore/replace a database, or change the supplied FTP password during development work.
