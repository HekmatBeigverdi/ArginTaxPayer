import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('Legacy Outbox sender uses Sandbox key custody, not production key selections',()=>{
 const worker=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyOutboxWorker.cs');
 const loader=read('src/ArginTaxPayer.Infrastructure/Signing/Connection/SandboxLegacySigningKeyLoader.cs');
 const registration=read('src/ArginTaxPayer.Infrastructure/Signing/Connection/SigningConnectionRegistration.cs');
 assert.ok(worker.includes('SandboxLegacySigningKeyLoader keys'));
 assert.ok(registration.includes('AddScoped<SandboxLegacySigningKeyLoader>()'));
 for(const term of ['FROM sandbox_fiscal_memories','Sandbox:true',
  'customer_user_id=@owner','taxpayer_id=@taxpayer',
  'UnprotectAsync','FixedTimeEquals'])assert.ok(loader.includes(term),term);
 assert.ok(!loader.includes('FROM fiscal_memories'));
 assert.ok(!loader.includes('SigningKeySelections'));
});
