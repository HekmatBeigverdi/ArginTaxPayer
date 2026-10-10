# ADR-0004 — Admin-created accounts and repeatable legacy import

- Status: Accepted product direction / Phase 01 implementation design pending
- Date: 2026-10-10
- Supersedes relevant migration assumptions in Phase 01 Step 02 (do NOT migrate legacy identity/passwords or plan/payment ledgers automatically)
- Target delivery: Phase 14 with prerequisites in Phase 02 (data model), Phase 03 (identity), Phase 04 (taxpayers), Phase 09 (key custody), Phase 12 (manual entitlement adjustments).

## Decision

1. **Fresh identities.** Root or duly authorized Level 2 creates each new user and taxpayer manually; login username is usually normalized mobile number. Admin sets temporary password through a secure flow, force change at first login; don't replicate legacy username/password/hash or social identity. Scope delegation remains root-controlled.
2. **Manual linkage.** After new taxpayer has been created and assigned, admin enters `LegacySourceId` and `LegacyCustomerId` on a secure import-link screen. Uniqueness must be `(legacySourceId, legacyCustomerId)`, mapped to exactly one target taxpayer/owning workspace. Link changes require root-controlled reassignment or highly audited reconciliation to prevent one old customer leaking into another tenant. Do not assume old numeric customer PK is the same as `customers.IdentityId`; inspect legacy relations first.
3. **Selected data only.** Import taxpayer identity details where explicitly authorized, tax counterparties, goods/services, historically registered invoice header/lines and submission responses. Exclude legacy auth identities, password hashes, wallet balances, plan purchase history, supplier credentials and financial partnership ledgers by default.
4. **New fiscal connection.** App supports generating new public/private keypair and PKCS#10 CSR for natural/legal taxpayers, OR securely importing **private key content** from existing system. Admin inputs fiscal-memory code from official portal. Verify algorithm/format, private-public correspondence, certificate/portal registration and actual authorized connection before marking ready. Generating a new key does not automatically activate the existing portal fiscal memory: corresponding public key/certificate must be registered or updated through official process. Never display imported private key again; encrypt at rest with vault/KMS, strict permission and signing-only usage.
5. **Manual entitlements.** Admin chooses a new annual/quantity plan. To recognize existing value, admin applies explicit `EntitlementAdjustment` (days remaining or units remaining), with before/after, effective date, reason, actor, authorization and idempotency. 300 days remains = calculate expiry from agreed migration effective date (explicit timezone/calendar). Quantity remains = set opening balance with audit. Do not infer historical partner financial liability from these opening balances.
6. **Old system continues.** The old MySQL database remains writable in old application. New import reads consistent immutable backup/snapshot, or **read-only** source connection, never writes to source. Each run identifies changed/new records per linked customer. Snapshot import and reruns are supported. File upload of refreshed DB backup remains possible; never upload real dumps to public GitHub.
7. **Source identity mapping.** `LegacyRecordMap(sourceId, entityType, legacyPk, targetId, targetWorkspaceId, legacyCustomerId, sourceFingerprint, firstImportedAt, lastSeenAt, lastImportedAt)` with unique `(sourceId, entityType, legacyPk)` and ownership checks. Invoice natural keys additionally include original taxId/sourceUid and duplicate matching. Idempotent upsert only on mutable master data where policy permits.
8. **Invoice safety.** Imported previously issued/sent invoice records become `HistoricalImported` (immutable fiscal identity and original status), never enqueued for tax submission or deducted from new quota. Store original taxId, issue time, UID, referenceNumber and send history. If source updates history, append state observations/reconcile; do not silently overwrite accepted fiscal snapshots. Imported unsent drafts require explicit selection and validation before ever submitting.
9. **Conflicts & changes.** Preview counts (new/updated/unchanged/conflict/skipped) before commit. Existing new-system edits to customer/product records must not be blindly overwritten; use field ownership policy, source fingerprint and conflict queue. Deleted source records must not cause destructive delete in new system; tombstone/retire only after explicit review.
10. **Security.** Imports are tenant restricted, validated with MIME/schema/size, malware checks, data encryption and retention limits. Each job stores actor, source version, capture timestamp, checkpoints, affected IDs and redacted error report. No raw private key/password or personal data in logs. Level 2 sees only assigned customers, never global imports or other delegates' data.

## Logical entities

- `LegacySource(id, type, connectionSecretReference, schemaVersion, enabled)`
- `LegacyCustomerLink(id, sourceId, legacyCustomerId, targetTaxpayerId, workspaceId, linkedBy, linkedAt, status)`
- `LegacyImportBatch(id, sourceId, snapshotId, startedBy, importedAt, state, counters, redactedReport)`
- `LegacyImportItem(id, batchId, recordType, sourceRecordId, operation, checksum, outcome, linkedTargetId)`
- `LegacyRecordMap(id, sourceId, recordType, sourcePk, targetPk, workspaceId, sourceFingerprint)`
- `LegacyImportConflict(id, batchId, recordType, sourceRecordId, targetRecordId, fieldName, resolution, resolvedBy)`
- `EntitlementAdjustment(id, subscriptionId, kind, previousValue, newValue, effectiveAt, reason, authorizedBy, idempotencyKey)`

## User-facing import workflow

1. Root grants Level 2 an assigned taxpayer/company.
2. Admin creates user + taxpayer, chooses access credentials and new plan.
3. Enter source and old customer identifier; validate link.
4. Set fiscal memory and generate/import signing key securely, separately from import.
5. Upload new SQL snapshot or connect to read-only database; preflight schema and preview only linked customer data.
6. Admin confirms preview; engine imports counterparties, goods and historical invoices with mapping.
7. Run reconciliation and display conflicts; leave previous successful imports untouched.
8. Set remaining plan days/units via audited manual adjustment.
9. Later re-import runs use mappings/checksums to fetch new/changed source rows; report discrepancies and never duplicate previous invoices.

## Example tests

- Old source contains 120 invoices; first import creates 120. Next snapshot includes 135; second import creates 15, leaves 120 unchanged; third identical import creates 0.
- Two delegates cannot link same old customer to different new taxpayer records without explicitly authorized root transfer.
- Import interrupted after 40 rows can restart safely; no duplicate lines/UID/taxId or accidental outbound sending.
- Import of historical accepted invoice never consumes quantity credit or creates an outbound submission.
- Source record altered after a newer-system manual edit => conflict surfaced rather than overwriting new values.
- Legacy plan with 300 days remaining may be manually granted via verified 300-day opening adjustment; quantity plan opening count similarly audited and may be corrected only with another adjustment.
- Rotated signing key fails connection until matching public key/certificate is registered with authority; invalid key does not leak into logs.

## Open technical verifications

- Verify `customers.Id`, `customers.IdentityId` and ownership references in old SQL when choosing legacy customer identifier for the UI.
- Define whether import reads latest SQL snapshot upload, read-only live connection, or both; determine checkpoint strategy if legacy tables lack reliable UpdatedAt timestamps.
- Determine how old invoice status history changes should be reconciled with API inquiries.
- Determine secure key custody method and official memory key-registration procedure for this deployment.
