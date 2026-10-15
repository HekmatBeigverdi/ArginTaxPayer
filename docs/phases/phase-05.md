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

## Step 12 — Unified Console Shell & Buyer/Taxpayer UX Overhaul (2026-10-14; owner acceptance pending)
**Owner-approved visual design:** One fixed RTL green/teal Next.js shared shell, persistent right sidebar/topbar; route changes update the center content only. A matching login palette, avatar dropdown with profile/security and real POST /api/identity/logout, compact ERP-grade tables, and buyers/taxpayers list with kind filters and independent create actions. Corrections replace previous Step12 rendering; no new roadmap step is added.

**Architecture:** New `apps/web/app/(console)/layout.tsx` server guard checks the existing validated identity session and renders `apps/web/app/components/console-shell.tsx` once. `/dashboard`, `/users`, `/parties`, `/account` moved into this route group without changing their public URLs. Their old page files removed to avoid duplicate conflicting routes. The layout's client sidebar survives navigation, active menu derives from pathname, avatar dropdown dismisses on outside click/Escape, and logout reuses the existing secure server proxy. No new backend authorization privileges.

**Buyers/taxpayers:** `apps/web/app/parties/buyer-taxpayer-directory.tsx` is the default list, sourced through existing scoped `/api/customer-owners/accounts`, `/api/customer-owners/{customerId}/taxpayers` and the secured party listing API. Filters include buyer vs taxpayer, individual vs company, text query and pagination. Unknown buyer identity kind is explicitly displayed as undefined when no linked Party exists. Actions are separate: buyer login account via existing `AdminCreateCustomer` and guided next-step create natural/legal Party; taxpayer creation limited to Level1+MFA by *existing backend rules*, followed by selected taxpayer fiscal-memory/metadata workspace. Level2 sees scoped records but cannot bypass existing Level1-only taxpayer creation. Customer role sees only its own taxpayer list. No fictitious tax gateway connection status or private key handling; actual fiscal-memory code registration uses existing Phase04 endpoints.
**Performance note:** The UI currently joins scoped accounts/Party/taxpayer sources client-side in bounded groups of six requests. At significant scale replace this with a server-aggregated, paginated, authorization-scoped endpoint, without loading global records or weakening permission checks. Buyer rows are displayed separately for each actively associated Party with its real natural/legal kind; accounts without Party are explicitly unclassified. Formal BuyerType, if separate from Party kinds, must be specified in a later data-model amendment.

**Styling and accessibility:** Unified Vazirmatn/Tahoma design, teal accent, no nested page shell, responsive sidebar, readable headings, compact tables, keyboard focus, dropdown roles and mobile layout. Login still protects full-screen navigation between successful authentication and dashboard mount.
**Existing interfaces preserved:** `UsersWorkspace` and its owner CAS operations, `AccountControls` MFA/session controls, `AdminNomination`, `PartyWorkspace`, `TaxpayerWorkspace`, existing login/MFA/OTP functionality and server proxies remain. UI tests updated for route-group file moves; new `apps/web/tests/unified-console.test.mjs` asserts layout, route de-duplication, dropdown logout and separate buyer/taxpayer actions.
**Build gate:** New GitHub Actions `.github/workflows/phase-05-ui-ci.yml` triggers npm ci, TypeScript typecheck, Node tests and Next.js build for this branch. CI status must be verified separately; no production DB access or migration.
**Local operator commands:**
```bash
git switch phase/05-parties-customers
git pull --ff-only origin phase/05-parties-customers
npm --prefix apps/web ci --no-audit --no-fund
npm --prefix apps/web run typecheck
npm --prefix apps/web test
npm --prefix apps/web run build
zsh scripts/dev-up.zsh
zsh scripts/dev-status.zsh
```
**Acceptance matrix:** Verify MFA/password and OTP login and click-lock, same visible sidebar/topbar across /dashboard /users /parties /account, avatar logout and cookie invalidation, compact table column densities and overflow, buyer filters with real linked Party types, Level1 and Level2 distinct scope, creating both buyer kinds and taxpayer kinds and fiscal-memory code, malformed/nonscoped customer denial, browser sizes 1440/1024/768/390px, and keyboard accessibility. No database migration. **Assistant verification:** GitHub writes succeeded; GitHub remote DNS was unavailable inside local test container, so local code execution was not independently possible. Owner acceptance pending.

## Step 12 — Navigation loaders and fiscal identity form completion (2026-10-15; approval pending)

**User feedback:** Route transitions must visibly block the *content panel* (not the sidebar/topbar), tables must use a Semantic-UI-style centered indicator, and buyer/taxpayer data should not appear empty while many authorized customers are being queried. Buyer and seller forms should capture invoice header attributes relevant to invoice types, grounded in the supplied tax authority SDK and the Sepidar explanatory article. This is a Step12 correction, not Step13.

