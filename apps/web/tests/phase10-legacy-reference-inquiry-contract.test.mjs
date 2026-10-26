import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('reference inquiry requires acknowledged dispatch and stored authority reference',()=>{
 const service=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyReferenceInquiryService.cs');
 for(const x of ['d.state=5','receipt.receipt_kind=1','receipt.authority_reference IS NOT NULL',
 'ReferenceNumbers=new[]{referenceNumber}','INQUIRY_BY_REFERENCE_NUMBER','Phase10InquiryReconciliation.Evaluate'])
  assert.ok(service.includes(x),x);
 assert.ok(!service.includes('normal-enqueue'));
 assert.ok(!service.includes('BeginSendAsync'));
});
test('reference inquiry is scoped, same-origin and gated by acknowledged UI state',()=>{
 const route=read('apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/inquiry-by-reference/route.ts');
 const ui=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 assert.ok(route.includes('isSameLocalOrigin'));
 assert.ok(route.includes('authoritySubmitted:false'));
 assert.ok(ui.includes('dispatch?.status==="Acknowledged"'));
 assert.ok(ui.includes('inquireByReference'));
});
