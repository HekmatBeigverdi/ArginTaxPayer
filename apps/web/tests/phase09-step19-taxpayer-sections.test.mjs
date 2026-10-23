import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const source = readFileSync(new URL("../app/taxpayers/taxpayer-workspace.tsx", import.meta.url), "utf8");
const panel = readFileSync(new URL("../app/taxpayers/taxpayer-connection-panel.tsx", import.meta.url), "utf8");
const connection = readFileSync(new URL("../app/taxpayers/signing-connection-test.tsx", import.meta.url), "utf8");

test("Step19 groups taxpayer details into responsive sections", () => {
  assert.match(source, /taxpayer-sections-grid/);
  assert.match(source, /حافظه‌های مالیاتی/);
  assert.match(source, /اتصال به سامانه مؤدیان/);
});

test("Steps20-21 wire import and generation while connection remains later", () => {
  assert.match(source, /<TaxpayerConnectionPanel/);
  assert.match(panel, /<ExistingKeyImport/);
  assert.match(panel, /<SigningKeyGeneration/);
  assert.match(panel, /<SigningConnectionTest/);
  assert.match(connection, /تست اتصال/);
});
