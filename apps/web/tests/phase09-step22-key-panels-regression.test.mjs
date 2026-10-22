import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("existing key input remains visible when custody is not configured", () => {
  const ui = read("app/taxpayers/existing-key-import.tsx");
  assert.match(ui, /<form onSubmit=\{submit\}/);
  assert.doesNotMatch(ui, /state\.configured && state\.freshLogin && <form/);
  assert.match(ui, /disabled=\{!canImport\}/);
  assert.match(ui, /متن کلید خصوصی/);
});
test("generation has independent readiness and preserves browser download URL", () => {
  const ui = read("app/taxpayers/signing-key-generation.tsx");
  assert.match(ui, /generationConfigured/);
  assert.match(ui, /document\.body\.appendChild\(link\)/);
  assert.match(ui, /setTimeout\(\(\) => URL\.revokeObjectURL\(url\)/);
});
test("connection status API projects generation readiness without exposing secrets", () => {
  const bff = read("app/api/signing/[taxpayerId]/[memoryId]/route.ts");
  const api = readFileSync(new URL("../../../src/ArginTaxPayer.Api/Taxpayers/SigningImportEndpoints.cs", import.meta.url), "utf8");
  assert.match(bff, /generationConfigured: data\.generationConfigured === true/);
  assert.match(api, /generationConfigured/);
});

test("Development API advertises effective signing flags for safe local diagnostics", () => {
  const api = readFileSync(new URL("../../src/ArginTaxPayer.Api/Program.cs", import.meta.url), "utf8");
  assert.match(api, /signingKeyProtectionEnabled = app.Configuration.GetValue<bool>/);
  assert.match(api, /signingKeyImportEnabled = app.Configuration.GetValue<bool>/);
  assert.match(api, /signingKeyGenerationEnabled = app.Configuration.GetValue<bool>/);
  assert.match(api, /signingConnectionTestEnabled = app.Configuration.GetValue<bool>/);
});

test("custody schema readiness fails closed in both UI workflows", () => {
  const api = readFileSync(new URL("../../src/ArginTaxPayer.Api/Taxpayers/SigningImportEndpoints.cs", import.meta.url), "utf8");
  const bff = read("app/api/signing/[taxpayerId]/[memoryId]/route.ts");
  const importUi = read("app/taxpayers/existing-key-import.tsx");
  const generationUi = read("app/taxpayers/signing-key-generation.tsx");
  assert.match(api, /var storageReady = false/);
  assert.match(api, /DbSigningKeyEnvelopes/);
  assert.match(bff, /storageReady: data.storageReady === true/);
  assert.match(importUi, /state.storageReady === true/);
  assert.match(generationUi, /!state?.storageReady/);
});
