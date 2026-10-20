# ArginTaxPayer — Phase 09: Cryptographic Keys and Digital Signing

**Status:** Step 01 approved. The owner's instruction to complete Step 03 authorizes continuation from the Step 02 design. Step 03 now includes persistence entities, EF configuration, a registered migration and frozen target model/snapshot, and 11 model/script tests. Implementation is ready for executable verification and owner review; it is not owner-approved.
**Documentation language:** English. This edition replaces the Persian master document without changing the agreed scope or step order.
**Last reviewed:** 2026-10-20.
**Repository:** HekmatBeigverdi/ArginTaxPayer.
**Base branch/commit:** main / `b8f307ed06041d902aed9d8432d3b082e1cd714a`.
**Previous release:** `phase-08-v1.0.0`.
**Phase branch:** `phase/09-cryptographic-keys-signing`.
**Fixed step count:** 18. Changing the count, order, or agreed scope requires explicit owner approval.
**Current source baseline:** `08b25892e89d79ae6524777929db723e2a634225`.
**Goal:** Secure generation/import, public-key handling, protected custody, and offline digital signing for the key-based path without an independent signing certificate.
**Execution:** Deliver one step at a time, update this document, provide macOS checks, and wait for explicit approval before continuing. No database migration may be applied without separate owner approval.

## 1. Scope and owner decisions

The initial 24-step proposal was reduced to 18 steps at the owner's explicit request to prioritize the certificate-free key-based path.

In scope:
- Generate a private/public key pair inside the application, with explicit informed server-custody consent.
- Import an existing private key whose corresponding public key may already be registered in the taxpayer portal.
- Derive and compare public keys, provide fingerprints, protect private material, and perform source-pinned offline signatures.
- Customer/taxpayer/memory isolation, audit, concurrency, local rotation, backup/recovery design, and controlled change review.
- Preserve compatibility with Phases 06–08.

Deferred until a separate owner decision:
- CSR generation, certificate import, certificate-chain/trust/revocation handling, and the certificate-based SDK adapter.
- Private-key export, local desktop agents, generic hardware-token drivers, and HSM integration.
- Live nonce/authentication/connection checks and invoice dispatch: Phase 10.
- Annual subscription enforcement and binding exceptions: Phase 12.
- Ticketing until the end of the overall project.
- Phase 08 bulk-import operational QA and Ubuntu script reconciliation.

Certificate-free does not mean unsigned. Do not apply the newer certificate/JWS rules to the legacy path by assumption. If authoritative evidence rules out the legacy path, stop and request an owner decision; do not silently switch to certificates.

No private key, password, DEK, KEK, authentication token, or raw secret may be committed, logged, returned through public API DTOs, or persisted in browser storage. Administrative access to taxpayer metadata does not imply permission to use or export that taxpayer's private key.

## 2. Repository and evidence baseline

The inspected main branch matched the Phase 08 base commit. The Phase 08 GitHub Release exists; some historical Phase 08 document text still says publication pending. Preserve that discrepancy as documentation debt rather than claiming the release is absent.

Existing components inspected:
- .NET SDK 9.0.202 with latestPatch; Domain/Application/Infrastructure/Api; EF Core/PostgreSQL.
- Next.js 15.5.27 and React 19.1.7 with the existing lockfile.
- `CertificateCustodyPolicy` and `IPrivateKeyCustody`: metadata/custody contracts only.
- `FiscalCertificateMetadata`: existing certificate fingerprint, opaque custody reference, restrictive memory FK, and one-current-certificate index. Its fingerprint must not be reinterpreted as a public-key fingerprint.
- `FiscalMemoryManagementService`: pending metadata onboarding, not proof of official connection or authority activation.
- `FiscalConnectionPolicyContracts`: Level2 may request an annual exception; approval of that exception belongs to Level1.
- `ISecretProvider` / `EnvironmentSecretProvider`: bootstrap abstractions, not a production vault.
- `IdentityMfaCrypto`: a separate security purpose; its key must not be reused for taxpayer-key custody.
- The Phase 08 verifier is branch-specific. Phase 09 has its own verifier.
- Existing PostgreSQL opt-in tests sometimes return early when disabled; a passed xUnit count alone is not proof of database coverage.

### Verified original evidence

Seven original files were materialized and hashed in Step 01. All matched the existing reference catalogue. Raw SDK ZIPs, binaries, and sample PEM material were not committed or executed.

| Evidence ID | Edition/version | Verified SHA-256 |
|---|---|---|
| TAX-CONNECT-16 | RC_TICS.IS_v1.6, Shahrivar 1404 | a4995c5a1f8766338a3816d28fd87bd3488dce6f91514557d304fe17d7eb595e |
| TAX-SDK-GUIDE-OLD | Farvardin 1404 | ef3439dcb1a8aa7b5dc1ca46dadf8ee333da01a38fe7910d483342135f1c134a |
| TAX-SDK-GUIDE-NEW | Farvardin 1405 | 5c4145ae701832ea5017ff68cc1a8a9ef0cffe2761a3b5ca94ceceef1823accf |
| SDK-SOURCE-OLD | 0.0.30 | 91f7b10914778b0ce13406bf09adce037c669ffd954b6db243b3f0f08528387a |
| SDK-NUGET-OLD | 0.0.34 | 8c08bc486ac7081cc1063058860c2accb3cf96ad7a50252ca1e72e230b868690 |
| SDK-SOURCE-NEW | 2.0.32 | 66f2936c67c61e1c9c40660e7f9e0bd7666576b8b741fc2d5232a7a1f0d4d2f0 |
| SDK-NUGET-NEW | 2.0.32 | abc9a5ea63b7d0ac7d4fe4548d3d92f981f8ab586b28fe1f4ba500bfc8d1997d |

