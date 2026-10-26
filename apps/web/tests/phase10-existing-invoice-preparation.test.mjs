import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
test('historical finalized invoices can be prepared and armed without triggering a send',()=>{
 const ui=readFileSync(resolve(root,'apps/web/app/invoices/invoice-send-readiness-panel.tsx'),'utf8');
 const start=ui.indexOf(' const readyExisting=');
 const end=ui.indexOf(' const inquireByReference=',start);
 const code=ui.slice(start,end);
 assert.ok(start>=0&&end>start);
 for(const term of ['NotPrepared','Prepared','/serial-reservation','/prepare-legacy','/arm','loadDispatch','Indeterminate'])
  assert.ok(code.includes(term),term);
 assert.ok(!code.includes('"/send"'));
 assert.ok(!code.includes('sendSandbox('));
 assert.ok(ui.includes('dispatch?.status==="Indeterminate"'));
});
