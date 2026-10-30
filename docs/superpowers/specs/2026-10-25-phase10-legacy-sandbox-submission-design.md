# Phase 10 Legacy Sandbox Submission Design

Date: 2026-10-25
Status: Approved by the owner request in conversation
Scope: Sandbox only; customer `425a8f26-7633-41ca-a3cc-431c9e2f1719`; invoice `00010-05-AAA`

## Intent

Use the owner-selected April 1404 certificate-free `SELF_TSP` SDK contract for the first Sandbox submission path. Defer the certificate/JWS/JWE v2 path without deleting it.

## Approaches

1. **Legacy adapter behind the existing dispatch boundary (chosen).** Reproduce the supplied `TaxCollectData.Library` request framing and endpoints, while retaining current owner/scope, immutable packet, Outbox and receipt controls.
2. **Parallel v2 fallback.** Rejected because it would mix incompatible signing profiles and keep producing v2-only blockers for a legacy taxpayer.
3. **Direct SDK calls from the API endpoint.** Rejected because it bypasses durable admission, idempotency and secret-custody boundaries.

## Goals

- Use the existing protected PKCS#8 key and fiscal-memory binding.
- Use the legacy signed transfer/token flow and legacy invoice enqueue contract.
- Keep transport TLS validation, no redirects/cookies, bounded responses and no token/private-key logging.
- Preserve owner authorization, tenant isolation, durable Outbox and receipt classification.
- Keep v2 transport unregistered and explicitly deferred.

## Non-goals

- No certificate generation, JWS/JWE, v2 endpoint or production access.
- No automatic real invoice transmission by the agent.
- No database migration or fabricated fiscal identifier/receipt.

## Acceptance

- Legacy transport contract tests cover token request, fiscal-information lookup, invoice payload framing, response correlation and safe failure classification.
- Readiness reports legacy blockers rather than certificate/v2 blockers.
- Send remains unavailable unless the existing explicit owner/Sandbox gates and protected configuration are satisfied.
- Full .NET and web suites remain green.
