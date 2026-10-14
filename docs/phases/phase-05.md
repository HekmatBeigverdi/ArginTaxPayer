# ArginTaxPayer — Phase 05: Parties and Customers

**Status:** Steps 01–11 owner approved by 2026-10-14; Step12 responsive RTL interface implemented, build and owner acceptance pending. Historical details: Steps 01–02 owner approved 2026-10-14. Step 02 six test criteria confirmed by owner (macOS/build, migration, PostgreSQL indexes/constraints). Step 03 owner approved 2026-10-14 (operator acceptance). Step 04 OWNER APPROVED 2026-10-14 (operator confirmation; automated logs not independently observed). Step 05 owner approved 2026-10-14 (owner reported tests successful). Step 06 OWNER APPROVED 2026-10-14 (owner confirms tests passed). Step 07 owner approved 2026-10-14 after operator testing. Step 08 implemented and committed; focused API tests/owner acceptance pending.
**Branch:** `phase/05-parties-customers`
**Baseline:** `docs/roadmap/18-phases.md`, `docs/phases/phase-04.md`, Phase 03 identity contracts.
**Policy:** This is the single evolving master plan. Changes to step count/scope require dated owner approval. Do not create per-step progress documents.

## Objective
Deliver natural/legal party records, economic identifiers, validation and deduplication, contacts, customer account profiles, account-to-many-taxpayer membership, customer self-management and scoped partner support. Preserve the distinction between authenticated customer account, party/counterparty and fiscal taxpayer profile.

## Fixed 15-Step Plan
| Step | Title | Deliverable | Acceptance | Status |
|---|---|---|---|---|
| 01 | Domain and API Contracts | Party/Customer/Taxpayer model, ownership matrix, migration strategy | No ambiguous identity or authorization semantics | Owner approved 2026-10-14 |
| 02 | Persistence and Migration | Entities, FKs, uniqueness, concurrency | Test DB migration reviewed; rollback and constraint tests | Owner approved 2026-10-14 |
| 03 | Natural Person Profiles | Personal identity validation and lifecycle | Invalid/duplicate data rejected | Owner approved 2026-10-14 |
| 04 | Legal Entity Profiles | Corporate identity and economic identifiers | Type-specific validation and unique rules | Owner approved 2026-10-14 |
| 05 | Validation Engine | Normalization, versioned rules, error contract | Golden and negative fixtures | Owner approved 2026-10-14 |
| 06 | Deduplication and Conflicts | Candidate matching, review, safe resolution | No silent merge or ownership change | Owner approved 2026-10-14 |
| 07 | Contacts and Addresses | Contacts, email, phone, addresses, history | Ownership and data validation enforced | Owner approved 2026-10-14 |
| 08 | Parties API | CRUD, pagination, filtering, concurrency | Authorized CRUD, negative paths, idempotency where applicable | Owner approved 2026-10-14 |
| 09 | Customer Account Profile | Profile status, party associations | No conflation of login identity and taxpayer identity | Owner approved 2026-10-14 |
| 10 | Customer-to-Many-Taxpayer Membership | Membership API and guarded linking | Customer owner scope and historic links preserved | Owner approved 2026-10-14 |
| 11 | Customer Self-Service | Account profile and taxpayer management subject to current entitlement | Unauthorized operations blocked; entitlement integration deferred to Phase 12 | Owner approved 2026-10-14 |
| 12 | Persian RTL UI | Lists, forms, search, details and accessibility | UI state, validation and route guards tested | Implemented; verification pending |
| 13 | Level1/Level2 Support Scope | Root and partner workflows | Former owner denied after transfer; global logs root only | Planned |
| 14 | Integration and Security Regression | API/DB/UI tests and concurrent operations | Cross-customer isolation and Phase03/04 regression | Planned |
| 15 | Final Hardening and Handoff | Mac regression, Ubuntu verification, risk register, signoff | Evidence recorded, owner approval; merge/tag/release separate | Planned |

## Invariants and Phase Boundaries
1. `CustomerUser` (authenticated account), `Party` (natural/legal economic actor), and `TaxpayerProfile` (fiscal subject) are distinct. A customer may have multiple taxpayers.
2. Commercial partner assignment is **customer-level**, not taxpayer-level. Root-owned customers do not produce Level2 participation. Customer ownership transfers are prospective, audited, root-only and cannot rewrite historical financial allocations.
3. Existing Phase04 `CustomerCommercialOwner` and `TaxpayerProfile.CustomerUserId` are authoritative. Never resurrect deprecated taxpayer-delegate grants.
4. Level2 accesses only currently owned customers and related parties/taxpayers; customer self-service is limited to own account. Enforce in server-side queries/commands, not UI alone.
5. Person/legal identity validation is type-aware, normalized and versioned. Deduplication produces review candidates; never silently merge accounts or taxpayers based on national ID, phone or email.
6. Avoid storing raw keys/secrets. Certificate custody remains Phase09; fiscal connectivity Phase10; actual paid-plan entitlement enforcement and finance Phase12; referral/expense approval Phase16.
7. For Phase05 self-service, implement a deny-by-default entitlement policy boundary, with only explicitly approved free operations available before Phase12. Never invent an active paid subscription.
8. Database migrations require explicit operator confirmation of `current_database() = 'argintaxpayer_test'`, never auto-apply at startup.
9. Preserve Phase03 authentication, Phase04 customer ownership and taxpayer workflows; no live SMS, public Development endpoints or production migrations.

