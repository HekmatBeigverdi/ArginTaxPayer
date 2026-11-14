# Local database profiles and shared Production serials

Development defaults to `ARGIN_DB_TARGET=test` in `.env.dev.local`. Existing test connection and credentials remain there. The launcher connects to `argintaxpayer_test` via SSH port 2222 and loopback forward 15432. It never applies migrations. Production schema changes remain in the deployment migration pipeline.

To diagnose the live database from macOS:

1. Copy `.env.production.local.example` to `.env.production.local` (ignored by git, mode 600).
2. Supply the deployed application's DB credentials, production CA certificate, OTP pepper, MFA encryption key and database signing custody key through this private file. Preserve the production encryption material; test keys cannot decrypt production rows. Use `argin_prod_app`, not a migration role, for the application.
3. Stop local processes using `zsh scripts/dev-down.zsh`.
4. Set `ARGIN_DB_TARGET=production` in `.env.dev.local`, then run `zsh scripts/dev-up.zsh`.
5. To return to development, stop processes, change the target to `test`, and start again.

Production diagnostics is a live connection: actions in the local UI operate on actual user data. Registration bypass is disabled in the production profile. TLS verification uses your private production CA. No credentials belong in the repository.

## Real Production sends while the invoice DB remains TEST

Set `ConnectionStrings__FiscalSerialRegistry` in `.env.dev.local` to the Production DB using port 15434 and TLS verification (see the example). The launcher opens a separate SSH forward for it. Production deployment defaults to its main Production DB as the registry and requires no new environment variable.

The shared table identifies a memory by its actual six-character code, not DB-specific UUIDs. Reservations serialize per memory with a PostgreSQL transaction lock. Retries for the same immutable invoice return the same serial. A reserved serial is never recycled after a rollback, timeout or failed send. Registry failure stops preparation without a local counter fallback. Sandbox keeps its independent numbering.

## One-time rollout

Stop real Production sends from both installations during initial synchronization. Apply migration `20261115120000_AddSharedProductionFiscalSerials` through the normal migration process. It creates append-only reservations and imported floors; it does not alter prior invoices or their tax IDs.

Before either installation sends using the new code, run the sync utility with private connections:

```sh
# SERIAL_SYNC_SOURCE: existing TEST DB connection
# SERIAL_SYNC_TARGET: Production migrator connection (used only by this utility)
dotnet run --project tools/ArginTaxPayer.SerialSync
```

It reads Production reservations from TEST and raises the same-code Production floor. Existing Production reservations are also considered for every allocation. It never lowers a floor, prints credentials, changes invoices or sends packets. Run it for each additional historical database. Existing LastUsedSerial settings remain lower bounds. Successful calls may leave harmless gaps in the sequence.

Deploy the new code and configure the shared registry on every local client before resuming sends. Older versions that allocate independently must no longer send using this memory. Merging/deploying afterward preserves the shared table and requires no repeated synchronization.

Existing rejected invoices retain their old reserved tax IDs. Correcting their retry workflow is separate from changing the global allocator; this change does not resend them.

## Configuration prepared on 2026-11-15

Both database connections were verified. The matching Production memory had maximum reserved serial 20 in TEST and 13 in Production. The existing `ProductionInvoiceLegacy__LastUsedSerial__<memory UUID>` setting was therefore raised to 20 in the server's private `/etc/argintaxpayer/api.env` and in both local private profiles, using each database's own memory UUID. This is an alternative bootstrap to importing floors with SerialSync: on its first new reservation the shared registry incorporates that lower bound and preserves the allocated number durably.

The server profile backup is `/etc/argintaxpayer/api.env.before-serial-bootstrap-20261115` (root only). The local configuration backup and production CA are under `~/.argintaxpayer-production/` (private). No credentials or encryption keys are tracked by git.

The running server was not restarted and the new migration was not applied manually. The next normal deployment must apply `20261115120000_AddSharedProductionFiscalSerials` and restart the API to load the saved lower bound. Start the updated local code afterward. Do not issue Production invoices from either old installation between this bootstrap and rollout; otherwise re-read the maxima before enabling the new sender. With no intervening reservations, the next new serial is 21. Previously rejected reservations 12 and 13 remain unchanged.
