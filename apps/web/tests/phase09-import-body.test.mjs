import test from 'node:test';
import assert from 'node:assert/strict';
import { readSigningBody } from '../lib/signing-import-body.mjs';

test('bounded import preserves bytes while clearing source chunks', async () => {
  const a = new Uint8Array([1, 2]), b = new Uint8Array([3, 4]);
  const body = new ReadableStream({ start(c) { c.enqueue(a); c.enqueue(b); c.close(); } });
  assert.deepEqual(await readSigningBody(body, 4), new Uint8Array([1, 2, 3, 4]));
  assert.deepEqual(a, new Uint8Array(2));
  assert.deepEqual(b, new Uint8Array(2));
});
test('oversize chunked import cancels before buffering beyond limit', async () => {
  let cancelled = false;
  const data = new Uint8Array([5, 6, 7]);
  const body = new ReadableStream({ start(c) { c.enqueue(data); }, cancel() { cancelled = true; } });
  await assert.rejects(readSigningBody(body, 2), /request_too_large/);
  assert.equal(cancelled, true);
  assert.deepEqual(data, new Uint8Array(3));
});
test('missing body is rejected', async () => {
  await assert.rejects(readSigningBody(null), /invalid_request/);
});
