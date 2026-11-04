# Phase 18 — Step 06: isolated Systemd supervision (pre-activation)

**Scope:** Prepare and audit dedicated unprivileged `systemd` services for ArginTaxPayer ASP.NET Core API and Next.js without publishing the application, changing Nginx, reusing the existing PostgreSQL database, or starting tax/SMS/payment operations. Steps 07 (production DB), 08 (credentials/keys), 09–12 (authority/security/recovery) and real HTTPS browser acceptance remain mandatory. Installing disabled units is not go-live.

## Ownership and resource contract

| Item | Defined state | Phase 06 action |
| --- | --- | --- |
| Linux service identity | `argintaxsvc`, no-login system account | Create on Ubuntu only via explicit privileged installer |
| API systemd unit | `argintaxpayer-api.service` | Install DISABLED and INACTIVE |
| Web systemd unit | `argintaxpayer-web.service` | Install DISABLED and INACTIVE |
| API internal HTTP | `127.0.0.1:15090` | Reserve; do not bind in Step06 |
| Web internal HTTP | `127.0.0.1:15091` | Reserve; do not expose through Nginx yet |
| Actual Postgres 17 | Existing shared Docker Postgres, port `127.0.0.1:5432` | NO TOUCH / NO MIGRATION |
| QA PostgreSQL | Disposable Phase18 `127.0.0.1:15433` | Keep for acceptance, never use for future Production runtime |
| Dedicated Production DB | To be provisioned and approved in Step07 | Not created/connected in Step06 |
| API release directory | `/opt/argintaxpayer/current/api` | Contract only; actual release staging/symlink in later controlled step |
| Next release directory | `/opt/argintaxpayer/current/web` | Contract only; later stage `node_modules` from lockfile + prebuilt `.next` |
| Private config files | `/etc/argintaxpayer/{api,web}.env` | Not created by Step06. Configure in Step08 with root-controlled access |
| Start approval | `/etc/argintaxpayer/phase18-activation-approved` | MUST NOT EXIST in Step06 |
| Public `tax.argin.ir` | Existing HTTPS maintenance route | Unchanged |
| Tax authority send and payment providers | Not authorized for production operation | Never exercised |

### Service safeguards

The unit files are `ops/phase18-systemd/argintaxpayer-api.service` and `argintaxpayer-web.service`. They have fixed loopback binding, distinct ports, Production runtime environment, `After` startup ordering, bounded failure restarts (`StartLimitBurst=3` in 300s, 10s restart delay), SIGTERM + timeout, locked-down `systemd` filesystem/permissions, no Linux capabilities, `UMask=0077`, dedicated state/runtime directories, journal logs, and a missing-by-design approval marker. Web has a narrowly scoped writable `.next/cache` exception because Next.js caches at runtime. During future staging this cache directory must exist, be writable to `argintaxsvc`, and contain **no application binaries or secrets**.

The optional `EnvironmentFile` lines are **future wiring only**. They can override hard-coded environment variables, so Step08 MUST review them against all admission gates before starting the services. No unit performs schema migrations, automatic invoice inquiry, key generation, or provider calls. Even when an operator manually creates the activation marker, missing release paths and Production DB configuration remain startup blockers; NEVER treat creation of the marker as deployment approval.

Production API process user is **not** the PostgreSQL admin/system user. Future database connection must use a least-privileged separate DB login and TLS. Web BFF uses internal API loopback; Nginx must be configured separately after explicit approval. Do not use `argintaxpayer_test` or `argin_phase18_acceptance` for Production.

## Stage A: identical source baseline, macOS (unprivileged)

```bash
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git status --short
git rev-parse HEAD
bash -n scripts/phase18-step06-*.sh
node --test apps/web/tests/phase18-step06-systemd-contract.test.mjs
bash scripts/phase18-step06-systemd-preflight.sh
```

Keep the full HEAD SHA. Darwin preflight explicitly marks `systemd-syntax UBUNTU_ONLY`.

## Stage B: Ubuntu nonroot read-only acceptance

```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git status --short
git rev-parse HEAD
bash -n scripts/phase18-step06-*.sh
node --test apps/web/tests/phase18-step06-systemd-contract.test.mjs
bash scripts/phase18-step06-systemd-preflight.sh
```

Do not proceed if macOS/Ubuntu SHAs differ or port 15090/15091 is occupied. The preflight reports `unit-contract PASS`, `systemd-syntax PASS`, both ports FREE, and no currently active service.

## Stage C: intentional Ubuntu installation — *DISABLED/INACTIVE*

Only after Stage B passes and the owner explicitly runs the following. The installer verifies branch/clean tree, Ubuntu 24.04, exact non-conflicting unit names, two free ports, no approval marker, and syntax **before any privileged changes**. It creates `argintaxsvc`, isolated directories, installs exactly two unit files and calls `systemctl daemon-reload`. It never calls `systemctl start`, `enable`, `restart` or reloads Nginx, Docker, PostgreSQL or SSH.

```bash
sudo bash scripts/phase18-step06-install-disabled.sh --install-disabled
bash scripts/phase18-step06-installed-audit.sh
systemctl is-enabled argintaxpayer-api.service argintaxpayer-web.service
systemctl is-active argintaxpayer-api.service argintaxpayer-web.service
```

Expected: both `disabled`, both `inactive`, unit installed-audit `PASS`. The read-only audit compares installed units byte-for-byte to GitHub reviewed templates; it records dedicated port ownership and no activation marker. Systemctl `is-active` commonly exits nonzero for inactive units; this is expected, not Step06 failure.

**No installation idempotency promise:** the privileged installer refuses existing service user/unit names rather than overwriting or taking ownership. A retry after partial privileged setup needs human review of the exact residual state; never delete or override blind.

## Health/readiness and staged release: explicitly future gated

Before actual future traffic, run `bash scripts/phase18-step06-local-health.sh --probe-local` after operator-approved service activation. It checks both listening addresses are exclusively `127.0.0.1`, API `/health/live=200`, DB-dependent `/health/ready=200`, Next.js BFF route responds locally, and Production `/health/dev-identity=404`. These checks are NOT expected to pass while services are deliberately inactive in Step06. A later release-switch must verify tarball SHA256 manifests, npm lockfile install, permissions, DB schema compatibility, approval gates and rollback; do not repoint `current` here.

The Step05 actual API/BFF and populated cross-tenant HTTP acceptance on isolated PostgreSQL PASS remains the functional local proof. Systemd supervision itself is verified by `systemd-analyze verify` and disabled-unit policy; restart recovery and live readiness cannot be claimed until actual authorized service staging in later steps.

## Rollback — only if units are still disabled and inactive

```bash
sudo bash scripts/phase18-step06-remove-disabled.sh --remove-disabled-units
```

Rollback refuses any service that is active/enabled, an activation marker, a symlinked or modified unit file. It deletes only **these two identical unit files** and calls `systemctl daemon-reload`; it retains the service identity, state/cache directories and QA DB for manual review. This is NOT a database rollback or a live-release rollback.

## Acceptance evidence and remaining gates

Record macOS and Ubuntu identical SHA, `bash -n`, Node unit contract, Ubuntu `systemd-analyze verify`, reserved free ports, successful disabled install, installed unit file parity and no active service, and report paths under `artifacts/phase18-step06/<SHA>/`. Mark Step06 **owner accepted only after actual Ubuntu installer+audit output**. Keep `production-go-live=BLOCKED`, HTTPS maintenance unchanged, and release/merge deferred. After that proceed to Step07 Production DB provisioning with a separate owner-approved, backup-aware plan, without modifying the shared Docker PostgreSQL.
