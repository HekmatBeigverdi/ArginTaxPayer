import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {dirname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('Legacy Worker uses Sandbox memory code for GET_TOKEN instead of invoice tax ID',()=>{
 const worker=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyOutboxWorker.cs');
 const coordinator=read('src/ArginTaxPayer.Infrastructure/Signing/Connection/LegacyInvoiceOutboxSendCoordinator.cs');
 for(const value of ['FROM sandbox_fiscal_memories','fiscalMemoryCode','PrepareAsync(message.PayloadJson, key, ct, fiscalMemoryCode)'])
  assert.ok(worker.includes(value),value);
 assert.ok(coordinator.includes('sandboxClientId ?? ReadFiscalId(outboxPayloadJson)'));
});
test('Legacy Outbox evidence is looked up by scoped immutable identifiers',()=>{
 const readiness=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LiveSendReadinessService.cs');
 for(const value of ['JOIN outbox_messages','customer_user_id=@owner','invoice_revision=@revision',
  'fiscal_memory_id=@memory','requestSha256','if(!signedPacketAvailable)'])
  assert.ok(readiness.includes(value),value);
 const bff=read('apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/readiness/route.ts');
 assert.ok(bff.includes('typeof data.signedPacketAvailable!=="boolean"'));
});
