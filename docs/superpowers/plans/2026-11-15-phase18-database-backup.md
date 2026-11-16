# Phase 18 Database Backup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver Level 1 controlled, encrypted, scheduled and manual backups of the real PostgreSQL database, with FTPS off-host retention and staged restore/reactivation.

**Architecture:** Add a dedicated backup controller to the existing Worker host. The controller owns scheduling, local files, encryption, FTPS, recovery staging and host service commands; the existing API remains the business service. A separate management route in the Next.js shell talks directly to the controller, so manual backup can stop the business API and background processing while leaving backup status and restart controls available. Scheduled backups run online. Restore switches to a verified recovery database and starts the app in recovery-only mode until Level 1 enables it.

**Tech Stack:** Existing .NET 9 solution, ASP.NET Core minimal endpoints, EF Core/Npgsql, PostgreSQL 17 `pg_dump`/`pg_restore`, Next.js 15 App Router, Linux systemd, explicit FTPS. Use authenticated encryption with an independently escrowed recovery key. Keep FTP and encryption secrets in controller-owned host-only files.

**Spec:** `docs/superpowers/specs/2026-11-15-phase18-database-backup-design.md`

## Global Constraints

- Run all backup operations through one durable lock; never publish incomplete files as restorable backups.
- Manual backup stops business API/background services and leaves them stopped until Level 1 starts them through backup control; the controller and management shell remain available.
- Scheduled automatic backups do not stop services and use a transactionally consistent PostgreSQL dump.
- Local retention is seven days. Remote retention includes recent backups plus 12 monthly points by default; duration is configurable.
- Require Level 1 with MFA in backend authorization for settings, manual operations, restore and lifecycle commands.
- Encrypt dumps before persistence or transfer; FTPS must validate TLS certificates and host name and must never fall back to plaintext FTP.
- Do not expose passwords, encryption keys, database contents or secret-bearing environment values in source, settings JSON, API responses, logs or UI.
- Restore into an isolated database first, preserve the current database before activation, and retain both if validation or restart fails.
- Do not run backup, FTPS upload, service stop/start or restore against the live production VPS/database during development.

## Review Focus

- A scheduled job colliding with a manual job or another worker must be rejected/serialized by the durable lock; cover this in the worker operation tests.
- FTPS certificate mismatch, AUTH TLS failure, or rename failure must fail closed without plaintext fallback or marking the backup remote-successful; cover in transport tests.
- A corrupt/truncated archive or unavailable encryption key must never activate a restore; cover in restore acceptance tests.
- If API restart fails after a manual backup, services must remain stopped and the controller must expose recovery status/action; cover lifecycle tests.
- Older restored database snapshots may contain stale backup configuration; controller-owned operation state and active recovery mode must survive database replacement; cover restore-state tests.

---

## File Map

