import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const observer = read('../../../src/ArginTaxPayer.Api/Invoices/Phase11InquiryQueueObserver.cs');
const program = read('../../../src/ArginTaxPayer.Api/Program.cs');
const sender = read('../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyOutboxWorker.cs');

test('observer is explicit opt-in in development only', () => {
  assert.match(program, /identityDevelopmentEnabled\s*&&\s*builder\.Configuration\.GetValue<bool>\("Phase11:EnableReadOnlyQueueObserver"\)/);
  assert.match(program, /AddHostedService<Phase11InquiryQueueObserver>\(\)/);
});
test('observer only reads test database state and does not send or mutate', () => {
  assert.match(observer, /BackgroundService/);
  assert.match(observer, /PeriodicTimer/);
  assert.match(observer, /current_database\(\)/);
  assert.match(observer, /argintaxpayer_test/);
  assert.match(observer, /WHERE state IN \(5,6\)/);
  assert.doesNotMatch(observer, /ProcessInvoiceAsync|SendPreparedAsync|InquireAsync|ExecuteSql|SaveChanges|INSERT INTO|UPDATE invoice|DELETE FROM/);
});
test('Phase 10 sender remains explicitly invoked', () => {
  assert.match(sender, /Explicitly invoked Sandbox worker/);
  assert.match(sender, /operation\.State != InvoiceDispatchState\.Reserved/);
});
