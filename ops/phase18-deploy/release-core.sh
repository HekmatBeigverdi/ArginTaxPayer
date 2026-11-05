#!/usr/bin/env bash
# Shared validation and privilege-boundary helpers for Phase 18 releases.
set -Eeuo pipefail

phase18_refuse() {
  echo "[REFUSED] $*" >&2
  return 2
}

phase18_validate_sha() {
  [[ "${1:-}" =~ ^[a-f0-9]{40}$ ]] || phase18_refuse 'Full lowercase 40-character Git SHA required'
}

phase18_verify_archive() {
  local archive="$1" expected_root="$2" required_member="$3" listing verbose
  [[ -f "$archive" && ! -L "$archive" ]] || phase18_refuse "Missing or unsafe archive: $(basename "$archive")"
  listing=$(tar --quoting-style=escape -tzf "$archive") || phase18_refuse "Invalid archive: $(basename "$archive")"
  [[ -n "$listing" ]] || phase18_refuse "Empty archive: $(basename "$archive")"
  while IFS= read -r entry; do
    [[ "$entry" != /* ]] || phase18_refuse 'Absolute archive path rejected'
    case "/$entry/" in */../*) phase18_refuse 'Parent-directory archive path rejected';; esac
    [[ "$entry" == "$expected_root/"* ]] || phase18_refuse "Unexpected archive root: $(basename "$archive")"
  done <<< "$listing"
  verbose=$(tar --quoting-style=escape -tvzf "$archive") || phase18_refuse "Invalid archive metadata: $(basename "$archive")"
  if printf '%s\n' "$verbose" | awk 'substr($1, 1, 1) == "l" || substr($1, 1, 1) == "h" { found=1 } END { exit !found }'; then
    phase18_refuse 'Archive links are not allowed'
  fi
  printf '%s\n' "$listing" | grep -Fxq "$required_member" ||
    phase18_refuse "Required archive member missing: $required_member"
}

phase18_verify_release_bundle() {
  local bundle="$1" sha="$2" resolved sha_lines sum_lines name
  phase18_validate_sha "$sha" || return
  [[ -d "$bundle" && ! -L "$bundle" ]] || phase18_refuse 'Bundle directory missing or symlinked'
  resolved=$(realpath -e -- "$bundle") || phase18_refuse 'Bundle directory cannot be resolved'
  [[ -f "$resolved/SHA256SUMS" && ! -L "$resolved/SHA256SUMS" &&
     -f "$resolved/build-info.txt" && ! -L "$resolved/build-info.txt" ]] ||
    phase18_refuse 'Missing or unsafe bundle metadata'
  [[ $(wc -l < "$resolved/SHA256SUMS") -eq 2 ]] || phase18_refuse 'Checksum manifest must contain exactly two archive entries'
  for name in api.tar.gz web.tar.gz; do
    [[ -f "$resolved/$name" && ! -L "$resolved/$name" ]] || phase18_refuse "Missing or unsafe $name"
    [[ $(grep -Ec "^[a-f0-9]{64}  $name$" "$resolved/SHA256SUMS" || true) -eq 1 ]] ||
      phase18_refuse "Incorrect checksum entry for $name"
  done
  (cd "$resolved" && sha256sum -c --status SHA256SUMS) || phase18_refuse 'Archive checksum mismatch'
  sha_lines=$(grep -Ec '^sha=' "$resolved/build-info.txt" || true)
  [[ "$sha_lines" -eq 1 ]] && grep -Fxq "sha=$sha" "$resolved/build-info.txt" ||
    phase18_refuse 'Source SHA mismatch'
  phase18_verify_archive "$resolved/api.tar.gz" api api/ArginTaxPayer.Api.dll || return
  phase18_verify_archive "$resolved/web.tar.gz" web web/.next/BUILD_ID || return
  phase18_verify_archive "$resolved/web.tar.gz" web web/node_modules/next/dist/bin/next || return
}

phase18_as_root() {
  if [[ "$(id -u)" == 0 ]]; then
    [[ "${PHASE18_ROOT_MODE:-}" == 1 ]] || phase18_refuse 'Root mode is reserved for the installed deployment wrapper'
    "$@"
  else
    sudo "$@"
  fi
}

phase18_as_runtime_user() {
  if [[ "$(id -u)" == 0 && "${PHASE18_ROOT_MODE:-}" == 1 ]]; then
    runuser -u argintaxsvc -- "$@"
  else
    sudo -u argintaxsvc -- "$@"
  fi
}