Normative implementation requires the applicable official document ID/edition/page, SDK source path/hash, exact algorithm/padding/encoding, and a corresponding test. A missing or conflicting source blocks that capability.

### Selected offline profile

Internal profile ID: `legacy-key-source-0.0.30-rsa-sha256-pkcs1-base64`.

This is a versioned offline behavioral reference, not an official protocol name or a live certification claim. Source 0.0.30 and binary 0.0.34 are not proven equivalent. No SDK dependency was adopted in Step 01.

| Source | Finding | Owning implementation step |
|---|---|---|
| Legacy guide pp4–9, especially pp8–9 | SELF_TSP key-based configuration; existing private key corresponding to the registered public key; PKCS#8 PEM input without a certificate parameter | 07–09 |
| SOURCE-OLD, Business/Signatory/Pkcs8Signatory.cs, lines45–46 | UTF-8 input; RSA SHA-256; PKCS#1 v1.5 signature padding; ordinary Base64 output, not Base64URL/JWS | 09 |
| SOURCE-OLD, Extensions/ServiceCollectionExtensions.cs, lines111–120 | Empty apiVersion selects ObjectNormalizer/ObjectTransferApi; v1 selects SimpleNormalizer/SimpleTransferApi | 09, then Phase 10 wire selection |
| SOURCE-OLD, ObjectNormalizer | Flatten dotted paths and E-indexed arrays; sort; # separator/## escaping; explicit empty/null handling; header treatment | 09 |
| SOURCE-OLD, SimpleNormalizer | JSON followed by selected header values in source-defined order; not interchangeable with ObjectNormalizer | 09 |
| TAX-CONNECT-16 p5 | 2048-bit public-key/certificate registration statement; current live legacy support still requires later verification | 06/09 and Phase 10 |
| TAX-CONNECT-16 pp9–12 and newer SDK guide pp4–7 | Certificate-backed nonce/JWS route with x5c; separate, deferred implementation | Reference only |

Source hashes retained:
- Pkcs8Signatory.cs: `3e050951a98e38fd60c15d10efb9b8665a8a3e297bc3733e47401a482289a107`.
- InMemorySignatory.cs: `2d42acc7f0e5415d93c9e9046b3b49815a12a48bc6c72b78c6ca5b4758838bc4`.
- ServiceCollectionExtensions.cs: `ea6cf6150d82387a291986577f7eeda70a3424d09aeeb876fbd8aa5f9d8e6f85`.
- ObjectNormalizer.cs: `8a0adcab1e6b8fb766d35582f1497d510c7e0b88e338abc91b4b298bd281ecdd`.
- SimpleNormalizer.cs: `9ab2ae3c6db5269a591f8eeaaebb1280a85ab94a4659ce43e8b30ccf1ff7562a`.

Important findings:
1. The legacy sample transport accepts every TLS certificate (ServiceCollectionExtensions.cs line130). Never adopt that transport unchanged.
2. DefaultEncryptor uses RSACng and catches errors by returning an "error" string. Do not copy this platform-dependent/error-masking behavior into custody.
3. ObjectNormalizer sorting and ToString behavior require culture/number/empty-object fixtures. Replacing comparison behavior without testing may change signed bytes.
4. Process-global SDK singleton state must not mix taxpayers.
5. The old package nuspec points to commit `d986076958bbfe27f122d0745712d8666046669a`; source/binary parity is unproven.
6. The newer guide cites 2.0.31 while supplied source/package are 2.0.32. Preserve both generations.
7. PKCS#8 private-key input does not establish the portal's public-key encoding. Confirm SPKI/PKCS#1 and PEM/Base64 acceptance before enabling a portal-approved download in Step 06.
8. A local mathematical match does not prove portal registration or live acceptance.

## 3. Approved design direction and contracts

### Custody architecture

The Step 02 design selects server custody with informed owner consent, .NET primitives, and a replaceable external vault adapter. The proposed concrete provider is Vault Transit for DEK wrapping/unwrapping and KV v2 for storing encrypted envelopes.

This is a design choice, not evidence of a deployed Vault, a purchased service, or permission to transfer real secrets. Pin and review the provider/server/client versions in Step 05 before adoption.

Transit does not store the application's ciphertext; KV stores the envelope. The business database stores only metadata and an opaque reference. The KEK is managed outside the business database and is never reused from MFA. There is no plaintext/file/environment-master-key fallback.

Developer fake custody is ephemeral and synthetic-only. Real generate/import/sign operations fail closed until a protected provider is configured and tested.

| Component | Responsibility | Excluded responsibility |
|---|---|---|
| Domain/Signing | Lifecycle and pure policies | Secret bytes, HTTP, SDK DTOs |
| Application/Signing | Server scope resolution, consent, workflow, revisions | KEK access or private-key responses |
| ProtectedKeyCustody | Transient generation/import, envelope protection, disposal | Plaintext temp files or body logging |
| Infrastructure/Signing | Source-pinned offline signature behavior | Arbitrary browser signing or dispatch |
| Persistence/Signing | Operations, public metadata, review and audit | Private key/password/DEK/KEK storage |
| Vault adapter | Restricted envelope storage, CAS, wrap/unwrap | Caller-supplied URLs or vault paths |
| Api/BFF | Authentication, Origin/CSRF, bounded upload, public DTOs | Private export or generic Sign(bytes) API |
| UI | Consent, public artifacts, fingerprints and history | Private preview or durable secret browser storage |

