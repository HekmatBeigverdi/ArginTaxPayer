# Phase 18 — GitHub release and Ubuntu deployment operations

This runbook describes the one-time setup for the merge-triggered release workflow in `.github/workflows/phase18-production-release.yml`. It does not itself activate the public application, change DNS/Nginx, migrate a database, or enable tax, SMS, or payment operations.

## 1. GitHub repository settings

### Protect `main`

In repository **Settings → Rules → Rulesets** (or branch protection rules), require changes to enter `main` through a Pull Request. Require at least one approving review, dismiss stale approvals after new commits, require conversation resolution, and require the package workflow's named status check before merge. Disable force pushes and deletion of `main`. Keep admin bypass disabled where practical. The exact displayed status-check name is determined after the first workflow run.

The workflow also runs for Pull Requests to `phase/18-production-deployment` during Phase 18 development. Deployment is possible only for a successful `push` to `main`; a feature branch, pull request, or phase branch cannot enter the production deployment job.

### Approve the post-merge Production deployment

Pull Request approval and Production deployment approval are separate actions. After the reviewed PR is merged to `main`, wait for the `package` job to pass. The `deploy` job references the GitHub Environment named `production`; when that Environment has a required reviewer configured, GitHub pauses the job at **Waiting** before it can read Environment secrets or contact the VPS.

To release that exact merge SHA, open the repository's **Actions** tab, select the `ArginTaxPayer versioned release` run for the `main` push, choose **Review deployments**, select `production`, add a short release note if desired, and choose **Approve and deploy**. Rejecting leaves the release undeployed. After approval, the job verifies the artifact SHA/checksums, uses pinned-host-key SSH, and invokes the fixed VPS helper. The helper promotes the package and performs its health check/rollback; it does not run migrations or change fiscal-authority settings.

The reviewer gate exists only if a required reviewer has been configured in **Settings → Environments → production**. `environment: production` in YAML associates the job and its secrets with that Environment; it does not by itself require manual approval. If **Prevent self-review** is enabled, the person who triggered the workflow cannot approve that deployment. GitHub's required-reviewer availability for private repositories depends on the repository plan.

### Configure the production approval gate

Create repository **Settings → Environments → New environment → `production`**. Add the designated owner/operator as a required reviewer, prevent self-review if available, and enable administrator bypass protection where available. Add these as **Environment secrets**, not repository-wide secrets:

| Secret | Value |
|---|---|
| `ARGIN_DEPLOY_HOST` | Verified Ubuntu VPS DNS name or IP reachable from GitHub Actions |
| `ARGIN_DEPLOY_USER` | `argin-deploy` |
| `ARGIN_DEPLOY_SSH_KEY` | Dedicated private SSH key for the restricted deploy account |
| `ARGIN_DEPLOY_KNOWN_HOSTS` | Independently verified SSH host-key line for the exact deploy host |

Obtain the host key fingerprint through a trusted administrative channel and compare it with the server console or an already trusted SSH connection. Do not trust a key collected by `ssh-keyscan` inside the workflow. Never use `StrictHostKeyChecking=no`.

Do not put production DB passwords, fiscal-memory private keys, API provider tokens, TLS private keys, or `.env` files in GitHub secrets or workflow artifacts. This deployment job requires SSH transport values only.

## 2. One-time Ubuntu preparation

Perform these commands in a reviewed maintenance session as an Ubuntu administrator. They create a dedicated restricted deployment account, a fixed upload directory, and a helper with a single SHA argument. They do not start services, touch the database, change app flags, or create the activation marker.

First, ensure a clean checkout of the reviewed source commit exists on the server and inspect its identity:

```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git status --short
git rev-parse HEAD
```

Review the phase branch / pull request containing the deployment helper before installing it. The installer intentionally refuses tracked modifications. Then provision the account and install its public key using the approved server access procedure:

```bash
sudo useradd --create-home --shell /bin/bash argin-deploy
sudo install -d -o argin-deploy -g argin-deploy -m 0700 /home/argin-deploy/.ssh
sudo install -o argin-deploy -g argin-deploy -m 0600 /dev/null /home/argin-deploy/.ssh/authorized_keys
```

