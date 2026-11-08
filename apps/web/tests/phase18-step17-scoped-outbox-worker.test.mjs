import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';

const root=fileURLToPath(new URL('../../../', import.meta.url));
const read=p=>readFileSync(resolve(root,p),'utf8');

test('new worker binds every outbound request to persisted operation, classified outbox and fiscal key',()=>{
  const s=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ScopedLegacyOutboxWorker.cs');
  for(const v of [
    'gate.ResolveAsync(operationId, owner, taxpayerId, invoiceId',
    'trusted.Environment != provider',
    'phase18.production.legacy.invoice.dispatch.v1',
    'phase10.sandbox.legacy.invoice.dispatch.v1',
    'LegacyInvoiceOutboxPacketRebuilder.Rebuild',
    'keys.RecoverAsync(owner, taxpayerId',
    'tokens.RequestVerifiedEndpointAsync',
    'admission.ClaimAsync(',
    'transport.PostOnceAsync(',
    'outcomes.RecordAsync(',
    'CancellationToken.None',
    'ExecuteUpdateAsync'
  ]) assert.ok(s.includes(v),v);
  assert.ok(s.indexOf('admission.ClaimAsync(') < s.indexOf('transport.PostOnceAsync('));
  assert.ok(s.indexOf('transport.PostOnceAsync(') < s.indexOf('outcomes.RecordAsync('));
  assert.doesNotMatch(s,/Task.Delay|while\s*\(|retryCount|requeue|ASP.NETCORE_ENVIRONMENT/);
});

test('payload rebuilder preserves legacy Sandbox default and rejects cross-environment schema',()=>{
  const s=read('src/ArginTaxPayer.Infrastructure/Signing/Connection/LegacyInvoiceOutboxPacketRebuilder.cs');
  assert.match(s,/TaxAuthorityEnvironment environment = TaxAuthorityEnvironment.Sandbox/);
  assert.match(s,/TaxAuthorityEnvironment.Production => "phase18.production.legacy.invoice.dispatch.v1"/);
  assert.match(s,/TaxAuthorityEnvironment.Sandbox => "phase10.sandbox.legacy.invoice.dispatch.v1"/);
  assert.match(s,/RequiredString\(root, "environment"\) != expectedEnvironment/);
  assert.match(s,/storedHash, expectedHash/);
});
