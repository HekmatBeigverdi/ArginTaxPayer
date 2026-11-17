import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';
const source = readFileSync(new URL('../app/backup-control-panel.tsx', import.meta.url), 'utf8');
const code = source.slice(source.indexOf(' async function testRemoteConnection('), source.indexOf(' async function uploadBundle('));
function harness(save, failure = false) {
 const calls = [], state = {};
 const context = { Error, settings: { ftpHost: 'sarv.shetabanhost.com' }, submit: async (...args) => { calls.push(args[0]);return save; }, call: async path => { calls.push(path);if(failure)throw new Error('TLS error');return {files: []}; }, setBusy: value => state.busy=value, setError: value => state.error=value, setNotice: value => state.notice=value, setRemoteFiles: value => state.files=value };
 runInNewContext(ts.transpileModule(code+'\nglobalThis.testRemoteConnection=testRemoteConnection;', {compilerOptions:{target:ts.ScriptTarget.ES2022}}).outputText,context);
 return { calls, state, run: context.testRemoteConnection };
}
test('connection test saves current host first and only lists remote files', async()=>{
 const h=harness(true);await h.run();assert.deepEqual(h.calls,['settings','artifacts/remote']);assert.equal(h.state.busy,false);assert.match(h.state.notice,/موفق/);
});
test('failed settings save prevents testing stale remote configuration',async()=>{
 const h=harness(false);await h.run();assert.deepEqual(h.calls,['settings']);
});
test('connection failure leaves a visible error and releases waiting state',async()=>{
 const h=harness(true,true);await h.run();assert.equal(h.state.error,'TLS error');assert.equal(h.state.busy,false);assert.equal(h.state.notice,'');
});
