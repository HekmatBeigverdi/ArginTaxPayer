# Phase09 Step21 — Generated Signing Key Onboarding Design

Date: 2026-10-22  
Status: Design approved in conversation; implementation plan pending review

## Goal

Connect the existing `SigningKeyGenerationService` to the taxpayer onboarding API/BFF/UI so an authenticated owner can generate a new signing key, download the public key, and receive the private key for initial setup in the accounting software.

The user-facing artifact is a two-file ZIP:

- `public.key` — uploaded by the owner to the tax portal.
- `private.key` — entered into the accounting/invoicing software.

CSR is explicitly out of scope for Step21 because it is not required by the current taxpayer connection flow. The tax portal supplies the tax-memory identifier after the public key is registered.

## Existing constraints

- Reuse `SigningKeyGenerationService`; do not reimplement key generation or Vault staging.
- Vault/provider readiness must be checked before generation.
- Private material must never be persisted in application storage, browser storage, logs, query strings, or environment variables.
- No public or generic private-key export endpoint may be added.
- No migration may be created or executed as part of this step without separate approval.
- Existing operation idempotency must remain owner- and taxpayer-scoped.
- Admin and non-owner access remains denied by default.

## Proposed flow

1. The UI loads generation readiness and the current owner-scoped signing state.
2. The owner starts a new operation with a client-generated `operationId`.
3. The API requires the owner’s fresh authentication, explicit consent, server-custody acceptance, and a ready Vault configuration.
4. The existing generation service reserves and claims the operation, creates the RSA key, stages the protected copy in Vault, and commits public metadata.
5. During that same request only, the custody boundary returns a short-lived in-memory handoff containing the PKCS#8 private PEM and public PEM. The response is streamed as a ZIP with `Cache-Control: no-store`, `Pragma: no-cache`, and no persistent temporary file.
6. The UI downloads the ZIP through the BFF and immediately clears transient response references. It displays the fingerprint and instructions to upload the public key to the tax portal and enter the private key into the accounting software.
7. A replay of the same `operationId` returns the committed public artifact and a safe `PrivateHandoffUnavailable` outcome; it never returns private material again. The user must explicitly start a new generation if the first download was lost.

This intentionally favors non-repeatable secret delivery over retaining a recoverable private export capability. The server remains the authoritative Vault custodian, while the owner gets the required initial software handoff.

## API and BFF boundaries

### API

Add an owner-scoped generation endpoint under the existing signing route family. It must:

- validate taxpayer and fiscal-memory scope;
- enforce fresh owner authentication and consent version;
- reject disabled generation/protection configuration;
- call the existing generation service;
- return a ZIP only for a newly committed operation that owns the handoff;
- return public metadata and a safe status for replays;
- never accept a private key, password, arbitrary file path, or export selector from the caller;
- use `no-store` headers and a strict download content disposition.

The API response contract distinguishes `Created`, `Replayed`, `Unavailable`, `Forbidden`, `Unauthenticated`, `Conflict`, `IntegrityFailure`, and `RecoveryRequired`. Private bytes are not represented in JSON contracts or persistence models.

### BFF

The BFF forwards only the authenticated same-origin request, enforces the existing body/origin rules, forwards the download response without buffering to durable storage, and preserves `no-store`. It must not expose a generic `/export` route or cache the ZIP.

## Custody boundary

Extend the internal custody contract only as needed for a one-request handoff. The handoff type must:

- contain private bytes only in managed memory;
- expose no unwrap-by-key-id or arbitrary export operation;
- be disposed/zeroed after response construction;
- be available only to the generation transaction that owns the claim;
- fail closed when Vault staging succeeds but the handoff cannot be produced.

The persisted Vault copy remains protected and is not used as a general export source. If the provider cannot support controlled initial handoff without exposing a generic unwrap operation, the private download requirement is blocked and the reason is documented rather than weakening the boundary.

## UI behavior

The taxpayer “کلید/توکن” section gains:

- Vault readiness status;
- “ساخت کلید جدید” action;
- explicit consent text describing public upload and private-key responsibility;
- fresh-authentication prompt;
- fingerprint and generation result;
- public-key download affordance;
- two-file ZIP download affordance for the newly generated operation;
- replay/lost-download guidance that does not imply private-key recovery.

No private key preview, durable browser state, clipboard persistence, or automatic upload to unrelated services is allowed.

## Idempotency and failure handling

- Same owner/scope/operation ID: replay the recorded public result, never the private handoff.
- Different owner or scope: forbidden.
- Reserved/Vault-staged/recovery-required operation: return the existing safe status and require recovery/new operation according to the existing journal rules.
- Vault unavailable: do not create a key or expose partial material.
- Commit failure after staging: zero in-memory handoff, record the safe failure state, and do not return private bytes.
- Client cancellation: zero the handoff; the operation cannot be recovered by private export.

## Tests

Add focused tests covering:

- owner fresh-authentication and consent requirements;
- Vault-not-ready rejection;
- first generation returns public metadata and a valid two-file ZIP;
- ZIP contains matching public/private material and the reported fingerprint;
- same operation replay never returns private bytes;
- cross-owner/scope access is forbidden;
- private bytes do not appear in JSON, audit metadata, logs, or persistence;
- BFF preserves download headers and does not cache;
- UI renders readiness, consent, fingerprint, and replay guidance;
- no CSR is produced or required.

## Acceptance criteria

Step21 is complete only when the owner can generate a new key from the taxpayer connection UI, download `public.key` and `private.key` in the initial controlled handoff, see the fingerprint, and receive clear tax-portal instructions, while all replay, authorization, Vault, and secret-handling tests pass. Completion must report actual test totals and macOS commands, and must not be announced as final until the owner confirms acceptance.
