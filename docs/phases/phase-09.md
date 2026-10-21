# ArginTaxPayer — Phase 09: Cryptographic Keys and Digital Signing

**Status:** Steps01,03,04,05,06,07,08 and09 owner-approved; Steps10,11,12,13,14 and15 source delivered for build/test, with runtime evidence pending. Step16 has not started. Prior build/live-provider and official portal-format evidence gaps remain explicit.
**Documentation language:** English. This edition replaces the Persian master document without changing the agreed scope or step order.
**Last reviewed:** 2026-10-21.
**Repository:** HekmatBeigverdi/ArginTaxPayer.
**Base branch/commit:** main / `b8f307ed06041d902aed9d8432d3b082e1cd714a`.
**Previous release:** `phase-08-v1.0.0`.
**Phase branch:** `phase/09-cryptographic-keys-signing`.
**Fixed step count:** 18. Changing the count, order, or agreed scope requires explicit owner approval.
**Current source baseline:** `7f746d6904f977bff044c889a01bd0f9394c757a`.
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
| 03 | Data model and additive migration proposal | Model/migration delivered; operator applied migration on isolated test DB | Owner-approved 2026-10-20 |
| 04 | Operation persistence, audit and idempotency | Atomic journal/consent/audit, replay, CAS and recovery states | Owner-approved 2026-10-21 |
| 05 | Envelope protection and vault provider | Tamper/AAD/missing-KEK denial; no private bytes in business DB | Owner-approved2026-10-21; prior provider execution gates retained |
| 06 | Key generation and public artifact | Fixed local profile; public-only artifact; official portal encoding remains blocked | Owner-approved2026-10-21; prior evidence limits retained |
| 07 | Existing private-key import | Bounded valid formats; malformed/weak input denied; no plaintext temp/log | Owner-approved2026-10-21; execution evidence pending |
| 08 | Private/public match and fingerprint | Canonical public match and challenge; registration not inferred | Owner-approved 2026-10-21; runtime evidence pending |
| 09 | Offline legacy signing and SDK compatibility | Exact encoding/normalization fixtures; no live authority request | Source delivered 2026-10-21; build/runtime pending |
| 10 | Application signing and immutable snapshot | Trusted snapshot/hash/key version; no Sent/dispatch mutation | Source delivered 2026-10-21; build/runtime pending |
| 11 | Key lifecycle and local rotation | Compromised/revoked denial; atomic selection and race behavior | Source delivered 2026-10-21; build/runtime pending |
| 12 | Backup, recovery and KEK rotation | Synthetic restore, resumable rewrap, failure without KEK, runbook | Source delivered 2026-10-21; build/runtime pending |
| 13 | Controlled change review | Reason, scope, revision, expiry and one-time approval; no annual bypass | Source delivered 2026-10-21; build/runtime pending |
| 14 | Secure API/BFF | HTTP authorization, CSRF, limits, no-store; no private-read/export/sign oracle | Source delivered 2026-10-21; build/runtime pending |
| 15 | Persian RTL key-management UI | Owner generate/import, public download/status/history, compact accessible UX | Source delivered 2026-10-21; build/runtime pending |
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

### Step 03 operator migration helper

Added `scripts/phase09_apply_migration.py` after the owner's request to replace the inline heredoc. The earlier inline script's input() failed because stdin was occupied by source text; that reported attempt stopped before connecting or applying any migration.

Run from the checkout after dev-up/dev-status and the focused model tests:

```bash
python3 scripts/phase09_apply_migration.py
```

The helper reads prompts from the terminal, accepts a hidden password, hardcodes loopback port15432 and argintaxpayer_test, uses the existing actual-database guard, requires the exact preceding migration, refuses unexpected pre-existing signing tables, checks EF model drift, creates a private custom-format backup and SQL review file, then requires APPLY. It rechecks the clean checkout and history before updating only the explicit target migration, and checks the resulting history/table count.

It does not start services/tunnels, reset history, or claim a complete restore/trigger/security test. The helper has been source-reviewed and its committed content checked, but execution and database application have NOT been performed by the assistant because the execution environment is unavailable. Operator results and Step03 approval remain pending.

### Step 03 operator migration result — 2026-10-20

The owner supplied macOS terminal output showing:
- Applying migration 20261020181500_AddPhase09SigningMetadata.
- EF reported Done.
- The helper reported the target migration applied to argintaxpayer_test and found all six signing tables.

Status: operator-reported migration application successful. This is not independent assistant execution or proof of trigger behavior, authorization, concurrency, recovery or key custody. No new model-test counts were supplied with this output, and final owner signoff of Step03 is still pending. Step04 has not started.

## 11. Step 04 — Operation persistence, atomic audit and idempotency

**Owner approval:** The owner explicitly approved Step03 and requested Step04 on 2026-10-20. Earlier Step03 pending text is historical evidence, not the current signoff. The existing migration was operator-reported applied to argintaxpayer_test; this step adds no migration/schema change.
**Source baseline:** `8b4abd9b35725984b66748eab7696c898919328a`.
**Status:** Owner-approved2026-10-21. Earlier execution limitations remain historical evidence; owner approval does not supply new runner output. Step05 is tracked in Section12.

### Delivered implementation

- Application/Signing/SigningOperationContracts.cs: scoped generate/import claims, public-only operation views, fixed failure enums and ISigningOperationRepository.
- Domain/Signing/SigningOperationPolicy.cs: source-pinned profile/consent versions, terminal-state protection, canonical public fingerprints and five-minute UTC authentication policy.
- Infrastructure/Persistence/Signing/EfSigningOperationRepository.cs: owner-authorized reservation/advance, scoped metadata lookup, optimistic revision checks, database serialization per memory and EF execution-strategy handling.
- SigningOperationAudit.cs: fixed-schema operation and consent audit staged on the same context transaction; no request body, exception or arbitrary log payload.
- PersistenceRegistration.cs: scoped DI registration only when PostgreSQL is configured.
- Three test files: policy, no-DB validation, and explicitly opt-in PostgreSQL integration.
- scripts/phase09_step04_verify.py: runs focused no-DB checks, prompts securely for the existing loopback test DB, then runs only the six PG tests without applying migrations.

No endpoint, provider, generated private key, ciphertext storage, live authority request, fiscal-memory activation, or entitlement update was added. These are internal persistence contracts, not a public method for a browser to assert arbitrary provider success.

### Reservation and replay contract

ReserveAsync accepts only Generate/Import, the pinned legacy profile, explicit server-custody acceptance and the current consent-text version. Import requires a canonical PUBLIC fingerprint derived before reservation; no private-key/password hash enters the journal.

The metadata digest is computed server-side from scope, kind, profile, consent version and optional public fingerprint. Same scoped OperationId with matching actor/digest replays the existing public-only result; changed metadata conflicts. A database unique key plus per-memory transaction locking protects concurrent attempts. A simultaneous losing transaction may return Conflict; a subsequent matching retry replays the winner. No claim is silently replaced.