## Test and Documentation Cadence
Every step: focused .NET and frontend tests; use `zsh scripts/dev-up.zsh`, `zsh scripts/dev-status.zsh`, `zsh scripts/dev-down.zsh` for relevant local UI/API smoke. Mac uses the existing launcher-managed AutoSSH tunnel to the VPS **test** PostgreSQL database. Never request manual tunnel setup in routine steps. Ubuntu final acceptance at Step15: `bash scripts/ubuntu-dev.sh verify`, `up`, `status`, `down`; earlier Ubuntu checks only for schema, infrastructure, cross-platform changes or unreproducible failures. No successful test may be claimed without evidence. Each step records changed paths, commit SHA, migration/DB target, macOS tests, Ubuntu status, CI, negative paths, owner signoff and blockers.

## Step 01 — Domain and API Contracts (Started)
**Status:** In progress; domain design baseline proposed, no code implementation/test claimed.
**Proposed model:** `Party` with immutable UUID, kind (NaturalPerson/LegalEntity), normalized identifiers and lifecycle; `PartyContact`/`PartyAddress`; explicit customer-party membership where needed; existing `TaxpayerProfile.CustomerUserId` retained. Party and TaxpayerProfile must not be auto-joined by matching identifiers. Preserve historical party identity through revisions and audit.
**Planned APIs:** authenticated, resource-scoped party list/create/detail/update; contact/address management; customer account profile and taxpayer membership views. Commands require optimistic concurrency and type-specific validation. Deny unauthorized foreign IDs.
**Acceptance:** Review identity distinctions, role/action matrix, duplicate handling, safe migration, self-service entitlement boundary, negative-path tests, and Phase04 compatibility before Step02.
**Migration:** None in Step01.
**macOS/Ubuntu/CI:** Not run or verified.
**Owner approval:** Approved 2026-10-14.
**Risks:** Existing customer and party schema must be inspected before selecting FK/cardinality; do not implement speculative destructive migration.

## Step 02 — Persistence and Migration (Implemented; acceptance pending)
**Status:** New EF entities, mappings, additive migration, snapshot and metadata tests committed to `phase/05-parties-customers`; no database migration was executed by the assistant.
**Changed paths:**
- `src/ArginTaxPayer.Infrastructure/Persistence/Parties/Party.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Parties/CustomerPartyLink.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/ArginTaxPayerDbContext.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261014090000_AddPartiesAndCustomerPartyLinks.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/ArginTaxPayerDbContextModelSnapshot.cs`
- `tests/ArginTaxPayer.Tests/PartyPersistenceMetadataTests.cs`

**Data model:** Party holds immutable UUID, natural/legal kind, normalized identifier, display name, status, optimistic concurrency revision and UTC audit timestamps. Database uniqueness on (kind, normalized_identifier) is deliberate pending Phase05 identifier policy review. CustomerPartyLink stores explicit customer identity FK and Party FK, keeps unlink history, restricts deletes, and prevents duplicate *active* links. Phase04 taxpayer and commercial ownership schema is unchanged. No automatic taxpayer-to-party matching or legacy backfill.

**Migration safety:** Additive schema only; does not touch customer ownership, taxpayers, identities, or financial history. PostgreSQL migration `20261014090000_AddPartiesAndCustomerPartyLinks` has Down operations for newly created tables; treat these as destructive if data is subsequently written. The operator must first verify `current_database() = 'argintaxpayer_test'`; back up test data when appropriate. Never run against production. No auto migration at runtime.

**Security:** No endpoints are exposed in this step. Authorization will be enforced when CRUD/membership APIs are introduced. Persistence FKs restrict accidental deletion; UI filtering alone is never sufficient.

**Verification instructions supplied for macOS:**
```bash
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~PartyPersistenceMetadataTests
```
Before applying migrations, confirm the **test** DB explicitly:
```bash
# Use the project-wide tested Npgsql field extraction in docs/roadmap/18-phases.md
psql -X -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d "$DB_NAME" -W -Atqc 'SELECT current_database();'
```
The returned database name must be exactly `argintaxpayer_test`. Inspect the generated EF SQL script and migration history before explicit apply. CLI environment variable names may differ from the developer's local launcher; never echo a connection string containing secrets. An optional PostgreSQL integration test must assert unique (kind, identifier), active customer link dedupe, FK restriction and rollback.

**Actual results (owner report 2026-10-14):** All six acceptance criteria succeeded: both table schemas, recorded EF migration, indexes/foreign keys, Release build, focused tests, and a rollback-only PostgreSQL uniqueness check. The owner applied the migration to the dedicated `argintaxpayer_test` database. Original terminal logs were not independently captured by the assistant; this is owner-reported verification, not independently observed execution. Ubuntu: final phase verification deferred; CI: not independently verified.
**Owner approval:** Approved 2026-10-14. **Commit before approval:** `7b817911dce4c07e1b0c13104196dbefca51260c`.
**Operator correction:** Parsing the Npgsql connection string as a libpq DSN caused a previous test command failure. Use the canonical `npgsql_value` helper, exact Host/Port/Database/Username extraction, and `psql -W` recorded in `docs/roadmap/18-phases.md` in all future steps/phases; ensure `current_database() = 'argintaxpayer_test'`.
**Residual risk:** Type-specific identifier normalization and legal national-ID rules are Step03–05. Global uniqueness may need an owner-approved policy change for special counterparty identifiers; do not silently change it.

