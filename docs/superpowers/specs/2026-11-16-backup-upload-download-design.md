# Backup upload and download

## Proposed approach
Download a ZIP containing exactly the existing encrypted `.argonbackup` and its `.manifest.json`. Do not include encryption keys. Show the source database name in the file list. Allow a root administrator to upload this ZIP, validate its manifest, size, checksum, key version, encrypted archive and PostgreSQL archive before registering it as a local backup. Use the existing isolated restore staging and explicit activation flow; upload never activates a database.

## Alternative
Upload archive and manifest as two separate files. This reuses the current on-disk format but requires users to keep both files together.

## Implementation boundaries
Authenticated root-only controller routes for streaming downloads and bounded streamed upload; same-origin web proxy forwards binary streams without JSON buffering. Enforce configurable upload size, free storage, exact ZIP entry names/count, no paths or symlinks, no extraction to arbitrary paths, restricted temporary files and cleanup on errors/cancellation. Verify claimed checksum before publishing, authenticate encrypted contents and validate the whole archive using pg_restore without running SQL. Reject missing keys and schema mismatches clearly. Handle duplicate artifact IDs without overwriting backups. Block upload during active backup/recovery transitions. Show upload bytes progress and real server validation stage progress, then use existing restore preparation/review/activation controls.

## Key prerequisite
The current artifact is from `argintaxpayer_test`, schema `phase18`, encrypted under `dev-mac-1`. Ubuntu must have the matching key provisioned separately; merely using the same version label does not provide the same key. No key upload/download through the browser. Local helper currently supports only test API lifecycle and does not support database activation; macOS activation requires a separate safe local target switch implementation before claiming end-to-end restore support. Do not activate a restore against the user's database as part of implementation checks without explicit authorization.

## Verification
Build backend and typecheck frontend; verify binary download matches original checksum, valid upload registers a verified artifact, malformed/oversized/path-traversal/corrupt/wrong-key/duplicate uploads fail safely. Verify isolated restore preparation only with an explicitly authorized test database; activation is a separate user action.
