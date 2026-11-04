#!/usr/bin/env bash
# Step07 schema/data backup and isolated restore drill: ONLY on the dedicated
# production PostgreSQL container and before user data, keys or application use.
# Never restore into the source DB or shared/QA PostgreSQL. No DROP DATABASE.
set -Eeuo pipefail
umask 077
fail(){ echo "[REFUSED] $*" >&2; exit 2; }
cd "$(dirname "${BASH_SOURCE[0]}")/.."
[[ "$(uname -s)" == Linux && "$(id -u)" != 0 ]] || fail 'Ubuntu nonroot only'
[[ "$(git branch --show-current)" == phase/18-production-deployment ]] || fail 'Wrong branch'
[[ -z "$(git status --porcelain --untracked-files=no)" ]] || fail 'Tracked files modified'
sha=$(git rev-parse HEAD)
[[ "${PHASE18_EXPECTED_SHA:-}" =~ ^[0-9a-f]{40}$ && "$sha" == "$PHASE18_EXPECTED_SHA" ]] ||
  fail 'Identical approved macOS/Ubuntu commit SHA required'
[[ "${PHASE18_RESTORE_DRILL_APPROVED:-}" == YES ]] ||
  fail 'Set PHASE18_RESTORE_DRILL_APPROVED=YES for disposable recovery DB creation'
for t in psql sha256sum sudo docker; do
  command -v "$t" >/dev/null || fail "Missing $t"
done
root="$PWD/artifacts/phase18-step07/production-private"
[[ -f "$root/compose.env" && -f "$root/ca/ca.pem" ]] ||
  fail 'Production DB not provisioned'
[[ "$(stat -c %a "$root/compose.env")" == 600 ]] ||
  fail 'Insecure Production secrets permissions'
migrator=$(sed -n 's/^PHASE18_PROD_MIGRATOR_PASSWORD=//p' "$root/compose.env")
[[ "$migrator" =~ ^[a-f0-9]{64}$ ]] || fail 'Missing Production migrator password'
export PGPASSWORD="$migrator" PGSSLROOTCERT="$root/ca/ca.pem" PGSSLMODE=verify-full
source_db=argintaxpayer_prod
dbhost=127.0.0.1
port=15434
user=argin_prod_migrator
common=(-h "$dbhost" -p "$port" -U "$user" -X -v ON_ERROR_STOP=1)
proof=$(psql "${common[@]}" -d "$source_db" -Atc "SELECT current_database(),current_user,(SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid());")
[[ "$proof" == 'argintaxpayer_prod|argin_prod_migrator|t' ]] ||
  fail 'Wrong database, role or TLS mode'
# Guard against generating an unencrypted backup of real customer data.
# This acceptance runs only before application has any persisted identity rows.
has_identity=$(psql "${common[@]}" -d "$source_db" -Atc "SELECT to_regclass('public.identity_users') IS NOT NULL;")
if [[ "$has_identity" == t ]]; then
  rows=$(psql "${common[@]}" -d "$source_db" -Atc 'SELECT COUNT(*) FROM public.identity_users;')
  [[ "$rows" == 0 ]] || fail 'Real identity rows detected; encrypted Step12 backup required instead'
fi
# The recovery DB is separate and uniquely named and is never reused/deleted.
stamp=$(date -u +%Y%m%d%H%M%S)
recovery="argin_p18_restore_$stamp"
out="$PWD/artifacts/phase18-step07/restore/$sha/$stamp"
mkdir -p "$out"; chmod 700 "$out"
echo '[START] Custom-format backup of dedicated, unused Production DB'
# Use the PostgreSQL 17 clients embedded in the dedicated container rather
# than changing the system's existing PostgreSQL client for unrelated apps.
sudo -n docker exec -u postgres argintaxpayer-phase18-production-postgres \
  pg_dump -U argin_prod_migrator -d "$source_db" \
    --format=custom --no-owner --no-acl \
  > "$out/production.dump" 2> "$out/dump.log" || fail 'pg_dump failed'
[[ -s "$out/production.dump" ]] || fail 'Backup empty'
chmod 600 "$out/production.dump"
sha256sum "$out/production.dump" > "$out/production.sha256"
sudo -n docker exec -i -u postgres argintaxpayer-phase18-production-postgres \
  pg_restore --list < "$out/production.dump" > "$out/archive-manifest.txt" ||
  fail 'Backup archive unreadable'
if [[ -z "$(sudo -n docker port argintaxpayer-phase18-production-postgres 5432/tcp | grep -Fx '127.0.0.1:15434')" ]]; then
  fail 'Dedicated Docker database port altered; refuse restore'
fi
# Only exact dedicated container; create a second DB with migrator ownership.
echo '[START] Create distinct recovery database on dedicated isolated instance'
sudo -n docker exec -u postgres argintaxpayer-phase18-production-postgres \
  psql -X -v ON_ERROR_STOP=1 -U argin_prod_bootstrap -d postgres \
  -c "CREATE DATABASE $recovery OWNER argin_prod_migrator;" \
  > "$out/recovery-create.log" 2>&1 ||
  fail 'Recovery DB creation failed. Never retry under another instance.'
echo "[INFO] Recovery database created: $recovery (retained for inspection)"
sudo -n docker exec -i -u postgres argintaxpayer-phase18-production-postgres \
  pg_restore -U argin_prod_migrator -d "$recovery" \
    --exit-on-error --no-owner --no-acl \
    < "$out/production.dump" > "$out/recovery-restore.log" 2>&1 ||
  fail 'Restore failed; retained separate recovery DB for safe investigation'
count_src=$(psql "${common[@]}" -d "$source_db" -Atc "SELECT count(*) FROM pg_tables WHERE schemaname='public';")
count_dst=$(psql "${common[@]}" -d "$recovery" -Atc "SELECT count(*) FROM pg_tables WHERE schemaname='public';")
[[ "$count_src" == "$count_dst" ]] || fail 'Restored schema table count mismatch'
# Compare EF history row count separately when migrations are installed.
hist_src=$(psql "${common[@]}" -d "$source_db" -Atc "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL;")
hist_dst=$(psql "${common[@]}" -d "$recovery" -Atc "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL;")
[[ "$hist_src" == "$hist_dst" ]] || fail 'Migration history presence mismatch'
if [[ "$hist_src" == t ]]; then
  a=$(psql "${common[@]}" -d "$source_db" -Atc 'SELECT count(*) FROM "__EFMigrationsHistory";')
  b=$(psql "${common[@]}" -d "$recovery" -Atc 'SELECT count(*) FROM "__EFMigrationsHistory";')
  [[ "$a" == "$b" ]] || fail 'Restored EF migration history differs'
fi
printf 'source_sha\t%s\nbackup\tPASS\narchive-integrity\tPASS\nrestore-database\t%s\nrestore\tPASS\nsource-tables\t%s\nrestored-tables\t%s\nproduction-live-traffic\tBLOCKED\n' \
  "$sha" "$recovery" "$count_src" "$count_dst" > "$out/manifest.tsv"
unset PGPASSWORD PGSSLMODE PGSSLROOTCERT migrator
echo "[PASS] Backup and restore drill on dedicated DB; recovery copy $recovery preserved."
echo "[DONE] Private report $out/manifest.tsv"
echo '[NOTE] Dump may be sensitive; retain under owner-only 0700 dir, never upload to Git.'
