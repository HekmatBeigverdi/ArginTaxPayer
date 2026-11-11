import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const read = (path) => readFileSync(resolve(root, path), 'utf8');

test('production workspace registration explicitly creates production memories', () => {
  const ui = read('apps/web/app/taxpayers/taxpayer-workspace.tsx');
  assert.match(ui, /authorityEnvironment: 1/);
  const service = read('src/ArginTaxPayer.Infrastructure/Persistence/Taxpayers/FiscalMemoryManagementService.cs');
  assert.match(service, /request\?\.AuthorityEnvironment is not/);
  assert.match(service, /AuthorityEnvironment = requestedEnvironment/);
});

test('legacy fiscal memories remain explicitly unclassified after migration', () => {
  const migration = read('src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261104220000_AddFiscalMemoryAuthorityEnvironment.cs');
  assert.match(migration, /nullable: true/);
  assert.doesNotMatch(migration, /defaultValue:/);
  assert.match(migration, /authority_environment IS NULL OR authority_environment IN/);
  assert.match(migration, /current_database\(\) = 'argintaxpayer_prod'/);
  assert.match(migration, /argin_prod_migrator/);
  assert.match(migration, /throw new NotSupportedException/);
});

test('legacy Sandbox dispatch blockers remain until full authority migration', () => {
  const script = read('scripts/phase18-step09-dual-authority-preflight.sh');
  assert.match(script, /Persisted authority environment and production dual-routing not established/);
  const apiUnit = read('ops/phase18-systemd/argintaxpayer-api.service');
  assert.match(apiUnit, /Environment=ArginTaxPayer__EnableOperationalEndpoints=false/);
});
