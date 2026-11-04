import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const read = (path) => readFileSync(path, 'utf8');
test('production systemd units remain activation-gated and services disabled by default', () => {
  const api = read('ops/phase18-systemd/argintaxpayer-api.service');
  const web = read('ops/phase18-systemd/argintaxpayer-web.service');
  for (const unit of [api, web]) {
    assert.match(unit, /ConditionPathExists=\/etc\/argintaxpayer\/phase18-activation-approved/);
    assert.match(unit, /NoNewPrivileges=true/);
    assert.match(unit, /ProtectSystem=strict/);
    assert.match(unit, /UMask=0077/);
  }
  assert.match(api, /Environment=ArginTaxPayer__EnableOperationalEndpoints=false/);
  assert.match(api, /Environment=Ghasedak__EnableLiveSending=false/);
  assert.match(web, /Environment=ARGIN_PRODUCTION_UI_ENABLED=false/);
});
test('staging script must not rotate keys, activate services or modify shared PostgreSQL', () => {
  const script = read('scripts/phase18-step08-stage-private-config.sh');
  assert.match(script, /--stage-disabled-private-config/);
  assert.match(script, /PHASE18_OWNER_APPROVED_CONFIG_STAGING/);
  assert.match(script, /refuse overwrite/);
  assert.match(script, /install -o root -g root -m 0600/);
  assert.doesNotMatch(script, /(?:systemctl\s+(?:start|enable|restart)|docker\s+(?:rm|stop|down)|dotnet ef database update|DROP DATABASE)/);
  assert.match(script, /ArginTaxPayer__EnableOperationalEndpoints=false/);
  assert.match(script, /Ghasedak__EnableLiveSending=false/);
  assert.match(script, /Identity__MfaEncryptionKey=\$mfa_key/);
  assert.match(script, /SigningKeyProtection__DatabaseMasterKey=\$wrapping_key/);
  assert.match(script, /Port=15434;Database=argintaxpayer_prod;Username=argin_prod_app/);
  assert.match(script, /sudo install -o root -g argintaxsvc -m 0640/);
});
test('Step08 preflight never prints or exports raw secrets', () => {
  const script = read('scripts/phase18-step08-config-custody-preflight.sh');
  assert.match(script, /PHASE18_EXPECTED_SHA/);
  assert.match(script, /source_sha/);
  assert.match(script, /key-restart-recovery\\tNOT_YET_TESTED/);
  assert.doesNotMatch(script, /(?:cat\s+.*compose\.env|sed\s+.*PASSWORD|echo\s+.*PASSWORD)/);
});

test('private configuration audit checks both keys without disclosing secret values', () => {
  const audit = read('scripts/phase18-step08-private-config-audit.sh');
  assert.match(audit, /base64\.b64decode\(a\[key\],validate=True\)/);
  assert.match(audit, /Identity__MfaEncryptionKey/);
  assert.match(audit, /SigningKeyProtection__DatabaseMasterKey/);
  assert.match(audit, /ArginTaxPayer__EnableOperationalEndpoints/);
  assert.match(audit, /root-only 0600/);
});
