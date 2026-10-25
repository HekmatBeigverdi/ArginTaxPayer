# Phase 10 Sandbox Real Submission — Design

Date: 2026-10-25
Status: Approved by owner for implementation in conversation
Scope: Customer `425a8f26-7633-41ca-a3cc-431c9e2f1719`, invoice `00010-05-AAA`, Sandbox only

## Intent

Complete the existing Phase 10 invoice path so an owner-authorized, legally finalized invoice can move through:

`Finalized → fiscal identifier → signed packet → encrypted packet → durable outbox → Sandbox send → receipt → inquiry/reconciliation`.

The implementation must remain local-testable. Codex must not send a real invoice, print credentials, fabricate a certificate, or connect to Production. The operator will perform any real Sandbox run after local gates pass.

## Current state and gap

The repository already contains invoice review/finalization primitives, scoped taxpayer and fiscal-memory authorization, Sandbox fiscal serial reservations, JWS/JWE cryptography, a Sandbox-v2 transport primitive, dispatch history, and read-only readiness/UI routes. The live path is intentionally fail-closed: readiness reports `invoice.sandbox_outbox.not_connected`, `IInvoiceSubmissionTransport` is disabled, inquiry is disabled, and the UI has no send mutation.

## Goals

- Reuse the existing invoice aggregate, revision, tenant authorization, key custody, dispatch tables and UI.
- Require server-side validation of seller, buyer, pattern/type, goods/services, VAT, settlement, fiscal memory and current revision.
- Require explicit single-invoice owner consent bound to customer, taxpayer, invoice, revision and expiry.
- Reserve a durable Sandbox fiscal serial transactionally and derive one unique fiscal identifier.
- Bind the exact immutable revision to a signed JWS and Sandbox-v2 JWE packet.
- Persist an immutable dispatch operation/outbox record before any HTTP call.
- Allow exactly one submission attempt for uncertain outcomes; never blind-retry.
- Persist sanitized provider receipt/reference data and support correlated inquiry/reconciliation.
- Expose readiness, progress, reference and authoritative status in the existing Persian RTL compact modal.
- Preserve customer/RBAC isolation, append-only audit/history, protected private-key custody and Production fail-closed behavior.

## Non-goals and anti-goals

- No Production endpoint, Production record, migration against a non-test database, or automatic deployment.
- No private-key export, replacement, logging or browser exposure.
- No fabricated certificate, authority key, credential, receipt or final acceptance.
- No parallel submission system or duplicate invoice tables.
- No claim that an HTTP acknowledgment equals final fiscal acceptance.
- No live transmission by the agent; live execution remains an operator action after explicit approval.

## Chosen architecture

Extend the current Phase 10 services and persistence boundaries. A finalization/application service will create an approved immutable revision and owner-consent-bound dispatch admission. The existing fiscal serial repository allocates a Sandbox-only serial in the same database transaction. A packet builder will load the authorized private key through the existing custody abstraction, verify its public binding and certificate metadata, produce the exact JWS/JWE bytes, and store only protected/opaque packet material with hashes.

The dispatch admission repository will persist the operation, immutable packet identity and outbox message before transport. A Sandbox-only transport adapter will use the verified endpoint configuration, nonce/auth contract and one-shot POST behavior. Response handling will append a sanitized receipt; inquiry will be available only for a persisted correlated operation and will append inquiry evidence. Production remains unreachable through endpoint allowlists and explicit environment checks.

## Safety gates

1. Before any schema change: verify `current_database() = 'argintaxpayer_test'`, migration state and a validated backup; require separate operator approval to apply.
2. Before finalization: current actor must be the customer owner, invoice must be in scope, revision must match and all official rule/field checks must pass.
3. Before send: single-invoice consent, Sandbox fiscal memory, valid custody/certificate evidence, exact packet hash, durable outbox and no prior uncertain/terminal send.
4. Before real HTTP: operator must explicitly approve this invoice and provide credentials/certificate/key material through existing protected configuration/custody; no secrets in arguments or logs.
5. After HTTP: only correlated provider evidence can produce an acknowledgment; final fiscal status requires inquiry/reconciliation evidence.

## Required unresolved evidence

The implementation will fail closed until the operator supplies the applicable current authority SDK/protocol details and real Sandbox credentials/certificate/encryption key in local protected configuration. Checked-in SDK archives and existing tests are implementation evidence, not proof that credentials or live authority acceptance exist.

