# Phase 18 — Step 10: explicit fiscal memory environment (schema and onboarding only)

## Implemented

- Nullable, typed `FiscalMemory.AuthorityEnvironment` and EF mapping (`Production=1`, `Sandbox=2`).
- A separately reviewed migration `20261104220000_AddFiscalMemoryAuthorityEnvironment`. Existing rows remain **NULL/unclassified**, deliberately not assumed Production.
- New fiscal memory registration requires explicit environment; list/registration APIs return it.
- Taxpayer onboarding UI requests environment and visibly marks legacy unclassified rows for review.
- Regression tests for migration and onboarding. No automated database migration is run by API, Next.js or deployment scripts.

## Not implemented — mandatory before go-live

The historical `sandbox_fiscal_memories` storage and legacy `argintaxpayer_test` restriction still exist. The submission/outbox/UID and reference inquiry/correction/cancellation/sales-return pipelines are NOT routed through the new authoritative environment column. Production tax dispatch MUST remain disabled. The UI's environment selector is only a registration attribute, not a dispatch switch.

Before routing, establish trusted customer/taxpayer/memory ownership binding, immutable environment on operations/receipts/lineage, per-environment keys and endpoint allowlists, same-environment legal successor checks, idempotency/no-resend on Indeterminate, and real cross-environment negative integration tests. Reconcile legacy Sandbox identity and storage in a separately reviewed migration and preserve all prior invoice evidence.

## Verification on macOS and Ubuntu (source only)

```bash
git fetch origin
git switch phase/18-production-deployment
git pull --ff-only origin phase/18-production-deployment
git status --short
git rev-parse HEAD
node --test apps/web/tests/phase18-step09-dual-authority-audit.test.mjs apps/web/tests/phase18-step10-fiscal-environment-onboarding.test.mjs
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --configuration Release --filter 'FullyQualifiedName~Phase18FiscalMemoryEnvironmentPersistenceTests|FullyQualifiedName~Phase18AuthorityRoutingPolicyTests|FullyQualifiedName~FiscalMemoryPersistenceTests'
(cd apps/web && npm run typecheck && npm run build)
```

Before touching PostgreSQL, generate SQL offline, inspect its exact contents and compare to this migration:

```bash
dotnet ef migrations script --idempotent \
  --project src/ArginTaxPayer.Infrastructure/ArginTaxPayer.Infrastructure.csproj \
  --startup-project src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj \
  --configuration Release --output /tmp/phase18-reviewed-migrations.sql
```

**Do not execute the SQL against Production yet.** First perform backup/restore evidence, exact-identity/TLS checks and migration review using the Step07 gate. Before applying schema, do not deploy the new API model: a missing column may break fiscal-memory queries. After schema migration, run application and UI integration acceptance. Keep `ArginTaxPayer__EnableOperationalEndpoints=false` and `ARGIN_PRODUCTION_UI_ENABLED=false` in the gated environment until the remaining mandatory work is accepted.

No provider send, Nginx change, key rotation, or automatic data backfill is part of this step.
