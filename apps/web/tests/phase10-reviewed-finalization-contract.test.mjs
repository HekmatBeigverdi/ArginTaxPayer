import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('finalization requires validated immutable approved invoice and journals change',()=>{
 const src=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoiceFinalizationService.cs');
 for(const part of ['ScopedCommandQuery','CustomerTaxpayerMemberships','InvoiceReviewState.Approved',
  'InvoiceLifecycleState.Validated','InvoiceDocumentRevisions','CreateExecutionStrategy',
  'BeginTransactionAsync','ExecuteUpdateAsync','sandbox_invoice_lifecycle_events','CommitAsync'])
  assert.ok(src.includes(part),part);
 assert.ok(src.indexOf('BeginTransactionAsync')<src.indexOf('ExecuteUpdateAsync'));
 assert.ok(src.indexOf('ExecuteSqlInterpolatedAsync')<src.indexOf('CommitAsync'));
 assert.ok(!src.includes('HttpClient'));
});
test('finalization API and BFF are registered as distinct internal actions',()=>{
 const api=read('src/ArginTaxPayer.Api/Invoices/InvoiceReviewDevelopmentEndpoints.cs');
 const host=read('src/ArginTaxPayer.Api/Program.cs');
 const bff=read('apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/finalize-internal/route.ts');
 assert.ok(api.includes('/finalize-internal'));
 assert.ok(api.includes('Phase10InvoiceFinalizationService'));
 assert.ok(host.includes('AddScoped<Phase10InvoiceFinalizationService>'));
 for(const term of ['isSameLocalOrigin','ACCESS_COOKIE','expectedRevision','finalize-internal'])
  assert.ok(bff.includes(term),term);
});
