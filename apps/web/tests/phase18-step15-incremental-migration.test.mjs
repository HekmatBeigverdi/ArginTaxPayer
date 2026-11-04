import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';

const repoRoot=fileURLToPath(new URL('../../../',import.meta.url));
const read=p=>readFileSync(resolve(repoRoot,p),'utf8');
test('step15 applies only named additive migration following step10 history',()=>{
  const script=read('scripts/phase18-step15-incremental-migrate.sh');
  assert.match(script,/target=20261104230000_BindDispatchAuthorityEnvironment/);
  assert.match(script,/20261104220000_AddFiscalMemoryAuthorityEnvironment/);
  assert.match(script,/database update "\$target"/);
  assert.match(script,/table_name='invoice_dispatch_operations' AND column_name='authority_environment' AND is_nullable='YES'/);
  assert.match(script,/sha256sum "\$review"/);
  assert.match(script,/phase18-step07-backup-restore-drill.sh/);
  assert.match(script,/Existing identity data: use separately approved ENCRYPTED backup/);
  assert.doesNotMatch(script,/phase18-activation-approved\s*\]\]/);
});
test('step15 migration remains additive and marks history as unclassified',()=>{
  const source=read('src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261104230000_BindDispatchAuthorityEnvironment.cs');
  assert.match(source,/ADD COLUMN authority_environment integer NULL/);
  assert.match(source,/CREATE TRIGGER trg_p18_dispatch_scope_immutable/);
  assert.match(source,/NEW.authority_environment IS DISTINCT FROM OLD.authority_environment/);
  assert.doesNotMatch(source,/UPDATE invoice_dispatch_operations[\s\S]*?SET authority_environment/);
});
