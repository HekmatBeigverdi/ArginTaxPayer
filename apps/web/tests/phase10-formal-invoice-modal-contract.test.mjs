import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('invoice modal has formal header, scrollable line table and footer',()=>{
 const preview=read('apps/web/app/invoices/invoice-formal-preview.tsx');
 const page=read('apps/web/app/invoices/workspace.tsx');
 for(const phrase of ['InvoiceHeader','InvoiceLines','InvoiceTotals','maxHeight: 300','overflow: "auto"'])assert.ok(preview.includes(phrase),phrase);
 assert.ok(page.includes('<InvoiceFormalPreview'));
 assert.ok(page.includes('مشخصات داخلی و فنی صورتحساب'));
});
test('inquiry report is expandable and old report is not fetched on modal open',()=>{
 const s=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 assert.ok(s.includes('<details aria-label="استعلام و گزارش سازمان"'));
 assert.ok(s.includes('onToggle={event=>'));
 assert.ok(!s.includes('void loadAuthorityReport(controller.signal);\n  return()=>controller.abort()'));
});
test('row actions require authoritative success; cancellation does not mutate original invoice',()=>{
 const s=read('apps/web/app/invoices/workspace.tsx');
 assert.ok(s.includes('authorityStatus={detail.authorityStatus}'));
 const preview=read('apps/web/app/invoices/invoice-formal-preview.tsx');
 assert.ok(preview.includes('authorityStatus==="SUCCESS"'));
 const b=read('apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/print-preview/route.ts');
 assert.ok(b.includes('ACCESS_COOKIE'));
 assert.ok(b.includes('no-store'));
});
