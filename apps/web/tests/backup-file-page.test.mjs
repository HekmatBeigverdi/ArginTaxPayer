import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';
const exports = {};
runInNewContext(ts.transpileModule(readFileSync(new URL('../app/backup-file-page.ts', import.meta.url), 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText, { exports, Intl, Date });
const page = exports.backupFilePage;
const files = Array.from({ length: 31 }, (_, id) => ({ id, date: new Date(Date.UTC(2026, 10, id + 1)).toISOString() }));
test('15 per page, newest first, last page clamped when files disappear', () => {
 const first = page(files, f => f.date, '', '', 1);
 assert.equal(first.items.length, 15);
 assert.equal(first.items[0].id, 30);
 assert.equal(first.pages, 3);
 assert.equal(page(files, f => f.date, '', '', 3).items.length, 1);
 assert.equal(page(files.slice(0, 2), f => f.date, '', '', 3).current, 1);
});
test('inclusive date range uses Tehran day boundaries before pagination', () => {
 const result = page([{ date: '2026-11-16T20:29:59Z' }, { date: '2026-11-16T20:30:00Z' }, { date: '2026-11-17T20:29:59Z' }, { date: '2026-11-17T20:30:00Z' }], f => f.date, '2026-11-17', '2026-11-17', 1);
 assert.equal(result.total, 2);
 assert.equal(result.items[0].date, '2026-11-17T20:29:59Z');
 assert.equal(page(files, f => f.date, '2027-01-01', '', 1).total, 0);
 assert.equal(page(files, f => f.date, '2026-12-01', '2026-11-01', 1).total, 0);
});
