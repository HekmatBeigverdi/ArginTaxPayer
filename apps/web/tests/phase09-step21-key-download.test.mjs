import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';

const route = fs.readFileSync(new URL('../app/api/signing/[taxpayerId]/[memoryId]/generate/route.ts', import.meta.url), 'utf8');

test('key generation BFF streams a non-cacheable ZIP response', () => {
  assert.match(route, /application\/zip/);
  assert.match(route, /Cache-Control.*no-store/);
  assert.match(route, /Content-Disposition/);
  assert.match(route, /upstream\.body/);
  assert.doesNotMatch(route, /upstream\.arrayBuffer\(\)/);
});

test('key generation BFF forwards only the authenticated same-origin JSON request', () => {
  assert.match(route, /isSameLocalOrigin/);
  assert.match(route, /ACCESS_COOKIE/);
  assert.match(route, /Authorization/);
  assert.match(route, /readSigningBody/);
  assert.match(route, /generate/);
});
