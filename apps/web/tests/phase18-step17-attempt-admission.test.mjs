import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

const root = fileURLToPath(new URL('../../../',import.meta.url));
const read = path => readFileSync(resolve(root,path),'utf8');

test('single-attempt send CAS is atomic and scoped to stored fiscal environment', () => {
  const source=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18DispatchAttemptAdmissionService.cs');
  for(const needle of [
    'd.authority_environment=@environment',
    'm.authority_environment=@environment',
    'm.status=2',
    'd.state=3 AND d.state_revision=@revision',
    'o.payload_json::jsonb->>\'environment\'=@environment_name',
    'o.payload_json::jsonb->>\'requestSha256\'=d.payload_sha256',
    'NOT EXISTS (SELECT 1 FROM invoice_dispatch_attempts',
    'NOT EXISTS (SELECT 1 FROM invoice_dispatch_receipts',
    'InvoiceSubscriptionGate.ReserveAsync',
    'await tx.CommitAsync(ct)'
  ]) assert.ok(source.includes(needle), needle);
  assert.match(source,/INSERT INTO invoice_dispatch_attempts[\s\S]*?VALUES \(@id,@operation,1,'tax-authority-submit',@now\)/);
  assert.doesNotMatch(source,/HttpClient|PostAsync|SendPreparedAsync|argintaxpayer_test/);
});

test('verified legacy one-shot enqueue obtains URL from joined operation routing',()=>{
  const source=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ScopedLegacyEnqueueTransport.cs');
  assert.match(source,/route.ResolveAsync\(operationId, customerUserId,/);
  assert.match(source,/registeredProvider, "ENQUEUE", ct/);
  assert.match(source,/PostVerifiedEndpointAsync\(endpoint/);
  assert.doesNotMatch(source,/Retry|Task.Delay/);
});

test('token and encryption endpoints use same persisted operation memory and selected key custody',()=>{
  const source=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ScopedLegacyCredentialsService.cs');
  assert.match(source,/gate.ResolveAsync\(operationId, customerUserId/);
  assert.match(source,/persisted.Environment != registeredProvider/);
  assert.match(source,/tokenEndpoint.Host != serverInfoEndpoint.Host/);
  assert.match(source,/keys.RecoverAsync\(customerUserId, taxpayerId/);
  assert.match(source,/LegacyInvoicePreparationCredentials.RequestVerifiedAsync/);
  assert.match(source,/using var key/);
});
