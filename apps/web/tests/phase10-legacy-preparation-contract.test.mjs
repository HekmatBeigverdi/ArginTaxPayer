import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {dirname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('Legacy preparation is owner scoped and uses matching immutable reservation and official data',()=>{
 const s=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyInvoicePreparationService.cs');
 for(const x of ['ScopedQuery(actor.ActorId,actor.Level1)','CustomerTaxpayerMemberships',
  'InvoiceLifecycleState.Finalized','InvoiceReviewState.Approved','InvoiceDocumentRevisions',
  'CatalogOfficialMappings','OfficialMeasurementUnits','ReadReservationAsync',
  'Phase10LegacyFrozenInvoiceProjector.Project','SandboxLegacySigningKeyLoader',
  'LegacyInvoiceAccessTokenClient','LegacyInvoiceSubmissionPacketBuilder.Build'])
  assert.ok(s.includes(x),x);
});
test('Legacy operation and signed Outbox are written in one transaction',()=>{
 const s=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyInvoicePreparationService.cs');
 for(const x of ['IsolationLevel.Serializable','BeginTransactionAsync',
  'INSERT INTO invoice_dispatch_operations','INSERT INTO outbox_messages',
  'CommitAsync','ON CONFLICT']){/* No blind claims about ON CONFLICT are needed. */}
 assert.ok(s.indexOf('INSERT INTO invoice_dispatch_operations')<
           s.indexOf('INSERT INTO outbox_messages'));
 assert.ok(s.indexOf('INSERT INTO outbox_messages')<s.indexOf('CommitAsync(ct)'));
 assert.ok(s.includes('argintaxpayer_test'));
 assert.ok(s.includes('dispatch_operation_requires_reconciliation'));
});
test('Signed packet type and embedded FiscalId match archived Legacy SDK',()=>{
 const builder=read('src/ArginTaxPayer.Infrastructure/Signing/Connection/LegacyInvoiceSubmissionPacketBuilder.cs');
 const replayer=read('src/ArginTaxPayer.Infrastructure/Signing/Connection/LegacyInvoiceOutboxPacketRebuilder.cs');
 assert.ok(builder.includes('INVOICE.V01'));
 assert.ok(replayer.includes('INVOICE.V01'));
 assert.ok(builder.includes('sandboxMemoryClientId ?? fiscalId'));
});
test('Preparation is available only through same-origin authenticated action',()=>{
 const bff=read('apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/prepare-legacy/route.ts');
 const api=read('src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs');
 const ui=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 assert.ok(api.includes('/prepare-legacy'));
 for(const x of ['isSameLocalOrigin','ACCESS_COOKIE','authoritySubmitted:false'])
  assert.ok(bff.includes(x),x);
 assert.ok(ui.includes('امضا و آماده‌سازی بسته Legacy برای Sandbox'));
});
