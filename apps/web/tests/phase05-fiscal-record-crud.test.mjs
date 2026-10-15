import { readFileSync } from "node:fs";
import assert from "node:assert/strict";
import { test } from "node:test";
const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("selected fiscal record ID not customer account ID drives its modal", () => {
  const directory = read("app/parties/buyer-taxpayer-directory.tsx");
  const modal = read("app/parties/fiscal-record-modal.tsx");
  assert.match(directory, /onClick=\{\(\) => setSelectedRecord\(r\)\}/);
  assert.match(directory, /record=\{selectedRecord\}/);
  assert.match(modal, /record\.id \+ \(record\.kind === 1/);
  assert.match(modal, /role="dialog"/);
  assert.match(modal, /role=\{record\.type === "buyer" \? "buyer" : "seller"\}/);
});

test("buyer edit is revision guarded and archive preserves persisted identity", () => {
  const modal = read("app/parties/fiscal-record-modal.tsx");
  assert.match(modal, /expectedRevision: person\.revision/);
  assert.match(modal, /archive: true/);
  assert.match(modal, /mode === "archive" \? buyerPath/);
  assert.doesNotMatch(modal, /method: "DELETE"/);
  assert.match(modal, /بایگانی خریدار|بایگانی /);
});

test("seller memory binding blocks archive but allows scoped rename", () => {
  const service = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Taxpayers/TaxpayerScopeService.cs");
  assert.match(service, /ArchiveAsync/);
  assert.match(service, /FiscalMemories\.AnyAsync/);
  assert.match(service, /TaxpayerStatus\.Archived/);
  assert.match(service, /participantScope\.CanManageAsync/);
});

test("independent create wizard names taxpayer and buyer distinctly", () => {
  const create = read("app/parties/participant-registration-modal.tsx");
  assert.match(create, /افزودن خریدار صورتحساب/);
  assert.match(create, /افزودن مؤدی/);
  assert.doesNotMatch(create, /افزودن فروشنده \/ مؤدی/);
});
