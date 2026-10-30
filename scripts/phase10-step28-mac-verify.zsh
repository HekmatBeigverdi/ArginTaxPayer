#!/bin/zsh
# Phase 10 Step 28 preliminary persistence and validation verification.
set -euo pipefail
cd "${0:A:h:h}"
if [[ ! -f .env.dev.local ]]; then
  print -u2 "Missing .env.dev.local"
  exit 1
fi
set -a
source .env.dev.local
set +a
export ASPNETCORE_ENVIRONMENT=Development
export DOTNET_ENVIRONMENT=Development
db="${ConnectionStrings__ArginTaxPayerDb:-}"
if [[ "$db" != *"Host=127.0.0.1;"* || "$db" != *"Port=15432;"* ||
      "$db" != *"Database=argintaxpayer_test;"* ]]; then
  print -u2 "Refusing to migrate any database other than argintaxpayer_test via port 15432."
  exit 1
fi
nc -z 127.0.0.1 15432 || { print -u2 "AutoSSH tunnel is unavailable"; exit 1; }
dotnet build ArginTaxPayer.sln
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase10Step28"
node --test apps/web/tests/phase10-step28-sales-return-flow.test.mjs
(cd apps/web && npm run typecheck && npm run build)
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
dotnet ef database update --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
print "Step28 persistence migration and quantity unit tests done. Sandbox submission NOT tested."
