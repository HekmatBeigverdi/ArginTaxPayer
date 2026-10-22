import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const ui = readFileSync(new URL("../app/taxpayers/signing-change-request-panel.tsx", import.meta.url), "utf8");
const parent = readFileSync(new URL("../app/taxpayers/taxpayer-connection-panel.tsx", import.meta.url), "utf8");
const bff = readFileSync(new URL("../app/api/signing/[taxpayerId]/[memoryId]/change-requests/route.ts", import.meta.url), "utf8");
const api = readFileSync(new URL("../../src/ArginTaxPayer.Api/Taxpayers/SigningChangeRequestEndpoints.cs", import.meta.url), "utf8");
const service = readFileSync(new URL("../../src/ArginTaxPayer.Infrastructure/Signing/Review/EfSigningChangeRequestSubmission.cs", import.meta.url), "utf8");

test("same selected fiscal memory drives change requests", () => {
  assert.match(parent, /<SigningChangeRequestPanel taxpayerId=\{taxpayerId\} memoryId=\{memoryId\}/);
  assert.match(ui, /expectedKeyRevision: revision/);
  assert.match(ui, /crypto\.randomUUID\(\)/);
  assert.match(ui, /state === 1 \|\| x\.state === 2/);
});
test("change requests do not approve, activate a second memory or alter keys", () => {
  assert.match(service, /ReadImportOnboardingAsync/);
  assert.match(service, /BeginTransactionAsync\(IsolationLevel\.Serializable/);
  assert.match(service, /memories\.Count != 1/);
  assert.match(service, /key\.Revision != input\.ExpectedKeyRevision/);
  assert.match(service, /State = 1/);
  assert.doesNotMatch(service, /SigningKeyVersions\.Add|\.LifecycleState\s*=(?!=)/);
  assert.doesNotMatch(ui, /privateKey|privatePem|tokenValue/i);
});
test("BFF validates origin, session, body and allowlists response", () => {
  assert.match(bff, /isSameLocalOrigin/);
  assert.match(bff, /ACCESS_COOKIE/);
  assert.match(bff, /request_too_large/);
  assert.match(bff, /currentKeyRevision/);
  assert.match(api, /IsLoopbackAddress/);
  assert.match(api, /RequireRateLimiting/);
});