A new reservation requires a currently valid authenticated owner session and a DB-recorded login timestamp within five minutes. The client cannot provide a reauthentication timestamp. Current taxpayer/memory membership and customer profile access are checked; administrative metadata access does not grant custody mutation. Metadata reads retain the existing scoped Level1/Level2 rules.

Operation, owner consent, operation-revision audit and consent audit are saved in one transaction. Deferred DB guards execute during commit before a success result is returned. Missing audit/FK/revision errors never become successful results. Unknown database failures propagate for safe higher-layer handling; caller/provider error strings are not persisted.

This service owns its clean DbContext transaction and refuses composition with pending changes or an already active transaction. It clears only its owned tracking lifecycle; external provider calls are never placed inside the retry callback.

### Staging, failure and recovery

The persisted state machine supports Reserved -> VaultStaged/Failed/RecoveryRequired; VaultStaged -> Committed/Failed/RecoveryRequired; RecoveryRequired -> VaultStaged/Committed/Failed. Terminal operations cannot restart. Compare-and-swap uses the persisted Revision; a matching replay of the immediately applied transition returns the current public-only view.

AdvanceAsync is an INTERNAL orchestration API. VaultStaged means a future trusted provider adapter reported a durable staging result; no public endpoint accepts this assertion. RecoveryRequired stores a fixed safe code such as PROVIDER_OUTCOME_UNKNOWN, not an exception body. The stable operation record ID and scope are the reconciliation identity for the future provider. No retry is authorized to create a second key merely because a provider call timed out.

Committed is denied until the same scoped originating operation has a LocallyReady key with sealed public metadata and owner consent. For imports, the sealed public fingerprint must also reproduce the original metadata digest, preventing a changed imported key from satisfying the old claim. This step does not manufacture that key or pretend a metadata row proves cryptographic validation. Actual generation/import/provider receipt validation belongs to Steps05–09.

There is no distributed transaction between PostgreSQL and Vault. Step04 supplies the durable operation/recovery journal and consent/audit atomicity; provider probing, envelope orphan reconciliation and protected-result persistence are completed with the provider in Step05 and recovery exercises in Step12. No provider cleanup deletes key history.

Session/ownership is checked within the transaction and again before saving a mutation. Serializable transaction admission defines ordering for overlapping changes; an operation admitted before concurrent revocation may complete under that ordering. Later signing admission and transfer/MFA/session race coverage remain required in Step16; this step does not claim a full authorization audit.

### Tests added and database safety

Pure tests: 18 policy cases and 3 invalid-input tests. Invalid requests use an unreachable PostgreSQL port to prove their validation path returns before opening a connection.

Six PostgreSQL tests cover:
1. Atomic reservation/consent/audit, replay, and changed-metadata conflict.
2. Unknown provider outcome, transition replay, recovery progression, refusal to commit without a sealed key, and terminal-state protection.
3. Missing consent, foreign customer and revoked-session denial.
4. Concurrent same-ID reservation: one persisted operation, deterministic retry replay.
5. Deferred missing-audit rollback and append-only audit rejection.
6. Successful completion/replay after matching scoped, sealed public metadata (ephemeral synthetic RSA public key; no real vault).

The PG attribute reports SKIPPED when RUN_ARGINTAXPAYER_PG_INTEGRATION is not1; it does not silently return a passing test. When enabled, fixtures require both the parsed DB name and actual current_database() to equal argintaxpayer_test, and require the already-approved Phase09 migration in history. Tests never migrate/EnsureCreated automatically.

Fixtures use synthetic accounts, taxpayers and memory IDs. Append-only journal/consent/audit fixtures are intentionally retained in the TEST database; cleanup does not disable triggers, truncate or delete protected history. No production/developer DB is permitted.

### macOS acceptance commands

From the repository root, first run the source/model/policy checks without database integration:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
python3 tests/phase09_verify_test.py
bash scripts/phase09-verify.sh focused --filter FullyQualifiedName~Phase09Signing
```

Expected focused coverage: 11 existing model/script tests +21 pure Step04 cases; six Step04 PG tests explicitly skipped when integration is disabled. Actual discovery/counts must be captured; none are reported as already executed by the assistant.

For the real six PostgreSQL tests, use the existing dev-up/dev-status tunnel and private exported test configuration, then run:

```bash
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
bash scripts/phase09-verify.sh integration --filter FullyQualifiedName~Phase09SigningOperationPostgresTests
```

ConnectionStrings__ArginTaxPayerDb must explicitly point to argintaxpayer_test using the approved loopback/tunnel configuration. The verifier checks the actual database before build/test. The interactive migration helper did not export its private Python environment back into the calling shell; configure the existing private test connection without posting credentials in chat. Do not assume dev-up's developer DB settings are the test DB.

Acceptance requires successful build, all focused pure/model tests, all six PG tests without skips, inspection of scoped rows/audit/revisions and explicit owner approval. If any test fails, stop and report the exact error. No additional migration or key-provider setup is needed for Step04. Step05 remains unstarted.

### Simplified Step04 operator command

The new helper avoids relying on a connection string exported by the earlier migration process:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
python3 scripts/phase09_step04_verify.py
```

It runs focused model/policy/validation checks first, then prompts for the test DB user/password and runs the six guarded PG tests. Passwords are not stored or printed. Expected no-DB focused result: 32 executed tests and six explicit PG skips; final PG run: six executed, zero skipped. Exact runner counts are evidence to capture, not assistant-observed PASS results.

### Step04 recheck — 2026-10-21

Reviewed the committed operation repository, PostgreSQL fixtures and operator helper at `35e7b89a1fae9fd16ffa0821eed07d33b3c656d7`. The runtime is now available for Python, but dotnet, psql and Docker are absent; C# compilation and PostgreSQL execution remain unverified.

Fixed an acceptance gap in the verifier: the exact Step04 integration filter now requires TRX totals of six total/executed/passed and zero failed/notExecuted. Previously a partly skipped run could satisfy the generic non-empty execution check. A regression test reproduced rejection failures for partial, skipped, failed and unexpected-count reports before the fix; all nine Python safety tests pass after the fix. These use synthetic TRX reports and do not prove the six database tests passed.

No schema or migration changes. Step04 remains ready for executable verification and owner review; Step05 has not started. Run `python3 tests/phase09_verify_test.py` and then the existing `python3 scripts/phase09_step04_verify.py` after dev-up/dev-status. Required evidence remains 32 focused no-DB tests and six real PostgreSQL tests without skips.

## 12. Step 05 — Envelope protection and Vault provider

