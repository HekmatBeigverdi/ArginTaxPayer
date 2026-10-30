import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {dirname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('UI sends only Reserved finalized Sandbox invoices and distinguishes acknowledgement',()=>{
 const ui=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 for(const marker of ['dispatch.status!=="Reserved"','report.environment!=="Sandbox"',
  'report.invoiceState!=="Finalized"','dispatch.submissionEnabled','sendSandbox',
  'state==="Indeterminate"','referenceNumber','ارسال واقعی صورتحساب به Sandbox'])
  assert.ok(ui.includes(marker),marker);
 assert.ok(ui.includes('بدون بررسی وضعیت مجدداً ارسال نکنید'));
});
test('same-origin send BFF pins owner scope and uses bounded POST timeout',()=>{
 const route=read('apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/route.ts');
 for(const marker of ['isSameLocalOrigin','ACCESS_COOKIE','send_payload_not_supported',
 'method === "POST" ? 45000 : 10000','referenceNumber'])assert.ok(route.includes(marker),marker);
});