A module boundary is not hardware or process isolation. A compromised custody process or privileged host operator remains a residual risk. Deployment separation must be demonstrated before claiming physical isolation.

### Protection format

Application security policy, not a claimed tax-authority requirement:
- One fresh CSPRNG 32-byte DEK for each encryption; AES-256-GCM; 12-byte nonce; 16-byte authentication tag.
- Canonical plaintext: DER PKCS#8 inside custody only.
- AAD: ASCII `argin:key:v1`, then CustomerUserId, TaxpayerId, FiscalMemoryId, KeyVersionId, each canonical lowercase UUID D format, separated by `|`.
- Envelope: FormatVersion=1, AlgorithmId, Nonce, Ciphertext, Tag, WrappedDek, ProtectionKeyVersion, KeyVersionId.
- Never reuse an old DEK for a second encryption. Rewrap changes WrappedDek without re-encrypting the private key.
- Public fingerprint: lowercase SHA-256 hex over DER SubjectPublicKeyInfo. This does not decide the portal-download encoding.
- Clear controllable buffers and dispose key handles in finally. Do not promise complete managed-memory erasure.
- Export of private keys stays disabled throughout this phase.
- Public API DTOs also exclude ciphertext, wrapped DEKs and internal vault locators.

### Infrastructure permissions

Api/BFF has no direct Vault capability by design. If initially hosted in a shared process, document the resulting shared-credential risk.

Use a restricted workload identity with controlled renewal, not a root/admin token in application configuration. Runtime may access only its designated envelope prefix and specific protection keys; no policy administration, KEK export, or irreversible history destruction. Resolve opaque paths internally. Validate TLS; never reuse the sample SDK's unconditional TLS callback.

Vault ACLs do not replace customer authorization. Commercial transfer to a different Level2 does not change the CustomerUserId that owns a taxpayer's keys.

### Authorization matrix

Customer means the software-account owner of the taxpayer. An administrator acting on their own legitimately owned account uses that ownership path; their role does not grant custody rights over someone else's account.

| Action | Owning Customer | Current scoped Level2 | Scoped Level1 |
|---|---|---|---|
| Metadata, public artifact, redacted business history | Allowed | Scoped read | Existing scoped read |
| Generate/import owner's key | Explicit consent + fresh reauthentication | Denied by default | Denied by default |
| Request rotation/change | Own scope | Reasoned scoped request | Reasoned scoped request |
| Approve local change | No managerial approval | KeyChangeReview + MFA, excluding annual reset | KeyChangeReview + MFA |
| Request local signature of own snapshot | Allowed through controlled use case | Denied by default | Denied by default |
| Read/export private key/password | Denied | Denied | Denied |
| Read Vault/unwrap DEK | Denied | Denied | Denied |
| Activate authority memory/reset annual binding in Phase 09 | Denied | Denied | Denied |

Only the internal custody workload uses private material transiently after business authorization. Managerial approval does not replace owner consent or allow a manager to import/generate the owner's key.

Requester and approver must differ. Annual taxpayer/memory exceptions still require Level1 and future Phase 12 enforcement; a local Level2 key review is not annual-reset approval.

Consent records contain the consent-text version, actor, scope, operation and UTC timestamp. Consent explains server custody and the absence of private export. No preselected consent checkbox.

Sensitive owner commands require reauthentication no more than five minutes old, bound to actor/session/purpose, using existing verified authentication and required account MFA. This is product policy. Background online signing needs its own future Phase 10 authorization contract.

### Contract definitions for later implementation

These are design contracts, not new executable interfaces in Step 02:
- SigningScope: CustomerUserId, TaxpayerId, FiscalMemoryId, resolved by the server from authenticated identity and current DB relations.
- GenerateKeyCommand: taxpayer/memory IDs, ProfileId, OperationId, ConsentVersion, ReauthReference, ExpectedRevision.
- ImportKeyCommand: equivalent non-secret metadata plus a separate disposable SensitiveKeyInput.
- SensitiveKeyInput: bounded byte buffer, not a serializable record; no public byte getter; redacted ToString; clearing Dispose.
- IKeyCustodyService.GenerateAsync / ImportAsync return KeyOperationResult with operation status, optional KeyVersionId and public metadata only.
- PublicKeyView: key ID, public fingerprint, profile ID, lifecycle, portal-verification state, revision and artifact availability.
- IKeyProtectionProvider: internal wrap/unwrap boundary only; no API/Application access to raw DEKs.
- IKeyOperationRepository: reserve operation and atomically commit business metadata with audit.
- ISigningAuditSink: fixed-schema event, not arbitrary JSON/body/exception serialization.
- IDigitalSigningService.SignAsync: internal AuthorizedSigningRequest with trusted snapshot ID/hash, scope, key/profile versions, expected revision and authorization context.
- SigningResult: signature Base64, snapshot hash, key version and profile; not a downloadable tax-authentication token.

Keep the existing IPrivateKeyCustody contract behind a scope-validating adapter; do not expose it directly as an endpoint. There is no GetPrivateKey method.

### Lifecycle and external state