**Frontend:** `apps/web/app/components/console-loader.tsx`, `apps/web/app/(console)/loading.tsx`, `apps/web/app/components/console-shell.tsx` and global CSS now provide an accessible teal spinning indicator (`role=status`, `aria-busy`, reduced motion) for route suspension and sidebar navigation. `apps/web/app/parties/buyer-taxpayer-directory.tsx` shows initial scoped account rows promptly, then incrementally joins up to six buyer profiles and taxpayer lists per batch, with overlay progress, no initial misleading empty state, and a true loadedOnce gate. No data from another customer's scope is included. Client-side aggregation should later be replaced by server-side paginated aggregation for high-volume accounts.

**Fiscal header model:** `InvoiceParticipantDetails` is a distinct persisted record for one scoped buyer Party or one seller TaxpayerProfile, never a login credential or proof of official tax authority verification. The additive `20261015101000_AddInvoiceParticipantDetails` migration adds `invoice_participant_details`, customer FK, mutually-exclusive PartyId/TaxpayerId, unique ownership indexes, restrictive FK relationships, revision concurrency, timestamps, and:
- `economic_code` (SDK `tinb` for buyer / `tins` for seller);
- `branch_code` (`bbc` for buyer / `sbc` for seller);
- `postal_code` (`bpc` for buyer; seller location metadata);
- `province`, `city`, `address`, `phone`;
- `foreign_national_code` (buyer `bid` can be national/civil participation/foreign code, handled separately from `Party.NormalizedIdentifier`);
- `passport_number` (`bpn` in applicable airline/travel schemes);
- `contract_reference` (for contract-specific invoices).
Identity person's name, national/legal identifier and legal registration number remain in `Party`; fiscal seller's assigned identifier and fiscal memory code stay with `TaxpayerProfile` and fiscal-memory tables. The new metadata deliberately does NOT overwrite those fields or store private keys.

**API and UI:** `InvoiceParticipantDetailsService` and `InvoiceParticipantEndpoints` expose scoped DEV-only GET/PUT under `/api/dev/customers/{customerId}/invoice-details/{buyer|seller}/{subjectId}`. Same-origin HTTP-only-cookie BFF exact-allowlists the path. Level1 with MFA/root commercial ownership can write seller or buyer; Level2 can write buyer for its currently owned customers, while seller writes remain Level1-only to match existing backend taxpayer write restriction. Customer self-service writes remain disabled. `ExpectedRevision` 0 creates; stale revisions get 409. Shared Party cross-customer mutation denied. `apps/web/app/parties/invoice-participant-form.tsx` is embedded in buyer Party details and seller TaxpayerWorkspace, with optional field guidance and invoice type selector.

**Invoice-type requirements and limits:** From Iranian tax-invoice guidelines and SDK: Type1 requires buyer/seller identities as applicable, Type2 generally allows omission of buyer identity, and Type3 is a different POS/electronic payment receipt workflow. **These guidance rules are not a substitute for a full official template-by-template validation engine.** Requirements depend on current official instruction version, invoice pattern and business context; final invoice template mandatory/conditional fields, buyer business type, foreign buyer/civil partnership support, and mapping to `tob/bid/tinb` must be enforced when invoice issuance is implemented. Partial metadata is saved as draft data, never claimed as officially valid. No real invoice transmission, memory certification, activation or payment occurs here.

**Tests:** `tests/ArginTaxPayer.Tests/InvoiceParticipantDetailsTests.cs`, `apps/web/tests/phase05-step12-loader-fiscal.test.mjs`; GitHub Actions `.github/workflows/phase-05-ui-ci.yml` runs frontend TS/Node/Next build and offline .NET Build/tests. Only a verified local test database named `argintaxpayer_test` may receive the migration. Review the generated SQL before applying; never auto-migrate or touch production.

**Local commands (Mac):**
```bash
git switch phase/05-parties-customers
git pull --ff-only origin phase/05-parties-customers
npm --prefix apps/web run typecheck
npm --prefix apps/web test
npm --prefix apps/web run build
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build
```
**Migration control:** Verify the current PostgreSQL database is exactly `argintaxpayer_test` through the established local AutoSSH tunnel and canonical `npgsql_value` connection parser, then inspect `dotnet ef migrations script 20261014190000_AddCustomerTaxpayerMemberships 20261015101000_AddInvoiceParticipantDetails --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api -o /tmp/phase05-step12-fiscal.sql`; apply target migration explicitly only after SQL review. Test DB-level XOR, FK, unique buyer/seller, revision, and scoped HTTP 401/403/404/409 negative scenarios.

**Status:** GitHub code committed. Automated test outcome recorded through Phase05 UI CI separately. Owner real-browser UX and DB migration signoff pending.