**Owner approval:** Step04 explicitly approved on 2026-10-21. No additional runner output was supplied with that approval; preserve prior execution limits.
**Design authority:** Section3 custody/protection design and approved step05 scope. Implementation is inline. No endpoint, schema change, Vault installation or real-secret transfer is authorized by this delivery.

### Implementation plan and file boundaries

1. `Signing/Protection/KeyProtectionContracts.cs`: non-secret scope binding, redacted envelope/error types and disposable buffer; provider/store boundaries contain no business DTO or secret logging.
2. `KeyEnvelopeProtector.cs`: .NET AES-256-GCM, new32-byte DEK and12-byte nonce per encryption,16-byte tag and canonical scope/key AAD; reject tamper/swap/unknown formats and zero owned buffers.
3. `VaultHttpTransport.cs` and `VaultKeyProtectionProvider.cs`: direct .NET HTTP client, TLS validation, redirects/retries disabled, bounded responses, fixed paths, rotating Agent token file, health/version and non-root/expiring-token admission, Transit DEK protection and KVv2 CAS create/rewrap. No third-party client package is introduced.
4. `ProtectedKeyEnvelopeStore.cs`: stable DB-operation/key identity, read-before-create reconciliation and create-only CAS; uncertain write returns recovery-required, never overwrite or silently generate another key. Rewrap uses expected KV version and retains encrypted key bytes.
5. `Phase09SigningKeyProtectionTests.cs` and `Phase09SigningVaultAdapterTests.cs`: synthetic crypto and HTTP-contract tests first; then focused verifier and existing Python safety tests. Live TLS/ACL/provider persistence is a separate explicit deployment acceptance gate.

Review focus: cross-scope AAD swaps; failed unwrap/tamper never yielding plaintext; unknown write outcomes/concurrent CAS; redirect/body/token leakage; missing configuration/KEK/version pin. Test doubles exist only in the test assembly.

### Provider contract and deployment gates

Pinned source-review baseline: Vault Community2.2.0; protocol `/v1` with Transit AES256-GCM96, derived=true and convergent_encryption=false; KVv2. No third-party .NET Vault client dependency. Version pinning is compatibility admission, not a security certification or completed installation; re-review advisories/server artifacts before deployment. The adapter refuses a different live version, sealed/uninitialized Vault, root/unexpected policy names, short token TTL, exportable/backup/deletable protection keys and insecure endpoints. Workload policies must be named `argin-signing` (optional default only); policy-name checking does not prove the effective ACL contents.

The derived Transit context is Base64 of the canonical local AAD plus `|OperationRecordId`; it is supplied to encrypt/decrypt/rewrap consistently. This uses the documented derived-key context contract instead of assuming rewrap supports an undocumented associated_data parameter. Local envelope AAD remains exactly the four UUIDs specified in Section3. Stable storage path is keys/customer/taxpayer/memory/operation/key using UUID N format. No caller URL or opaque arbitrary vault path is accepted.

`AddPhase09KeyProtection` is an explicit registration extension for the future custody host; Api/BFF do not call it in this step. Missing/disabled settings register only unavailable implementations. Enabled configuration requires HTTPS origin, validated mount/key segments, an absolute protected Agent token file, and ServerVersion=2.2.0. No environment KEK, MFA key, private-key export or plaintext fallback exists. Caller-owned PKCS8 buffers must be disposed/cleared by the later generate/import boundary. The protector enforces size/envelope integrity; it deliberately does not replace the Step06/07 PKCS8 parsing and strength rules.

The coordinator reads before CAS create, authenticates existing material before matching a replay, and never overwrites a winning envelope. Uncertain durable writes surface RecoveryRequired; recovery only probes and authenticates the stable identity. A missing probe is not authorization to generate another key. Later Step06/07 orchestration must reserve/advance the Step04 DB journal before external work and atomically seal public metadata/audit before signing admission. No provider call runs inside EF retries. Rewrap authenticates the existing envelope, changes wrapped DEK metadata only and writes with the exact expected KV version. No key history is deleted.

Secrets: owned DEKs, plaintext output buffers, serialized request bytes and raw response buffers are cleared. Disposed handles/buffers reject reuse. Managed/runtime/TLS/header/JSON buffers and host compromise remain residual risks; complete memory erasure or process isolation is not claimed. Agent sink directories must be owned by the workload account with restricted parent-directory access; the adapter rejects symlink sinks and group/other Unix file permissions and rereads the sink each request. Windows Agent sink ACLs require operator verification. External diagnostics, crash dumps and tracing on the eventual custody host must not capture request bodies/headers.

A reviewed test workload policy is included at `deploy/vault/phase09-test-workload.hcl`. It is not applied automatically. An operator must provision a separate TLS test Vault with trusted local certificate, derived nonexportable Transit key, KVv2 with CAS required, the exact restricted policy and an Agent-managed expiring token; show denied key-export/delete/admin capability evidence before enabling real custody. Vault server installation/unseal/Agent auth, TLS chain/hostname checks, ACL enforcement, restart persistence and restore remain NOT EXECUTED acceptance gates. Step12 owns full restore exercises.

Official references checked2026-10-21:
- https://developer.hashicorp.com/vault/api-docs/secret/transit — derived context, encrypt/decrypt/rewrap contracts and key metadata.
- https://developer.hashicorp.com/vault/api-docs/secret/kv/kv-v2 — CAS=0 creation and exact-version replacement.
- https://developer.hashicorp.com/vault/api-docs/system/health — initialized/sealed/version admission.
- https://releases.hashicorp.com/vault/2.2.0/ — versioned binaries and signed checksum files exist; no binary was downloaded or verified by this delivery.

### Verification and operator commands

Added40 no-network C# test cases across crypto, staging, configuration and HTTP-adapter fixtures, plus one explicitly skipped-by-default real-Vault test. C# test discovery/execution is NOT verified: dotnet --info and attempted dotnet test returned command not found. No .NET compiler, PostgreSQL, Docker or Vault is available here. Synthetic HTTP fixtures test the adapter contract only; they do not prove TLS, ACL, Transit or KV compatibility in a running Vault.

Python safety suite:12 tests passed, including inherited live-Vault suppression and isolated-endpoint admission. The new suppression regression failed before the fix. Shell syntax and Python parsing passed. No PostgreSQL connection, migration or real key was used.

