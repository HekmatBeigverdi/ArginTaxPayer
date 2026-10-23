#!/usr/bin/env bash
# No migration, deployment or tax-authority request is performed here.
set -Eeuo pipefail
exec python3 "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/phase09_verify.py" "$@"
