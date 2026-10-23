import test from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { readFileSync } from 'node:fs';
const require = createRequire(import.meta.url);
const ts = require('typescript');
require.extensions['.ts'] = (module, filename) => module._compile(ts.transpileModule(
  readFileSync(filename, 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS,
    target: ts.ScriptTarget.ES2020, esModuleInterop: true } }).outputText, filename);
const { NextRequest } = require('next/server');
const { GET, POST } = require('../app/api/signing/[taxpayerId]/[memoryId]/route.ts');
const id = '11111111-1111-4111-8111-111111111111';
const context = () => ({ params: Promise.resolve({ taxpayerId: id, memoryId: id }) });
const url = `http://localhost:3000/api/signing/${id}/${id}`;
function request(method, extra = {}) {
  return new NextRequest(url, { method, headers: { host: 'localhost:3000',
    origin: 'http://localhost:3000', cookie: 'argin_identity_access=synthetic-test-token',
    'content-type': 'application/json', ...extra }, ...(method === 'POST' ? { body: '{}' } : {}) });
}
async function run(action, upstream) {
  const old = { fetch: globalThis.fetch, mode: process.env.NODE_ENV, enabled: process.env.ARGIN_IDENTITY_UI_ENABLED };
  process.env.NODE_ENV = 'development'; process.env.ARGIN_IDENTITY_UI_ENABLED = 'true';
  globalThis.fetch = upstream;
  try { await action(); } finally {
    globalThis.fetch = old.fetch;
    if (old.mode === undefined) delete process.env.NODE_ENV; else process.env.NODE_ENV = old.mode;
    if (old.enabled === undefined) delete process.env.ARGIN_IDENTITY_UI_ENABLED; else process.env.ARGIN_IDENTITY_UI_ENABLED = old.enabled;
  }
}
test('cross-origin and unauthenticated requests never reach the API', async () => run(async () => {
  assert.equal((await POST(request('POST', { origin: 'https://other.example' }), context())).status, 403);
  assert.equal((await POST(request('POST', { cookie: '' }), context())).status, 401);
}, () => { assert.fail('unexpected backend call'); }));
test('oversized declared body is rejected before proxying', async () => run(async () => {
  assert.equal((await POST(request('POST', { 'content-length': '98305' }), context())).status, 413);
}, () => { assert.fail('unexpected backend call'); }));
test('successful import response projects public metadata only and clears proxy body', async () => {
  let received;
  await run(async () => {
    const response = await POST(request('POST'), context());
    assert.equal(response.status, 201);
    assert.equal(response.headers.get('cache-control'), 'no-store');
    const data = await response.json();
    assert.deepEqual(Object.keys(data).sort(), ['fingerprintSha256', 'keyVersionId', 'portalVerified', 'publicKeyCompared']);
    assert.equal(data.portalVerified, false);
    assert.equal(received.every(byte => byte === 0), true);
  }, async (_, init) => {
    received = init.body;
    assert.equal(init.headers.Authorization, 'Bearer synthetic-test-token');
    assert.equal(init.redirect, 'manual');
    return Response.json({ publicKey: { keyVersionId: id, fingerprintSha256: 'a'.repeat(64),
      privateKey: 'never-forward', custodyReference: 'never-forward' }, publicKeyCompared: true }, { status: 201 });
  });
});
test('unrecognized upstream errors cannot echo private input', async () => run(async () => {
  const response = await POST(request('POST'), context());
  assert.deepEqual(await response.json(), { error: 'request_rejected' });
}, async () => Response.json({ error: 'private-key-canary' }, { status: 400 })));
test('status endpoint keeps portal unverified and drops unknown fields', async () => run(async () => {
  const response = await GET(request('GET'), context());
  const data = await response.json();
  assert.equal(data.portalVerified, false);
  assert.equal(data.privateKey, undefined);
  assert.equal(data.configured, false);
}, async () => Response.json({ privateKey: 'canary', portalVerified: true, configured: false })));
