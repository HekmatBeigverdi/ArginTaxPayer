#!/usr/bin/env bash
# Phase18 non-mutating readiness report for the active Ubuntu deployment.
# Read only: NO start/restart, DB write, secret output, migration, or tax-authority request.
set -Eeuo pipefail
umask 077
[[ "$(uname -s)" == Linux ]] || { echo '[REFUSED] Ubuntu only' >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
sha=$(git rev-parse HEAD)
out="$PWD/artifacts/phase18-step18/readiness/$sha/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"
chmod 700 "$out"
manifest="$out/manifest.tsv"
printf 'source_sha\t%s\n' "$sha" > "$manifest"
record() { printf '%s\t%s\n' "$1" "$2" | tee -a "$manifest"; }
for tool in curl systemctl ss nginx; do
  command -v "$tool" >/dev/null || { record "tool-$tool" MISSING; exit 2; }
done
record source-branch "$(git branch --show-current)"
if [[ -n "$(git status --porcelain --untracked-files=no)" ]]; then
  record tracked-source DIRTY
else
  record tracked-source CLEAN
fi
if sudo -n test -f /etc/argintaxpayer/phase18-activation-approved 2>/dev/null; then
  record activation-marker PRESENT
elif sudo -n test -d /etc/argintaxpayer 2>/dev/null; then
  record activation-marker NOT_PRESENT_OR_UNREADABLE
else
  record activation-marker UNKNOWN_NO_SUDO
fi
for service in argintaxpayer-api.service argintaxpayer-web.service; do
  active=$(systemctl is-active "$service" 2>/dev/null || true)
  record "$service" "${active:-unknown}"
  # Redacted systemd status only; never print Environment or EnvironmentFile.
  status=$(systemctl show "$service" --no-pager --property=Result --value 2>/dev/null || true)
  record "$service-result" "${status:-unknown}"
done
for port in 15090 15091; do
  listeners=$(ss -ltnH | awk '{print $4}' | grep -E ":$port$" || true)
  if [[ -z "$listeners" ]]; then
    record "listener-$port" NOT_LISTENING
  elif [[ "$listeners" == "127.0.0.1:$port" ]]; then
    record "listener-$port" LOOPBACK_ONLY
  else
    record "listener-$port" REVIEW_EXPOSURE
  fi
done
probe() {
  local label="$1" url="$2"
  local result
  result=$(curl --noproxy '*' --max-time 8 --silent --show-error \
    --output /dev/null --write-out '%{http_code}' "$url" 2>/dev/null) || result=UNREACHABLE
  record "$label" "$result"
}
probe api-live http://127.0.0.1:15090/health/live
probe api-ready http://127.0.0.1:15090/health/ready
probe web-identity http://127.0.0.1:15091/identity
probe web-root http://127.0.0.1:15091/
probe public-https https://tax.argin.ir/
if nginx -t > "$out/nginx-syntax.log" 2>&1; then
  record nginx-syntax PASS
else
  record nginx-syntax REVIEW
fi
record operational-production-authority NOT_VERIFIED
record production-invoice-producer NOT_ACCEPTED
record production-invoice-send NOT_TESTED
record production-legal-successors NOT_TESTED
record release-permission NOT_GRANTED
echo "[DONE] $manifest"
echo '[NOTE] HTTP 200/307 alone does not verify authenticated access or tax-authority functionality.'
