import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, dirname } from 'node:path';
import test from 'node:test';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const source = readFileSync(resolve(root,
  'src/ArginTaxPayer.Infrastructure/Persistence/Invoices/SandboxInvoiceFiscalSerialRepository.cs'), 'utf8');

test('serial reservation resolves memory from server-side immutable invoice snapshot', () => {
  assert.match(source, /TryGetProperty\("sandboxFiscalMemoryId",out var mid\)/);
  assert.match(source, /mid\.TryGetGuid\(out linkedMemory\)/);
  assert.match(source, /WHERE id=@memory AND customer_user_id=@owner AND taxpayer_id=@taxpayer/);
  assert.match(source, /\("memory",linkedMemory\)/);
  assert.ok(source.indexOf('await using var invoiceCmd=') <
    source.indexOf('await using var memoryCmd='),
    'invoice snapshot must be resolved before fiscal memory lookup');
});
