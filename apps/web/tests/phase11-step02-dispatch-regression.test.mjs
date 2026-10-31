// Phase 11 Step 02: offline regression guard over the proven Phase 10 dispatch path.
// No authority calls, no database, no runtime behavior changes.
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const source = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const worker = source('../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyOutboxWorker.cs');
const outbox = source('../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10SandboxOutboxService.cs');
const reconciliation = source('../../../src/ArginTaxPayer.Application/Invoices/Phase10InquiryReconciliation.cs');
const evidence = source('../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10DispatchEvidenceRepository.cs');

test('proven Phase 10 worker is explicitly invoked, not silently scheduled to send', () => {
  assert.match(worker, /Explicitly invoked Sandbox worker/);
  assert.match(worker, /not registered as a background/);
  assert.match(worker, /ProcessInvoiceAsync\(/);
  assert.match(worker, /operation\.State != InvoiceDispatchState\.Reserved/);
  assert.match(worker, /BeginSendAsync\(/);
  assert.match(worker, /AppendInitialReceiptAsync\(/);
});
test('Outbox retains immutable identity and disables automatic resend', () => {
  assert.match(outbox, /deduplicationKey/);
  assert.match(outbox, /automaticRetry = false/g);
  assert.match(outbox, /invoice\.sandbox\.legacy\.dispatch\.requested/);
  assert.doesNotMatch(outbox, /SendPreparedAsync\(/);
});
test('inconclusive inquiry never authorizes resend or final fiscal acceptance', () => {
  assert.match(reconciliation, /MayAutomaticallyResend => false/);
  assert.match(reconciliation, /IsFiscalAccepted => false/);
  assert.match(reconciliation, /ProvesNotDelivered => false/);
});
test('evidence updates require scoped revision comparison and preserve append-only inquiry', () => {
  assert.match(evidence, /expectedStateRevision/);
  assert.match(evidence, /FOR UPDATE/);
  assert.match(evidence, /state_revision=state_revision\+1/);
  assert.match(evidence, /advanceTo: null/);
});