From a clean macOS checkout:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
python3 scripts/phase09_step05_verify.py
```

No dev-up/database is required for the default command. It runs Python safety tests and all Phase09 signing no-DB checks. New no-network coverage adds40 cases to the previous32 (expected72 executed if discovery matches source); six PG tests and one Vault test are expected skips in this mode. Treat actual TRX counts as evidence, not a preobserved result.

Only after separately provisioning the isolated TLS test Vault and Agent sink:

```bash
export PHASE09_VAULT_ADDRESS='https://localhost:18200/'
export PHASE09_VAULT_TOKEN_FILE='/absolute/private/path/to/agent-token'
python3 scripts/phase09_step05_verify.py --vault
```

This opt-in accepts only loopback HTTPS port18200 and hardcodes mounts argin-transit-test/argin-signing-test and key signing-test-kek. It never installs/migrates/configures Vault. The single real provider test must pass with zero skips; it creates synthetic encrypted RSA fixtures and retains their history. Its TRX gate rejects partial/empty/skipped success. Production mounts/credentials do not belong in this command.

**Current acceptance:** source implementation ready for build/test and owner review; runtime/provider acceptance pending. No Step06 work started, no migration created/applied, and no production key custody enabled.


### Review ledger

Implementation rulings: preserve the previously approved design and one phase document; keep the provider unwired from Api/BFF until custody-host deployment checks pass; use documented derived Transit context for all three wrapping operations. Cost of an incorrect version/API assumption is failed provider admission, so runtime custody stays disabled until the live gate passes.

An independent source reviewer found no Critical/Important implementation defect. Two test-coverage gaps were regraded as required security-acceptance coverage and addressed in source: forged envelope identity headers now exercise cryptographic binding, and synthetic cancellation/malformed-success/unknown-length oversize responses require RecoveryRequired and no retry. Rewrap output is authenticated against the original key before CAS persistence. Those C# regressions remain unexecuted because dotnet is absent; no RED/GREEN evidence is claimed. No remaining minor review finding is deferred.

## 13. Step06 — Key generation and public artifact

**Owner approval:** Step05 approved2026-10-21, with no additional runner transcript. Preserve its runtime/production-enablement gates. Read and preserve the latest owner's compiler correction408e8942.
**Design:** Implement the existing Section3 server-custody design inline, with a public-only application use case and internal crypto/provider boundaries. No API/UI activation, certificate/CSR, private export, selection/rotation or fiscal-memory activation belongs to this step.

### Implementation plan

1. Add Application/Signing/SigningKeyGenerationContracts.cs and Infrastructure/Signing/Generation/RsaSigningKeyMaterial.cs: fixed2048-bit RSA/e65537 local profile, disposable PKCS8, canonical SPKI fingerprint and technical-review PEM. Tests reject weak/wrong/extra data and ensure no private fields or false portal approval.
2. Add internal generation workflow/custody adapter and a partial EfSigningOperationRepository generation journal. Reserve owner consent through Step04, perform provider readiness, then atomically allocate the stable staged key ID and CAS-claim the operation before generating anything. The winner alone generates; replays only probe the same envelope. Seal key metadata/operation/audit atomically after authenticated custody. Test missing provider, concurrent claim/replay, uncertain outcome, scope/owner/consent denial and revoked/stale session.
3. Add explicit future-custody-host DI registration; no Api/BFF registration. Add no-DB and opt-in PostgreSQL verification. Existing model/migration/snapshot stay unchanged. Capture executable evidence honestly; .NET remains absent here.

Review focus: duplicate generation on retry/crash/DB retry; cross-customer scope spoofing; revoked/fresh authentication at mutation; orphan/provider metadata integrity; PEM accidentally containing private material or implying official portal approval.

### Source and portal format gate

Rehashed original official guides. TAX-CONNECT-16 p5 requires2048-bit RSA; TAX-SDK-GUIDE-OLD p9 describes PKCS8 private input corresponding to the public key uploaded in the portal. These do not establish exact portal upload SPKI/PKCS1 wrapping. Searches of official tax domains did not supply authoritative exact encoding evidence. Do not substitute third-party advice or the new certificate protocol. The local artifact is explicitly SPKI DER in PUBLIC KEY PEM for technical review only; PortalUploadApproved is always false and portal state remains Unverified. Portal-approved download is blocked until official format evidence is pinned and tested. This is an outstanding Step06 acceptance gate, not an assumed compatibility claim.

### Implementation decisions and crash behavior

The internal generation service resolves CustomerUserId from the current token, reserves the fixed legacy profile/consent through Step04, and checks protected-provider readiness before claiming. Claim transaction persists one staged SigningKeyVersion (revision1, initial VersionNumber1) and moves the journal Reserved->RecoveryRequired with a fixed unknown-outcome code and revision2. This is a conservative pre-generation intent, not evidence that Vault failed or contains a key. Both subjects have atomic audit. Only the transaction winner receives OwnsClaim=true; execution-strategy attempts reset that flag. RSA creation and all Vault calls are outside database retry callbacks.

A crash after intent and before durable envelope leaves a deliberately nonregenerating recovery state. Subsequent calls only probe the stable identity; no envelope does not grant a second generation. Availability in that case requires the later controlled operator/recovery path. A fresh OperationId cannot bypass this rule or key-rotation review: initial generation is rejected once any key row exists for that memory. No staged/private material is logged, returned to the browser or stored in PostgreSQL.

Vault staging authenticates the full envelope, parses canonical RSA PKCS8, checks strength and derives public SPKI before producing an internal receipt. Completion rechecks current owner/session, fresh login, original operation/kind/profile/digest, current consent version and receipt identity/fingerprint. Key metadata, LocallyReady state, operation Committed and both revision audits commit together. No key selection is inserted; memory status/revision, certificate metadata and authority connection state remain unchanged. A session revoked between custody and completion cannot commit; a fresh owner session may reconcile the same key.

ReadPublicAsync resolves the actual owner from PostgreSQL and applies the existing Level1/Level2/owner metadata scope. Public material is returned only after a committed operation, canonical SPKI validation and stored-fingerprint match. All downloadable bytes are regenerated PUBLIC KEY PEM from parsed public DER. There is no private-key export method, CSR, certificate, generic signing endpoint or authority request.

Enabled generation registration is explicit and separate from API/BFF: both SigningKeyGeneration:Enabled and SigningKeyProtection:Enabled must be true in the future custody host, with PostgreSQL plus approved Vault settings. Otherwise it registers an unavailable service; there is no synthetic provider in product code. Readiness/protocol gates from Step05 and its real-Vault test remain required before enabling real keys. This delivery does not install or configure that host.

### Acceptance and verification

Added10 no-DB C# cases for fixed RSA strength, canonical formats, public-only artifacts, one-generation/replay, unknown acknowledgements, empty recovery, unavailable provider, input/owner denial and disabled DI. Added five opt-in PostgreSQL tests for audit/state sealing, concurrent claims, provider-gap recovery, foreign owner/admin/stale/revoked denial and revocation after custody followed by fresh-owner recovery. PostgreSQL tests use synthetic custody; they do not establish deployed Vault compatibility. They assert six audit events for a normal generation (reserve/consent, claim/key intent, commit/key seal), no selected key and unchanged fiscal-memory status. Append-only synthetic fixture histories remain in argintaxpayer_test. Tests never migrate, disable guards or touch a different DB.

Python verifier extends exact TRX acceptance to all five generation PostgreSQL tests with zero skips, and strips inherited generation-enablement settings in no-DB runs. The new count regression failed before implementation. All13 Python safety tests pass after the fix. Attempted dotnet --info and focused dotnet test returned command not found: no C# build/discovery/test, PostgreSQL or live-Vault PASS is claimed here. Test counts below are source-derived expectations pending runner evidence, not observed .NET results.

Default macOS command, clean checkout, no DB/dev-up required:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
python3 scripts/phase09_step06_verify.py
```

