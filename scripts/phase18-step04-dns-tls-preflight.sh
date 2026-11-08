#!/usr/bin/env bash
# Phase18 Step04 read-only DNS/TLS preflight; no changes to VPS/DNS/UFW.
set -Eeuo pipefail
umask 077
[[ "$(uname -s)" == Linux ]] || exit 2
. /etc/os-release
[[ "$ID" == ubuntu && "$VERSION_ID" == 24.04 ]] || exit 2
[[ "$(id -u)" != 0 ]] || { echo "[REFUSED] Run without sudo"; exit 2; }
cd "$(dirname "$0")/.."
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || exit 2
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || exit 2
out="artifacts/phase18-step04/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$out"; chmod 700 "$out"
echo "[INFO] Git SHA: $(git rev-parse HEAD)"
echo "[INFO] Report: $out"
for cmd in dig nginx certbot curl openssl; do
  if command -v "$cmd" >/dev/null; then echo "[PRESENT] $cmd"; else echo "[MISSING] $cmd"; fi
done
if command -v dig >/dev/null; then
  {
    echo '--- apex NS ---'; dig +short argin.ir NS
    for type in A AAAA CNAME CAA; do
      echo "--- tax.argin.ir $type ---"
      dig +short tax.argin.ir "$type"
    done
  } > "$out/dns.log" 2>&1
  echo "[PASS] DNS interrogation completed; examine $out/dns.log"
else
  echo "[PENDING] install DNS client later for record inspection"
fi
ss -ltnH | awk '{print $4}' | sort -u > "$out/listeners.log"
systemctl is-active nginx > "$out/nginx-service.log" 2>&1 || true
{ nginx -v; certbot --version; } > "$out/tls-tools.log" 2>&1 || true
printf 'DNS A vs actual VPS public IP: MANUAL_PENDING\n'
printf 'External 80/443 ingress and IPv6: MANUAL_PENDING\n'
printf 'Nginx virtual-host conflicts: MANUAL_PENDING\n'
printf 'TLS certificate and renewal: MANUAL_PENDING\n'
printf '[DONE] Read-only; no service/firewall/DNS/database changes.\n'
