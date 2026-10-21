# Phase09 Step21 Key Download Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Connect the existing signing-key generation service to API/BFF/UI and deliver a first-use ZIP containing matching public and private key files without adding a generic private-key export path or a database migration.

**Architecture:** Extend the internal generation handoff so private material exists only during the owner’s successful generation request. The API/BFF stream a non-cacheable ZIP only for the claim owner; replayed operations expose public metadata and a safe no-private-handoff status. The taxpayer UI drives fresh authentication, consent, readiness, fingerprint display, and download guidance.

**Tech Stack:** ASP.NET Minimal APIs, existing EF signing journal, existing Vault custody adapter, Next.js/React BFF, xUnit, existing Phase09 test helpers.

**Spec:** `docs/superpowers/specs/2026-10-22-phase09-step21-key-download-design.md`

## Global Constraints

- Reuse `SigningKeyGenerationService`; do not reimplement key generation or Vault staging.
- Private material must never be persisted in application storage, browser storage, logs, query strings, or environment variables.
- No public or generic private-key export endpoint may be added.
- No migration may be created or executed as part of this step without separate approval.
- CSR is out of scope; only `public.key` and `private.key` are delivered.
- Admin and non-owner access remains denied by default.

## Review Focus

- Same operation replay must never return private bytes — covered by generation service/API replay tests.
- Vault readiness failure must not create a key or partial download — covered by custody/API tests.
- Network cancellation or commit failure must zero the in-memory handoff — covered by handoff disposal tests.
- ZIP contents must match the reported public fingerprint — covered by crypto artifact tests.
- BFF/UI must not cache or persist the private response — covered by HTTP and UI tests.

### Task 1: Define the ephemeral handoff contract and ZIP builder

**Files:**
- Modify: `src/ArginTaxPayer.Infrastructure/Signing/Generation/GenerationContracts.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Signing/Generation/RsaSigningKeyMaterial.cs`
- Create: `src/ArginTaxPayer.Infrastructure/Signing/Generation/SigningKeyDownloadPackage.cs`
- Test: `tests/ArginTaxPayer.Tests/Phase09SigningKeyGenerationTests.cs`

**Interfaces:**
- Produces an internal disposable `GeneratedKeyHandoff` containing only UTF-8 PEM bytes and an owner-only `SigningKeyDownloadPackage` builder.
- The builder returns a byte array/stream suitable for one response and never persists a file.

- [ ] Write failing tests for matching public/private PEM, fingerprint, exact `public.key`/`private.key` ZIP entries, and zeroing/disposal.
- [ ] Run the focused tests and verify they fail because the handoff/package types are absent.
- [ ] Implement the minimal disposable handoff and ZIP builder using `System.IO.Compression`; derive public material from the generated PKCS#8 bytes and validate the fingerprint.
- [ ] Run the focused tests and verify they pass.
- [ ] Commit `feat(phase09): add ephemeral signing key download package`.

### Task 2: Thread the handoff through the existing generation service

**Files:**
- Modify: `src/ArginTaxPayer.Application/Signing/SigningKeyGenerationContracts.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Signing/Generation/SigningKeyGenerationService.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Signing/Generation/VaultGeneratedKeyCustody.cs`
- Modify: `src/ArginTaxPayer.Infrastructure/Signing/Generation/SigningKeyGenerationRegistration.cs`
- Test: `tests/ArginTaxPayer.Tests/Phase09SigningKeyGenerationTests.cs`

**Interfaces:**
- `SigningKeyGenerationResult` gains an internal-only optional handoff, while public application contracts remain free of private bytes.
- `SigningKeyGenerationService.GenerateAsync` returns the handoff only when the current request owns a newly committed generation; replay returns `PrivateHandoffUnavailable`.

