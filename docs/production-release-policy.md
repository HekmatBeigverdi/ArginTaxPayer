# Production release policy — owner approved 2026-11-17

The owner has approved production operation, including live SMS and Sandbox and
Production invoice submission, against the main `argintaxpayer_prod` application
database. Routine releases must preserve approved feature settings and credentials.
Later implementation phases must not silently disable previously enabled features.
CI/test process flags are isolated from runtime and must never be copied to server
configuration. Per-user authorization, fiscal-memory environment routing, configured
provider endpoints, and maintenance controls continue to apply.

## One-time activation before the next deploy

Run from the reviewed checkout on the VPS:

```bash
sudo python3 scripts/phase18-enable-production-features.py
```

The command preserves provider credentials, records a private backup of existing
configuration, and enables API/UI, SMS for real users (disabling the test-recipient-only gate), hosted Sandbox, and both legacy invoice
providers in the existing environment files. API/Web must then be restarted or
loaded by the next approved deploy. Routine deploy does not rewrite these files.
Historical `--initial-disabled-ui` provisioning is not part of routine release.

The activation command does not send messages/invoices and does not switch the
database. A restore may currently select a `recovery_*` database containing the
latest production data. `argintaxpayer_prod` in the base configuration is not proof
that it is the active database. Do not clear `backup-db.env` or rename databases
without a separately reviewed data-preserving recovery cutover.

Remote backup uses Explicit FTPS (port 21), not SFTP. Save settings before the
password; the UI saves the current username/settings before saving its password.
