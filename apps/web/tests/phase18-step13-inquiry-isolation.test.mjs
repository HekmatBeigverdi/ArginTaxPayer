import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';

const root = fileURLToPath(new URL('../../../', import.meta.url));
function read(name) {
  return readFileSync(resolve(root, 'src/ArginTaxPayer.Infrastructure/Persistence/Invoices/' + name), 'utf8');
}

for (const name of ['Phase10LegacyUidInquiryService.cs', 'Phase10LegacyReferenceInquiryService.cs']) {
  test(name + ' prevents cross-environment legacy inquiry', () => {
    const source = read(name);
    assert.match(source, /!ArginTaxPayer\.Infrastructure\.Configuration\.HostedSandboxDeployment\.IsSandboxStoreDatabaseName\(databaseName\)/);
    assert.match(source, /sandbox_database_required/);
    assert.match(source, /o.payload_json::jsonb->>'environment'='Sandbox'/);
    assert.match(source, /o.payload_json::jsonb->>'schema'='phase10.sandbox.legacy.invoice.dispatch.v1'/);
    assert.match(source, /o.payload_json::jsonb->>'fiscalMemoryId'=d.fiscal_memory_id::text/);
    assert.ok(source.indexOf('sandbox_database_required') < source.indexOf('keys.RecoverAsync'));
  });
}
