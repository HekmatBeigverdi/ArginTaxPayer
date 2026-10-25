import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {dirname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('review cursor has owner-scoped retrieval and dedicated UI actions',()=>{
 const api=read('src/ArginTaxPayer.Api/Invoices/InvoiceReviewDevelopmentEndpoints.cs');
 const service=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10ReviewReadService.cs');
 const panel=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 for(const x of ['ScopedQuery','InvoiceDocuments','InvoiceReviews','ReviewState']) assert.ok(service.includes(x),x);
 for(const x of ['review-status','review-action','finalize-internal']) assert.ok(panel.includes(x),x);
 for(const x of ['review-status','finalize-internal']) assert.ok(api.includes(x),x);
});
test('Sandbox dispatch FK migration refuses existing evidence or production database',()=>{
 const sql=read('src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261025235900_AlignSandboxDispatchMemoryForeignKey.cs');
 for(const x of ['argintaxpayer_test','EXISTS(SELECT 1 FROM invoice_dispatch_operations)',
  'REFERENCES sandbox_fiscal_memories(id)','DROP CONSTRAINT',
  'throw new NotSupportedException']) assert.ok(sql.includes(x),x);
});
