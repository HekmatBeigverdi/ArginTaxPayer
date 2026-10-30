import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
test('Sandbox worker logs sanitized initial enqueue classification',()=>{
 const source=readFileSync(resolve(root,'src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyOutboxWorker.cs'),'utf8');
 for(const key of ['Phase10 Sandbox enqueue outcome','OperationId={OperationId}','AttemptId={AttemptId}','SafeCode={SafeCode}','HasReference={HasReference}']) assert.ok(source.includes(key),key);
 assert.ok(!source.includes('logger.LogInformation("requestJson'));
});
