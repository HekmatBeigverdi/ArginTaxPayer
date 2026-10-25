import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {dirname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const source=p=>readFileSync(resolve(root,p),'utf8');
test('internal validation is same-origin, scoped and revision bound',()=>{
 const route=source('apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/validate-internal/route.ts');
 for(const term of ['isSameLocalOrigin','ACCESS_COOKIE','expectedRevision','validate-internal','AbortSignal.timeout','authoritySubmitted:false'])
  assert.ok(route.includes(term),term);
});
test('Draft readiness offers action and refreshes report',()=>{
 const ui=source('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 for(const term of ['report.invoiceState!=="Draft"','اعتبارسنجی داخلی صورتحساب','expectedRevision:report.invoiceRevision','await refresh()'])
  assert.ok(ui.includes(term),term);
});