- `src/ArginTaxPayer.Domain/Administration/DatabaseBackupContracts.cs`: validated schedule, backup metadata, operation status and restore command contracts.
- `src/ArginTaxPayer.Infrastructure/Persistence/Administration/DatabaseBackupSettingsService.cs`: Level 1/MFA settings persistence and settings revision.
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/*AddDatabaseBackupControl.cs`: settings, durable lock/operation request records, and recovery metadata schema additions.
- `src/ArginTaxPayer.Workers/DatabaseBackup/*`: scheduler, controller endpoints, local archive lifecycle, encryption, FTPS, restore validation, state and audit.
- `src/ArginTaxPayer.Workers/Program.cs`: register backup controller and job services; listen only on the configured loopback endpoint.
- `src/ArginTaxPayer.Workers/ArginTaxPayer.Workers.csproj`: required narrowly scoped transfer dependency if the implementation uses a library.
- `tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj`: add a Worker project reference for worker contract and endpoint coverage.
- `ops/phase18-systemd/argintaxpayer-backup.service` and `scripts/phase18-backup-host-helper.sh`: restricted controller service lifecycle and root-owned application/database switch operations.
- `apps/web/app/api/backup-control/[...parts]/route.ts`: same-origin BFF proxy to the backup controller using the existing identity cookie, with size, method and origin controls.
- `apps/web/app/(console)/backup-control/page.tsx`, `apps/web/app/invoices/backup-control-panel.tsx`: Level 1 backup control page/panel; backend remains authoritative when the main API is stopped.
- `apps/web/app/(console)/admin-settings/page.tsx`: link to backup policy and FTPS settings.
- `apps/web/tests/phase18-database-backup-ui.test.mjs`: panel route, credential redaction, manual stop/restart flow and recovery-mode UI contract.
- `tests/ArginTaxPayer.Tests/DatabaseBackup*Tests.cs`: domain, API authorization, worker scheduling, archive integrity, FTPS and restore lifecycle coverage.
- `ops/phase18-systemd/*`, `docs/phases/phase-18-backup-restore.md`: controller deployment, runbook, path display, secret provisioning, escrow, restore and operator recovery.

## Task 1: Settings and Level 1 authorization

**Files:** Domain contracts; EF migration; persistence settings service; Worker registration; contract tests.

**Interfaces:** `DatabaseBackupSettings` contains `Enabled`, `LocalScheduleKind` (`Daily` or `IntervalHours`), `IntervalHours` (1–24), `RemoteEnabled`, `MonthlyRetentionCount` (default 12), `LocalPath`, `RemoteDirectory`, `FtpHost`, `FtpPort` (21), `FtpUsername`, `FtpPasswordConfigured`, `EncryptionKeyVersion`, `Revision`, and `UpdatedAtUtc`. Password and key bytes are never part of this record. The service exposes `GetAsync(bearer)` and `SaveAsync(bearer, request)` and enforces active Level 1 plus MFA.

- [x] Add failing tests for unauthenticated, non-Level1, Level1 without MFA, allowed Level1+MFA, interval bounds, and redaction of FTPS password/key fields.
- [x] Run the focused .NET tests and confirm they fail for the missing contracts/service.
- [x] Add the additive migration and the settings read/write service using the existing `application_settings` patterns. Validate UTC schedule values and reject invalid intervals/retention counts.
- [x] Register the settings service in the Worker host; controller endpoints are added in Task 4 so settings and operations remain available while the business API is stopped.
- [x] Run the focused .NET tests; expect authorization, validation and redaction cases to pass.

## Task 2: Local archive, encryption and rolling retention

**Files:** `src/ArginTaxPayer.Workers/DatabaseBackup/LocalBackupStore.cs`, `PostgresBackupRunner.cs`, `BackupEncryption.cs`, `BackupManifest.cs`; Worker DI; worker tests.

**Interfaces:** `PostgresBackupRunner.CreateAsync(BackupOperation operation, CancellationToken ct)` streams PostgreSQL custom-format output to an encrypted temporary artifact. `LocalBackupStore.ListAsync(ct)` returns metadata only. `LocalBackupStore.ApplyRetentionAsync(nowUtc, retention, ct)` removes expired verified artifacts only after the new artifact passes archive validation. Manifest fields: artifact ID, UTC creation time, DB identity, schema/migration identity, SHA-256, encryption key version, and archive integrity result.

- [x] Add failing tests for encrypted temporary output, no plaintext intermediate on disk, partial failure cleanup, checksum mismatch, pg archive validation failure, atomic publish, and seven-day rollover preserving the newest valid artifact.
- [x] Run focused tests and confirm failures.
- [x] Implement streamed `pg_dump -Fc --no-owner --no-acl`, encryption to a `.partial` artifact, checksum/manifest generation, `pg_restore --list` validation after decryption through a pipe/temp protected workspace, atomic rename, and seven-day local retention.
- [x] Confirm unavailable key, low disk-space preflight, empty dump, and child-process nonzero exit leave no usable backup record.
- [x] Run focused tests; expect all archive and retention cases to pass.

## Task 3: Explicit FTPS and remote retention

**Files:** `src/ArginTaxPayer.Workers/DatabaseBackup/ExplicitFtpsClient.cs`, `RemoteBackupStore.cs`, remote tests, package manifest if needed.

**Interfaces:** `ExplicitFtpsClient.UploadAtomicallyAsync(localArtifact, remoteTempPath, remoteFinalPath, ct)` requires explicit TLS, passive mode, certificate and hostname validation, bounded retries and server-side rename. `RemoteBackupStore.ApplyRetentionAsync(nowUtc, rollingRetention, monthlyRetentionCount, ct)` protects the newest rolling artifacts and one monthly point for each retained month. FTPS secrets are read only by the controller from its 0600 secret file.

- [x] Add tests for TLS negotiation, hostname/certificate rejection, no plaintext fallback, interrupted transfer, temporary-name cleanup, atomic rename, duplicate dates and monthly retention boundaries using a local protocol test server or replaceable transport interface.
- [x] Run focused tests and confirm failures.
- [x] Implement FTPS upload/download/list/delete with TLS validation, atomic temporary upload and metadata-only listing; never log credential material or remote file contents.
- [x] Add Level 1 endpoint update and password-set commands over the restricted local controller socket; persist host, port, username and secret separately, return only `passwordConfigured`.
- [x] Run focused remote-store tests; expect retention cases to pass without contacting the supplied production endpoint.

## Task 4: Worker scheduler and durable operation state

**Files:** `src/ArginTaxPayer.Workers/DatabaseBackup/DatabaseBackupScheduler.cs`, `BackupOperationStore.cs`, `BackupControllerEndpoints.cs`, Worker `Program.cs`; worker tests.

**Interfaces:** `BackupOperationStore.StartAsync(kind, actorId, mode, ct)` atomically takes the system-wide lock and persists state under controller-owned `StateDirectory`, outside the restored database. `GetStatusAsync()` and `ListArtifactsAsync(location, ct)` expose redacted metadata. `DatabaseBackupScheduler` runs one durable daily/hourly job in UTC and invokes online mode; manual mode is explicitly marked offline-required.

- [x] Add tests for daily/hourly UTC scheduling, 1–24-hour bounds, one-worker lock, duplicate job suppression, restart recovery of operation status, controller filesystem paths, and credential redaction.
- [x] Run focused tests and confirm failures.
- [x] Implement scheduler, bounded retry/report state, host-owned manifests/audit records, loopback-only management endpoints, and storage path validation. Treat DB settings as policy; operation state survives DB restore.
- [x] Configure a web management route to call the controller directly with the existing access token; controller verifies bearer through the shared identity service and requires Level 1+MFA.
- [x] Run focused tests; verify automatic jobs select online mode and never request systemd stop.

## Task 5: Service lifecycle and manual backup control

**Files:** `scripts/phase18-backup-host-helper.sh`, `ops/phase18-systemd/argin...backup...service`, `src/ArginTaxPayer.Workers/DatabaseBackup/ServiceLifecycle.cs`, systemd contract tests, Phase 18 install/deploy scripts.

**Interfaces:** `ServiceLifecycle.StopBusinessServicesAsync()` and `StartBusinessServicesAsync()` invoke one root-owned helper with a fixed allowlist of service names, no shell interpolation and no arbitrary command arguments. The backup controller remains running; the web management shell stays available for status and restart control.

- [x] Add failing helper contract tests for exact service allowlist, forged names, start/stop ordering, incomplete backup, helper absence, failed restart, and services left stopped after a manual backup.
- [x] Run focused shell/Node contract tests and confirm failures.
- [x] Implement least-privilege sudoers/systemd permissions, lifecycle coordination, lock handling, maintenance banner, manual backup workflow that stops business services before dump and deliberately leaves them stopped after completion, and a Level 1 restart action.
- [x] Ensure scheduled automatic backup never calls lifecycle methods. Ensure restore failures keep business services disabled and the separate controller available.
- [x] Run focused contract tests and `systemd-analyze verify` on the new unit files where available.

## Task 6: Staged restore, activation and recovery-only mode

**Files:** `src/ArginTaxPayer.Workers/DatabaseBackup/RestoreCoordinator.cs`, `RecoveryModeStore.cs`, recovery endpoints and tests; API startup/readiness gating; root helper database switch; deployment configuration.

**Interfaces:** `StageRestoreAsync(artifactId, actorId)` decrypts and validates into a uniquely named isolated recovery DB and returns metadata. `ApproveActivationAsync(stageId, actorId, confirmation)` requires a distinct Level 1+MFA action, preserves the current database as a recoverable copy, switches the configured active DB, restarts API in recovery-only mode, and retains staged/current DBs. `EnableBusinessModeAsync(actorId, confirmation)` is the separate final Level 1+MFA action.

- [x] Add tests for corrupted archive, missing key, wrong database identity, duplicate activation, double-confirmation requirement, current DB preservation, configuration-switch rollback, startup/readiness in recovery mode, explicit re-enable, and out-of-DB operation/audit persistence.
- [x] Run focused tests and confirm failures.
- [x] Implement isolated `pg_restore`, schema/integrity checks, database identity guards, audited activation, recovery mode outside restored DB, rollback-safe DB selection, and Level 1 re-enable action. Do not drop the former DB or auto-enable business operations.
- [ ] Run focused tests and local disposable-PostgreSQL restore acceptance; do not target production.

## Task 7: Console UX and operational documentation

**Files:** Next.js API proxy, backup control page/panel, `admin-settings/page.tsx`, CSS, UI tests, `docs/phases/phase-18-backup-restore.md`, relevant systemd/deploy docs.

- [x] Add UI contract tests for seven-day local file list, remote monthly list, paths, sizes/dates/integrity, manual-backup stop state, explicit restart, restore staging/review/activation, recovery-only mode, and masked password state.
- [x] Run focused Node tests and confirm failures.
- [x] Build Persian RTL settings and file-browser controls for Level 1, call only the same-origin BFF/controller endpoints, require explicit confirmation for restore activation and re-enable, and clearly show that manual backup leaves business services stopped.
- [x] Document package/service installation, FTPS explicit TLS settings, secure password/key provisioning and escrow, disk and failure monitoring, exact storage paths, seven-day and 12-month retention, manual backup/restart, scheduled online backup, restore staging, recovery-mode checks, rollback and production operator steps.
- [x] Run focused UI tests, `npm run typecheck`, and static diff checks; confirm no real credential or dump appears in repository changes.

## Task 8: Integrated isolated acceptance

**Files:** only focused fixes from prior tasks; acceptance script/report under ignored `artifacts/` if the owner requests saving evidence.

- [ ] Against a disposable PostgreSQL instance, create an encrypted local backup, verify listing/checksum/manifest, restore into a distinct recovery DB, verify expected schema, and prove the former DB remains available.
- [ ] Use a local FTPS test endpoint to verify successful TLS upload, rename and retention without using the supplied production credentials or endpoint.
- [ ] Verify Level 1/MFA enforcement, controller availability during API stop, manual service restart and scheduled online backup with no service stop.
- [ ] Review generated configuration and logs for secret leakage; check `git diff --check` and confirm only intended source/docs files are changed.
- [ ] Record unverified VPS-only steps plainly; never claim live backup, transfer or restore acceptance without an explicitly authorized production operation.