Expected13 Python safety tests and82 no-DB Phase09 signing cases pass; eleven PostgreSQL tests and one real-Vault test are skipped. Capture actual TRX counts.

For the five real PostgreSQL generation-journal tests with synthetic custody, use the established tunnel/service helpers:

```bash
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
python3 scripts/phase09_step06_verify.py --postgres
```

The helper securely prompts for test credentials, verifies actual current_database()=argintaxpayer_test, and requires all five generation tests passed, none skipped. No new migration or Vault setup is needed for these DB tests. The independent Step05 real-Vault gate is still separate.

**Current acceptance:** Step06 local source implementation ready for build/test and owner review. Exact official portal-upload encoding is not established, so portal-approved export remains explicitly blocked. Step07 has not started. No migration/model/snapshot, production key, API/UI wiring or tax-authority state was changed.

### Review ledger

Independent read-only review identified no concrete Critical, Important or Minor source finding. It checked claim ownership across EF retries, nonregenerating recovery, atomic metadata/audit completion, RSA/public-only material, opt-in DI and the Level1/MFA fixture. Compilation and C#/PostgreSQL execution were unavailable to both implementer and reviewer. The owner's previous JSON out-discard shadow fix was preserved.

Rulings: initial generation deliberately stops after any durable key intent rather than allowing a new OperationId to bypass recovery/rotation; a crash before envelope persistence can require operator recovery and costs availability. Official portal encoding is unproven, so technical SPKI output remains explicitly unapproved for portal upload. No runtime security/compatibility success was inferred from source review. No minor review item is deferred.

## 14. Step07 — Existing private-key import

**Owner approval:** Step06 explicitly approved2026-10-21; no new runner transcript was supplied. Continue the existing Section3 design inline. Earlier runtime/Vault/portal-encoding gates remain unverified where stated.

### Implementation plan and policy

1. Application/Signing/SensitiveKeyInput.cs and SigningKeyImportContracts.cs: bounded owned byte/character buffers, explicit transfer/disposal, redacted string and forbidden JSON serialization. Accept PKCS8 DER/PEM and encrypted PKCS8 DER/PEM only; no public-only/certificate/PKCS1 input, CSR, private export or API/UI wiring.
2. Infrastructure/Signing/Import/Pkcs8SigningKeyParser.cs and Pbes2ImportPolicy.cs: strict single PEM block or complete DER, RSA2048/e65537, canonical protected output and a local mathematical sign/verify check. Encrypted import permits PBES2/PBKDF2-HMAC-SHA256/AES256-CBC with salt8..64, IV16, optional keyLength32, iterations1..600000 and no nested/trailing data. Limits are product input policy, not a claimed tax-authority PBE requirement. Password maximum1024 chars; encoded input maximum64KiB; canonical private DER maximum16KiB. Bound KDF cost before .NET PBKDF2/AES-CBC decryption; validate the decrypted buffer with the same strict, complete DER policy before RSA import.
3. Add owner-authorized import orchestration. Authorize/fresh-login before expensive parsing; derive the public fingerprint server-side before Step04 reservation. Share the Step06 CAS-claim and atomic sealing implementation parameterized by Generate/Import; no secret hash in DB. Retried requests with different public fingerprints conflict; unknown results reconcile the same operation/key. Existing key/history blocks uncontrolled replacement.
4. Add synthetic parser/workflow and opt-in PostgreSQL tests, exact no-skip verifier gate and one macOS helper. No migration/model/snapshot or automatic live provider call. Run available safety checks and record missing .NET execution honestly; review the shared-generation regression surface.

Review focus: private/password logging/serialization/disposal on all exits; hostile DER/PEM and KDF resource limits; changed-key replays; owner/admin/session/scope authorization; shared generation behavior and same-key recovery without overwriting Vault history. Step08 public-key comparison and official portal verification are not implemented by this step.

### Implementation and verification evidence

SensitiveKeyInput copies bounded source bytes and password characters into owned buffers, denies JSON read/write and redacts ToString. ImportAsync takes ownership and disposes it even for metadata/authorization denial, cancellation, parser/provider failure and replay; caller-owned original buffers remain the caller's responsibility. Parser-owned PEM characters, decoded/decrypted DER, derived encryption keys, canonical private copies and handles are cleared/disposed. Complete managed-memory/TLS/host isolation is not promised. No input/password/private-key hash or raw exception is sent to the operation journal.

AuthorizeImportAsync resolves the owner from the current authenticated session and requires fresh login before any expensive native key parse/decryption. Validated canonical RSA material yields only the server-derived public fingerprint used by the existing Import reservation digest. Import reuses Step06's claim/sealing engine with operation kind2 and origin2; generation remains kind1. The shared commit verifies the import receipt's public fingerprint against the original operation digest. Only the durable claim winner stages material. Replays probe the same key identity; no envelope does not authorize overwriting or restaging. Password/encoding changes with the same validated public key replay; a different public key conflicts. No factory generates a replacement key in this path.

No certificate/CSR, portal registration claim, key selection, fiscal-memory activation or uncontrolled rotation was added. PublicMatch staysNotCompared and PortalState staysUnverified: the local mathematical self-test is not the Step08 comparison with user-provided/registered public material. Existing public-only artifact and deployment/provider gates remain unchanged. Explicit future custody-host registration requires SigningKeyImport:Enabled plus SigningKeyProtection:Enabled; missing settings return Unavailable and still dispose the input. Api/BFF do not call it.

Added22 no-DB C# cases (17 parser/input and5 orchestration/DI), five opt-in PostgreSQL import cases, and one Python safety-test method with subcases for exact5 import and10 combined generation/import results. Tests cover format parity, wrong password/algorithm/strength, extra/multiple/prefixed PEM, KDF cap before decryption, zeroing/nonserialization, owner denial before parsing, same/different-key replay, unknown acknowledgements, cancellation and disabled registration; PostgreSQL covers atomic six-event audit, kind/origin binding, concurrency, stale/revoked sessions and fresh-owner recovery. DB tests use synthetic custody only, retain append-only fixture histories, and never migrate or call a real Vault.