## Step 12 — Buyer/Seller Fiscal Registration Modals Fix (2026-10-15; owner acceptance pending)
**Issue identified by owner:** The "+ Add buyer" action incorrectly rendered the administrative software login account creation form; new seller/taxpayer "continue to tax connection" looked nonfunctional; refresh provided no visible confirmation; forms were not properly scoped to fiscal invoice counterparties and were vertically cumbersome.

**Corrected behavior:** `apps/web/app/parties/participant-registration-modal.tsx` provides a two-stage, keyboard-accessible, RTL modal with explicit buyer/seller mode. First choose an existing authorized owning customer account (NOT create an IdentityUser); then create a `Party` natural person (national code/name/family) or legal entity (national identifier/registered name/registration number), or create a `TaxpayerProfile` using the pre-existing restricted endpoint. Successful creation requires a returned entity ID, transitions visibly inside the modal to `InvoiceParticipantForm`, and for sellers also to their specific fiscal-memory `TaxpayerWorkspace` (`initialTaxpayerId`, `customerId`, no duplicate create form). Existing invoice participant metadata persists through the newly introduced scoped endpoints; none of these operations imply verified government registration or connection. Registration avoids duplicate account creation and does not change commercial customer ownership, MFA or subscription entitlements. Level2 seller-create remains denied by server; Level1+MFA required.
**Directory:** `apps/web/app/parties/buyer-taxpayer-directory.tsx` routes the distinct action buttons to fiscal modals instead of embedded `AdminCreateCustomer`. Buyer/taxpayer filters, paged table, progress overlay and previous details panels remain. Refresh fetches the current authorized account and party/taxpayer data; timestamp indicates last refresh, and saves trigger `reload`. Software user creation remains only in its administrative module.
**UI:** Modal has initial focus, Escape close, Tab focus trap, overlay outside-click closure, busy guard, compact form fields and mobile sizing. Fiscal fields are captured in Step 2, with instructions according to official invoice type/pattern; secret private keys are not collected. Seller fiscal memory is a separate second-stage section. `apps/web/tests/phase05-fiscal-modals.test.mjs` plus updated existing route and dashboard regression tests.
**Design constraint:** Current data model requires `Party` and `TaxpayerProfile` records to associate with an *existing owning customer user ID*, even though these records are NOT themselves login users. A truly account-independent buyer registry requires a distinct domain change and cannot be implied by a UI-only fix.
**Acceptance:** On test DB with migration `20261015101000_AddInvoiceParticipantDetails` installed: add both buyer types, persist fiscal metadata and retrieve after refresh, add seller and observe selected ID-specific memory screen, test Level2 denial for taxpayer create, filter and refresh timestamp, close modal and keyboard accessibility. No new migration in this correction. Verify frontend npm typecheck/test/build and .NET regression on GitHub Actions. Owner final signoff pending.

## Step 12 — Fiscal buyer/taxpayer modal correction (2026-10-15, approval pending)
- Owner reported buyer creation was incorrectly showing the admin software-login signup form; taxpayer creation did not visibly continue to fiscal connection; inline forms were inconvenient and directory refresh seemed inert.
- `apps/web/app/parties/participant-registration-modal.tsx`: distinct modal registration for buyer (`Party`) versus seller (`TaxpayerProfile`), select existing customer account as ownership scope without creating an `IdentityUser`, natural/legal identity fields, then show the persisted `InvoiceParticipantForm` after successful creation. Seller additionally opens scoped `TaxpayerWorkspace` for fiscal-memory identifier and certificate metadata, with explicit non-verified status; only Level1 with existing MFA+server authorization can create fiscal seller. Modal has focus trapping, Escape/outside close, disabled writes while pending and responsive sizing.
- `buyer-taxpayer-directory.tsx` now opens registration modals with separate buttons; existing list filters and details panels are retained. Reload re-queries scoped customers, parties and taxpayers and displays a refresh timestamp. Registration saves cause reload. No standalone buyer software login creation from this screen; the software user module retains its own account workflow.
- Styling `globals.css`; new `phase05-fiscal-modals.test.mjs` and updated historical UI tests. No new DB migration beyond the already documented `20261015101000_AddInvoiceParticipantDetails`. Verify the existing test DB migration before attempting to save tax-header metadata. Modal creation and fiscal profile save are **separate durable operations**, so if stage two fails, recover by opening the existing record, not by blindly re-registering the same ID.
- Operator smoke: from /parties, create natural and legal buyers for a permitted customer and save economic code/postcode/address; create Level1 taxpayer and confirm continuation shows the correct newly-created taxpayer and memory input; verify Level2 cannot create taxpayer, overlay and modal keyboard behavior, refresh timestamp and duplicate identifier handling. Production/API approval remains separate.

