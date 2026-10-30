import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('internal validation requires immutable revision and atomic journal',()=>{
 const service=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InternalInvoiceValidationService.cs');
 for(const item of ['InvoiceDocumentRevisions.AsNoTracking','captured.SnapshotJson != invoice.SnapshotJson',
  'CreateExecutionStrategy().ExecuteAsync','BeginTransactionAsync','ExecuteUpdateAsync','ExecuteSqlInterpolatedAsync','sandbox_invoice_lifecycle_events',
  'CommitAsync']) assert.ok(service.includes(item),item);
 assert.ok(service.indexOf('BeginTransactionAsync')<service.indexOf('ExecuteUpdateAsync'));
 assert.ok(service.indexOf('ExecuteSqlInterpolatedAsync')<service.indexOf('CommitAsync'));
});
test('audit migration guards test DB and prohibits mutation',()=>{
 const migration=read('src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261025230000_AddSandboxInvoiceLifecycleEvents.cs');
 for(const item of ["argintaxpayer_test","sandbox_invoice_lifecycle_events","BEFORE UPDATE OR DELETE",
 "REFERENCES invoice_documents","throw new NotSupportedException"]) assert.ok(migration.includes(item),item);
});
