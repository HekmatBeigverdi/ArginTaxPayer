#!/usr/bin/env bash
set -Eeuo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
sha=$(git rev-parse HEAD)
[[ "$(git branch --show-current)" == phase/18-production-deployment ]]
[[ -z "$(git status --porcelain --untracked-files=no)" ]]
[[ "${PHASE18_EXPECTED_SHA:-}" == "$sha" ]]
node --test apps/web/tests/phase18-step09-dual-authority-audit.test.mjs
echo "[PASS] Source boundary audit complete for $sha"
echo "[BLOCKED] Persisted authority environment and production dual-routing not established"
echo "[BLOCKED] Sandbox operations still bound to legacy test database"
echo "[BLOCKED] No authority requests, application startup or production go-live authorized"
