# Local macOS Backup Development Design

## Intent

Allow the existing backup page to take local encrypted backups of the local development database on macOS. The current development launcher starts the API and backup controller, but the controller defaults to a Linux root helper and system PostgreSQL paths that are absent on macOS.

## Approaches

1. **Dedicated development helper and private test state (selected):** `dev-up.zsh` opts into local backup support only for the `argintaxpayer_test` profile. A non-sudo helper manages only the API process tracked by this launcher's PID file. The launcher supplies a private local backup path, a mode-0600 development key, and PostgreSQL tools beside its existing `psql` binary. Local development disables remote FTPS.
2. **Install the production helper on macOS:** rejected because it expects Linux systemd and sudoers, and would grant broader host privileges than the local workflow needs.
3. **Leave the local page read-only:** rejected because the user wants to verify backup behavior on the Mac before enabling it on Ubuntu.

## Goals and constraints

- Keep production's default root-owned helper and sudo path unchanged.
- Enable the local helper only when the launcher is using the test database; the local development key must never be used for Ubuntu backups.
- Stop and start only the API process launched by `dev-up.zsh`; never match or kill by a broad process name or port alone.
- Never enable remote FTPS from the local-development profile.
- Store local encrypted backups and key material under `~/.argintaxpayer-test`, with the key readable only by the current user. Preserve an existing key and reject malformed or symlinked key files instead of replacing them.
- Do not start, stop, back up, restore, or deploy services while implementing or verifying this change.

## Design

Add configurable helper path and sudo use to `ProcessBackupHostCommandRunner`; defaults retain current production behavior. Worker startup validates, using the host's resolved environment, that local mode is in Development and the base connection string selects `argintaxpayer_test`, before any optional recovery database override. In test-mode `dev-up.zsh`, configure a new macOS helper, set `UseSudo=false`, local backup/key paths, and `pg_dump`/`pg_restore` beside `psql`. The helper accepts only `stop-business`, `start-business`, and `status-business`; it validates the API PID's command line, checkout working directory, and launcher-generated nonce before stopping it, launches the same `dotnet watch` command for restart, and checks the existing loopback readiness endpoint. In the Worker settings service, an explicit local-development flag forces remote backup off and uses the configured local path and development key version for both reads and writes.

## Non-goals

- No Ubuntu changes or deployment in this phase.
- No restore activation through the local helper; production activation remains on the Ubuntu helper.
- No backup operation or service restart during verification.
