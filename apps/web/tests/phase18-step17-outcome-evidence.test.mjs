import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

const root=fileURLToPath(new URL('../../../',import.meta.url));
const src=readFileSync(resolve(root,
  'src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18DispatchOutcomeRepository.cs'),'utf8');

test('append-only initial receipt is correlated to sending attempt under environment lock',()=>{
  for(const fragment of [
    'm.authority_environment=@environment',
    'd.authority_environment=@environment',
    'a.id=@attempt AND a.attempt_number=1',
    'd.state=4 AND d.state_revision=@revision',
    'NOT EXISTS(SELECT 1 FROM invoice_dispatch_receipts',
    'FOR UPDATE OF d',
    'INSERT INTO invoice_dispatch_receipts',
    'receipt_kind,authority_reference,',
    'InvoiceSubscriptionGate.ConsumeAsync'
  ]) assert.ok(src.includes(fragment),fragment);
  assert.match(src,/acknowledged \? 5 : 6/);
});

test('indeterminate result has no automatic resend, no success claim, and no receipt',()=>{
  assert.match(src,/if \(acknowledged\)\s*\{\s*await using var receipt/);
  assert.match(src,/acknowledged \? "Acknowledged" : "Indeterminate"/);
  assert.doesNotMatch(src,/Retry|PostAsync|HttpClient|Reenqueue|SendAsync/);
});

test('outcome cannot skip valid reference or stored authority match',()=>{
  assert.match(src,/acknowledged != \(referenceNumber is not null\)/);
  assert.match(src,/gate.ResolveAsync\(operationId, customerUserId/);
  assert.match(src,/persisted.Environment != registeredProvider/);
  assert.match(src,/EvidenceHash\(/);
});