| From | Action | To | Gate |
|---|---|---|---|
| No key | Generate/import | Staged | Owner, consent, reauth, operation reservation |
| Staged | Validate, seal, commit with audit | LocallyReady | Durable envelope and valid metadata |
| Staged | Failure/uncertain provider outcome | Failed/RecoveryRequired operation | Never sign |
| LocallyReady | Select for offline signing | SelectedLocal view | Revision and unique selection |
| SelectedLocal | Suspend | Suspended | Authorized action or safety stop |
| LocallyReady/SelectedLocal/Suspended | Report compromise | Compromised | Immediate local stop |
| SelectedLocal | Approved local replacement | Superseded | Atomic selection replacement |
| Suspended | Approved resume | LocallyReady | Current revision and required approval |
| Compromised/Superseded | Sign/reactivate | Denied | Terminal for new signing |

SelectedLocal is represented by the selection relation, not a second mutable flag on the key. Portal state is separately Unverified or AwaitingExternalVerification in this phase. MatchesProvidedPublicKey is only a local comparison result.

### Transactions and concurrency

Reserve a scope-bound OperationId with a non-secret metadata digest. Never hash a private key/password into the operation table. Retry must reconcile an existing staged provider object instead of generating a second key after a timeout.

Vault and PostgreSQL have no distributed transaction. Persist staging/operation identifiers, reconcile uncertain results, and prohibit signing orphan envelopes.

Recheck identity/session/ownership/revision before commit and at signing admission. A signature admitted before suspension may finish; suspension before admission rejects it. Do not promise revocation of already-produced historical signatures.

Record a durable signing intent before accessing the key and a result audit before releasing the result. Missing audit/provider/DB is a failure, not success.

Change approvals are actor-distinct, scope/revision-bound, one-time, and expire after 24 hours under this product policy. Consume approval atomically with the authorized command. Memory activation and annual binding remain unchanged.

### Recovery and residual risks

Back up business metadata/audit, encrypted KV envelopes/operation metadata, and recoverable protection-key state under separate access control. A DB-only backup cannot restore signing keys.

KEK rotation rewraps the DEK; signing-key rotation creates a new key version/fingerprint. Do not discard old protection-key versions until backup retention and restore coverage permit it. RPO/RTO, cadence and actual restore evidence belong to Step 12, not an untested readiness claim.

Review focus: IDOR; former-Level2 access; administrator signing; tenant/AAD swap; secret logging; malicious PEM; provider outage; retry/double generation; rotate/sign races; old/new SDK mixing; false portal-verification claims.

Provider references inspected in Step 02:
- https://developer.hashicorp.com/vault/docs/secrets/transit
- https://developer.hashicorp.com/vault/docs/secrets/kv/kv-v2
- https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm?view=net-9.0

## 4. Safety and verification contract

- Database writes/tests may target only argintaxpayer_test, with parsed configuration and a read-only actual-database guard.
- Generating a migration is not approval to apply it.
- Do not start live services with inherited developer DB settings without checking their destination.
- Do not print credentials or place tokens/passwords in shell history.
- Same-commit macOS/Ubuntu verification is required for final acceptance; environment differences stay outside source.
- Existing certificate data, invoices, fiscal-memory bindings and historical identities remain unchanged.
- A successful build is not proof of HTTP authorization, Vault recovery, official connection, or tax acceptance.

## 5. Fixed implementation and acceptance matrix

| Step | Deliverable | Acceptance | Status |
|---|---|---|---|
| 01 | Baseline and legacy-source audit | Seven source hashes; exact offline profile; differences/security findings recorded | Owner-approved 2026-10-20 |
| 02 | Architecture, custody and authorization contracts | Consent, protected provider design, lifecycle and default-deny access | Design accepted for continuation; runtime checks not rerun |
| 03 | Data model and additive migration proposal | Scope FKs, uniqueness/CAS, reviewed SQL and recovery plan; no DB execution | Source delivered; executable verification and owner review pending |
| 04 | Operation persistence, audit and idempotency | Approved test migration only; atomic audit and recoverable provider/DB outcomes | Not started |
| 05 | Envelope protection and vault provider | Tamper/AAD/missing-KEK denial; no private bytes in business DB | Not started |
| 06 | Key generation and public artifact | Official profile; no certificate/CSR/private response; portal encoding verified | Not started |
| 07 | Existing private-key import | Bounded valid formats; malformed/weak input denied; no plaintext temp/log | Not started |
| 08 | Private/public match and fingerprint | Canonical public match and challenge; registration not inferred | Not started |
| 09 | Offline legacy signing and SDK compatibility | Exact encoding/normalization fixtures; no live authority request | Not started |
| 10 | Application signing and immutable snapshot | Trusted snapshot/hash/key version; no Sent/dispatch mutation | Not started |
| 11 | Key lifecycle and local rotation | Compromised/revoked denial; atomic selection and race behavior | Not started |
| 12 | Backup, recovery and KEK rotation | Synthetic restore, resumable rewrap, failure without KEK, runbook | Not started |
| 13 | Controlled change review | Reason, scope, revision, expiry and one-time approval; no annual bypass | Not started |
| 14 | Secure API/BFF | HTTP authorization, CSRF, limits, no-store; no private-read/export/sign oracle | Not started |
| 15 | Persian RTL key-management UI | Owner generate/import, public download/status/history, compact accessible UX | Not started |
| 16 | Security/authorization/concurrency/regression | Real isolated HTTP/PG cases, transfer/session races, leak capture, Phases06–08 | Not started |
| 17 | macOS and Ubuntu acceptance | Identical SHA, clean trees, independent build/test/browser/restore evidence | Not started |
| 18 | Closeout and conditional publication | All signoffs; separate permission for merge/tag/release | Not started |

