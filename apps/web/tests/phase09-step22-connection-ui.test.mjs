import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const route = readFileSync(new URL('../app/api/signing/[taxpayerId]/[memoryId]/connection-test/route.ts', import.meta.url), 'utf8');
const ui = readFileSync(new URL('../app/taxpayers/signing-connection-test.tsx', import.meta.url), 'utf8');
const workspace = readFileSync(new URL('../app/taxpayers/taxpayer-workspace.tsx', import.meta.url), 'utf8');

test('connection checks origin, session and avoids cache', () => {
  assert.match(route, /isSameLocalOrigin/);
  assert.match(route, /ACCESS_COOKIE/);
  assert.match(route, /no-store/);
});
test('connection accepts an empty POST body without mistaking the request stream for payload', () => {
  assert.match(route, /getReader\(\)/);
  assert.match(route, /reader\.read\(\)/);
  assert.match(route, /bodyBytes > 0/);
  assert.doesNotMatch(route, /if \(req\.body\) return fail\(413/);
});
test('connection result only exposes known public metadata', () => {
  assert.match(route, /authenticationConfirmed/);
  assert.match(route, /portalRegistrationConfirmed: false/);
  assert.match(route, /fiscalMemoryActivationConfirmed: false/);
  assert.match(route, /fiscalInformation: authenticated \? fiscalInformation : null/);
});
test('taxpayer workspace exposes read-only connection test', () => {
  assert.match(ui, /تست اتصال/);
  assert.match(ui, /UnsupportedProfile/);
  assert.match(ui, /اتصال موفق است؛ اطلاعات حافظه مالیاتی دریافت شد/);
  assert.match(ui, /economicCode/);
  assert.match(workspace, /SigningConnectionTest/);
});
