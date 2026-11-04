# Phase 18 / Step 08 — Production configuration and cryptographic custody

**State:** SOURCE IMPLEMENTED; operator review and acceptance PENDING. No live provider credentials, runtime DB connection, or signing/RSA secrets were generated, imported, rotated or activated by Step08.

## Mandatory distinction

Application deployment Production is **not** the tax authority destination. Existing Sandbox and real fiscal-memory credentials remain distinct, with invoice/memory-environment routing subject to independent Step09/10 server-side verification. There must be no global setting that sends every invoice to the real authority.

## Source findings

- The API unit uses `EnvironmentFile=-/etc/argintaxpayer/api.env`, which is **optional**. The Web unit likewise uses `web.env`. Both units have an explicit activation marker and are currently disabled; a missing env file cannot be mistaken for ready config.
- The API has default operational opt-in OFF. The service unit disables Ghasedak live sending and legacy Sandbox dispatch. The Web unit disables Production UI and identity UI.
- `appsettings.Production.json` contains only the deployment label; it is **not** a secret store or sufficient production configuration.
- Step07 dedicated PostgreSQL uses `argintaxpayer_prod` through TLS VerifyFull with separate migrator/app passwords. The Step07 operator-private credentials and dedicated CA must never be printed, committed or distributed.
- Key material format and persistence for MFA, JWT/session, Phase09 RSA-wrapping/custody, real/Sandbox fiscal memories, payment/SMS gateways require independent code-level checks before operational enablement. Do not invent or rotate a key based on guessed configuration names.

## Stage A: macOS source validation (NO writes)

```bash
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
bash scripts/phase18-step08-config-custody-preflight.sh
git rev-parse HEAD
```

## Stage B: Ubuntu read-only verification

After matching **exact** macOS SHA:

```bash
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
export PHASE18_EXPECTED_SHA="$(git rev-parse HEAD)"
sudo -v
bash scripts/phase18-step08-config-custody-preflight.sh
```

This never starts app or database services and does not access secret contents.

## Stage C: owner-controlled PRIVATE skeleton staging (new files only)

Only if source preflight PASS and owner separately approves staging:

```bash
export PHASE18_OWNER_APPROVED_CONFIG_STAGING=YES
bash scripts/phase18-step08-stage-private-config.sh --stage-disabled-private-config
```

Creates only the **new** `/etc/argintaxpayer/api.env` and `web.env` with `root:root 0600` and disabled switches. Does NOT configure database access or encryption, issue fiscal-memory keys, seed data, send authority requests, activate API/Web services, touch Nginx, or overwrite existing files. Owner should never paste private file content into a chat.

## Mandatory outstanding Step08 security acceptance (BLOCKING)

1. Verify specific **actual source consumers and supported formats** for persisted MFA encryption, application JWT/session signing, fiscal private-key encryption/wrapping, and provider configuration; document their configuration-key names **without** values.
2. Design per-service access to only required secrets, with root ownership and 0600 configuration, and ensure keys persist across API restarts/deploys/DB backup restoration. Never write a new key atop ciphertext protected by an old one.
3. Distinguish Sandbox fiscal-memory and real fiscal-memory credentials and test mismatch/unknown environment rejection before any real API call. Step09/10 implement/validate routing.
4. Provider credentials (Ghasedak, SEP, ZarinPal) remain OFF or absent until specifically approved; validate only presence/format without making outbound calls or charging money.
5. Confirm session/JWT cookie flags (`Secure`, `HttpOnly`, `SameSite`, expiry), CSRF/origin and proxy trust under Production. Step11 owns full browser authorization regression.
6. Perform a **controlled** restart-persistence test after authenticated private test data/keys exist in this dedicated Production environment. Without it, claim only static custody readiness, not real key recovery.
7. Encrypted off-host backup/restore of both DB and independent key escrow remains Step12 before go-live. Step07 acceptance backups are not substitutes.
8. Verify source SHA parity (macOS/Ubuntu), no secret leaks in Git or logs, web/API units remain inactive, and production-go-live BLOCKED.

**No Step08 signoff, no public production runtime startup, and no enabling of live tax/SMS/payment APIs until all required gates are evidenced.**