## 6. Implementation ownership and verification

Planned paths follow existing src/ArginTaxPayer.* projects:
- Domain/Signing: policies/lifecycle.
- Application/Signing: scope/contracts/use cases.
- Infrastructure/Persistence/Signing: the Step 03 records and mappings.
- Infrastructure/Security/KeyProtection: envelope/provider.
- Infrastructure/Signing: generation/import/matching/legacy signing.
- Api/Signing and apps/web/app/api/signing: controlled endpoints/BFF.
- apps/web/app/(console)/signing and app/signing: UI.
- tests/ArginTaxPayer.Tests/Phase09*: behavioral/model/HTTP/PG tests.
- tests/phase09_verify_test.py: verifier safety tests already delivered in Step 01.

Every delivery records source evidence, changed files/commit, executed tests/counts, unexecuted checks, migration status and owner decision here. No separate per-step report document is created.

Existing verifier:
- preflight: base ancestry, clean tree, exactly 18 ordered plan rows; no DB or .NET requirement.
- pure: restore/build/.NET and npm ci/test/typecheck/build with inherited DB settings removed and PG integration disabled.
- focused --filter: no DB; rejects an empty test selection.
- integration/full: parse the restricted test connection, verify actual current_database() using psql before build/test, then enable PG integration.
- All modes avoid migrations and service startup. TRX counts distinguish executed/not-executed tests. Disabled legacy early-return cases still do not prove PG acceptance.

Step 17 Ubuntu entry sequence from macOS:

```ini
AUTOSSH_GATETIME=0 autossh -M 0 argin-vps
```

Then on Ubuntu:

```bash
tmux new -A -s argin
cd /home/argindev/projects/ArginTaxPayer
git status --short
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
git rev-parse HEAD
# Only after reviewing isolated test DB configuration:
bash scripts/phase09-verify.sh full
bash scripts/ubuntu-dev.sh verify
bash scripts/ubuntu-dev.sh status
git status --short
```

Never reset/stash/merge unknown Ubuntu changes automatically. Use an isolated clean checkout if required; the old script reconciliation debt remains separate.

## 7. Step 01 delivery and approval record

Delivered at commit `28b978664469c8f417891e41a4148933df0a3dd3`:
- Seven original evidence hashes matched.
- Legacy signing profile and sample-SDK security concerns documented.
- scripts/phase09-verify.sh, scripts/phase09_verify.py, tests/phase09_verify_test.py.
- Python safety tests: 8/8 passed; Bash syntax and clean-tree preflight passed.
- Web regression: 187/187 passed after npm ci. The earlier run lacked TypeScript and failed mfa-proxy.test.mjs; dependency installation resolved that environment failure without changing product source.
- .NET build/tests and actual PostgreSQL integration were not executed in the available environment.
- macOS/Ubuntu/browser execution was not independently demonstrated by those local checks.

Owner explicitly approved Step 01 on 2026-10-20 and requested Step 02. No raw new macOS output accompanied that approval; preserve the distinction between owner signoff and independently observed test evidence.

## 8. Step 02 delivery and approval record

Design delivered at `8112f38a485dd5620fe4c5a8faf7f405c99c691f`; only the master document changed.

A requested retest on 2026-10-20 at 21:24 Tehran failed to start because no usable execution environment was available. No Python, Bash, preflight or .NET execution occurred in that attempt. The GitHub connector returned no associated workflow runs for the inspected design commit; that is not a passing CI result.

Six document-structure checks succeeded: 18-step order, Step 02 presence, legacy profile retained, private-export prohibition, separate external state, and Step 03 not yet started. These were document checks, not runtime tests.

The owner's subsequent instruction to correct documentation language and complete Step 03 is recorded as acceptance of the design for continuation. It does not convert the blocked runtime checks to PASS. The 18-step sequence is unchanged.

## 9. Step 03 — Data model and migration proposal

**Status:** Implementation files delivered; build/model/SQL execution is still pending because the execution environment is unavailable. Migration registered but NOT APPLIED. No live database inspected.
**Delivery correction:** The earlier documentation-only delivery did not complete Step 03. The owner explicitly requested the real implementation. Step 03 now owns the entities, EF mapping, migration, frozen target model/snapshot and persistence-model tests; Step 04 remains operation repositories and transaction services. Files were manually authored/aligned against existing repository conventions, not scaffolded or compiled by EF tooling in this unavailable environment.
**Inspected existing source:** ArginTaxPayerDbContext.cs, FiscalMemory.cs, TaxpayerProfile.cs, FiscalCertificateMetadata.cs, and 20261018090000_ScopeFiscalParticipantsPerCustomer.cs at the current source baseline.

### 9.1 Compatibility decisions

1. Add independent signing tables. Do not add a certificate requirement or put a bare public-key fingerprint into fiscal_certificate_metadata.
2. Reference existing identity_users(id), taxpayer_profiles(id, customer_user_id), and fiscal_memories(id, taxpayer_id).
3. Add two parent unique indexes to support composite FKs. These do not rewrite existing rows, but acquiring index locks requires review and a maintenance plan.
4. Legacy taxpayer rows with null CustomerUserId cannot acquire new key records until explicit ownership reconciliation. Do not infer ownership from tax IDs/mobile numbers.
5. New records enforce the customer/taxpayer/memory tuple at database level; service query scoping and authorization remain mandatory.
6. Public fingerprints are not globally unique. The same public key or taxpayer identifier in another customer must not create an existence leak or cross-customer deduplication.
7. Existing global ux_fiscal_memories_identifier is unchanged. Any conflict with customer-level duplicate-memory requirements needs a separate reviewed decision; this proposal does not silently drop that existing constraint.
8. Commercial Level2 reassignment does not rewrite key ownership or historical actor/owner evidence. Changing a taxpayer's CustomerUserId while referenced keys exist is restricted by composite FK; it requires an explicit future reconciliation workflow.
9. SelectedLocal is a selection relation. Do not store a second independently writable is_selected flag.
10. No private material, encrypted envelope bytes, passwords, wrapped DEKs, KEKs, auth JWTs or raw signed payloads are stored in these tables.