Available verification:14 Python safety tests passed. New no-skip count checks failed before the gate was added. Dotnet focused execution was attempted and returned command not found, so C# build/discovery/execution and PostgreSQL PASS remain unclaimed. Source-derived expectations are104 no-DB Phase09 cases,16 PostgreSQL skips plus1 real-Vault skip in default mode. Combined DB mode must execute the five existing generation and five new import tests with zero skips, preserving the shared engine's regression gate.

From a clean macOS checkout:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
python3 scripts/phase09_step07_verify.py
```

For the10 real PostgreSQL journal tests using synthetic custody and the established test tunnel:

```bash
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
python3 scripts/phase09_step07_verify.py --postgres
```

The helper securely prompts for the test DB credentials and confirms actual current_database()=argintaxpayer_test. Exact combined TRX count10 and no skips are mandatory. It applies no migration and uses no real taxpayer key. Step05's separate real-Vault deployment gate is still required before actual custody enablement.

Official API references reviewed2026-10-21: Microsoft Learn Rfc2898DeriveBytes.Pbkdf2, SymmetricAlgorithm.DecryptCbc, PemEncoding.TryFind, AsnReader/TryReadInt32 (net9.0). These support the implementation APIs, not an Iranian portal-format claim.

**Current acceptance:** Step07 source delivered for build/test and owner review, with executable/provider evidence still pending. No Migration/model/snapshot or API/UI change. Step08 has not started.

### Independent source review

One Important policy gap was identified: encrypted PKCS8 used a BER-capable native importer without proving strict DER for its decrypted contents. The implementation now uses bounded .NET PBKDF2/AES-CBC, validates the complete decrypted DER with the shared plaintext validator, and clears the derived key and plaintext in finally. Two added regression cases target encrypted inner trailing data and nonminimal BER length. They are ADDED, NOT EXECUTED; no C# RED/GREEN evidence is claimed. No other concrete source finding was identified in authorization, disposal, API accessibility or shared kind/origin/digest handling. No minor review item is deferred.

Preserved the owner commit3e869745 email-normalization fix in Step06 and applied the same normalization to the new import fixture.

## 15. Step08 — Private/public match and fingerprint

**Owner approval:** Step07 explicitly approved2026-10-21 if no migration exists. Step07 added no migration; this implements the already approved Section3 comparison direction inline. Owner approval is recorded separately from missing runtime evidence. No new migration/model/snapshot is required or applied.

### Design and implementation plan

Goal: compare a supplied public key with an already sealed owner key using canonical SPKI, its SHA256 fingerprint, and a server-generated local challenge. A match proves local possession only; it cannot prove portal registration, official upload encoding, authority activation or online acceptance.

Architecture: retain the existing owner/session authorization and protected-custody boundary. Parse public material only after owner authentication and fresh login. Persist a key-revision intent audit before reading private custody, run a purpose-separated CSPRNG32-byte challenge with RSA2048/e65537/SHA256/PKCS1 internally, then reauthorize and atomically persist the result/audit before returning public metadata. No challenge, signature, private bytes or provider locator leaves that boundary.

Accepted comparison formats: SPKI DER/PUBLIC KEY PEM and PKCS1 RSAPublicKey DER/RSA PUBLIC KEY PEM. Both canonicalize to DER SPKI; fingerprint is lowercase SHA256 hex over canonical SPKI. These are product comparison formats, not certified portal formats. Exact input bounds:16KiB encoded,4KiB decoded, one ASCII PEM block plus whitespace only, exact DER re-export equality and complete consumption. Reject private/certificate/CSR input, unknown formats, extra blocks/data, BER, wrong strength/exponent. Caller owns the original input; service copies bounded input after authorization and clears its copy and parser buffers, including accidentally supplied private PEM.

Implementation tasks (single master document as requested):
- Application/Signing/SigningKeyMatchContracts.cs: public request contains taxpayer/memory/key IDs, expected key revision and format. ISigningKeyMatchService.MatchAsync takes a separate ReadOnlyMemory<byte> public input and returns status/key ID/two public fingerprints/revision only. PortalRegistrationProven is always false.
- Infrastructure/Signing/Matching/SigningPublicKeyParser.cs and RsaKeyMatchProof.cs: canonicalization, fixed local challenge and protected read-only VaultKeyMatchProof. Compare the protected key's derived SPKI with immutable DB metadata first; corrupted custody/metadata is IntegrityFailure, never a user mismatch. Missing envelope is RecoveryRequired; do not create, replace or regenerate anything.
- MatchContracts.cs and SigningKeyMatchService.cs: authorize, parse, begin audited intent, run proof, complete audited result; fail closed on unavailable/corrupt custody, cancellation or failed commit. No public fingerprint/result is released on a commit failure.
- Persistence/Signing/EfSigningKeyMatchJournal.cs: reuse the existing serializable per-memory lock and atomic audit helper, without changing Generate/Import journal behavior. Begin and complete each increment key revision and create SubjectKind2 audit at that exact revision. Intent stores the supplied public fingerprint and is valid for five minutes. Completion binds intent ID, actor, scope, original key operation, revision and fingerprints. Stale revision/concurrent changes deny the result. Only committed, consented owner keys in LocallyReady/Suspended are admitted; staged/terminal/selected keys are denied. Ready mismatch becomes Suspended because the existing DB invariant forbids Ready+Mismatch. A later match never automatically resumes a suspended key; Step11 owns controlled resume. PortalState and fiscal-memory/selection rows remain unchanged.
- SigningKeyMatchRegistration.cs: explicit future custody-host opt-in requires SigningKeyMatching:Enabled and SigningKeyProtection:Enabled, otherwise Unavailable. Api/BFF/UI do not call it. Synthetic parser/proof/workflow/provider and five opt-in PostgreSQL tests, plus scripts/phase09_step08_verify.py and exact no-skip verifier checks.

Review focus: hostile/mislabeled public input; old/foreign/admin sessions before custody; provider corruption versus mismatch; revision/session changes between intent/proof/commit; mismatch suspension without activation or portal claims.

### Execution ledger and rulings

- Baseline:14 Python safety tests passed. Dotnet is unavailable (command not found), so C# tests are source deliverables only; no build, discovery, runtime, PostgreSQL or C# RED/GREEN result is claimed.
- Verifier test-first evidence: exact Step08 PG count and inherited matching-enable tests produced three expected failures before implementation, then15 Python tests passed after the gates were added. Printed TRX values inside these Python tests are synthetic fixture assertions, not actual DB runs.
- Ruling: use the existing key revision/audit rows for comparison intent and completion rather than extending the Generate/Import operation-kind constraint. This preserves the approved schema. A lost response may require reading the current public revision and retrying; no exactly-once client replay is promised.
- Ruling: deny the key currently selected in this step rather than changing selection or silently suspending a selected row against the existing selection-ready trigger. Step11 owns coordinated selection/lifecycle changes. Cleared selections or selection of a different key do not block comparison of this key. This limits availability for later selected keys while preserving current invariants.
- Ruling: bounded implementation continues under the owner's explicit instruction to complete the next approved step; no new approval round or extra design/plan file is introduced. Work remains isolated from the stale/dirty main checkout and publishes against an expected branch head.

Official .NET API reference reviewed2026-10-21: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rsa?view=net-9.0 . No third-party crypto dependency or tax-protocol inference.

### Final review and executable checks

Independent source review found one Important availability defect: both guards rejected retained selection rows, including cleared selections. Both predicates now reject only the exact selected key. The first PostgreSQL case includes an audited null selection which must permit a match and an audited selection of the same key which must deny a second private admission. These C# regression assertions remain unexecuted; no RED/GREEN claim is made. A selected-different-key fixture is not separately exercised; the narrowed predicate is source-reviewed behavior, not runtime evidence. No other concrete source finding was reported; no minor is deferred.

Added28 no-DB C# cases (17 parser/proof,9 workflow/DTO and2 protected-custody cases) plus five opt-in real PostgreSQL journal cases with synthetic private custody. Source-derived expected focused count is132 no-DB cases,21 PostgreSQL skips and1 live-Vault skip in default mode. These are discovery expectations, NOT executed counts. The Step08 PostgreSQL gate requires exactly five executed/passed cases with zero skips. Tests cover normal match, mismatch+suspension+no auto-resume, stale/foreign/revoked sessions, concurrency, post-proof revocation, current-revision retry, lifecycle revision race and selection guards. All fixtures are synthetic, retain append-only history and apply no migrations.

Fresh available checks:15 Python safety tests pass; Python syntax and helper --help pass. C# build/discovery/runtime and PostgreSQL execution remain pending because dotnet is absent. Step05 live Vault and exact official portal format gates remain separate.

From a clean macOS checkout:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
python3 scripts/phase09_step08_verify.py
```

