import assert from 'node:assert/strict';

export function assertIsolatedDatabaseAdmission(sql) {
  assert.match(sql, new RegExp(
    String.raw`IF\s+NOT\s*\(\s*current_database\(\)\s*=\s*'argintaxpayer_test'\s*` +
    String.raw`OR\s*\(\s*current_database\(\)\s*=\s*'argin_phase18_acceptance'\s*` +
    String.raw`AND\s+current_user\s*=\s*'phase18_acceptance'\s*` +
    String.raw`AND\s+EXISTS\s*\(\s*SELECT\s+1\s+FROM\s+pg_stat_ssl\s*` +
    String.raw`WHERE\s+pid\s*=\s*pg_backend_pid\(\)\s+AND\s+ssl\s*\)\s*\)\s*\)\s*THEN\s+RAISE\s+EXCEPTION`
  ));
}