### 9.2 Records and ownership

| Record | Purpose | Key invariants |
|---|---|---|
| SigningOperation | Reserve/reconcile a scope-bound generate/import/change/sign operation | Unique scoped OperationId; metadata-only digest; revision; no secret hash |
| SigningConsent | Immutable evidence of owner consent for a specific operation | Actor equals owning CustomerUserId; text version and UTC; not managerial consent |
| SigningKeyVersion | Public metadata and opaque custody locator for one key | Scope FKs; one originating operation; immutable public identity once sealed |
| SigningKeySelection | Current key selected for local signing | One retained row per fiscal memory; nullable key on unselection; same-scope FK; LocallyReady selected key only |
| SigningChangeRequest | Review state, reason, distinct approver, expiry, one-time consumption | Revision-bound; no subscription mutation; annual exception not applied here |
| SigningAuditEvent | Append-only fixed-schema sensitive-operation history | Scoped actor/event/operation/time; no arbitrary JSON or request body |

All identifiers are UUIDs generated in the application; all instants are timestamptz/UTC; revisions are positive bigint concurrency tokens. No uuid extension is required.

KeyVersion states: Staged=1, LocallyReady=2, Suspended=3, Compromised=4, Superseded=5.
Portal states: Unverified=1, AwaitingExternalVerification=2. There is deliberately no AuthorityVerified value in this phase.
Operation kinds: Generate=1, Import=2, LocalChange=3, LocalSign=4.
Operation states: Reserved=1, VaultStaged=2, Committed=3, Failed=4, RecoveryRequired=5.
Review states: Pending=1, Approved=2, Rejected=3, Consumed=4, Expired=5.
Review kinds: LocalKeyRotation=1, LocalKeyResume=2, MemoryReviewOnly=3. The last kind records a request/review, not permission to change an active memory.
PublicMatch: NotProvided=1, MatchesProvided=2, Mismatch=3. Mismatch cannot produce a LocallyReady key.
Origin: Generated=1, Imported=2.

Reauthentication references are not persisted as reusable tokens. Record an evidence event ID or timestamp, never the reauth bearer credential. Vault references are opaque, internal, immutable after sealing, and excluded from public projections.

### 9.3 Registered additive migration — NOT APPLIED

Migration ID: `20261020181500_AddPhase09SigningMetadata`.