Append the public key corresponding to the GitHub Environment's `ARGIN_DEPLOY_SSH_KEY` to `/home/argin-deploy/.ssh/authorized_keys` through the trusted console. The account needs `/bin/bash` because GitHub Actions runs remote `mkdir` and the fixed helper command, and SCP uses an SSH remote subsystem. Prefix the key with `restrict` (or the equivalent `no-pty,no-port-forwarding,no-agent-forwarding,no-X11-forwarding` options); restrict source addresses in `authorized_keys` if a stable allowlist is available. The SSH identity can run commands as the unprivileged `argin-deploy` user, so keep its home private and grant it no access to app secrets or other users' files. Its only passwordless privilege is the fixed root-owned release helper with a SHA argument; inspect and remove any other sudo grants for this account.

After the reviewed source is available, install the fixed helper:

```bash
cd ~/projects/ArginTaxPayer
sudo bash scripts/phase18-install-deploy-helper.sh --install
sudo visudo -cf /etc/sudoers
sudo stat -c '%U %G %a %n' \
  /usr/local/sbin/argin-taxpayer-release \
  /usr/local/lib/argin-taxpayer-deploy \
  /var/lib/argin-taxpayer-deploy \
  /var/lib/argin-taxpayer-deploy/incoming
```

Expected ownership: root owns the wrapper, library and parent state directory; `argin-deploy` owns the `incoming` directory. The only passwordless sudo grant is the exact wrapper with the required `--sha` argument. Never grant `NOPASSWD:ALL` or SSH access as root. If `argin-deploy` already exists, inspect its shell, group, home, authorized keys, and sudo policy rather than overwriting it.

Before enabling GitHub deployment, check the server-side preconditions with the Phase 18 operator: service units and their Node runtime, `/etc/argintaxpayer/api.env` and `web.env` permissions, activation marker policy, health probe, and a known-good prior release/rollback decision. Locked production Node dependencies are built into the CI artifact; the host does not fetch packages during deployment. These items are separate from installing the SSH helper. Production API and UI activation remain governed by Phase 18 owner approval and do not become approved merely because the workflow can connect.

## 3. Developer workflow

For each routine application change:

```bash
git switch -c feature/short-description
# edit and run local checks using local/test settings
git status --short
git add <reviewed-files>
git commit -m "type: describe change"
git push -u origin feature/short-description
```

Open a Pull Request to `main`. Review the diff and CI results. Keep environment-specific settings, private keys, and database passwords out of the commit. After required reviewers and checks approve, merge the PR. The push to `main` builds one artifact for that merge SHA; deployment waits for approval in the `production` Environment. Approve only after confirming the SHA and release purpose. GitHub Actions reports checksum, SSH, helper, and health-check results; a failed health check triggers app-only rollback to the prior symlink target.

Do not download a different artifact or rebuild on the VPS. The deployment job reuses the artifact made from that exact `main` SHA.

## 4. Database changes are a separate release operation

The automated application workflow deliberately contains no database credentials and does not call `dotnet ef database update`. For any schema change:

1. Review the EF migration and generate a deterministic SQL script from the reviewed commit.
2. Verify the server and database identity, expected database name, migration history, and connection target before execution. Never rely on a forwarded local port as proof of the target database.
3. Verify a recent encrypted backup and a successful restore into an isolated recovery database; preserve required key material separately.
4. Obtain explicit owner/operator approval, apply the reviewed SQL under the migration role, and record its SHA and result.
5. Deploy only an application release compatible with both the pre- and post-migration schema where possible. Application rollback does not roll back SQL; use the documented restore/recovery plan if the database change itself must be reversed.

The AutoSSH tunnel described in local development notes is not used by GitHub Actions. It is not a production migration procedure.

## 5. Safety limits and remaining setup evidence

- Deployment restarts only `argintaxpayer-api.service` and `argintaxpayer-web.service`; it does not alter Nginx, PostgreSQL, DNS, unrelated services, or environment files.
- Deployment does not toggle SMS, Sandbox, live tax submission, identity onboarding, payments, or production UI flags.
- Fiscal-memory routing continues to depend on the app's persisted environment contract. The release workflow does not establish or certify that contract.
- A successful package build is not proof of a successful VPS release. First automated deploy still requires GitHub rules/Environment configuration, deploy secrets, the server account/key/helper, reviewed activation preconditions, and an approved release SHA.
- Record the first actual run ID, source SHA, Environment approver, artifact checksum, helper output and health outcome in the Phase 18 step log. Do not claim deployment before those results exist.
