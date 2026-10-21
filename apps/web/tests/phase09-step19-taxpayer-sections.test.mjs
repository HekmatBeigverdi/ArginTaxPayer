import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const source = readFileSync(new URL("../app/taxpayers/taxpayer-workspace.tsx", import.meta.url), "utf8");

test("Step19 groups taxpayer details into responsive sections", () => {
  assert.match(source, /taxpayer-sections-grid/);
  assert.match(source, /حافظه‌های مالیاتی/);
  assert.match(source, /اتصال و کلید\/توکن/);
});

test("Step20 wires import while generation and connection remain later steps", () => {
  assert.match(source, /<ExistingKeyImport/);
  assert.match(source, /ساخت کلید جدید/);
  assert.match(source, /تست اتصال/);
  assert.match(source, /گام‌های ۲۱ و ۲۲/);
});
