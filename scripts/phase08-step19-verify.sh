#!/usr/bin/env bash
set -Eeuo pipefail

# Phase08 Step19: read-only source/build regression. No migrations or DB writes.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
echo "=== Phase08 Step19: independent source/build regression ==="
echo "Commit: $(git rev-parse HEAD)"
echo "Branch: $(git branch --show-current)"
if [[ "$(git branch --show-current)" != "phase/08-invoice-workflows-ui" ]]; then
  echo "ERROR: expected phase/08-invoice-workflows-ui branch" >&2
  exit 2
fi
if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "ERROR: working tree has uncommitted changes; commit or stash before testing" >&2
  exit 2
fi
if ! command -v dotnet >/dev/null || ! command -v npm >/dev/null; then
  echo "ERROR: dotnet and npm are both required" >&2
  exit 2
fi
printf '\n=== dotnet SDK and source/build ===\n'
dotnet --version
dotnet restore ArginTaxPayer.sln
dotnet build ArginTaxPayer.sln --no-restore
printf '\n=== .NET regression tests (no external writes intended) ===\n'
dotnet test ArginTaxPayer.sln --no-build --verbosity normal
printf '\n=== Web regression tests ===\n'
npm --prefix apps/web test
npm --prefix apps/web run typecheck
npm --prefix apps/web run build
printf '\nSTEP19 source/build regression completed.\n'
printf 'Note: this is NOT browser, HTTP, PostgreSQL, bulk import or Ubuntu acceptance.\n'
