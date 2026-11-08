# Phase 18 — Step16: persistently scoped legacy-origin selection

## Implemented
- `Phase18LegacyAuthorityEndpointSelector` defines two **pinned** legacy SELF_TSP origins. The repo's 0.0.x SDK guide documents `https://tp.tax.gov.ir/req/api/` for legacy Production; the existing Sandbox transport is pinned to `https://sandboxrc.tax.gov.ir/req/api/`.
- A new server-side `Phase18AuthorityEndpointRoutingService` reads the persisted `invoice_dispatch_operations.authority_environment` and `fiscal_memories.authority_environment` through `Phase18PersistedAuthorityOperationGate`. It refuses null, unknown, mismatched tenant, memory, taxpayer or invoice IDs and mismatched provider.
- Only named legacy protocol operations are routable: `GET_TOKEN`, `GET_SERVER_INFORMATION`, `ENQUEUE`, `INQUIRY_BY_REFERENCE_NUMBER`, `INQUIRY_BY_UID`. Caller-provided arbitrary paths/hosts are rejected.
- The existing `LegacyInvoiceSubmissionHttpTransport.TryResolveSandboxBase` now delegates its base origin check to the same pinned registry, with its previous Sandbox-only checks retained. Existing Phase10 transport, encryption, signing, signed outbox, indeterminate outcome and receipt behavior are unchanged.
- Production origin selection requires a matching persisted Production operation/memory binding, `ProductionInvoiceLegacy:Enabled=true`, `ProductionInvoiceLegacy:OperatorApprovedEndpoint=true`, a canonical exact Production URL, and the separate `ArginTaxPayer:EnableOperationalEndpoints=true` authorization gate. These are gates, not a user-controlled environment selector.

## Not implemented or authorized in this step
The new persisted-operation router **does not send packets**. There is no new Production signing/encryption/token/outbox worker or legal correction transport. The existing Phase10 legacy sender remains restricted to Sandbox and `argintaxpayer_test`. Neither UI input nor `ASPNETCORE_ENVIRONMENT` selects tax authority for a dispatch. Changing the global `SandboxInvoiceLegacy:ApiBaseUrl` to Production is explicitly rejected.

Production endpoint naming is sourced from archived legacy documentation and must be independently verified with the current tax-authority contract and actual authorized credentials. The v2 SDK uses a different base and certificate/nonce/JWS authentication; it is not interchangeable with legacy SELF_TSP by URL substitution.

The already approved Phase18 Step15 incremental migration was applied on Ubuntu by the owner. No new database migration was added in Step16.

## Source acceptance
```bash
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj -c Release --filter 'FullyQualifiedName~Phase18LegacyAuthorityEndpointSelectorTests|FullyQualifiedName~Phase10LegacyInvoiceTransportTests|FullyQualifiedName~Phase18AuthorityRoutingPolicyTests'
node --test apps/web/tests/phase18-step16-routed-endpoint-source.test.mjs
bash scripts/phase18-step14-one-shot-source-acceptance.sh
```
These tests are offline and must pass on macOS and Ubuntu. Running tests does not authorize network calls to Production or the public UI.