- [ ] Add failing tests for first-owner handoff, replay without handoff, unavailable Vault, and failed commit without handoff.
- [ ] Run the focused generation tests and verify the expected failures.
- [ ] Implement the smallest service/custody changes that keep the protected Vault copy and expose no unwrap-by-key-id method.
- [ ] Run all generation and custody tests; verify no existing Phase09 regressions.
- [ ] Commit `feat(phase09): expose one-time initial signing key handoff`.

### Task 3: Add authenticated API generation endpoint

**Files:**
- Create or modify: `src/ArginTaxPayer.Api/Taxpayers/SigningGenerationEndpoints.cs`
- Modify: `src/ArginTaxPayer.Api/Program.cs`
- Test: `tests/ArginTaxPayer.Tests/Phase09SigningGenerationHttpTests.cs`

**Interfaces:**
- `POST /api/dev/signing/{taxpayerId:guid}/{memoryId:guid}/generate` accepts only operation ID, consent version, and custody acceptance.
- New handoff response is `application/zip` with `Content-Disposition`, `Cache-Control: no-store`, and no JSON containing secret material.
- Replay and safe failures use the existing signing status/error mapping.

- [ ] Add failing endpoint tests for owner auth, fresh reauth, consent, readiness, ZIP headers, replay, and cross-scope denial.
- [ ] Run endpoint tests and verify they fail because the route is unmapped.
- [ ] Implement route validation and service invocation without accepting secret inputs or creating export routes.
- [ ] Run focused API tests plus existing signing HTTP security tests.
- [ ] Commit `feat(phase09): expose owner-scoped key generation endpoint`.

### Task 4: Wire the BFF download route

**Files:**
- Create or modify: `apps/web/app/api/signing/[taxpayerId]/[memoryId]/generate/route.ts`
- Test: `apps/web/app/api/signing/[taxpayerId]/[memoryId]/generate/route.test.ts` (or the repository’s established web test location)

**Interfaces:**
- Same-origin POST forwards the bearer token and JSON body to the API generation endpoint.
- The BFF streams the API response, preserves safe download headers, rejects oversized/malformed requests, and never stores the ZIP.

- [ ] Add failing BFF tests for origin, body limits, bearer forwarding, no-store, and replay response handling.
- [ ] Run the focused web tests and verify failure before the route exists.
- [ ] Implement the route by following the existing signing-import BFF boundary.
- [ ] Run web lint/type/test commands used by the repository.
- [ ] Commit `feat(web): proxy secure signing key download`.

### Task 5: Connect taxpayer onboarding UI

**Files:**
- Modify: `apps/web/app/taxpayers/taxpayer-workspace.tsx`
- Modify or create: `apps/web/app/taxpayers/signing-key-generation.tsx`
- Test: repository-established taxpayer UI test files

**Interfaces:**
- The UI starts a fresh operation ID, collects consent and fresh-authentication state, downloads the ZIP as a browser download, and displays fingerprint/instructions.
- It never writes private bytes to local/session storage, IndexedDB, logs, or a preview component.

- [ ] Add failing UI tests for disabled readiness, consent gating, successful fingerprint/download state, and replay/lost-download guidance.
- [ ] Run focused UI tests and verify expected failures.
- [ ] Implement the generation section and keep import/connection-test behavior intact.
- [ ] Run UI tests and lint/type checks.
- [ ] Commit `feat(web): connect taxpayer signing key generation flow`.

### Task 6: Update Phase09 documentation and verify the full change

**Files:**
- Modify: `docs/phases/phase-09.md`
- Modify: `scripts/phase09_step21_verify.py` if the repository’s verifier is missing or incomplete
- Test: all Phase09 tests and the Step21 verifier

- [ ] Add the Step21 implementation/acceptance section documenting two-file delivery, no CSR, one-time handoff, replay behavior, and no migration.
- [ ] Run `git diff --check` and verify no migration files changed.
- [ ] Run the focused new tests and the full Phase09 suite; record actual totals.
- [ ] Run the macOS verification commands, including the Step21 script and relevant `dotnet test`/web checks.
- [ ] Commit `docs(phase09): document completed step21 onboarding`.

