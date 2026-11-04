# Phase 18 — Step 17: classified legacy Outbox consumer, credentials and reference inquiry

## Implemented on the phase/18-production-deployment branch

1. Shared legacy HTTP `PostVerifiedEndpointAsync` reuses Phase10 bounded request/receipt handling, but accepts only the two pinned, canonical legacy enqueue endpoints. Existing Sandbox `PostAsync` remains Sandbox-only.
2. Legacy `GET_TOKEN` and `GET_SERVER_INFORMATION` clients support an endpoint obtained from server-side persisted operation/memory routing. Original Sandbox methods retain their existing Sandbox-only checks. Token cache is keyed by endpoint, fiscal client ID and signing public key hash; response secrets remain in memory.
3. `Phase18ScopedLegacyCredentialsService` obtains a selected database-encrypted key from the same fiscal memory and obtains token plus server public-encryption key from the same trusted authority origin.
4. `Phase18DispatchAttemptAdmissionService` atomically claims a reserved operation and appends attempt number 1 in one transaction with subscription reservation. It checks immutable operation/memory environment, active memory and membership, finalized matching invoice revision, classified Outbox schema/payload/digest and absence of **any** prior attempt or receipt. No automatic retry path.
5. `Phase18DispatchOutcomeRepository` atomically records correlated initial acknowledgement receipt or classifies an unconfirmed attempt `Indeterminate`, advancing the operation state/revision. It never equates enqueue acknowledgement with fiscal SUCCESS. The append-only evidence and subscription reservation/consumption boundaries are preserved.
6. `LegacyInvoiceOutboxPacketRebuilder` now accepts an **explicit** classified Production schema for encrypted data re-signing, while defaulting to the original Sandbox schema and refusing cross-schema replay.
7. `Phase18ScopedLegacyOutboxWorker` composes persisted operation scope, classified outbox, key custody, fresh scoped token, packet re-sign, single-attempt claim, scoped HTTP send and durable outcome. It is DI-scoped but **not** registered as a background service, HTTP endpoint or scheduled process. No Production sends have been triggered.
8. `Phase18ScopedLegacyReferenceInquiryService` reads an actual acknowledged reference, selects the exact stored environment and signing key, sends `INQUIRY_BY_REFERENCE_NUMBER` only, and correlates UID, reference and memory. It never retransmits the invoice or infers fiscal success from a `SUCCESS` string.
9. `Phase18LegalSuccessorAuthorityGate` lets a legal child inherit only the persisted predecessor memory/environment under the same owner/taxpayer. It **does not** replace the production legal-relation workflow or certify fiscal acceptance.
10. Regression tests cover pinned origin validation, signed token, encryption server info, cross-schema replay, no-resend single-attempt state transition, scoped worker and legal successor isolation. All are offline tests.

## Mandatory blockers still not implemented or live-verified

- **Production producer/admission**: existing Phase10 invoice snapshot validation, official serial reservation, legal invoice projection, finalize/prepare Outbox and API entrypoints are explicitly Sandbox-only and use separate `sandbox_* ` tables and `argintaxpayer_test`. The new classified worker cannot be safely called until an independently validated Production producer atomically writes the correct classified Outbox and immutable dispatch operation. Do not clone Sandbox SQL or flip an environment string.
- **Fiscal success inquiry**: the new reference probe returns a correlated-but-pending outcome, not authoritative fiscal SUCCESS/FAILED. A separate trusted, persisted, versioned authority-status report is required.
- **Legal successors**: Production cancellation, correction and sales-return creation/submission and chain UI updates require environment-aware official lineages, approved legal rules and server-side statuses.
- **Production contract and custody**: the legacy Production origin documented by the older 0.0.x guide still needs compatibility verification with currently applicable authority instructions and actual authorized Production credentials. The newer 2.0.x certificate-based `requestsmanager` protocol is not compatible by changing an origin string.
- **Real acceptance/deployment**: isolated Sandbox send+reference inquiry; separately authorized Production connectivity and send+reference inquiry; cross-environment negatives; indeterminate case; legal successor outcomes; source/DB migration parity; rollback; HTTPS/nginx+systemd smoke. The currently gated Production host `https://tax.argin.ir` has NOT been promoted by these commits.

## Safety and operator actions

Keep `ArginTaxPayer__EnableOperationalEndpoints=false`, `SandboxInvoiceLegacy__Enabled=false` and `ARGIN_PRODUCTION_UI_ENABLED=false` on the Ubuntu Production service. Never flip these global deployment switches as a substitute for persisted per-memory environment routing.

No new EF migration was introduced in Step17; owner confirmed Step15 migration already applied. The new worker is intentionally not exposed to the API to prevent accidental Production sends.

Acceptance (macOS and Ubuntu):
```bash
cd ~/projects/ArginTaxPayer
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
bash scripts/phase18-step14-one-shot-source-acceptance.sh
```

The script performs complete source tests and builds but NO authority I/O or live rollout.
