#!/usr/bin/env bash
# ArginTaxPayer Phase 18 / Step 03 — read-only Ubuntu 24 VPS preflight.
# Never changes DNS, firewall, SSH, DB, processes, packages, credentials or system services.
set -Eeuo pipefail
umask 077

refuse() { printf '[REFUSED] %s\n' "$*" >&2; exit 2; }
[[ "$(uname -s)" == Linux ]] || refuse 'Linux required'
[[ -r /etc/os-release ]] || refuse 'Missing /etc/os-release'
. /etc/os-release
[[ "${ID:-}" == ubuntu && "${VERSION_ID:-}" == 24.04 ]] || refuse 'Ubuntu 24.04 required'
[[ "$(id -u)" != 0 ]] || refuse 'Run as ordinary argindev account, not root/sudo'
command -v git >/dev/null || refuse 'git missing'
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || refuse 'Switch to Phase 18 branch'
sha="$(git rev-parse HEAD)"
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || refuse 'Tracked worktree dirty; inspect first'
stamp="$(date -u +%Y%m%dT%H%M%SZ)"
out="artifacts/phase18-step03/ubuntu-$stamp"
mkdir -p "$out"
chmod 700 "$out"
manifest="$out/manifest.tsv"
printf 'check\tresult\tnote\n' > "$manifest"
printf '[INFO] SHA=%s\n[INFO] Report=%s\n' "$sha" "$out"
failure=0
run() {
  local name="$1"; shift
  local log="$out/$name.log"
  if "$@" > "$log" 2>&1; then
    printf '[PASS] %s\n' "$name"
    printf '%s\tPASS\t%s\n' "$name" "$log" >> "$manifest"
  else
    local rc=$?
    printf '[WARN] %s: exit %s (see local report)\n' "$name" "$rc"
    printf '%s\tREVIEW(%s)\t%s\n' "$name" "$rc" "$log" >> "$manifest"
    failure=$((failure+1))
  fi
}
run platform bash -c 'set -e; printf "os=%s version=%s\n" "$1" "$2"; uname -m; uptime -p; date -u +%FT%TZ; timedatectl show -p NTPSynchronized -p Timezone --value' _ "$ID" "$VERSION_ID"
run resources bash -c 'set -e; free -h; df -h /; df -i /; findmnt -no FSTYPE /'
run runtime bash -c 'for cmd in dotnet node npm psql nginx certbot docker; do if command -v "$cmd" >/dev/null; then printf "%s:present\n" "$cmd"; else printf "%s:absent\n" "$cmd"; fi; done; dotnet --version 2>/dev/null || true; node --version 2>/dev/null || true'
# Only socket address and port (no process owners, command-line arguments or secrets).
run sockets bash -c "ss -ltnH | awk '{print \$4}' | sort -u"
run firewall bash -c 'if command -v ufw >/dev/null; then ufw status 2>&1 || true; else printf "ufw not installed\n"; fi'
run services bash -c 'for svc in ssh sshd postgresql nginx docker; do state=$(systemctl is-active "$svc" 2>/dev/null || true); printf "%s=%s\n" "$svc" "$state"; done'
run sshd_validation bash -c 'if command -v sshd >/dev/null; then if [ -x /usr/sbin/sshd ]; then /usr/sbin/sshd -t; else sshd -t; fi; else echo "sshd missing"; exit 1; fi'
run disk_ownership bash -c 'stat -c "%A %U:%G %n" "$HOME" "$PWD" 2>/dev/null'
# Public host/network probing is deferred until endpoints and authorized IP are verified.
printf 'external_dns\tMANUAL_PENDING\tAuthoritative DNS provider/IPv4/AAAA/CDN not yet verified\n' >> "$manifest"
printf 'firewall_approval\tMANUAL_PENDING\tNever enable UFW until SSH allowlist and rollback validated\n' >> "$manifest"
printf 'backup_snapshot\tMANUAL_PENDING\tProvider snapshot/restore confirmation required before any change\n' >> "$manifest"
printf 'outbound_tax_apis\tMANUAL_PENDING\tVerify exact official hosts and authorized TLS diagnostics in Step 10\n' >> "$manifest"
printf 'production_go_live\tBLOCKED\tRequires Steps 04-18 incl. secure Production API route registration\n' >> "$manifest"
printf '[DONE] Read-only inventory complete. Review %s locally. Warning count: %s\n' "$manifest" "$failure"
printf '[CAUTION] Logs may contain hostnames/internal IPs; do not post raw files publicly. Summarize results and redact before sharing.\n'