## Step 03 — Natural Person Profiles (Owner approved 2026-10-14)
**Status:** Implementation committed to `phase/05-parties-customers`; owner-pushed validation correction preserved (commit `8ee5c054e55a1279d96fcde8b25789dea1e621b4`). No claim of executed tests, schema application or owner signoff.
**Files:**
- `src/ArginTaxPayer.Domain/Parties/NaturalPersonProfilePolicy.cs` (owner correction: reject control characters in original input)
- `src/ArginTaxPayer.Infrastructure/Persistence/Parties/Party.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Parties/NaturalPersonService.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/ArginTaxPayerDbContext.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261014110000_AddNaturalPersonNames.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/ArginTaxPayerDbContextModelSnapshot.cs`
- `tests/ArginTaxPayer.Tests/NaturalPersonProfilePolicyTests.cs`
- `tests/ArginTaxPayer.Tests/PartyPersistenceMetadataTests.cs`

**Behavior:** Natural-person input validation reuses Phase04 Iranian national-code checksum and Unicode digit normalization, requires first/last names, rejects control characters and bad lengths; never claims authoritative government verification. New nullable `first_name`/`last_name` columns on `parties` preserve legal-person records and old rows. Scoped service handles creation (Party + CustomerPartyLink in serializable transaction), read, update and archival with expected revision, active customer association, live root/partner commercial owner, admin MFA, duplicate database unique-conflict handling and restrictive foreign keys. Customer self-service writes are deliberately blocked until Phase12 plan enforcement. If a Party is actively linked to another customer, mutation is denied pending Phase06 conflict-resolution policy. Archived parties cannot be restored or edited by this service.
**Boundary:** No public endpoint in Step03: publication belongs to Step08; no new taxpayer or certificate changes, no forced legacy name splitting/automatic deduplication, no money flows changed.
**Migration:** `20261014110000_AddNaturalPersonNames` is additive and must be explicitly applied ONLY to verified `argintaxpayer_test`. No schema migration executed by assistant. Any Down would discard structured names and requires review.
**Verification:** On Mac after pulling, run
```bash
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~NaturalPersonProfilePolicyTests|FullyQualifiedName~PartyPersistenceMetadataTests"
```
Use the approved `npgsql_value` extraction from `docs/roadmap/18-phases.md`, `psql -W` and `SELECT current_database()` before inspecting history/DDL. Generate EF SQL for `20261014090000_AddPartiesAndCustomerPartyLinks` → `20261014110000_AddNaturalPersonNames`, review then explicitly apply target migration, verify nullable columns and history. No connection strings/passwords in logs. Negative-path database/service integration tests of duplicate codes, stale revision, former partner isolation, shared party denial and rollback remain a gate before owner signoff.
**Actual verification:** macOS automated: not observed; test DB migration: not applied by assistant; Ubuntu: deferred pending schema-related focused operator verification; CI: not checked. **Owner approval:** Approved 2026-10-14; implementation and test history above retained. Operator supplied approval; assistant did not independently execute migration or tests.

## Step 04 — Legal Entity Profiles (Implementation committed; acceptance pending)
**Scope:** Legal-entity identity validation, registration number, guarded create/read/update/archive and EF persistence. The step follows the approved 15-step plan and leaves HTTP publication until Step08.

**Changes:**
- `src/ArginTaxPayer.Domain/Parties/LegalEntityProfilePolicy.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Parties/LegalEntityService.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Parties/Party.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/ArginTaxPayerDbContext.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261014123000_AddLegalEntityRegistrationNumber.cs`
- `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/ArginTaxPayerDbContextModelSnapshot.cs`
- `tests/ArginTaxPayer.Tests/LegalEntityProfilePolicyTests.cs`
- `tests/ArginTaxPayer.Tests/PartyPersistenceMetadataTests.cs`

**Contracts and behavior:** Reuse Phase04 11-digit legal identifier normalization (Persian, Arabic and Latin numerals) without falsely claiming authoritative registry verification. Required registered name is normalized Unicode NFC, non-blank, 2–256 characters and rejects raw controls; optional registration number accepts only 1–32 normalized ASCII digits when supplied, and never serves as a substitute for national identifier. No VAT rate, official tax confirmation or registry status is assumed. Unique `(kind, normalized_identifier)` index from Step02 prevents duplicate economic actors. No silent party merge or ownership reassignment.
**Authorization and lifecycle:** EF-backed `LegalEntityService` is NOT a public endpoint. Root/Level2 must hold active role + MFA and live commercial ownership of the target customer. Customer writes are blocked until Phase12 entitlement boundary. Party and link creation run together inside a serializable transaction. Read is scoped to active customer links. Update/archive require expected revision and deny archived entities, stale versions or a shared active party; no physical deletes. Phase04 commercial ownership and taxpayer tables remain untouched.
**Migration:** Additive `20261014123000_AddLegalEntityRegistrationNumber` adds nullable `registration_number VARCHAR(32)` to `parties`. No backfill or automated runtime migration. Down would drop data and is not a routine rollback strategy.
**Mac verification:**
```bash
git switch phase/05-parties-customers
git pull --ff-only origin phase/05-parties-customers
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~LegalEntityProfilePolicyTests|FullyQualifiedName~PartyPersistenceMetadataTests|FullyQualifiedName~NaturalPersonProfilePolicyTests"
```
Use the owner-approved `npgsql_value` parsing of `.env.dev.local`, `psql -W`, and actual `SELECT current_database()` guard from `docs/roadmap/18-phases.md`. Inspect EF SQL from `20261014110000_AddNaturalPersonNames` to `20261014123000_AddLegalEntityRegistrationNumber`, explicitly apply to verified `argintaxpayer_test`, inspect `\\d+ parties` and `__EFMigrationsHistory`. The PostgreSQL negative-path tests for uniqueness, scope reassignment, concurrency and rollback are required before owner acceptance.
**Evidence:** Build/tests not executed by assistant, PostgreSQL migration not applied by assistant, Ubuntu final phase verification deferred, CI not verified. **Owner signoff:** Approved by owner 2026-10-14; actual migration and test logs not independently available to assistant.

