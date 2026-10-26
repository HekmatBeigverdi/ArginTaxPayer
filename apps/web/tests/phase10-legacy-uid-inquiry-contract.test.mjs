import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('UID inquiry scopes existing Indeterminate operation without submission',()=>{
 const service=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyUidInquiryService.cs');
 for(const phrase of ['d.state=6','invoice_dispatch_attempts','SandboxLegacySigningKeyLoader',
 'INQUIRY_BY_UID','UidList=new[]{uid}','FiscalId=memoryCode','inquiry_result_shape_unrecognized','inquiry_no_results','self-tsp/sync/INQUIRY_BY_UID/','packetType!="INVOICE.V01"',
 'inquiry_http_404','inquiry_http_405','Phase10InquiryReconciliation.Evaluate','AppendInquiryEvidenceAsync','uid!=operationId.ToString("D")'])
  assert.ok(service.includes(phrase),phrase);
 assert.ok(!service.includes('normal-enqueue'));
 assert.ok(!service.includes('dispatch/send'));
});
test('same-origin UID inquiry UI only appears for Indeterminate',()=>{
 const ui=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 const bff=read('apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/inquiry-by-uid/route.ts');
 assert.ok(ui.includes('dispatch?.status==="Indeterminate"'));
 assert.ok(ui.includes('inquireByUid'));
 assert.ok(bff.includes('isSameLocalOrigin'));
 assert.ok(bff.includes('authoritySubmitted:false'));
});