Authoritative source:
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261020181500_AddPhase09SigningMetadata.cs`.
- Its `.Designer.cs` contains a frozen full target model.
- `ArginTaxPayerDbContextModelSnapshot.cs` includes the same six metadata entities.

The earlier SQL draft has been replaced by the actual migration source to avoid two divergent copies. This migration is manually authored, matching existing raw-SQL migration conventions; do not describe it as generated by a successful scaffold command.

Up creates six signing tables, two DB-only parent-scope unique indexes, mapped keys/indexes/checks, scoped FKs and database guard functions/triggers. It uses EF's migration transaction with bounded local lock/statement timeouts and checks `current_database() = 'argintaxpayer_test'`. It never applies itself on host startup.

Legacy `TaxpayerProfile.CustomerUserId` stays nullable. Its composite parent index/FK is an additional SQL-level scope guard, not an EF alternate key: promoting the nullable parent property into an EF key would incorrectly make legacy ownership required. The EF model maps restrictive direct parent FKs for operations, plus composite same-scope FKs among new entities. Snapshot and migration target preserve existing parent model definitions.

Selection records are retained rather than deleted when no key is selected: set KeyVersionId to null and increment Revision with an audit event. This avoids reusing a revision after delete/reinsert. Selections with non-null keys must reference LocallyReady at commit.

Audit gains fixed SubjectKind and SubjectId fields and a unique subject/revision index. Subject kinds are Operation=1, KeyVersion=2, Selection=3, Review=4, Consent=5. The deferred guard requires matching scope, subject and revision; no arbitrary JSON is added.

The nullable consumed-operation reference uses a filtered unique index rather than an EF alternate key, so pending requests remain nullable.

No connection, database write, migration script execution, or provider operation was performed while creating these files.

### 9.4 Implemented migration guards and service boundaries

The migration now contains append-only/retention, mutation/revision, selection locking/readiness, consent and deferred audit guards. They require PostgreSQL execution tests before approval to apply. The following remain the full acceptance contract, including authorization and recovery responsibilities implemented by later services:
- Append-only consent/audit: reject UPDATE, DELETE and TRUNCATE. Runtime role is not table owner and cannot disable triggers or alter/drop these tables.
- Key identity immutability: scope, origin, originating operation, version number, profile and created time never change. Fingerprint/SPKI/custody locator can be populated while Staged and previously null, then are immutable. Protection-key version may change only through audited CAS rewrap; public identity remains unchanged.
- Revision guard: mutable updates advance revision by exactly one and never move updated_at_utc backwards. Application updates still use WHERE revision = ExpectedRevision; an increment trigger alone does not prevent lost updates.
- Lifecycle guard: allow Staged->LocallyReady, LocallyReady->Suspended/Compromised/Superseded, Suspended->LocallyReady/Compromised/Superseded. Compromised and Superseded cannot return to signing. Failed staging stays an operation failure; it is not silently promoted.
- Selection consistency: deferred constraint triggers on both key lifecycle changes and selection insert/update/delete validate at commit that every remaining selection references a LocallyReady key. Lock the key row during selection/sign admission; use serializable coordination or equivalent row locks and retry to prevent write skew. Unselect a suspended/compromised key within the same transaction.
- Review state guard: Pending->Approved/Rejected; Approved->Consumed/Expired. Terminal states cannot reverse. Actor, scope, current key, expected revision, kind, reason and requested time are immutable after submission. No editing approval fields after decision; consumption fields fill exactly once.
- Operation guard: immutable request scope, caller operation ID, actor, kind, profile and metadata digest; legal state transitions documented by the repository implementation; do not overwrite an old idempotency claim with a different request.
- Same-operation consent: sealing a generated/imported key requires exactly one matching owner-consent record for its originating operation, with the actual authenticated owner and reauth evidence checked by Application. FK existence alone is not proof of consent validity.
- Successful operation completion and key/selection/review updates require corresponding business audit in the same transaction. Denied unauthenticated/unresolved-scope attempts go to the separate redacted security sink, not a fabricated scoped business operation.
- Public-key fingerprint/SPKI consistency is computed/verified in custody/application; SQL does not pretend to parse RSA or prove key possession.
- Expired approvals are rejected by server UTC even before a background worker marks them Expired; CHECK constraints do not use clock-dependent NOW() as an expiry mechanism.

A selection is unselected by setting KeyVersionId to null in an audited revision; its row cannot be deleted/truncated. Audit and consent cannot be updated/deleted/truncated. Other signing rows retain immutable identity fields and positive revision progression. Actual role/grant restrictions and authenticated authorization remain later service/deployment gates; table-owner/superuser access is not neutralized by triggers.

### 9.5 EF mapping and concurrency contract

Step 03 now provides six entity classes and SigningModelConfiguration, wires six DbSets into the existing context, updates the frozen snapshot and migration target, and adds model/script tests. The following requirements include later repository behavior:
- DbSets and explicit configuration for all six records; DB-only parent composite indexes preserve nullable legacy ownership. Do not add an EF alternate key containing nullable TaxpayerProfile.CustomerUserId.
- Composite scoped FKs with Restrict; positive long Revision as IsConcurrencyToken; timestamptz fields accepting only UTC; numeric enum persistence matching this document.
- PublicSpkiDer bytea is PUBLIC key data, not encrypted/private material. Do not expose internal entities directly as API DTOs.
- Explicit model snapshot alignment and an EF no-pending-model-changes check. No hand-waved successful scaffold is recorded in this step.
- One atomic selection update per memory. First selection uses INSERT with unique-conflict handling; replacement uses expected selection revision and current key ID in the predicate.
- Allocate version_number under a fiscal-memory row lock or equivalent serialized allocator; MAX+1 outside a lock is prohibited.
- Key, audit and review changes use the same DB unit of work. External Vault work is coordinated through SigningOperation rather than assumed to share that transaction.
- Redacted, bounded reason/error/action codes. Reject secret-looking content in free-text explanations and never log raw rejected input. A schema length check alone does not sanitize secrets.
- Do not add a general JSON metadata field that can become a secret dump.

### 9.6 Preflight, migration approval and recovery

Before requesting permission to apply an executable migration:
1. Verify HEAD and clean tree; inspect migration history and actual current_database() in a read-only session.
2. Verify existing parent table/column/index names and types; compare actual schema with the inspected source. Stop on drift.
3. Report orphan/legacy-null taxpayer ownership counts without dumping customer rows. Do not backfill ownership automatically.
4. Estimate row counts and lock duration of the two parent indexes. Confirm no conflicting index/constraint names or partially installed signing schema.
5. Review final generated SQL, trigger bodies, model/snapshot diff, runtime grants, and any migration-history changes.
6. Run a synthetic isolated rehearsal only after approval. Never use the developer/production database to satisfy a test.
7. Capture a tested backup/restore plan before touching non-disposable data.

Forward migration must be additive: no UPDATE/DELETE/backfill of taxpayer, memory, certificate or invoice rows; no renaming/reinterpreting existing fingerprints; no automatic key or consent creation.

Rollback policy: abort an uncommitted transaction on failure. After commit, a Down migration may remove the new schema only when every new table is empty, the operator has explicitly approved it, and no dependent objects exist. If any business/audit/key history exists, Down must raise and stop; use a forward repair or an approved restore. No CASCADE DROP, audit deletion, or KEK destruction is a normal rollback.

The proposed migration remains test-only guarded. Production enablement requires a separate future deployment review; do not remove the guard merely to make a deployment succeed.

### 9.7 Acceptance scenarios and evidence status

| Check | Expected result | Current evidence |
|---|---|---|
| English master document and 18-step order | No Persian prose; order 01–18 unchanged | Structural document review |
| Existing certificate/invoice data | No alterations/backfill/private-key data | Proposed DDL inspection only |
| Customer A with taxpayer/memory B | Composite FK rejection | Implemented in migration; PostgreSQL execution pending |
| Two selected keys for one memory | Unique selection rejection | Implemented constraints; concurrency execution pending |
| Stale key/selection revision | One accepted write, deterministic conflict | Model tests added; EF/PG execution pending |
| Cross-scope consumption/consent/audit | Composite FK rejection | Implemented in migration; PG execution pending |
| Repeat OperationId | Same compatible public-only outcome; conflicting request rejected | Designed; repository tests pending |
| Audit/consent mutation | UPDATE/DELETE/TRUNCATE denied | Trigger source delivered; PostgreSQL tests pending |
| Rotate/suspend versus select/sign | No invalid selected key at commit; documented admission order | Deferred triggers and locking source delivered; runtime tests pending |
| Private material in DB/schema | No private/password/DEK/KEK/envelope columns | Structural proposal inspection |
| Legacy null owner | Refuse key setup; no inferred customer | Composite FK/application design |
| Actual EF/SQL/build validation | Migration, snapshot and tests delivered | NOT EXECUTED; execution environment unavailable |

The source implementation is delivered, including migration guards. No successful .NET build, EF scaffold, PostgreSQL execution, model-test run or macOS run is claimed. Generated SQL and runtime/snapshot parity must now be verified with the delivered tests. Step 04 must not begin until the owner reviews Step 03; applying this migration remains separately approval-gated.

### 9.8 Implementation inventory, checks and macOS acceptance

Files delivered:
- Six classes under `Infrastructure/Persistence/Signing/`: SigningOperation, SigningConsent, SigningKeyVersion, SigningKeySelection, SigningChangeRequest, SigningAuditEvent.
- SigningModelConfiguration.cs and six DbSets/context registration.
- Migration `20261020181500_AddPhase09SigningMetadata.cs` and frozen `.Designer.cs`.
- Updated ArginTaxPayerDbContextModelSnapshot.cs.
- `tests/ArginTaxPayer.Tests/Phase09SigningPersistenceModelTests.cs`: 11 tests.

The tests cover entity/table registration, restrictive FKs, no shadow/secret properties, composite scope, nullable legacy ownership and approval consumption, retained selection revisions, no global fingerprint uniqueness, snapshot parity, migration discovery/target model and generated Up/Down script safety. They do not connect to PostgreSQL or apply the migration.

Structural review performed through the available orchestration runtime:
- Exactly six CREATE TABLE statements; all 80 mapped scalar properties appear in entity/configuration/snapshot/DDL.
- Constraint/index/trigger identifiers fit PostgreSQL's 63-character limit.
- Frozen snapshot/target definitions are included; they do not call the mutable live configuration.
- Complete trigger source is present; the earlier incomplete proposal is no longer the migration implementation.
- An issue found during source generation was corrected before delivery: replacement text containing a SQL regex dollar sign must not be interpreted as JavaScript replacement expansion. The final migration has six tables, not duplicated DDL.
- This structural review is not a C# compiler, EF model initialization or a PostgreSQL parser. The 11 tests are ADDED, NOT EXECUTED.

Execution was attempted again and returned no usable execution environment. Build, .NET tests, real migration generation and PostgreSQL guard behavior cannot be marked PASS. No database migration has been applied.

From the repository root on macOS:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
git status --short
git log -1 --oneline
python3 tests/phase09_verify_test.py
bash scripts/phase09-verify.sh preflight
bash scripts/phase09-verify.sh focused --filter FullyQualifiedName~Phase09SigningPersistenceModelTests
```