## Step 05 — Shared Validation Engine (Implementation complete; owner acceptance pending)
**Status:** Implemented and pushed to `phase/05-parties-customers`. All code delivery is a single step. No migration and no changes to Phase03/04 persistence, identity or ownership.
**Changed:**
- `src/ArginTaxPayer.Domain/Parties/PartyValidationEngine.cs` (new)
- `src/ArginTaxPayer.Domain/Parties/NaturalPersonProfilePolicy.cs` (compatibility facade)
- `src/ArginTaxPayer.Domain/Parties/LegalEntityProfilePolicy.cs` (compatibility facade)
- `tests/ArginTaxPayer.Tests/PartyValidationEngineTests.cs` (new golden/negative fixtures)

**Contract:** `PartyValidationEngine.RuleVersion = "phase05.party.v1"`. Entry points `ValidateNatural(nationalCode, firstName, lastName)` and `ValidateLegal(nationalIdentifier, registeredName, registrationNumber)` return `PartyValidationResult` with `Version`, `IsValid`, zero-or-more `PartyValidationIssue(Field, Code)`, and exactly one typed payload when valid. Stable codes: `party.validation.required`, `party.validation.identifier_invalid`, `party.validation.length_invalid`, `party.validation.control_character`, `party.validation.digits_only`. API layer in Step08 may translate error codes to Persian without leaking raw PII. No external authority confirmation is implied.
**Normalization:** Single Phase04 `IranianTaxpayerIdentifier` normalization/checksum source; Latin/Persian/Arabic decimal input to ASCII, never strip punctuation from national IDs. Names trim and Unicode NFC normalize; reject original control characters *before* Trim (preserves operator/Codex Step03 fix), reject blanks and out-of-policy lengths. Registration number optional, otherwise digits only, 1–32 ASCII-normalized, with null distinct from invalid whitespace. Independent field errors aggregate deterministically.
**Backwards compatibility:** Existing `NaturalPersonProfilePolicy.TryValidate` and `LegalEntityProfilePolicy.TryValidate` retain their signatures and typed results; both now invoke central engine. Legal ID is structural-only and NOT asserted as registry-verified. Business duplicate/conflict resolution remains Step06; this is a pure stateless validation engine, no database writes.
**Fixtures:** Natural-person Persian/Arabic digit normalization, legal-entity number normalization, field-level multi-error, control characters (including trailing newline), invalid national IDs and registration formats, and compatibility-facade equivalence. Existing Step03/04 tests must also pass.
**macOS:**
```bash
git switch phase/05-parties-customers
git pull --ff-only origin phase/05-parties-customers
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~PartyValidationEngineTests|FullyQualifiedName~NaturalPersonProfilePolicyTests|FullyQualifiedName~LegalEntityProfilePolicyTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
```
**Migration:** NONE; therefore no new PostgreSQL DDL or schema apply for Step05. **Ubuntu:** Deferred to Step15 unless OS-specific failure. **CI/actual test execution:** Not independently verified. **Owner approval:** Owner approved 2026-10-14; run logs not independently observed.
**Known boundary:** Official rule changes must create a new rule-version implementation instead of silently changing historical validation expectations; any future field-level rule changes require versioned fixtures and documented impact.

## Step 06 — Deduplication and Conflicts (Implementation committed; acceptance pending)
**Owner authorization:** Step05 approved 2026-10-14. **Step06 state:** Code and additive EF migration pushed, awaiting focused tests, database migration, and owner signoff.
**Files:** `src/ArginTaxPayer.Infrastructure/Persistence/Parties/PartyDuplicateReview.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/Parties/PartyDeduplicationService.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/ArginTaxPayerDbContext.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261014143000_AddPartyDuplicateReviews.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/ArginTaxPayerDbContextModelSnapshot.cs`, `tests/ArginTaxPayer.Tests/PartyDuplicateReviewMetadataTests.cs`.
**Candidate finding:** Requires active Level1/Level2 identity with MFA and currently authorized commercial customer ownership. Retrieve target only through active customer-party link. Search same-kind exact normalized identifier or exact normalized display name among the *same customer's* active links; cap results at 50, stable ordering. Never query/return other customers' party records. Potential matches are review-only; no automatic merge. A global unique index from Step02 continues rejecting duplicate kind+identifier and may cause conflicts across customers without disclosing third-party details; review and linking decisions remain explicit future work.
**Review decision:** `ConfirmedDuplicate` or `NotDuplicate` with reason (3–500 chars), reviewer, customer, two different party IDs and UTC timestamp in append-only `party_duplicate_reviews`; transactional check both currently linked to same customer and same kind; uses PostgreSQL serializable transaction. Reviews **do not** alter Party, CustomerPartyLink, TaxpayerProfile, customer ownership or financial history. Previous reviews are retained as history; no destructive auto resolution. No HTTP routes until Step08.
**Migration:** `20261014143000_AddPartyDuplicateReviews` creates table with foreign keys to both parties, reviewer and customer, status/reason constraints, and customer/time index. Zero changes to existing Party or Phase04 tables. Applying migration is operator-owned, *only* after verifying actual `current_database() = 'argintaxpayer_test'`; use established `npgsql_value` helper and `psql -W`.
**Focused Mac tests:**
```bash
git switch phase/05-parties-customers
git pull --ff-only origin phase/05-parties-customers
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~PartyDuplicateReviewMetadataTests|FullyQualifiedName~PartyPersistenceMetadataTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
```
**EF SQL review:**
```bash
dotnet ef migrations script 20261014123000_AddLegalEntityRegistrationNumber 20261014143000_AddPartyDuplicateReviews --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api --output /tmp/phase05-step06.sql
```
After reviewing SQL and actual database name, apply target migration explicitly; verify table schema with `psql -W -c '\\d+ party_duplicate_reviews'` and history table. Test duplicate FK, distinct-party constraint, scoped negative paths and immutable history; require postgres integration evidence for accepted step. A transaction rollback test must not leave fixture records.
**Evidence:** Build/test/CI not independently run; migration not applied by assistant; Ubuntu phase gate remains Step15 with focused earlier checks only if needed. **Owner signoff:** Approved 2026-10-14 (owner-reported success; raw logs not independently observed).
**Important limitations:** Cross-customer deduplication is deliberately not exposed (privacy). Name matching is exact rather than fuzzy and is a *candidate*, never proof. No physical merge workflow authorized in this step.

