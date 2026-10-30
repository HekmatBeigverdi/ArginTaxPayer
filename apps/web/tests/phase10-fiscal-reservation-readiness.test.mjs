import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('readiness reflects scoped durable fiscal reservation instead of fixed blockers',()=>{
 const service=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LiveSendReadinessService.cs');
 for(const item of ['sandbox_invoice_fiscal_reservations','customer_user_id=@owner','taxpayer_id=@taxpayer',
   'invoice_id=@invoice','invoice_revision=@revision','sandbox_memory_id=@memory','if (!reserved)'])
   assert.ok(service.includes(item),item);
});
test('finalized invoice reservation stays scoped and precedes one-click preparation',()=>{
 const proxy=read('apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/serial-reservation/route.ts');
 const ui=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 for(const item of ['isSameLocalOrigin','ACCESS_COOKIE','dispatch/serial-reservation',
  'expectedRevision','IdenticalReplay'])
  assert.ok(proxy.includes(item),item);
 const service=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10OneClickSandboxSubmissionService.cs');
 assert.ok(ui.includes('/send-and-inquire'));
 assert.ok(service.includes('if(info.InvoiceState!="Finalized")return Stop'));
 assert.ok(service.includes('serials.ReserveAsync(bearer,taxpayerId,invoiceId,revision,ct)'));
 assert.ok(service.indexOf('serials.ReserveAsync')<service.indexOf('preparation.PrepareAsync'));
});
