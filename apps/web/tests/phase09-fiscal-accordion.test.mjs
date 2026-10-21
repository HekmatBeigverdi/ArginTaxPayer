import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const read = path => readFileSync(new URL('../app/' + path, import.meta.url), 'utf8');
for (const path of ['parties/fiscal-record-modal.tsx', 'parties/participant-registration-modal.tsx']) {
  test(path + ' uses invoice shell, exclusive sections and scoped dossier', () => {
    const source = read(path);
    for (const token of ['invoice-register__overlay', 'invoice-register__dialog fiscal-dialog',
      'invoice-register__dialog-header', 'name="fiscal-sections"', 'showTaxpayerList={false}',
      'getClientRects().length > 0']) assert.ok(source.includes(token), token);
    assert.doesNotMatch(source, /className="argin-modal-close"/);
    assert.match(source, /aria-label="بستن پنجره"[^>]*>×/);
  });
}
test('memory and connection are independent exclusive accordion sections', () => {
  const source = read('taxpayers/taxpayer-workspace.tsx');
  assert.equal((source.match(/<details name="fiscal-sections"/g) || []).length, 2);
  assert.match(source, /<summary>حافظه‌های مالیاتی\{detailsLoading && <SectionLoading \/>\}<\/summary>/);
  assert.match(source, /<summary>اتصال به سامانه مؤدیان — کلید\/توکن\{importLoading && <SectionLoading \/>\}<\/summary>/);
  assert.match(source, /if \(!showTaxpayerList\) return;/);
  assert.match(source, /showTaxpayerList && customerId/);
});