For the five PostgreSQL tests, keep the established AutoSSH test tunnel and run:

```bash
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
python3 scripts/phase09_step08_verify.py --postgres
```

The helper prompts for test DB credentials, hardcodes loopback15432/argintaxpayer_test, checks actual current_database(), applies no migration and calls no real Vault or tax authority. Its default mode does not need a database. Matching result never establishes portal registration. Step09 proceeded after owner approval of Step08 and adds no migration/model/snapshot, API/UI wiring or fiscal-memory activation.


## 16. Step09 — Offline legacy signing and SDK compatibility

**Scope:** implement a local-only signing engine from the exact supplied SDK source `TaxCollectData.SDK` 0.0.30. The inspected source archive SHA-256 is recorded in Section2; the source files and hashes are the evidence for this compatibility profile. The separate 0.0.34 binary is not proven equivalent. This work does not infer official tax-authority acceptance or authorize live submission.

### Behavior implemented

- `LegacyPayloadNormalizer` reproduces both source profiles: default object DTO signing uses Newtonsoft.Json 13.0.1 and directly flattens the `JObject.FromObject` token tree (matching the inspected 0.0.30 project dependency and conversion contract without a JSON round-trip); the separate byte-snapshot overload follows the SDK's `NormalizeJson`/`JObject.Parse` path. Both apply sorted dotted keys, `E0` array paths, unescaped `#` null/empty markers, `##` literal-hash escaping and transfer-header rules. Explicit `v1` uses the SDK's System.Text.Json camel-case/string-enum settings and ordered direct header concatenation. It supports content and transfer normalization, enforces an 8 MiB bound and returns normalized bytes in disposable `SensitiveBytes`.
- `LegacyOfflineSigningEngine` imports canonical PKCS#8 RSA private material, checks full input consumption, approved key strength and the derived SPKI against the stored public key, then signs UTF-8 normalized bytes with SHA-256/RSA PKCS#1 v1.5 and returns ordinary Base64 plus a generic profile identifier. Private material is not logged, returned, persisted or sent to a provider.
- `OfflineSigningRegistration` adds the internal engine only when `SigningKeyOfflineSigning:Enabled=true` is explicitly set by a future host. No current API/BFF registers or calls it. The engine performs no network I/O.
- Added21 focused no-database tests:14 normalization/serialization cases,6 crypto/integrity cases and1 default-registration isolation case. Python verifier tests enforce exactly21 passed with zero skips and remove inherited offline-signing enablement from the test environment.

### Execution and decision record

- TDD C# execution was attempted, but this environment has no `dotnet` executable. Therefore these C# tests have not been compiled, discovered or run; no C# RED/GREEN or runtime pass is claimed. The added Python verifier safety test exercises exact-count and skip rejection.
- Normalization consumes the exact request JSON produced at the source-compatible boundary; callers must not parse and reserialize arbitrary payload snapshots between serialization and signing. Simple-mode non-ASCII JSON escaping follows `System.Text.Json` defaults, matching the reviewed SDK options.
- No migration exists or is needed. No migration, database, key provider, live authority, UI/API endpoint or production enablement is involved.
- Compatibility is established against inspected 0.0.30 source behavior only. Parity with binary 0.0.34 and external service acceptance remain unverified.

From a clean macOS checkout, run the local safety gate (no database tunnel is required):

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
python3 scripts/phase09_step09_verify.py
```

The helper runs the Python verifier tests and the focused C# gate in an isolated environment. It never applies migrations or connects to PostgreSQL, Vault or the tax authority.

**Current acceptance:** Step09 source delivered for build/test; runtime verification remains pending because `dotnet` is unavailable here. No migration/model/snapshot or API/UI integration was added. Step10 is now delivered for build/test.

## 17. Step10 — Application signing and immutable snapshot

**Owner approval:** Step09 was approved before this implementation. Step10 adds no migration, model, snapshot update, API/BFF endpoint or tax-authority request.

### Scope and implementation

- `AuthorizedSigningRequest` is an internal request containing invoice scope, immutable invoice revision, immutable key revision, profile and expected snapshot hash. It does not accept arbitrary caller bytes.
- `TrustedInvoiceSnapshot` is loaded from an authenticated, scope-aware source. The service verifies ownership identifiers, both revisions, the pinned profile, object-shaped JSON, an 8 MiB bound and the lowercase SHA-256 hash before touching custody.
- `IInvoiceSigningAudit.BeginAsync` must durably record signing intent before `IInvoiceSigningCustody.UnwrapAsync` is called. Completion is recorded before any signature is returned. A failed completion never releases the signature.
- The custody boundary returns disposable `SensitiveBytes`; the service passes it only to the legacy offline RSA signer. No private material, password, envelope, vault locator or generic `Sign(bytes)` endpoint is returned.
- The signature uses the Step09 source-pinned object profile and returns Base64, snapshot hash, key version and profile. `IsFiscalSubmissionAuthorized` remains false. The service performs no network or authority dispatch.
- Registration is future-host-only and requires both `SigningKeyOfflineSigning:Enabled=true` and `SigningKeyProtection:Enabled=true`; current API/BFF composition does not register it.

### Verification record

Added four focused no-database C# cases covering invalid request short-circuit, snapshot hash/revision integrity, intent-before-custody ordering, and completion-failure redaction. The Python verifier adds an exact four-test no-skip gate. C# compilation/discovery/runtime remains pending where `dotnet` is unavailable; the Python verifier tests remain executable safety checks.

From a clean macOS checkout:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
python3 scripts/phase09_step10_verify.py
```

