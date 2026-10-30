import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {dirname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
test('invoice server pagination uses 15 rows and detail modal has one dispatch surface',()=>{
 const ui=readFileSync(resolve(root,'apps/web/app/invoices/workspace.tsx'),'utf8');
 assert.match(ui,/const listPageSize = 15;/);
 assert.match(ui,/query\.set\("page", String\(listPage\)\)/);
 assert.match(ui,/query\.set\("pageSize", String\(listPageSize\)\)/);
 assert.match(ui,/<InvoiceSendReadinessPanel/);
 assert.doesNotMatch(ui,/<InvoiceDispatchPanel/);
 assert.doesNotMatch(ui,/<InvoiceDispatchHistoryPanel/);
});
