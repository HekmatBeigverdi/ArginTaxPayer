# Production deployment on the replacement VPS

## Current behavior

Merge to `main` runs API/web tests and packaging. The deploy job uses the existing protected `production` environment. When that environment requires approval, the job waits for its reviewer; repository code does not remove or bypass approval rules. A manual run can opt in with `deploy=true`.

GitHub builds the application once, uploads only the two archives and their source/checksum metadata to `~/argin-deploy-incoming/<SHA>/`, and invokes:

```bash
sudo -n /usr/local/sbin/argin-taxpayer-release --deploy <40-character-SHA>
```

The helper serializes deployments, pins and snapshots regular upload files, validates the SHA and checksums, and extracts only bounded regular files and directories. Upload-provided scripts, links, special files and private configuration are rejected. npm installs locked runtime dependencies as `argin-deploy` with lifecycle scripts and user/global npm configuration disabled. No .NET or Next.js build runs on the VPS.

Final runtime files are copied through no-follow descriptors into a new root-owned, group-readable release. npm `.bin` symlinks are omitted because systemd launches Next directly. The only application-writable directory is `.next/cache`. A fixed root-owned probe checks loopback listeners, API live/ready, login availability and unauthenticated identity rejection. Restart or health failure restores the prior application symlink; database rollback is never attempted.

## One-time installation

Prerequisites: dedicated `argin-deploy` and `argintaxsvc` accounts, Node/npm under `/opt/argintaxpayer/runtime/node/bin`, working root-owned API/web units and private environment files, and the existing activation marker. Python 3.11+ is required; the installer does not assume Ubuntu 24.04.

Use an isolated checkout of the reviewed helper commit. As `argindev`, substitute that exact commit SHA below:

```bash
git fetch origin <helper-branch>
git worktree add --detach /home/argindev/argin-release-helper <reviewed-full-SHA>
cd /home/argindev/argin-release-helper
python3 -m unittest discover -s tests/ops -p 'test_phase18_deploy_helper.py' -v
sudo bash scripts/phase18-install-deploy-helper.sh --install <reviewed-full-SHA>
```

The installer verifies the source commit and tracked-file cleanliness. It installs the helper and health probe as root-owned files, with only the helper permitted in sudoers. Re-running the same installer is safe; a different existing helper is refused and requires separate operator review. Installation does not start/restart services or change databases, feature switches, secrets, TLS or Nginx.

Verify that the deployed account cannot pass arbitrary input (expected refusal and exit code 2):

```bash
sudo -u argin-deploy -H sudo -n /usr/local/sbin/argin-taxpayer-release --deploy invalid
```

Do not merge the workflow change until helper installation is complete. Cancel old waiting runs that still use the previous direct-sudo deployment commands.

## GitHub configuration

Set `ARGIN_DEPLOY_HOST`, `ARGIN_DEPLOY_USER=argin-deploy`, `ARGIN_DEPLOY_SSH_KEY`, and `ARGIN_DEPLOY_KNOWN_HOSTS` in the protected production environment. Verify the host fingerprint directly on the VPS before setting known_hosts. Retain SSH host verification; `restrict` on the authorized public key disables forwarding while still allowing the required exec/SFTP operations.

The workflow checks current `main` just before uploading, rejecting obsolete approvals. GitHub concurrency and a separate root-held VPS lock prevent overlapping deployment mutations. Production credentials remain on the VPS and are not provided to CI.

## Database changes

This workflow deploys applications only. Before approving a release with schema changes: take a database backup, generate and review SQL for the exact target database, apply the migrations, and verify the `argin_prod_app` permissions on every new table/view/sequence. Readiness alone does not prove all invoice queries work. Use backward-compatible migrations so application rollback remains possible.

Never restore a development/test database over the live database as part of a release. Production dispatch, Sandbox dispatch, SMS and payment settings are preserved in `/etc/argintaxpayer`; application deployment does not enable them.

## Verification and incident handling

After the first real GitHub deployment, verify the active SHA, HTTPS login, existing invoices and service states. Local tests cannot verify the GitHub private key, production environment reviewer settings, inbound SSH reachability, or the real service restart on this VPS.

Matching immutable releases created by this helper can be retried without replacing their code. A legacy staged release without a stored checksum manifest, or a different artifact for the same SHA, is refused. The operator can inspect private npm logs at `/opt/argintaxpayer/deploy-logs/<SHA>.npm.log`; never post credential-bearing logs.

The helper retains old application releases and incoming artifacts; disk retention/cleanup is an operator task. Signals are handled with application rollback when possible, but SIGKILL or machine/power loss cannot execute rollback code. Inspect `/opt/argintaxpayer/current` and systemd states after such interruption. Source/dependency errors before promotion leave the active symlink unchanged. A failed initial deployment stops both services if no prior application exists.