The command does not require the AutoSSH PostgreSQL tunnel and never applies a migration. Step11 is now delivered for build/test.

**Current acceptance:** Step10 source delivered for build/test; runtime verification remains pending because `dotnet` is unavailable in the authoring environment. No migration/model/snapshot or API/UI integration was added.


## 18. Step11 — Key lifecycle and local rotation

**Owner approval:** Step10 was approved before this implementation. This step uses the existing Phase09 signing tables and migration contract; it adds no migration or schema change.

### Scope and implementation

- `SigningKeyLifecyclePolicy` centralizes the allowed state graph: `Staged→LocallyReady`, `LocallyReady→Suspended/Compromised/Superseded`, and `Suspended→LocallyReady/Compromised/Superseded`. Compromised and Superseded are terminal and cannot return to signing.
- `ISigningKeyLifecycleService` accepts a scope-bound command with expected key revision, action, bounded reason and optional replacement identifier. It rejects invalid input before authorization and never trusts caller-provided scope without the journal.
- Selection is permitted only for `LocallyReady`; unselection requires the exact current selected key. The journal must apply selection, lifecycle, audit and revision changes atomically with a database CAS. The service deliberately does not implement an in-memory lock.
- Resume is restricted to `Suspended`; compromise reporting remains an immediate local stop. Rotation is represented as a controlled journal operation and cannot use a replacement key supplied as an unreviewed shortcut.
- No API/BFF endpoint, private-key export, authority activation or automatic key replacement is added.
- `SigningKeyLifecycleRegistration` is future-host-only and requires explicit `SigningKeyLifecycle:Enabled=true`; the current API/BFF composition does not register lifecycle commands.

### Verification record

Added ten focused no-database C# cases: lifecycle transition matrix, invalid reason/revision rejection, authorization denial, selection readiness, exact selection unselect guard and revision forwarding. The Python verifier adds an exact ten-test no-skip gate. C# compilation/discovery/runtime remains pending because `dotnet` is unavailable here.

From a clean macOS checkout:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
python3 scripts/phase09_step11_verify.py
```

No AutoSSH PostgreSQL tunnel is required for the local gate, and no migration is applied. Step12 has not started.

**Current acceptance:** Step11 source delivered for build/test; runtime verification remains pending because `dotnet` is unavailable in the authoring environment.

## 19. Step12 — Backup, recovery and KEK rotation

Step12 adds a custody-host recovery coordinator for resumable envelope rewrap. It consumes an explicit scope-bound workload and expected envelope version, advances only after each successful CAS rewrap, and returns a checkpoint containing index, completed count and opaque storage path. A missing protection service or stale version pauses recovery without claiming success. The coordinator never exports plaintext, DEK, KEK or private material.

The local gate adds eight no-database cases for empty restore, invalid checkpoints, resumability, explicit CAS version, missing-key-protection and conflict outcomes, secret-free checkpoints and scope-bound paths. No migration, model or snapshot change was added. Vault restore, backup archive creation and real KEK execution remain pending runtime gates because `dotnet` and the deployed custody service are unavailable in the authoring environment.

From a clean macOS checkout:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
python3 scripts/phase09_step12_verify.py
```

No database tunnel is required for this local gate and no migration is applied. Step13 has not started.

**Current acceptance:** Step12 source delivered for build/test; runtime verification remains pending.

## 20. Step13 — Controlled change review

Step13 adds a pure review policy and future-host service for key changes. Requests are bound to customer, taxpayer, fiscal memory, current key version and expected revision. Approval requires a distinct reviewer, a non-empty bounded decision reason, expires after 24 hours, and can be consumed only once. Pending, approved, rejected, consumed and expired transitions are explicit; annual fiscal-memory reset is not a review kind and receives no bypass through this service.

The local gate adds ten no-database cases covering actor separation, one-time approval, 24-hour expiry, rejection, pending/consumed/expired denial, scope and revision requirements, reason validation and annual-reset exclusion. No migration, model or snapshot change was added. Database CAS wiring, authenticated host integration and runtime execution remain pending because `dotnet` is unavailable in the authoring environment.

From a clean macOS checkout:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
python3 scripts/phase09_step13_verify.py
```

No database tunnel is required for this local gate and no migration is applied. Step14 has not started.

**Current acceptance:** Step13 source delivered for build/test; runtime verification remains pending.

## 21. Step14 — Secure API/BFF contract

Step14 adds a host-neutral HTTP security policy for future signing endpoints. It requires authentication and server-resolved scope, applies a bounded request size and per-window rate limit, requires JSON and CSRF validation for mutating requests, and marks every response `no-store`. Requests for private material or a generic signing oracle are rejected before any custody operation. The policy is not wired into the current API/BFF or exposed as a public endpoint in this step.

The local gate adds ten no-database cases covering authentication, scope, CSRF, content type, request size, rate limit, private-material denial, signing-oracle denial, no-store and invalid input. No migration, model or snapshot change was added. Real middleware integration and authenticated HTTP/PG execution remain Step16/17 acceptance work.

From a clean macOS checkout:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
python3 scripts/phase09_step14_verify.py
```

No database tunnel is required for this local gate and no migration is applied. Step15 has not started.

**Current acceptance:** Step14 source delivered for build/test; runtime verification remains pending.

## 22. Step15 — Persian RTL key-management UI

Step15 adds the authenticated console route `/keys` and a compact Persian RTL key-management workspace. It shows taxpayer/fiscal-memory scope facts, public fingerprint and lifecycle history, revision, selection status and portal-verification state. Private-key, password, DEK, KEK and export values are not represented in the component. Generate, import and rotation controls remain visibly disabled until the authenticated secure API contract and later runtime gates are enabled.

The UI uses the existing console shell, responsive table/panel layout, LTR isolation for identifiers and a security notice explaining that private material is never displayed or downloaded. The route is intentionally not wired to a new API in this step. No migration, model or snapshot change was added.

From a clean macOS checkout:

```bash
git fetch origin
git switch phase/09-cryptographic-keys-signing
git pull --ff-only
python3 scripts/phase09_step15_verify.py
```

The local gate runs the Python safety suite and the focused Node UI tests. It requires no database tunnel and performs no API, Vault or authority request. Step16 has not started.

**Current acceptance:** Step15 source delivered for build/test; runtime/browser evidence remains pending.
