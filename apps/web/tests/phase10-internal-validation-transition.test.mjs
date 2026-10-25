import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
const service=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InternalInvoiceValidationService.cs');
const api=read('src/ArginTaxPayer.Api/Invoices/InvoiceReviewDevelopmentEndpoints.cs');
const host=read('src/ArginTaxPayer.Api/Program.cs');

test('internal validation is owner scoped, revision pinned and CAS-only',()=>{
  for(const code of ['scope.ResolveAsync','scope.ScopedQuery','CustomerTaxpayerMemberships',
    'SnapshotSha256','ValidSandboxSnapshot','ExecuteUpdateAsync',
    'i.Revision == expectedRevision','i.State == InvoiceLifecycleState.Draft',
    'InvoiceLifecycleState.Validated']) assert.ok(service.includes(code),code);
  assert.ok(!service.includes('InvoiceLifecycleState.Finalized'));
  assert.ok(!service.includes('HttpClient'));
});
test('internal validation is registered behind loopback-only route',()=>{
  assert.ok(host.includes('AddScoped<Phase10InternalInvoiceValidationService>()'));
  assert.ok(api.includes('validate-internal'));
  assert.ok(api.includes('if (!IsLocal(http))'));
  assert.ok(api.includes('authoritySubmitted = false'));
});