## Step 12 — Buyer creation 403 diagnosed and guarded (2026-10-15; acceptance pending)
**Owner report:** Level1 actor receives "مجوز دسترسی ندارید" at Step1 fiscal buyer modal. Reported mobile is not sufficient to identify whether the number belongs to actor or selected customer; no live PostgreSQL access was available.

**Root cause in code:** `CustomerCommercialOwnershipService.ListCustomerAccountsPageAsync` deliberately shows all registered customer identities to Level1, including unresolved and other-root assignments. Meanwhile `NaturalPersonService.IsAdminScopedAsync` and `LegalEntityService.IsAdminScopedAsync` require that the selected customer's `CustomerCommercialOwner.RootLevel1UserId` equals the logged-in Level1 actor (and MFA is enabled). Consequently a visible customer can be selected, then buyer creation returns 403. A separate BFF same-origin check can independently produce 403 before the request reaches the service. The customer's actual DB record has **not** been inspected, so the precise cause for the reported incident remains provisional.

**Changes:** `apps/web/app/(console)/parties/page.tsx` passes verified `actor.userId` through `buyer-taxpayer-directory.tsx` to `participant-registration-modal.tsx`. In buyer/seller modal, customer options without active commercial ownership matching the logged-in Level1 (or assigned Level2 partner) are disabled, while explanatory labels and errors point administrators to supervised commercial ownership review. Submit also independently fails closed for nonowned/inactive selections. Existing backend ownership and MFA checks are NOT weakened; no silent owner assignment. `apps/web/app/api/party-workspace/[...path]/route.ts` now returns safely distinguishable `origin_not_allowed` (Next.js CSRF origin denial) vs `backend_permission_denied` (HTTP403 from .NET) instead of indistinguishable 403; UI maps them to appropriate guidance. Added `apps/web/tests/phase05-buyer-permission.test.mjs`.

**Acceptance:** Pull latest branch, run `npm --prefix apps/web run typecheck && npm --prefix apps/web test && npm --prefix apps/web run build`. In UI verify root-owned customers enabled, unresolved or other-root disabled, Level2 cannot create seller, BFF origin rejection reports origin diagnostic, backend role/MFA denial mentions owner/MFA, and no identity/ownership records are modified. If a specific customer should belong to the actor but does not, resolve through existing authorized ownership transfer/CAS workflow; do not manually bypass or patch a root-level role. To determine actual cause on test DB, inspect only the customer's normalized mobile, `identity_user_roles`, `identity_mfa_states`, and `customer_commercial_owners` in a read-only query. No migration.
**State:** GitHub code pushed; CI pending and user browser verification pending.

## Step 12 — Role-scoped fiscal participant registration approved design (2026-10-15, implementation verification pending)
**Owner clarification supersedes the earlier Level1-only create restriction:** All three authenticated roles can register buyer fiscal persons (`Party`) and fiscal seller taxpayers (`TaxpayerProfile`) and enter first fiscal memory metadata, exclusively within their own valid scope. Level1 may act for root-owned customers including those delegated to a Level2; Level2 acts for accounts assigned to their own partner ownership; Customer acts only for their own customer account. This is *registration of fiscal identities*, not issuance of invoices, payment rights, subscription entitlements, tax authority verification or fiscal-memory activation.
**Backend:** New `ParticipantScopeService` enforces active user/customer roles and current commercial ownership in database; administrators require enrolled MFA, customers may act on their own account. `NaturalPersonService`, `LegalEntityService` use it for create/update; `TaxpayerCreationService` uses it for create; `InvoiceParticipantDetailsService` applies it for both buyer/seller details GET/PUT; `FiscalMemoryManagementService` allows scoped first-memory register/read while **keeping Level1-only status transitions** and pending-verification safety restrictions. `TaxpayerScopeService` allows customers to list/read only their profiles while keeping existing administrative rename restrictions. `PartyDirectoryService` permits own-account buyer listing without requiring customer MFA. No privilege is inferred from a client-supplied customer ID.
**Frontend:** `/parties` lists customer-owned buyer Party and fiscal taxpayer records and shows both registration actions to Customer, Level1 and Level2; in modal, customer account selection includes the actor's own account or allowed root/partner-owned customer; prohibited/foreign customer remains disabled. Admin registration selector fetches the full scoped customer account collection, not just currently paged rows. Upon successful seller creation, specific seller fiscal details and first fiscal memory can be entered by any properly scoped role. No change to software-login user creation.
**Negative tests required:** customer A cannot create/read/update for customer B; Level2-X cannot act for Level2-Y; root Level1 cannot silently adopt a customer with a different root assignment; stale partner assignment is denied by live database checks; inactive customers cannot register; anonymous requests 401; first-memory duplicate rejected; attempts to activate an unverified fiscal memory remain forbidden; invoice submission still separately controlled. Focused UI regression verifies the scope chain, backend build and full test suite in CI.
**Database:** No new migration and no automatic changes to commercial ownership.
