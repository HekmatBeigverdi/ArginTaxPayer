# Local fiscal-memory workspace checks

Prepared locally on 2026-11-11. No commit, push, deployment, server configuration
change or server migration was performed. Existing private keys were not moved.

The taxpayer dossier now has one memory/connection section with separate
Production and Sandbox tabs. Configured memories show a summary and explicit
test/manage actions. Import/generation/recovery controls appear during setup or
management, and a network error does not offer automatic key replacement.

## Local testing

Run the application with the existing local development workflow. In the taxpayer
dossier, open “حافظه مالیاتی و اتصال”.

1. A configured Sandbox key should have one summary, not a second generic test
   against the unused same-identifier regular-memory registration.
2. Click “تست اتصال Sandbox” explicitly. Reloading the page must preserve a
   recorded result after the migration below is applied.
3. “مدیریت تنظیمات Sandbox” opens same-key recovery. It requires fresh owner
   authentication, consent and a matching private key. A different public key is
   rejected; IDs and historical invoice bindings are retained.
4. Production setup/import/generation remains scoped to the selected registered
   memory. Test success and local activation are shown separately.
5. Switching tabs while a test is pending must not close or clear the other
   memory's settings. Failed network tests retain the previous successful test
   as historical evidence.

First-time Sandbox setup offers an existing PKCS#8 RSA-2048 key or generation.
For an existing key, its public counterpart is derived locally. Only explicit
final registration sends public metadata, followed by protected private-key
storage using the existing isolated Sandbox endpoints.

## Additive observation migration

`20261111180000_AddFiscalMemoryConnectionObservations` creates a separate,
append-only safe observation table. It does not migrate keys, rewrite old test
results, activate memories or change invoices. Existing unpersisted Sandbox
successes cannot be reconstructed; test explicitly once after schema readiness.

The migration has NOT been applied to the application's current database. Before
applying it to a selected test database, load the existing private local
configuration and generate/review its SQL:

```sh
set -a
source .env.dev.local
set +a
dotnet ef migrations script \
  20261109010000_AllowPartialSalesReturns \
  20261111180000_AddFiscalMemoryConnectionObservations \
  --project src/ArginTaxPayer.Infrastructure/ArginTaxPayer.Infrastructure.csproj \
  --startup-project src/ArginTaxPayer.Api/ArginTaxPayer.Api.csproj \
  --output /tmp/argintax-memory-connection-observations.sql
```

Without the table, existing configuration is still readable and the UI reports
that history recording is unavailable; a temporary test result is not fabricated
into durable success. The development launcher does not apply migrations.
Future server migration/deployment remains a separate owner decision.

Unclassified one-time probes that do not complete verified classification and
activation are not projected as a configured connection in either environment.
Once classification succeeds, evidence is bound to the actual persisted
environment and the same key that was probed.

## Validation record

Focused tests cover source/owner/key isolation, stale probe rejection, actual
Sandbox probe persistence with a synthetic HTTP provider, corrupted custody,
classification/activation revision changes, same-key recovery policy, bounded
BFF projection and preservation of existing invoice selections. PostgreSQL tests
use disposable schemas on an isolated temporary local server.

Browser checks use synthetic API responses and synthetic keys; no invoice is
submitted as a validation step. The unrelated existing web test
“GitHub workflow never deploys automatically on push” already fails on the base
checkout because it references the removed phase-specific workflow filename.
