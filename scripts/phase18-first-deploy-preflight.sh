#!/usr/bin/env bash
# Phase18 one-time deployment preflight: read-only, redacted, no deployment.
set -Eeuo pipefail
umask 077
cd "$(dirname "${BASH_SOURCE[0]}")/.."
refuse(){ echo "[REFUSED] $*" >&2; exit 2; }
[[ "$(uname -s)" == Linux && "$(id -u)" != 0 ]] || refuse 'Ubuntu nonroot required'
sha=$(git rev-parse HEAD)
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] ||
  refuse 'Expected reviewed deployment branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || refuse 'Dirty tracked files'
[[ "${PHASE18_EXPECTED_SHA:-}" == "$sha" ]] || refuse 'Exact confirmed Git SHA required'
for cmd in curl ss sudo nginx systemctl sha256sum; do command -v "$cmd" >/dev/null || refuse "Missing $cmd"; done
sudo -v
out="$PWD/artifacts/phase18-fast-release/$sha/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
printf 'source_sha\t%s\n' "$sha" > "$out/manifest.tsv"
record() { printf '%s\t%s\n' "$1" "$2" >> "$out/manifest.tsv"; }
if sudo nginx -t > "$out/nginx-check.log" 2>&1; then record nginx-syntax PASS; else record nginx-syntax FAIL; fi
if curl -fsSI --max-time 10 https://tax.argin.ir/ > /dev/null 2>&1; then
  record public-https PASS
else
  record public-https UNVERIFIED
fi
for name in argintaxpayer-api.service argintaxpayer-web.service; do
  status=$(systemctl is-active "$name" || true)
  record "$name" "$status"
done
for port in 15090 15091 15434; do
  addresses=$(ss -ltnH | awk '{print $4}' | grep -E ":$port$" || true)
  if [[ -z "$addresses" ]]; then
    record "listener-$port" NOT_LISTENING
  elif [[ "$addresses" == "127.0.0.1:$port" ]]; then
    record "listener-$port" LOOPBACK
  else
    record "listener-$port" REVIEW_BOUND_ADDRESS
  fi
done
for path in /etc/argintaxpayer/api.env /etc/argintaxpayer/web.env; do
  if sudo test -f "$path"; then
    mode=$(sudo stat -c %a "$path")
    [[ "$mode" == 600 ]] || refuse "Private config file has unsafe permissions: $path"
  else
    record "private-env-$(basename "$path")" MISSING
  fi
done
if [[ -e /etc/argintaxpayer/phase18-activation-approved ]]; then
  record activation-marker EXISTS
else
  record activation-marker ABSENT
fi
record startup-and-db-writes NOT_PERFORMED
record authority-send NOT_PERFORMED
record deploy-and-public-cutover NOT_PERFORMED
echo "[DONE] $out/manifest.tsv"
cat "$out/manifest.tsv"
