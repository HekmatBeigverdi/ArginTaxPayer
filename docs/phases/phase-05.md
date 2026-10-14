# ArginTaxPayer — Phase 05: Parties and Customers

**Status:** Step 01 owner approved 2026-10-14; Step 02 implementation committed, test/migration execution and owner acceptance pending.
**Branch:** `phase/05-parties-customers`
**Baseline:** `docs/roadmap/18-phases.md`, `docs/phases/phase-04.md`, Phase 03 identity contracts.
**Policy:** This is the single evolving master plan. Changes to step count/scope require dated owner approval. Do not create per-step progress documents.

## Objective
Deliver natural/legal party records, economic identifiers, validation and deduplication, contacts, customer account profiles, account-to-many-taxpayer membership, customer self-management and scoped partner support. Preserve the distinction between authenticated customer account, party/counterparty and fiscal taxpayer profile.

## Fixed 15-Step Plan
| Step | Title | Deliverable | Acceptance | Status |
|---|---|---|---|---|
| 01 | Domain and API Contracts | Party/Customer/Taxpayer model, ownership matrix, migration strategy | No ambiguous identity or authorization semantics | Owner approved 2026-10-14 |
| 02 | Persistence and Migration | Entities, FKs, uniqueness, concurrency | Test DB migration reviewed; rollback and constraint tests | Implemented; verification pending |
| 03 | Natural Person Profiles | Personal identity validation and lifecycle | Invalid/duplicate data rejected | Planned |
| 04 | Legal Entity Profiles | Corporate identity and economic identifiers | Type-specific validation and unique rules | Planned |
| 05 | Validation Engine | Normalization, versioned rules, error contract | Golden and negative fixtures | Planned |
| 06 | Deduplication and Conflicts | Candidate matching, review, safe resolution | No silent merge or ownership change | Planned |
| 07 | Contacts and Addresses | Contacts, email, phone, addresses, history | Ownership and data validation enforced | Planned |
| 08 | Parties API | CRUD, pagination, filtering, concurrency | Authorized CRUD, negative paths, idempotency where applicable | Planned |
| 09 | Customer Account Profile | Profile status, party associations | No conflation of login identity and taxpayer identity | Planned |
| 10 | Customer-to-Many-Taxpayer Membership | Membership API and guarded linking | Customer owner scope and historic links preserved | Planned |
| 11 | Customer Self-Service | Account profile and taxpayer management subject to current entitlement | Unauthorized operations blocked; entitlement integration deferred to Phase 12 | Planned |
| 12 | Persian RTL UI | Lists, forms, search, details and accessibility | UI state, validation and route guards tested | Planned |
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

**Verification required on macOS:**
```bash
dotnet build ArginTaxPayer.sln -c Release
dotnet test ArginTaxPayer.sln -c Release --no-build --filter FullyQualifiedName~PartyPersistenceMetadataTests
```
Before applying migrations, confirm the **test** DB explicitly:
```bash
psql "$ConnectionStrings__ArginTaxPayerDb" -tAc 'SELECT current_database();'
```
The returned database name must be exactly `argintaxpayer_test`. Inspect the generated EF SQL script and migration history before explicit apply. CLI environment variable names may differ from the developer's local launcher; never echo a connection string containing secrets. An optional PostgreSQL integration test must assert unique (kind, identifier), active customer link dedupe, FK restriction and rollback.

**Actual results:** macOS build/test: NOT RUN by assistant; PostgreSQL migration: NOT APPLIED; Ubuntu: deferred except required schema-specific check; CI: NOT VERIFIED.
**Owner approval:** Pending acceptance results.
**Residual risk:** Type-specific identifier normalization and legal national-ID rules are Step03–05. Global uniqueness may need an owner-approved policy change for special counterparty identifiers; do not silently change it.

## Step Completion Record Contract
Append Step02–15 completion records here with scope, paths, commit, migration, local focused test evidence, Ubuntu verification/deferment, CI, security negative tests, owner acceptance and unresolved blockers. Phase close requires separately approved main merge, tag and GitHub Release.
