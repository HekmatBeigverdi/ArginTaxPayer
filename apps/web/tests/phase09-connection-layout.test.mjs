import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const read = file => readFileSync(new URL("../" + file, import.meta.url), "utf8");

test("connection dossier uses exactly one segmented panel instead of stacked workflows", () => {
  const dossier = read("app/taxpayers/taxpayer-workspace.tsx");
  const panel = read("app/taxpayers/taxpayer-connection-panel.tsx");
  assert.match(dossier, /<TaxpayerConnectionPanel/);
  assert.doesNotMatch(dossier, /<ExistingKeyImport|<SigningKeyGeneration|<SigningConnectionTest/);
  assert.match(panel, /role="tablist"/);
  assert.match(panel, /aria-selected=\{method === "existing"\}/);
  assert.match(panel, /aria-selected=\{method === "generate"\}/);
  assert.match(panel, /method === "existing" &&/);
  assert.match(panel, /method === "generate" &&/);
});

test("one fiscal memory is shared between import, generation and connection test", () => {
  const panel = read("app/taxpayers/taxpayer-connection-panel.tsx");
  assert.match(panel, /const chosen = currentMemory \? \[currentMemory\] : \[\]/);
  assert.equal((panel.match(/memories=\{chosen\}/g) ?? []).length, 3);
  assert.match(panel, /setRevision\(v => v \+ 1\)/);
  assert.match(panel, /recoveryAvailable/);
});

test("message box supports safe result states and dismissal", () => {
  const component = read("app/taxpayers/operation-message-box.tsx");
  const connection = read("app/taxpayers/signing-connection-test.tsx");
  assert.match(component, /"success" \| "error" \| "warning" \| "info"/);
  assert.match(component, /aria-label="بستن پیام"/);
  assert.match(component, /role=\{kind === "error" \? "alert" : "status"\}/);
  assert.match(connection, /<OperationMessageBox/);
  assert.match(connection, /Authenticated/);
});
