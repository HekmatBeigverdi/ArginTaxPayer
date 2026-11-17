import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';
const source = readFileSync(new URL('../app/backup-control-panel.tsx', import.meta.url), 'utf8');
const submit = source.slice(source.indexOf(' async function submit('), source.indexOf(' async function uploadBundle('));
function harness(fails) {
 const state = { error: '', notice: '', busy: false, refresh: 0 };
 const context = { state, Error, settingsDirty: { current: true }, call: async () => { if (fails) throw new Error('save_failed'); }, setBusy: value => state.busy = value, setError: value => state.error = value, setNotice: value => state.notice = value, refresh: async () => { state.refresh++; } };
 runInNewContext(ts.transpileModule(submit + '\n globalThis.submit = submit;', { compilerOptions: { target: ts.ScriptTarget.ES2022 } }).outputText, context);
 return { context, state };
}
test('failed save remains visible and keeps draft without refresh', async () => {
 const { context, state } = harness(true);
 assert.equal(await context.submit('settings', 'PUT', {}), false);
 assert.equal(state.error, 'save_failed');
 assert.equal(state.refresh, 0);
 assert.equal(state.busy, false);
 assert.equal(context.settingsDirty.current, true);
});
test('successful save clears dirty draft and reloads persisted settings', async () => {
 const { context, state } = harness(false);
 assert.equal(await context.submit('settings', 'PUT', {}), true);
 assert.equal(state.refresh, 1);
 assert.equal(context.settingsDirty.current, false);
});
