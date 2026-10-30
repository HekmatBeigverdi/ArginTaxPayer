#!/bin/zsh
# Phase10 Step27: macOS only; uses existing SSH tunnel to remote test DB.
set -euo pipefail
cd "${0:A:h:h}"
if [[ ! -f .env.dev.local ]]; then
 print -u2 "Missing .env.dev.local";exit 1
fi
set -a
source .env.dev.local
set +a
export ASPNETCORE_ENVIRONMENT=Development DOTNET_ENVIRONMENT=Development
if [[ "${ConnectionStrings__ArginTaxPayerDb:-}" != *"Database=argintaxpayer_test;"* ||
 "${ConnectionStrings__ArginTaxPayerDb:-}" != *"Port=15432;"* ||
 "${ConnectionStrings__ArginTaxPayerDb:-}" != *"Host=127.0.0.1;"* ]]; then
 print -u2 "Refusing non-test DB or connection without local AutoSSH port 15432."
 exit 1
fi
if ! nc -z 127.0.0.1 15432; then
 print -u2 "AutoSSH tunnel unavailable: start scripts/dev-up.zsh first or open tunnel."
 exit 1
fi
dotnet build ArginTaxPayer.sln
dotnet ef database update --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
dotnet test tests/ArginTaxPayer.Tests/ArginTaxPayer.Tests.csproj --filter "FullyQualifiedName~Phase10Step27"
node --test apps/web/tests/phase10-step27-*.test.mjs
(cd apps/web && npm run typecheck && npm run build)
print "Offline Step27 tests completed; real Sandbox user/browser send and inquiry are NOT implied."
