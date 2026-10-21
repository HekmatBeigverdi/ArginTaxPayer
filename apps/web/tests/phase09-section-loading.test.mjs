import test from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';

const require = createRequire(import.meta.url);
const ts = require('typescript');
// Compile the real components for server rendering; no network or state mocks.
for (const extension of ['.tsx', '.ts']) {
  require.extensions[extension] = (module, filename) => {
    const result = ts.transpileModule(readFileSync(filename, 'utf8'), {
      fileName: filename, compilerOptions: {
        jsx: ts.JsxEmit.ReactJSX, module: ts.ModuleKind.CommonJS,
        target: ts.ScriptTarget.ES2020, esModuleInterop: true
      }
    });
    module._compile(result.outputText, filename);
  };
}
const React = require('react');
const { renderToStaticMarkup } = require('react-dom/server');
const FiscalRecordModal = require('../app/parties/fiscal-record-modal.tsx').default;
const Registration = require('../app/parties/participant-registration-modal.tsx').default;
const record = { id: 'taxpayer-1', buyerId: 'customer-1', type: 'taxpayer',
  kind: 1, name: 'نمونه', identifier: '', status: 'فعال' };
const summaries = html => [...html.matchAll(/<summary[^>]*>(.*?)<\/summary>/gs)].map(x => x[1]);

test('existing dossier renders all four titles before any request resolves', () => {
  const html = renderToStaticMarkup(React.createElement(FiscalRecordModal, {
    record, isLevel1: false, onClose() {}, async onChanged() {}
  }));
  assert.equal(summaries(html).length, 4);
  assert.equal((html.match(/class="fiscal-section-loading"/g) || []).length, 3);
  assert.match(html, /اتصال به سامانه مؤدیان/);
  assert.doesNotMatch(html, /برای مشاهده اطلاعات، یک مؤدی را از فهرست انتخاب کنید/);
});

test('new taxpayer shows dependent titles with prerequisites, not endless spinners', () => {
  const html = renderToStaticMarkup(React.createElement(Registration, {
    mode: 'seller', buyers: [], level1: false, actorUserId: 'user-1',
    onClose() {}, async onCompleted() {}
  }));
  assert.equal(summaries(html).length, 4);
  assert.equal((html.match(/ابتدا هویت مؤدی را ثبت کنید/g) || []).length, 3);
  assert.doesNotMatch(html, /fiscal-section-loading/);
});

test('buyer dossier does not expose taxpayer memory or connection sections', () => {
  const html = renderToStaticMarkup(React.createElement(FiscalRecordModal, {
    record: { ...record, type: 'buyer' }, isLevel1: false,
    onClose() {}, async onChanged() {}
  }));
  assert.equal(summaries(html).length, 2);
  assert.equal((html.match(/class="fiscal-section-loading"/g) || []).length, 2);
  assert.doesNotMatch(html, /حافظه‌های مالیاتی/);
});

test('continuing registration preserves identity heading and loads dependent sections', () => {
  const html = renderToStaticMarkup(React.createElement(Registration, {
    mode: 'seller', buyers: [], level1: false, actorUserId: 'user-1',
    initialCustomerId: 'customer-1', initialSubjectId: 'taxpayer-1',
    onClose() {}, async onCompleted() {}
  }));
  assert.equal(summaries(html).length, 4);
  assert.equal((html.match(/class="fiscal-section-loading"/g) || []).length, 2);
  assert.doesNotMatch(html, /ابتدا هویت مؤدی را ثبت کنید/);
});
