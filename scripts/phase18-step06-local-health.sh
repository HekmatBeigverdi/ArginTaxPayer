#!/usr/bin/env bash
# Phase18 future runtime health/readiness gate: local loopback only.
# Step06 must remain inactive; probe becomes useful only after Steps07/08.
set -Eeuo pipefail
umask 077
[[ "$#" == 1 && ( "$1" == "--probe-local" || "$1" == "--probe-local-disabled-ui" ) ]] ||
  { echo '[REFUSED] Use --probe-local or --probe-local-disabled-ui' >&2; exit 2; }
[[ "$(uname -s)" == Linux ]] || { echo '[REFUSED] Ubuntu required' >&2; exit 2; }
command -v curl >/dev/null || exit 2
command -v ss >/dev/null || exit 2
for port in 15090 15091; do
  # Every bound address for a reserved port must be the exact IPv4 loopback.
  # This rejects wildcard, public IPv4, and IPv6 wildcard exposure alike.
  listeners=$(ss -ltnH | awk '{print $4}' | grep -E ":${port}$" || true)
  [[ -n "$listeners" ]] || { echo "[FAIL] Nothing listening on $port" >&2; exit 1; }
  while IFS= read -r addr; do
    [[ "$addr" == "127.0.0.1:$port" ]] ||
      { echo "[FAIL] Refusing non-loopback listener on reserved port $port" >&2; exit 1; }
  done <<< "$listeners"
done
api="http://127.0.0.1:15090"
web="http://127.0.0.1:15091"
code() { curl --noproxy '*' -sS -o /dev/null -w '%{http_code}' --max-time 5 "$1"; }
[[ "$(code "$api/health/live")" == 200 ]] || { echo '[FAIL] API liveness' >&2; exit 1; }
[[ "$(code "$api/health/ready")" == 200 ]] || { echo '[FAIL] API PostgreSQL readiness' >&2; exit 1; }
webcode=$(code "$web/identity")
if [[ "$1" == "--probe-local-disabled-ui" ]]; then
  [[ "$webcode" == 200 || "$webcode" == 307 || "$webcode" == 308 || "$webcode" == 404 ]] ||
    { echo '[FAIL] Staged BFF process not healthy' >&2; exit 1; }
else
  [[ "$webcode" == 200 ]] ||
    { echo "[FAIL] Login unavailable (HTTP $webcode); verify both identity UI switches in web.env" >&2; exit 1; }
  [[ "$(code "$api/api/dev/identity/authorization/me")" == 401 ]] ||
    { echo '[FAIL] Operational identity API unavailable or not session-protected; verify API activation' >&2; exit 1; }
  [[ "$(code "$web/api/identity/me")" == 401 ]] ||
    { echo '[FAIL] Identity BFF unavailable or not session-protected; verify UI activation' >&2; exit 1; }
fi
[[ "$(code "$api/health/dev-identity")" == 404 ]] ||
  { echo '[FAIL] Development diagnostics enabled in Production' >&2; exit 1; }
echo '[PASS] Loopback API live/ready and production dev-diagnostic isolation.'
if [[ "$1" == "--probe-local-disabled-ui" ]]; then
  echo '[STAGED_ONLY] UI may remain disabled: application login and business availability NOT verified.'
else
  echo '[PASS] Login available; API and BFF reject unauthenticated identity requests.'
fi
echo '[NOTE] This does not replace real HTTPS/Nginx/browser tests or tax authority checks.'
