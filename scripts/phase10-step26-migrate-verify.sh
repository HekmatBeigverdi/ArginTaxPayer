#!/usr/bin/env bash
# Usage: bash scripts/phase10-step26-migrate-verify.sh
# Run on Ubuntu VPS against the isolated local argintaxpayer_test DB only.
set -Eeuo pipefail
cd "$(dirname "$0")/.."
config="$HOME/.argintaxpayer-test/ubuntu.env"
if [[ ! -f "$config" ]]; then echo "Missing $config" >&2; exit 1; fi
set -a
# shellcheck disable=SC1090
source "$config"
set +a
conn="${ConnectionStrings__ArginTaxPayerDb:-}"
if [[ "$conn" != *"Host=127.0.0.1;"* || "$conn" != *"Port=5432;"* ||
      "$conn" != *"Database=argintaxpayer_test;"* ]]; then
  echo "Refusing migration outside localhost argintaxpayer_test." >&2
  exit 1
fi
export ASPNETCORE_ENVIRONMENT=Development DOTNET_ENVIRONMENT=Development
echo "1. Validating .NET build"
dotnet build ArginTaxPayer.sln --nologo
echo "2. Checking pending migrations"
dotnet ef migrations list --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
echo "3. Applying migrations to isolated Sandbox test DB"
dotnet ef database update --project src/ArginTaxPayer.Infrastructure --startup-project src/ArginTaxPayer.Api
echo "4. Verifying committed migration + legal relation table"
if command -v psql >/dev/null; then
  # Convert local connection string for PostgreSQL CLI without echoing credentials.
  export PGPASSWORD="$(printf '%s' "$conn" | sed -n 's/.*Password=\([^;]*\).*/\1/p')"
  psql -X -v ON_ERROR_STOP=1 -h 127.0.0.1 -p 5432 -U argintaxpayer -d argintaxpayer_test -c '
SELECT migration_id FROM "__EFMigrationsHistory"
WHERE migration_id = '\''20261029121500_AddSandboxLegalInvoiceRelations'\'';
SELECT to_regclass('\''public.sandbox_invoice_legal_relations'\'') AS legal_relations_table;
SELECT indexname FROM pg_indexes WHERE tablename='\''sandbox_invoice_legal_relations'\'' ORDER BY indexname;
'
  unset PGPASSWORD
fi
echo "Migration verification complete. No cancellation send is performed by this script."
