# Local macOS Backup Development Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable safe local encrypted backups of the test database from the existing macOS development stack.

**Architecture:** Add an explicitly configured, non-sudo local host helper that controls only the API process tracked by `dev-up.zsh`. Supply private test-state storage, a local-only key, and PostgreSQL binaries; keep remote transfers disabled in this mode while preserving production defaults.

**Tech Stack:** ASP.NET Core Worker and Infrastructure, zsh/Bash developer launch scripts, macOS process tools, Homebrew libpq.

**Spec:** `docs/superpowers/specs/2026-11-16-macos-dev-backup-design.md`

## Global Constraints

- Enable local lifecycle management only for `argintaxpayer_test`.
- The local helper may control only the API PID created by `dev-up.zsh` and must not use sudo.
- Local development backup settings must not transfer files through FTPS.
- Preserve and validate any existing local encryption key; never replace it automatically.
- Do not run tests, backups, restores, or deployments during implementation/verification.
- The user requested local Mac activation; after static verification, restart the local dev stack so it receives the new environment and lifecycle helper.

## Review Focus

- A stale or forged PID file must not cause an unrelated process to be stopped.
- Production settings and the production helper/sudo path must remain unchanged when the local-development flag is absent.
- A local key must keep its version aligned with the effective local settings and must retain mode 0600.
- A test-profile backup must not publish to the configured remote FTPS destination.
- `dev-up.zsh` must fail clearly if the Homebrew PostgreSQL tools are unavailable, without echoing connection secrets.

---

### Task 1: Add a scoped macOS lifecycle helper

**Files:**
- Modify: `src/ArginTaxPayer.Workers/DatabaseBackup/ServiceLifecycle.cs`
- Modify: `src/ArginTaxPayer.Workers/Program.cs`
- Create: `scripts/phase18-dev-backup-host-helper.sh`
- Modify: `scripts/dev-up.zsh`
- Modify: `scripts/dev-down.zsh`

**Interfaces:**
- `ProcessBackupHostCommandRunner` accepts the helper path, sudo path, and `useSudo` (default true); when false, invoke the helper directly with the existing fixed action allowlist.
- Development helper actions: `stop-business`, `start-business`, and `status-business`; reject all other actions.

- [x] Add configurable helper path and sudo behavior while retaining current defaults.
- [x] Register the configured runner through a DI factory.
- [x] Fail Worker startup when local mode is requested outside the host-resolved Development environment or the base DB is not `argintaxpayer_test`.
- [x] Add a helper that verifies `api.pid`, checkout cwd, exact launcher identity, and the current launcher nonce before stopping its process tree; implement start via the same dev-up command and readiness URL.
- [x] Review all helper branches to confirm no general process/port killing and no support for arbitrary command input.
- [x] Run `bash -n scripts/phase18-dev-backup-host-helper.sh` and Worker build.

### Task 2: Supply test-profile backup prerequisites

**Files:**
- Modify: `scripts/dev-up.zsh`
- Modify: `src/ArginTaxPayer.Infrastructure/Persistence/Administration/DatabaseBackupSettingsService.cs`

**Interfaces:**
- In the `test` profile, set `BackupController__LocalDevelopmentMode=true`, `BackupController__UseSudo=false`, helper/key/local paths under `~/.argintaxpayer-test`, and `PgDumpPath`/`PgRestorePath` beside the verified `psql` binary.
- Effective test-profile settings use a distinct fixed development key version, local backup path, and `RemoteEnabled=false` for reads, saves, and scheduled settings.

- [x] In `dev-up.zsh`, configure local helper and tool paths only for the test target; do not opt into local backup for the production target. Create a unique API launch marker and remove it through the normal dev-down path.
- [x] Create the development key only when absent, mode 0600, under a restrictive umask; reject symlinks or malformed existing keys without overwriting them.
- [x] Add explicit local-development settings overrides for path, key version, and remote-off behavior.
- [x] Review the settings read/save/scheduled paths to confirm FTPS remains disabled in the local profile.
- [x] Run `zsh -n scripts/dev-up.zsh` and web typecheck.

### Task 3: Verify the local configuration without running operations

- [x] Build Worker and run web typecheck; syntax-check both scripts.
- [x] Inspect diff and confirm no tests or backup/restore operations were run.
- [x] Restart the local development stack with `scripts/dev-down.zsh` and `scripts/dev-up.zsh` to apply the approved test-profile configuration.
- [x] Verify the backup controller and API are ready and the helper's read-only status action recognizes only the launched API; do not run a backup.


### Follow-up: repeated manual backup failure (2026-11-16)

User requested detailed diagnostics, a complete fix and local verification before Ubuntu deployment.

- Root cause at `stop-services`: `dotnet watch` survives TERM after its application child exits. The local helper now captures the owned process tree, waits after TERM, rechecks checkout/nonce ownership before forced termination, and confirms that the launcher and listener have stopped.
- Added timestamped lifecycle diagnostics in `~/.argintaxpayer-test/backup-host-helper.log`; worker failures log operation ID, action, stage, exception types and stack without dumping connection strings or credentials. Host helper failures include bounded stderr and exit code.
- Root cause at `verify-archive`: `pg_restore --list -` treats `-` as an input filename. Even without that filename, `--list` exits after the TOC, leaving the decryptor with a broken pipe. Validation now generates SQL to drained stdout via `--file=-`, consuming the complete archive without executing SQL or writing a plaintext archive.
- Verified using the actual `BackupExecutionService` and PostgreSQL test database from a temporary diagnostic console: manual offline backup succeeded, archive validation and checksum succeeded, encrypted artifact 1,516,840 bytes; operation reached 100%. API restarted through the service and readiness confirmed. No restore or Ubuntu deployment performed.
- Worker build passed (0 errors); helper shell syntax and git whitespace checks passed. One diagnostic attempt encountered a transient PostgreSQL tunnel read failure; the subsequent execution reached archive validation and the final run succeeded.
