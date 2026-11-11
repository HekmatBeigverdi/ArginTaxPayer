import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';
import test from 'node:test';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const src = readFileSync(resolve(root, 'apps/web/app/taxpayers/taxpayer-connection-panel.tsx'), 'utf8');

test('Production signing can present unclassified memories for an explicitly scoped read-only test', () => {
  assert.match(src, /productionSelectableMemories = memories\.filter\(m=>m\.authorityEnvironment===1 \|\| m\.authorityEnvironment==null\)/);
  assert.match(src, /oneTimeEnvironment=\{currentMemory\.authorityEnvironment==null\?"Production":undefined\}/);
  assert.match(src, /const chosen = currentMemory \? \[currentMemory\] : \[\]/);
});

test('UI tab is presentation only and cannot select a provider for invoice send', () => {
  assert.match(src, /A UI tab is a display filter only/);
  assert.doesNotMatch(src, /\/send-and-inquire/);
});
