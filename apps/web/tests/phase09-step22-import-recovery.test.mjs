import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("recover imported key without client-side PKCS8 or password upload", () => {
  const ui = read("app/taxpayers/existing-key-import.tsx");
  const route = read("app/api/signing/[taxpayerId]/[memoryId]/recover-import/route.ts");
  assert.match(ui, /بازیابی و نهایی‌سازی کلید ذخیره‌شده/);
  assert.match(ui, /state\?\.recoveryAvailable/);
  assert.match(ui, /!state\.recoveryAvailable/);
  assert.match(route, /method: "POST"/);
  assert.match(route, /isSameLocalOrigin/);
  assert.doesNotMatch(route, /privateKey|privateMaterial|password|pem/i);
});

test("API recovery is limited to loopback and authenticated owner", () => {
  const api = readFileSync(new URL("../../../src/ArginTaxPayer.Api/Taxpayers/SigningImportRecoveryEndpoints.cs", import.meta.url), "utf8");
  const service = readFileSync(new URL("../../../src/ArginTaxPayer.Infrastructure/Signing/Import/DatabaseSigningImportRecovery.cs", import.meta.url), "utf8");
  assert.match(api, /IsLoopbackAddress/);
  assert.match(api, /RequireRateLimiting/);
  assert.match(api, /ISigningImportRecovery/);
  assert.match(service, /ReadImportOnboardingAsync/);
  assert.match(service, /state.FreshLogin/);
  assert.match(service, /PROVIDER_OUTCOME_UNKNOWN/);
  assert.match(service, /custody.RecoverAsync/);
  assert.match(service, /CompleteImportAsync/);
  assert.doesNotMatch(service, /StageAsync|GenerateAsync|PrivatePem/);
});
