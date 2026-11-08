import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const src = readFileSync(resolve(root, 'scripts/phase18-step18-domain-readiness.sh'), 'utf8');

test('domain preflight is observation only and does not activate authority or services', () => {
  assert.match(src, /phase18-activation-approved/);
  assert.match(src, /systemctl is-active/);
  assert.match(src, /https:\/\/tax\.argin\.ir\//);
  assert.match(src, /\/health\/ready/);
  assert.match(src, /production-invoice-send NOT_TESTED/);
  assert.match(src, /release-permission NOT_GRANTED/);
  assert.doesNotMatch(src, /systemctl\s+(?:start|restart|enable|stop)|dotnet ef database update|psql |pg_dump |normal-enqueue|rm -rf|chmod 777/);
});