## Step 07 — Contacts and Addresses (Implementation committed; acceptance pending)
**Owner authorization:** Step06 owner approved after reporting local tests successful on 2026-10-14.
**Scope:** Structured, multiple contacts and addresses for a Party with at most one active primary of each contact kind and one active primary address. Preserve history through soft deactivation. Changes are additive and do not alter taxpayer, customer commercial ownership, financial, or identity records.
**Changed:** `src/ArginTaxPayer.Infrastructure/Persistence/Parties/PartyContact.cs`, `PartyAddress.cs`, `PartyContactAddressService.cs` (same folder), `src/ArginTaxPayer.Domain/Parties/PartyContactAddressValidation.cs`, EF DbContext and ModelSnapshot, `src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261014160000_AddPartyContactsAndAddresses.cs`, `tests/ArginTaxPayer.Tests/PartyContactAddressTests.cs`.
**Fields and rules:** `party_contacts` stores party FK, contact kind (mobile, telephone, email), normalized value, optional named contact person, primary/active flags, Revision concurrency, creation/update/deactivation UTC. `party_addresses` stores party FK, label, ISO two-letter country, province, city, street, optional postal code, primary/active, Revision and UTC lifecycle. Validate original control characters before normalization (owner's Codex correction); normalize Iranian/Arabic phone and postal digits, lower-case email, NFC-normalize Persian address/name fields. Iranian postal codes are required to be ten digits *when supplied*, but nullable by design. Do not confuse contact addresses with authentication email/mobile or fiscal taxpayer identity.
**Security:** Service requires authenticated active Level1 or Level2 with MFA and current customer commercial ownership; link must be active to this party, and Party must be active. Shared multi-customer parties are not editable through one customer. No unauthorized customer self-service writes until Phase12 entitlement policy. List filters active records by scoped party. Create/edit/deactivate are guarded by expected revision and serializable transaction; no routine physical deletions. Primary and duplicate-active contact rules are enforced through partial unique PostgreSQL indexes; conflicting writes fail closed.
**Migration:** `20261014160000_AddPartyContactsAndAddresses`, additive two tables with Party restrictive FKs, uniqueness, basic checks, lookup indexes; no automatic execution or backfill. Down would discard history; avoid after data is written.
**Mac local acceptance:**
```bash
git switch phase/05-parties-customers
git pull --ff-only origin phase/05-parties-customers
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~PartyContactAddressTests|FullyQualifiedName~PartyPersistenceMetadataTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
```
**Migration acceptance:** use `scripts/dev-up.zsh` for tunnel and the canonical `npgsql_value` + `psql -W` approach documented in `docs/roadmap/18-phases.md`. Confirm actual `current_database() = 'argintaxpayer_test'` and the prior migration applied. Generate and inspect EF script between `20261014143000_AddPartyDuplicateReviews` and `20261014160000_AddPartyContactsAndAddresses`. Then explicitly apply target migration on TEST only; verify `\\d+ party_contacts`, `\\d+ party_addresses`, and `__EFMigrationsHistory`. A rollback-only fixture should test duplicate active primary, duplicate active contact value, FK restriction and soft-deactivation.
**Not yet verified:** Local build/tests and PostgreSQL schema have not been run by assistant; focused service negative-path integration (former partner, wrong customer, stale Revision, concurrent primary updates) remains a signoff gate. Ubuntu final verification in Step15, or earlier if platform-specific issue. No Step07 HTTP endpoints; Step08 owns API publication. **Owner approval:** Approved by owner 2026-10-14; operator-reported tests successful, no independent raw terminal logs.

## Step 08 — Parties API (Implementation committed; acceptance pending)
**Owner context:** Step07 owner approved 2026-10-14. No new database schema in Step08.
**Changed files:** `src/ArginTaxPayer.Api/Parties/PartyDevelopmentEndpoints.cs`; `src/ArginTaxPayer.Api/Program.cs`; `src/ArginTaxPayer.Infrastructure/Persistence/Parties/PartyDirectoryService.cs`; `tests/ArginTaxPayer.Tests/PartyApiContractTests.cs`.
**Exposure:** All routes are mapped **only** when `Development` and PostgreSQL connection configured, and explicitly deny non-loopback callers at handler entry. Not exposed in Production or database-free hosts. Registration uses scoped DI for NaturalPersonService, LegalEntityService, PartyContactAddressService, PartyDirectoryService and PartyDeduplicationService. No public reverse proxy forwarding assumptions. Production activation requires separate review.
**Endpoints (all under `/api/dev/customers/{customerId}/parties`):**
- `GET /`: customer-scoped stable ordering, page (default 1), pageSize (default 20, max 100), optional name/identifier query (max 100 chars) and kind, total count, empty out-of-range page.
- `POST /natural`, `POST /legal`: create validated party with customer link, HTTP 201.
- `GET /{partyId}/natural`, `GET /{partyId}/legal`: type-specific details, never arbitrary cross-customer lookup.
- `PUT /{partyId}/natural`, `PUT /{partyId}/legal`: expectedRevision optimistic concurrency; archive flag implements soft archival, no hard delete.
- `GET /{partyId}/details`: active contacts and addresses.
- `POST /{partyId}/contacts`, `PUT /{partyId}/contacts/{id}?expectedRevision=N`, `DELETE /{partyId}/contacts/{id}?expectedRevision=N`: create/update/deactivate.
- `POST /{partyId}/addresses`, `PUT /{partyId}/addresses/{id}?expectedRevision=N`, `DELETE /{partyId}/addresses/{id}?expectedRevision=N`: create/update/deactivate.
- `GET /{partyId}/duplicates`, `POST /duplicates/reviews`: restricted review candidates / append-only review decision without automatic merge.
**Safety:** HTTP handlers map unauthenticated to 401, forbidden 403, invalid 400, absent 404, stale revision and uniqueness violations 409. Services enforce live MFA + role + current customer ownership, active association and cross-customer conflict policy; self-service writes remain deny-by-default until Phase12 entitlements. No raw connection secrets, official registry claims or identity/ownership auto merge.
**Idempotency:** Creation is protected by unique party-identifier index but does not offer HTTP Idempotency-Key replay yet. Calls with equivalent requests can produce 409; retry-safe client semantics should be evaluated before any production exposure. Client-controlled expectedRevision required for updates.
**Focused Mac:**
```bash
git switch phase/05-parties-customers
git pull --ff-only origin phase/05-parties-customers
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~PartyApiContractTests|FullyQualifiedName~PartyContactAddressTests|FullyQualifiedName~PartyValidationEngineTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
```
**Manual HTTP smoke:** Use existing `zsh scripts/dev-up.zsh`; with no token, `GET /api/dev/customers/{customerId}/parties` must return 401. Authorized Level1 with MFA/root ownership or current Level2 partner should list, create and update only their customer's records, while former partner/wrong customer must receive denial. Attempt duplicate identifier, stale revision, repeated primary contact and foreign party IDs; expect 409/403/404 without information leakage. No live SMS used.
**Migration:** NONE in Step08. Existing Step07 PostgreSQL migration is prerequisite. **Actual evidence:** commit only; build, HTTP smoke, macOS tests, Ubuntu and CI not independently executed. **Owner acceptance:** Pending; targeted integration tests and idempotency recovery are residual risk gates.

## Step Completion Record Contract
Append Step02–15 completion records here with scope, paths, commit, migration, local focused test evidence, Ubuntu verification/deferment, CI, security negative tests, owner acceptance and unresolved blockers. Phase close requires separately approved main merge, tag and GitHub Release.

## Step 09 — Customer Account Profile (Implementation committed; acceptance pending)
- Step08 owner approved 2026-10-14.
- New `CustomerAccountProfile` entity and `CustomerAccountProfileService` maintain independently versioned business display name, Active/Suspended profile status, and optional PreferredPartyId. No login credential or TaxpayerProfile changes. Status does not activate/deactivate IdentityUser.
- Customer may read own profile; Level1 or current Level2 partner administrators with MFA can read or write within live commercial ownership scope. Self-service writes deferred.
- Create/upsert expects revision 0 for first creation and expected revision thereafter. PreferredPartyId must be actively linked to same customer. Invalid/stale revisions fail closed.
- Development-loopback GET/PUT endpoints: `/api/dev/customers/{customerId}/profile`. Not exposed in production.
- Additive migration `20261014173000_AddCustomerAccountProfiles` and model snapshot. Migration must only target verified `argintaxpayer_test`, via canonical `npgsql_value` + `psql -W` workflow. Never auto-apply.
- Changed: `src/ArginTaxPayer.Infrastructure/Persistence/Customers/`, `src/ArginTaxPayer.Api/Customers/`, EF DbContext/snapshot/migration, API Program, and `tests/ArginTaxPayer.Tests/CustomerAccountProfileTests.cs`, `CustomerProfileApiTests.cs`.
- Focused macOS: `dotnet build ArginTaxPayer.sln -c Release`, then `dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~CustomerAccountProfileTests|FullyQualifiedName~CustomerProfileApiTests"`. Follow with full regression. Manually test owner transfer, foreign preferred party, revisions and FK constraints.
- Build, PostgreSQL migration, API smoke, integration tests, Ubuntu and CI were not executed by assistant. Owner signoff pending.

## Step 10 — Customer-to-Many-Taxpayer Membership (Implemented; acceptance pending)
**Step09 owner acceptance:** Confirmed by owner 2026-10-14.
**Design:** Customer account can have multiple fiscal taxpayer profiles. The Phase04 authoritative `TaxpayerProfile.CustomerUserId` is never changed. New explicit `customer_taxpayer_memberships` tracks opt-in active relation plus unlinked history, actor IDs and UTC timestamps. Only allow link if existing TaxpayerProfile.CustomerUserId is exactly the requested customer. This prevents automatic migration of taxpayers between customers, reassignment of commercial partner, or rewriting past transactions. A previously unlinked membership can be re-linked as a new row, preserving prior history.
**Implementation:** `CustomerTaxpayerMembership`, scoped `CustomerTaxpayerMembershipService` (list active/all, link, unlink), `CustomerTaxpayerMembershipEndpoints` under `/api/dev/customers/{customerId}/taxpayer-memberships`, DI registration, EF mapping/snapshot, additive migration `20261014190000_AddCustomerTaxpayerMemberships`, and `CustomerTaxpayerMembershipTests`.
**Security:** Authenticated active customer can read own scoped memberships; writes require current Level1/Level2 owner with active MFA. Endpoints Development + loopback only. Non-members, former partners and unauthorized foreign IDs denied. The service rechecks authorization and taxpayer ownership inside serializable transactions. Deactivation requires expected Revision and records UnlinkedByUserId/UnlinkedAtUtc, never physically deletes rows.
**Database:** Restrictive FKs to customer, taxpayer, linking actor, unlinking actor; unique partial index on (customer_user_id,taxpayer_id) WHERE unlinked_at_utc IS NULL; check constraints on revision and unlink timestamp/actor consistency. Existing TaxpayerProfile records are NOT automatically linked or altered; those need deliberate authorized linking. The standalone FK does not enforce matching TaxpayerProfile.CustomerUserId; application transaction enforces the invariant and future security testing should evaluate DB-level composite FK for defense in depth.
**Mac verification:**
```bash
git switch phase/05-parties-customers
git pull --ff-only origin phase/05-parties-customers
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~CustomerTaxpayerMembershipTests|FullyQualifiedName~CustomerAccountProfileTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
```
**Test database migration:** Explicitly verify `current_database() = 'argintaxpayer_test'` using the owner-approved `npgsql_value` parsing and `psql -W` described in the canonical roadmap. Generate and inspect SQL from `20261014173000_AddCustomerAccountProfiles` to `20261014190000_AddCustomerTaxpayerMemberships`, then apply only on verified TEST database. Inspect `\\d+ customer_taxpayer_memberships` and EF migration history. No automatic migration by runtime.
**Required HTTP negative tests:** unauthed 401, wrong customer 403, nonexistent/foreign taxpayer 404, repeated link 409, unlink with stale Revision 409, customer GET own list 200, no production exposure; verify history retained and Phase04 ownership unchanged. **Execution:** Not independently tested by assistant; CI/Ubuntu pending normal phase gates. **Owner signoff:** Pending.

## Step 11 — Customer Self-Service (Implementation committed; acceptance pending)
**Owner signoff for Step10:** Confirmed by owner 2026-10-14. The membership migration and prior test results were owner-reported; not independently observed by the assistant.
**Scope:** Read-only, server-authorized customer self-service dashboard. Keep `IdentityUser`, `CustomerAccountProfile`, `Party` and `TaxpayerProfile` strictly distinct. A login never gains administrative rights or paid-plan entitlements by having a profile, party link or taxpayer association.
**Implementation paths:** `src/ArginTaxPayer.Domain/Customers/CustomerSelfServicePolicy.cs`, `src/ArginTaxPayer.Infrastructure/Persistence/Customers/CustomerSelfServiceService.cs`, `src/ArginTaxPayer.Api/Customers/CustomerSelfServiceEndpoints.cs`, `src/ArginTaxPayer.Api/Program.cs`, `tests/ArginTaxPayer.Tests/CustomerSelfServicePolicyTests.cs`, `CustomerSelfServiceApiTests.cs`.
**Policy:** Version `phase05.selfservice.v1` is explicit allowlist. Only own-dashboard, own-profile, own-taxpayer and own-membership reads are free. Profile edits, fiscal taxpayer create/edit, linking/unlinking taxpayers and invoice submission are categorically denied to the customer before Phase12 entitlement implementation. Unknown actions also fail closed. No feature flag or pseudo-subscription may silently grant these operations.
**API:** GET `/api/dev/customer/self-service` on Development, configured database, local loopback only. Caller identity comes from validated Bearer session, never a supplied customer ID. Requires active IdentityUser and Customer role. Response includes own customer ID, optional business profile name/status/linked preferred Party ID, list of taxpayer IDs/kind/display name/status/revision filtered by authoritative `TaxpayerProfile.CustomerUserId`, count of active same-customer memberships, policy version, and `writesEnabled=false`. No raw national ID, email, mobile, certificate material or unrelated party is returned. Existing customer profile and membership reads continue to use prior scoped endpoints; no new write endpoints.
**Migration:** NONE. Uses existing Phase04 and Phase05 Step09–10 schema; do not reapply migrations.
**macOS tests:**
```bash
git switch phase/05-parties-customers
git pull --ff-only origin phase/05-parties-customers
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter "FullyQualifiedName~CustomerSelfServicePolicyTests|FullyQualifiedName~CustomerSelfServiceApiTests|FullyQualifiedName~CustomerTaxpayerMembershipTests"
dotnet test ArginTaxPayer.sln -c Release --no-build
```
**HTTP acceptance:** With `zsh scripts/dev-up.zsh`, verify GET `/api/dev/customer/self-service` returns 401 without bearer, 403 for non-customer administrator, 200 for active customer; verify returned taxpayer IDs all belong to the authenticated customer and `writesEnabled=false`. Re-test with two distinct customer sessions; no cross-customer data. Verify POST/PUT on this route are unmapped (405 or 404) and authenticated customer cannot use administrator membership/Profile mutation routes.
**Known deferred work:** UI is Step12; full production entitlement provider and permitted paid operations are Phase12. Server authorization and future entitlement gates must be integration-tested before production release.
**Execution state:** Code and focused test fixtures committed; macOS build, HTTP smoke, cross-customer integration, Ubuntu and CI not independently executed. **Owner acceptance:** Pending.

## Step 12 — Persian RTL UI (Code committed; acceptance pending)
**Prior step:** Step11 approved by owner 2026-10-14.
**Added:** `apps/web/app/parties/page.tsx`, `apps/web/app/parties/party-workspace.tsx`, `apps/web/app/api/party-workspace/[...path]/route.ts`, `apps/web/tests/phase05-step12-ui.test.mjs`; updated `apps/web/app/globals.css`.
**Route:** `/parties`. Next.js server page checks `identityUiEnabled` and current session, permits Customer/Level1/Level2 role, redirects other users to account. Customer gets a read-only dashboard from the Step11 own-identity endpoint; no impersonated customer identifier, no customer mutation forms. Administrator has customer UUID field, server-scoped paginated Party list, kind/name/id search, active profile summary, type-specific create/edit forms, and contact/address entry with existing backend validation. Persian labels, RTL layout, LTR input IDs/phone/email and responsive layout.
**Proxy:** Same-origin `/api/party-workspace` BFF uses HTTP-only cookie on server, exact path/method allowlist and local-only API origin from existing `identity.ts`. Mutations enforce same-origin request check, JSON bounded length, stripped/allowlisted query parameters, no wildcard backend URL and no client-controlled bearer. Backend still enforces current MFA/owner/role and type validation. No production API exposure is added.
**Test contract:** Node static checks cover route role guard, RTL, proxy restrictions, customer read-only view, error/loading/pressed states and responsive CSS. These are regression fixtures, NOT proof of all browser functionality. Require manual macOS browser functional check for Customer vs Level1 vs Level2, two customer identities, invalid Party, stale Revision, paginated search, responsive mobile width and keyboard focus. Run `npm --prefix apps/web run typecheck`, `npm --prefix apps/web test`, `npm --prefix apps/web run build`; also `dotnet build ArginTaxPayer.sln -c Release` and `dotnet test ArginTaxPayer.sln -c Release --no-build`. Existing `zsh scripts/dev-up.zsh` for local smoke.
**Migration:** None. No production deployment, no new paid entitlement, no unreviewed identity/ownership changes. **Actual verification:** assistant committed code but did not run frontend build/browser or integration tests; owner acceptance pending.

## Step 12 — Owner UI/UX Rework (2026-10-14; verification pending)
**Feedback:** Owner rejected the prior dashboard and nested-tab login UX; requested professional Persian styling, separate administrative vs general-customer navigation, proper discoverability of Parties and Customers, and full-screen transition lock during sign-in. This work is a correction to Step12, NOT Step13 acceptance or any new roadmap step.
**Changes:** `apps/web/app/dashboard/page.tsx` (new role-aware dashboard); `apps/web/app/identity/page.tsx`, `identity/identity-flow.tsx` (simple login, no nested tabs, MFA retained, permanent navigation state lock until route unmount); `apps/web/app/page.tsx` (redirect authenticated users to dashboard); `apps/web/app/globals.css` (responsive RTL design system, optional Vazirmatn via Google Fonts stylesheet with local Tahoma fallback, focus states, full-screen wait layer); header links for `/account`, `/users`, `/parties`. `apps/web/app/parties/party-workspace.tsx` now offers a selector populated from existing server-scoped `/api/customer-owners/accounts`, overview of that customer's taxpayers, and explicit Level1-only new-taxpayer form using existing /api/taxpayers/new endpoint. `apps/web/app/parties/page.tsx` passes capability from verified user role. Added regression fixture `apps/web/tests/dashboard-redesign.test.mjs`.
**Dashboard navigation:** `/dashboard` is the authenticated landing destination. Level1/Level2 admins see software-user management (`/users`), buyer/person/taxpayer operations (`/parties`), customer ownership, administration/security and placeholders for finance and invoices. Customer sees own parties and taxpayers (`/parties`), profile (`/account`), and visible but **disabled** modules for goods, invoice templates/sending/list, settings, plans and financial transactions. User requirements for single-taxpayer 1–12-month plans and annual multi-taxpayer quota are design constraints **only**, not fake enforced entitlements; Phase12 business implementation remains future.
**Safety:** Existing authentication and MFA are unchanged. A login request remains locked while pending; on successful cookie/session establishment, `navigating` stays true, form is disabled, full-viewport status overlay covers all interaction, and route transitions to /dashboard without re-enabling submit. Role and customer checks occur on server; client UI does not substitute for them. Party creation is restricted by existing backend commercial owner policy. Fiscal taxpayer creation remains **Level1 + MFA** only, respecting Phase04; Level2 cannot silently gain a new privilege. No database migrations, auto merges, payments or genuine fiscal invoices.
**Acceptance/test checklist:** macOS `npm --prefix apps/web run typecheck`, `npm --prefix apps/web test`, `npm --prefix apps/web run build`, `dotnet test ArginTaxPayer.sln -c Release`; manually verify MFA and password/OTP login, submit cannot be clicked during route transition, direct /dashboard access based on role, both admin identities vs two customers, customer account dropdown, natural/legal person create, Level1 taxpayer create, Level2 taxpayer creation blocked by backend, privacy and responsive keyboard layout, inactive modules non-clickable. Confirm Persian Vazirmatn loads when font CDN accessible; fallback works without network.
**Verification status:** GitHub commits confirmed. Assistant could not clone repository into its test container because outbound DNS resolution of github.com failed. No local TypeScript/build, Node tests, browser, or API integration execution attested. Owner approval of reworked Step12 pending.
