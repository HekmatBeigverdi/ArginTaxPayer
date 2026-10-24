import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const connection=readFileSync("app/taxpayers/taxpayer-connection-panel.tsx","utf8");
const sandbox=readFileSync("app/taxpayers/sandbox-fiscal-memory-panel.tsx","utf8");
const production=readFileSync("../../src/ArginTaxPayer.Infrastructure/Signing/Connection/LegacyTaxAuthorityConnection.cs","utf8");
const scopes=readFileSync("../../src/ArginTaxPayer.Domain/Taxpayers/FiscalConnectionEnvironmentPolicy.cs","utf8");

test("sandbox is a distinct tab and production components remain intact",()=>{
  assert.match(connection,/setEnvironment\("sandbox"\)/);
  assert.match(connection,/setEnvironment\("production"\)/);
  assert.match(connection,/SandboxFiscalMemoryPanel taxpayerId=\{taxpayerId\}/);
  assert.match(connection,/ExistingKeyImport/);
  assert.match(connection,/SigningKeyGeneration/);
  assert.match(connection,/SigningConnectionTest/);
});
test("sandbox generates independent browser RSA keypair without loading production key",()=>{
  assert.match(sandbox,/crypto\.subtle\.generateKey/);
  assert.match(sandbox,/modulusLength:2048/);
  assert.match(sandbox,/crypto\.subtle\.exportKey\("spki"/);
  assert.match(sandbox,/crypto\.subtle\.exportKey\("pkcs8"/);
  assert.match(sandbox,/argin-sandbox-public\.pem/);
  assert.match(sandbox,/argin-sandbox-private\.pem/);
  assert.doesNotMatch(sandbox,/\/api\/signing\//);
});
test("sandbox cannot silently connect to actual production token endpoint",()=>{
  assert.match(sandbox,/تست اتصال Sandbox/);
  assert.match(sandbox,/disabled title="آدرس رسمی API/);
  assert.match(sandbox,/registerMemory/);
  assert.match(sandbox,/\/api\/sandbox-memory\//);
  assert.doesNotMatch(sandbox,/\/api\/signing\//);
  assert.match(production,/https:\/\/tp\.tax\.gov\.ir\/req\/api\//);
  assert.match(scopes,/AllowsFallback/);
  assert.match(scopes,/fallback\) => false/);
});
