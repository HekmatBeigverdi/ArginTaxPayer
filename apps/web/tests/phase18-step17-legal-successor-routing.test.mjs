import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

const root=fileURLToPath(new URL('../../../',import.meta.url));
const read=path=>readFileSync(resolve(root,path),'utf8');

test('cancel, correction and return inherit the predecessor persisted memory environment only',()=>{
  const source=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18LegalSuccessorAuthorityGate.cs');
  assert.match(source,/gate.ResolveAsync\(predecessorOperationId/);
  assert.match(source,/original.Environment != registeredProvider/);
  assert.match(source,/i.Subject != InvoiceSubject.Original/);
  assert.match(source,/i.CustomerUserId == customerUserId/);
  assert.match(source,/i.TaxpayerId == taxpayerId/);
  assert.match(source,/original with \{ InvoiceId = successorInvoiceId \}/);
  assert.match(source,/Phase18AuthorityRoutingPolicy.CanInheritLegalSuccessor/);
  assert.doesNotMatch(source,/HttpClient|PostAsync|SendAsync/);
});
