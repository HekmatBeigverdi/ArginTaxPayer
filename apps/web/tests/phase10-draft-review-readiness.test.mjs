import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const source=readFileSync(resolve(root,
  'src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LiveSendReadinessService.cs'),'utf8');

test('readiness reports validation and approval as distinct lifecycle gates',()=>{
  assert.match(source,/invoice\.lifecycle\.validation_required/);
  assert.match(source,/invoice\.review\.approval_required/);
  assert.match(source,/invoice\.finalization\.not_finalized/);
  assert.match(source,/db\.InvoiceReviews\.AsNoTracking\(\)/);
  assert.match(source,/r\.InvoiceId==invoiceId && r\.CustomerUserId==owner\.Value/);
});
