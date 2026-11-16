# Backup ZIP Upload and Download Implementation Plan

**Goal:** Root administrators can download encrypted backup bundles, upload and validate bundles, then prepare them through the existing isolated restore workflow.

**Architecture:** A focused bundle service packages verified artifacts and imports bounded ZIPs through the controller work queue. The web proxy streams binary requests/responses. The UI reports byte upload progress and actual server validation stages. Encryption keys stay provisioned on the host.

**Tech Stack:** .NET 9, System.IO.Compression, Next.js, React.

- [x] Implement private bounded ZIP staging, exact entry validation, checksum/key/archive verification and duplicate protection in BackupBundleService.
- [x] Add authenticated download/upload routes, queue integration and validation progress stages; protect against concurrent recovery/backup work.
- [x] Stream binary payloads through the same-origin web proxy and add upload, progress, database name and download controls to the existing page.
- [x] Build/typecheck; check valid roundtrip, malformed ZIP, missing key, duplicate and corrupt input using a temporary diagnostic harness without activating a restore.
- [x] Document key provisioning and current macOS activation limitation; verify running controller health.


## Completion evidence

- Worker build: 0 errors (existing Unix-platform analyzer warnings); frontend typecheck passed.
- Temporary service-level diagnostic harness exercised the real artifact: ZIP download/import roundtrip, full archive verification, mounted-volume capacity and missing destination ancestry, transfer serialization and disposal, startup orphan cleanup, duplicate preservation, bad ZIP, size limit, missing/wrong key, extra entry, checksum mismatch and path traversal. All passed. No database restore or activation executed.
- Running controller health ready; unauthenticated upload/download both return 401.
- Review found download capacity/concurrency, destination mount capacity, recovery transition during upload and orphan staging cleanup gaps; all corrected and relevant diagnostics rerun successfully.
- Ubuntu Nginx streaming/size configuration added to source, not deployed. macOS activation explicitly blocked; restore credentials/key provisioning still required on deployment target.
- Ruling: execute in the existing shared checkout to preserve the already running controller and approved local changes; avoid copying unrelated dirty files. A focused read-only reviewer checked the final feature.
