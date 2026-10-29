import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';

const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');

test('invoice detail accordion makes the click action explicit',()=>{
  const page=read('apps/web/app/invoices/workspace.tsx');
  const css=read('apps/web/app/globals.css');

  assert.match(page,/invoice-detail-section__summary-action/);
  assert.match(css,/invoice-detail-section__summary-action/);
  assert.match(css,/invoice-detail-section\[open\]\s*>\s*summary/);
  assert.match(css,/summary:hover/);
  assert.match(css,/summary:focus-visible/);
});

test('authority inquiry observation time is formatted in Tehran Persian calendar',()=>{
  const panel=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');

  assert.match(panel,/formatObservedAt/);
  assert.match(panel,/fa-IR-u-ca-persian/);
  assert.match(panel,/Asia\/Tehran/);
  assert.match(panel,/formatObservedAt\(authorityResult\.observedAtUtc\)/);
});

test('authority report accordion tells the user exactly what to click',()=>{
  const panel=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
  const css=read('apps/web/app/globals.css');

  assert.match(panel,/invoice-inquiry-section/);
  assert.match(panel,/برای مشاهده استعلام و گزارش نتیجه سازمان کلیک کنید/);
  assert.doesNotMatch(panel,/باز کردن در صورت نیاز/);
  assert.match(css,/invoice-inquiry-section/);
  assert.match(css,/invoice-inquiry-section.*summary::after/s);
  assert.match(css,/invoice-inquiry-section\s*>\s*button/);
  assert.match(css,/margin:\s*12px auto/);
});

test('authority report accordion exposes a loading state while the report is fetched',()=>{
  const panel=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
  const css=read('apps/web/app/globals.css');

  assert.match(panel,/authorityLoading/);
  assert.match(panel,/در حال دریافت گزارش استعلام/);
  assert.match(panel,/role="status"/);
  assert.match(css,/invoice-inquiry-section__loading/);
});
