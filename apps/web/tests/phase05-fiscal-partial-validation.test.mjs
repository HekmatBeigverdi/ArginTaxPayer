import { readFileSync } from "node:fs";
import { test } from "node:test";
import assert from "node:assert/strict";
const read = p => readFileSync(new URL("../" + p, import.meta.url), "utf8");

test("fiscal GET returns JSON literal null, never 204 with empty body", () => {
  const endpoint = read("../../src/ArginTaxPayer.Api/Parties/InvoiceParticipantEndpoints.cs");
  assert.match(endpoint, /Results\.Json\(result\.Details\)/);
  const form = read("app/parties/invoice-participant-form.tsx");
  assert.match(form, /const body = await res\.text\(\)/);
  assert.match(form, /if \(!body\.trim\(\)\) return null/);
});

test("only changed fields are patched with expected revision, not full required form", () => {
  const form = read("app/parties/invoice-participant-form.tsx");
  const service = read("../../src/ArginTaxPayer.Infrastructure/Persistence/Parties/InvoiceParticipantDetailsService.cs");
  const proxy = read("app/api/party-workspace/[...path]/route.ts");
  assert.match(form, /for \(const key of changed\)/);
  assert.match(form, /method: "PATCH"/);
  assert.match(form, /expectedRevision: details\.revision/);
  assert.match(service, /PatchAsync\(/);
  assert.match(service, /values\[property\.Name\] =/);
  assert.match(service, /if \(!touched\)/);
  assert.match(proxy, /method === "PATCH"/);
  assert.doesNotMatch(form, /required\s*\/>/);
});

test("optional field rules validate only provided values and normalize Persian numerals", () => {
  const validation = read("app/parties/invoice-participant-validation.ts");
  assert.match(validation, /if \(!value\) return null/);
  assert.match(validation, /digitNormal/);
  assert.match(validation, /postalCode.*\!\/\^\\d\{10\}/s);
  assert.match(validation, /"province".*max: 120/);
  assert.match(validation, /"city".*max: 120/);
});

test("record modal gives actionable response errors rather than uncaught json parse", () => {
  const modal = read("app/parties/fiscal-record-modal.tsx");
  assert.match(modal, /const raw = await res\.text\(\)/);
  assert.match(modal, /if \(!raw\.trim\(\)\)/);
  assert.match(modal, /try \{ details = JSON\.parse\(raw\)/);
});
