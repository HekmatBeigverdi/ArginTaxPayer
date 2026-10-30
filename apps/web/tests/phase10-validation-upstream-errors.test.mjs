import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {dirname,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');

test('validation BFF handles empty-body authentication errors before JSON parsing',()=>{
 const src=read('apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/validate-internal/route.ts');
 for(const [status,code] of [[401,'unauthorized'],[403,'forbidden'],[404,'not_found']])
  assert.ok(src.includes(`if(upstream.status===${status})return fail(${status},"${code}")`));
 assert.ok(src.indexOf('if(upstream.status===401)')<src.indexOf('const raw=await upstream.text()'));
 assert.ok(src.includes('upstream_server_error'));
});
test('UI translates authentication and service failures',()=>{
 const src=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 for(const value of ['نشست ورود معتبر نیست','سرویس Backend خطای داخلی دارد','sandbox_snapshot_validation_failed'])
  assert.ok(src.includes(value));
});
