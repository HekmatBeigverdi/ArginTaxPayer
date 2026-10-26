import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('Prepared is state 1 while Reserved is state 3',()=>{
 const states=read('src/ArginTaxPayer.Domain/Invoices/InvoiceDispatchContracts.cs');
 assert.match(states,/Prepared = 1/);assert.match(states,/Reserved = 3/);
});
test('arming checks existing signed outbox, no attempts and never calls HTTP',()=>{
 const service=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10DispatchArmService.cs');
 for(const x of ['InvoiceDispatchReadService','state=3','d.state=1',
  'o.event_type=\'invoice.sandbox.legacy.dispatch.requested\'',
  'NOT EXISTS(SELECT 1 FROM invoice_dispatch_attempts','o.processed_at_utc IS NULL',
  'o.payload_json::jsonb','BeginTransactionAsync','CommitAsync'])
  assert.ok(service.includes(x),x);
 assert.ok(!service.includes('HttpClient'));
});
test('UI arms before offering actual send',()=>{
 const ui=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 const route=read('apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/arm/route.ts');
 assert.ok(ui.includes('dispatch?.status==="Prepared"'));
 assert.ok(ui.includes('armDispatch'));
 assert.ok(route.includes('isSameLocalOrigin'));
 assert.ok(route.includes('authoritySubmitted:false'));
});
