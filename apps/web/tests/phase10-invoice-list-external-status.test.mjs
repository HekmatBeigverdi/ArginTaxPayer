import test from 'node:test';import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';import {resolve,dirname} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');const read=p=>readFileSync(resolve(root,p),'utf8');
test('external status is backed by scoped authority inquiry rather than enqueue receipt',()=>{
 const service=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceDraftApplicationService.cs');
 const ui=read('apps/web/app/invoices/workspace.tsx');
 assert.ok(service.includes("sandbox_authority_inquiry_reports"));
 assert.ok(service.includes("r.report_json->>'Status'"));
 assert.ok(service.includes('authorityByInvoice.GetValueOrDefault(x.Id,"NOT_SENT")'));
 assert.ok(ui.includes('<th>وضعیت سازمان</th>'));
 assert.ok(ui.includes('item.authorityStatus==="SUCCESS"'));
 assert.ok(ui.includes('item.authorityStatus==="ACKNOWLEDGED"'));
 assert.ok(ui.includes('const listPageSize = 15;'));
});

test('invoice list checks optional authority table through a text-compatible PostgreSQL result',()=>{
 const service=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceDraftApplicationService.cs');
 assert.match(service,/SELECT to_regclass\('public\.sandbox_authority_inquiry_reports'\)::text/);
});
