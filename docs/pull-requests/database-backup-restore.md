# Add encrypted database backup, ZIP transfer and controlled restore

## Changes

Add the root-admin backup page and a dedicated loopback controller with queued operations, real stage progress, encrypted PostgreSQL archives, checksum/archive verification, local retention and FTPS support. ZIP downloads contain encrypted archive plus manifest; bounded authenticated uploads verify structure, checksum, key and complete archive before registration. Restore stages an isolated database and requires explicit activation and recovery review.

Prepare Ubuntu deployment with a compatible restricted sudo helper, an API database-selection drop-in, delayed controller restart after API activation, private storage separate from migration archives, and a read-only operator preflight run before manual deployment. Block later deployments with an active restore override until their migration target has been reviewed. Local macOS support is limited to test backups and ZIP validation; activation is Ubuntu-only.

## Validation

- .NET backup/FTPS/lifecycle tests: 40 passed.
- Phase18 web/deploy tests: 95 passed.
- Python deploy-helper/schema tests: 32 passed.
- Web TypeScript check and shell syntax/whitespace checks passed.
- Prior local real test backup and ZIP roundtrip/archive/checksum verification succeeded, including invalid/wrong-key/oversized/traversal/duplicate rejection.

## Ubuntu acceptance before enabling backups

Follow docs/phases/phase-18-backup-restore.md. Install the reviewed helper/unit/API drop-in, provision version-compatible PostgreSQL clients, private key and independent restore credentials, review Nginx limits, run the read-only backup preflight, then deploy through the existing SHA confirmation gates. Perform a full backup and restore/activation drill on an isolated Ubuntu test deployment. Preserve old database and key escrow.

No Ubuntu configuration, database restore, main merge or deployment was performed in preparing this PR. The current macOS archive is from argintaxpayer_test and is not a production database backup. Deployments after restore intentionally require manual migration-target review. Linux sandbox execution still needs the operator preflight on the target host.
