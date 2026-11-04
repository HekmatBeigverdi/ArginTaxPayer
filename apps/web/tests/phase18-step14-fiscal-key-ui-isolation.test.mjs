import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
import assert from 'node:assert/strict';
import test from 'node:test';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const src = readFileSync(resolve(root, 'apps/web/app/taxpayers/taxpayer-connection-panel.tsx'), 'utf8');

test('Production signing never presents a Sandbox or unclassified fiscal memory', () => {
  assert.match(src, /productionMemories = memories\.filter\(m => m\.authorityEnvironment === 1\)/);
  assert.match(src, /productionMemories\.find\(m => m\.id === memoryId\)/);
  assert.match(src, /productionMemories\.map\(m => <option/);
  assert.match(src, /unclassifiedCount = memories\.filter\(m => m\.authorityEnvironment == null\)/);
});

test('UI tab is presentation only and cannot select a provider for invoice send', () => {
  assert.match(src, /A UI tab is a display filter only/);
  assert.doesNotMatch(src, /\/send-and-inquire/);
});
