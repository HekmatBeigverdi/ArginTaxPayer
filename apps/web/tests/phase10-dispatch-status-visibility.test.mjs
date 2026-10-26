import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {dirname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('Reserved dispatch errors and disabled flags are visible',()=>{
 const ui=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 for(const word of ['dispatchError','response.status','به‌روزرسانی وضعیت عملیات ارسال','تنظیمات فعال‌سازی ارسال Legacy',
 'dispatch?.status==="Reserved"'])assert.ok(ui.includes(word),word);
});
test('status readiness and send enforce same sandbox endpoint flags',()=>{
 const endpoint=read('src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs');
 const head=endpoint.slice(endpoint.indexOf('group.MapGet("/status"'),endpoint.indexOf('group.MapGet("/history"'));
 assert.ok(head.includes('SandboxInvoiceLegacy:Enabled'));
 assert.ok(head.includes('SandboxInvoiceLegacy:OperatorApprovedEndpoint'));
});
