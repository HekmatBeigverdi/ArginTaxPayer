import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {dirname, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';

const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const backend=readFileSync(resolve(root,'src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LiveSendReadinessService.cs'),'utf8');
const bff=readFileSync(resolve(root,'apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/readiness/route.ts'),'utf8');
const ui=readFileSync(resolve(root,'apps/web/app/invoices/invoice-send-readiness-panel.tsx'),'utf8');

test('server-side readiness diagnostic codes remain accepted and localized',()=>{
  const declared=[...backend.matchAll(/blockers\.Add\("([^"]+)"\)/g)].map(match=>match[1]);
  assert.ok(declared.length>0);
  for(const code of new Set(declared)){
    assert.ok(bff.includes('"'+code+'"'),'BFF rejected code: '+code);
    assert.ok(ui.includes('"'+code+'"'),'UI lacks translation: '+code);
  }
});
