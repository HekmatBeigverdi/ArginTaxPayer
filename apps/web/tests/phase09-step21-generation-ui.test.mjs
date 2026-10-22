import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';

const component = fs.readFileSync(new URL('../app/taxpayers/signing-key-generation.tsx', import.meta.url), 'utf8');
const workspace = fs.readFileSync(new URL('../app/taxpayers/taxpayer-workspace.tsx', import.meta.url), 'utf8');
const panel = fs.readFileSync(new URL('../app/taxpayers/taxpayer-connection-panel.tsx', import.meta.url), 'utf8');
const connection = fs.readFileSync(new URL('../app/taxpayers/signing-connection-test.tsx', import.meta.url), 'utf8');

test('generation UI gates download on consent, fresh login and Vault readiness', () => {
  assert.match(component, /consent/);
  assert.match(component, /freshLogin/);
  assert.match(component, /configured/);
  assert.match(component, /application\/zip/);
  assert.match(component, /URL\.createObjectURL/);
  assert.doesNotMatch(component, /localStorage|sessionStorage|indexedDB/i);
});

test('taxpayer workspace renders generation flow and leaves connection test deferred', () => {
  assert.match(workspace, /TaxpayerConnectionPanel/);
  assert.match(panel, /SigningKeyGeneration/);
  assert.match(panel, /ساخت کلید جدید|SigningKeyGeneration/);
  assert.match(workspace, /SigningConnectionTest/);
  assert.match(connection, /تست اتصال/);
});
