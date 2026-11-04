import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';

const root=fileURLToPath(new URL('../../../',import.meta.url));
const read=p=>readFileSync(resolve(root,p),'utf8');

test('existing Sandbox legacy transport is wired to pinned endpoint registry',()=>{
  const source=read('src/ArginTaxPayer.Infrastructure/Signing/Connection/LegacyInvoiceSubmissionHttpTransport.cs');
  assert.match(source,/Phase18LegacyAuthorityEndpointSelector.TryResolveConfiguredBase/);
  assert.match(source,/TaxAuthorityEnvironment.Sandbox/);
  assert.match(source,/new HttpRequestMessage\(HttpMethod.Post, InvoiceEndpoint\)/);
});

test('new operation endpoint routing reads immutable persisted scope, never request environment',()=>{
  const source=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18AuthorityEndpointRoutingService.cs');
  assert.match(source,/gate.ResolveAsync\(operationId, customerUserId/);
  assert.match(source,/persisted.Environment != registeredProvider/);
  assert.match(source,/Phase18LegacyAuthorityEndpointSelector.TryResolveOperationEndpoint/);
  assert.doesNotMatch(source,/HttpClient|SendAsync|PostAsync/);
});

test('Production endpoint is not accidentally enabled by URL alone',()=>{
  const source=read('src/ArginTaxPayer.Infrastructure/Signing/Connection/Phase18LegacyAuthorityEndpointSelector.cs');
  assert.match(source,/configuration\["ArginTaxPayer:EnableOperationalEndpoints"\] != "true"/);
  assert.match(source,/configuration\[\$"\{prefix\}:Enabled"\] != "true"/);
  assert.match(source,/configuration\[\$"\{prefix\}:OperatorApprovedEndpoint"\] != "true"/);
  assert.match(source,/string.Equals\(configured, expected.OriginalString, StringComparison.Ordinal\)/);
});