Expected: eight verifier safety tests and eleven new .NET model/script tests pass; the new migration is discoverable and the runtime model matches the committed snapshot. No database connection is opened by those model/script tests.

Optional full no-DB regression after the focused run:

```bash
bash scripts/phase09-verify.sh pure
```

To create a SQL file for owner review only (the file is not applied):

```bash
mkdir -p /tmp/argin-phase09-review
dotnet ef migrations script 20261020104500_AddInvoiceWorkspaceRevisionAudit 20261020181500_AddPhase09SigningMetadata \
  --project src/ArginTaxPayer.Infrastructure \
  --startup-project src/ArginTaxPayer.Api \
  --output /tmp/argin-phase09-review/phase09-signing-metadata.sql
```

Do not run database update, integration/full, or execute this SQL without separate owner approval. Do not regenerate or remove migrations to bypass a failed snapshot test; report the exact failure for correction.

Acceptance: real files present, build/model/script tests successful on macOS, unchanged legacy null ownership/certificate data, only additive guarded migration, and owner review. PostgreSQL execution/trigger tests remain an explicit subsequent approved test-database gate. Step 03 is not owner-approved and Step 04 has not started.

## 10. Deferred debt and final release boundary

| Item | Status |
|---|---|
| Phase 08 bulk-import operational QA and durable partial-failure improvements | Deferred; separate owner decision |
| Phase 08 Ubuntu local-script reconciliation and historical same-SHA gap | Deferred; Step 17 verifies this phase's identical SHA only |
| Phase 08 unitemized authenticated-browser acceptance | Historical evidence limitation retained |
| Phase 08 document publication text versus actual release | Recorded discrepancy; not silently rewritten |
| CSR/certificate path and private export | Deferred; not a Phase 09 completion gate |
| Ticketing | Deferred until overall project completion |
| Portal public-key encoding and current legacy live support | Required gates before the corresponding public-artifact/live features |

Step 18 requires all agreed signoffs and explicit treatment of remaining limitations. Functional acceptance alone does not authorize publication. Main merge, proposed tag phase-09-v1.0.0, and GitHub Release require separate approval and verified artifact identities.
