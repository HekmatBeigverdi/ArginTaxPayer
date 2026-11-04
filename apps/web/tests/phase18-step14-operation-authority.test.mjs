import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';
import test from 'node:test';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const read = path => readFileSync(resolve(root, path), 'utf8');

test('dispatch records retain immutable authority environment without legacy backfill', () => {
  const migration = read('src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261104230000_BindDispatchAuthorityEnvironment.cs');
  assert.match(migration, /ADD COLUMN authority_environment integer NULL/);
  assert.match(migration, /authority_environment IS NULL OR authority_environment IN \(1,2\)/);
  assert.match(migration, /trg_p18_dispatch_scope_immutable/);
  assert.match(migration, /NEW.authority_environment IS DISTINCT FROM OLD.authority_environment/);
  assert.doesNotMatch(migration, /UPDATE invoice_dispatch_operations\s+SET\s+authority_environment/);
});

test('server route reads persisted operation and memory with exact owner and active membership', () => {
  const route = read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18PersistedAuthorityOperationGate.cs');
  assert.match(route, /JOIN fiscal_memories m ON m.id=d.fiscal_memory_id/);
  assert.match(route, /d.authority_environment, m.authority_environment/);
  assert.match(route, /m.status=2/);
  assert.match(route, /c.unlinked_at_utc IS NULL/);
  assert.match(route, /reader.IsDBNull\(1\) \|\| reader.IsDBNull\(2\)/);
  assert.match(route, /Phase18AuthorityRoutingPolicy.CanUseMemory/);
});

test('Sandbox worker and both inquiries cannot use a Production-classified operation', () => {
  const dir = 'src/ArginTaxPayer.Infrastructure/Persistence/Invoices/';
  for (const name of ['Phase10LegacyOutboxWorker.cs','Phase10LegacyUidInquiryService.cs','Phase10LegacyReferenceInquiryService.cs']) {
    const source = read(dir + name);
    assert.match(source, /to_jsonb\(d\)->>'authority_environment' IS NULL OR to_jsonb\(d\)->>'authority_environment'='2'/, name);
    // Legacy memory IDs belong to the dedicated Sandbox table. The PostgreSQL
    // scope regression exercises this binding with no regular-memory row.
    assert.match(source, /sandbox_fiscal_memories/, name);
  }
});

test('operational Production deployment remains gated', () => {
  const unit = read('ops/phase18-systemd/argintaxpayer-api.service');
  assert.match(unit, /ArginTaxPayer__EnableOperationalEndpoints=false/);
  assert.match(unit, /SandboxInvoiceLegacy__Enabled=false/);
});
