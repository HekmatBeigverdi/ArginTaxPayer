import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const api=readFileSync("../../src/ArginTaxPayer.Api/Taxpayers/SandboxFiscalMemoryDevelopmentEndpoints.cs","utf8");
const store=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Signing/SandboxFiscalMemoryMetadataService.cs","utf8");
const migration=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261024103000_AddSandboxFiscalMemoryMetadata.cs","utf8");
const bff=readFileSync("app/api/sandbox-memory/[taxpayerId]/route.ts","utf8");
const ui=readFileSync("app/taxpayers/sandbox-fiscal-memory-panel.tsx","utf8");
const custody=readFileSync("app/api/sandbox-memory/[taxpayerId]/private-key/route.ts","utf8");
const identity=readFileSync("../../src/ArginTaxPayer.Infrastructure/Signing/Protection/KeyProtectionContracts.cs","utf8");
const offline=readFileSync("app/api/sandbox-memory/[taxpayerId]/key-self-test/route.ts","utf8");

test("sandbox metadata is additive test-only and never writes Production key tables",()=>{
  assert.match(migration,/current_database\(\) <> 'argintaxpayer_test'/);
  assert.match(migration,/CREATE TABLE sandbox_fiscal_memories/);
  assert.doesNotMatch(migration,/ALTER TABLE fiscal_memories|DROP TABLE|DELETE FROM/);
  assert.match(store,/current_database\(\)/);
  assert.match(store,/argintaxpayer_test/);
  assert.match(store,/scope\.ScopedQuery\(/);
  assert.match(store,/customer_user_id=@owner/);
  assert.match(store,/ON CONFLICT\(customer_user_id,taxpayer_id\) DO NOTHING/);
  assert.doesNotMatch(store,/INSERT INTO fiscal_memories|UPDATE signing_key_versions/);
});
test("only validated public PEM and sandbox code are submitted via BFF",()=>{
  assert.match(bff,/memoryIdentifier","publicKeyPem/);
  assert.match(bff,/isSameLocalOrigin/);
  assert.match(bff,/ACCESS_COOKIE/);
  assert.match(store,/ImportFromPem/);
  assert.match(store,/ExportSubjectPublicKeyInfo/);
  const registerMemory=ui.match(/async function registerMemory\(\)[\s\S]*?\n  \}\n\n  async function generateKeys/);
  assert.ok(registerMemory,"registerMemory function must remain present");
  assert.match(registerMemory[0],/publicKeyPem:publicKey/);
  assert.doesNotMatch(registerMemory[0],/privateKeyPem:/);
});
test("live sandbox token endpoint remains fail-closed",()=>{
  assert.match(api,/sandbox_authority_api_not_verified/);
  assert.match(api,/authenticationConfirmed=false/);
  assert.match(api,/StatusCodes|statusCode:503/);
  assert.doesNotMatch(api,/GET_TOKEN|tp\.tax\.gov\.ir|HttpClient/);
  assert.match(ui,/آدرس رسمی API سندباکس هنوز تأیید نشده است/);
});

test("sandbox private key is encrypted with scoped independent AEAD identity",()=>{
  assert.match(store,/ImportPrivateAsync/);
  assert.match(store,/FixedTimeEquals\(spki,registeredSpki\)/);
  assert.match(store,/KeyProtectionIdentity\(authorized.Owner,taxpayerId/);
  assert.match(store,/Sandbox:true/);
  assert.match(store,/protector.ProtectAsync\(identity,pkcs8,ct\)/);
  assert.match(store,/private_envelope_json=@envelope/);
  assert.match(store,/CryptographicOperations.ZeroMemory\(pkcs8\)/);
  assert.match(identity,/argin:key:sandbox:v1/);
  assert.match(identity,/argin:key:v1/);
  assert.match(migration,/private_envelope_json text NULL/);
  assert.match(migration,/origin_operation_id uuid NOT NULL/);
  assert.match(migration,/signing_key_version_id uuid NOT NULL/);
});
test("private key transit is session authenticated with no secret response",()=>{
  assert.match(custody,/isSameLocalOrigin/);
  assert.match(custody,/ACCESS_COOKIE/);
  assert.match(custody,/privateKeyPem/);
  assert.match(custody,/keyCustodyReady/);
  assert.doesNotMatch(custody,/return.*privateKeyPem/);
  assert.match(ui,/type="file"/);
  assert.match(ui,/privateKeyPem:pem/);
  assert.match(api,/MapPost\("\/private-key"/);
  assert.doesNotMatch(api,/GET_TOKEN|HttpClient/);
});

test("sandbox offline key verification does not authenticate with authority",()=>{
  assert.match(api,/MapPost\("\/key-self-test"/);
  assert.match(store,/SelfTestAsync/);
  assert.match(store,/protector.UnprotectAsync\(identity,envelope,ct\)/);
  assert.match(store,/privateRsa.SignData/);
  assert.match(store,/publicRsa.VerifyData/);
  assert.match(offline,/authorityAuthenticated:false/);
  assert.match(ui,/تست صحت کلید ذخیره‌شده \(آفلاین\)/);
  assert.doesNotMatch(offline,/tp\.tax\.gov\.ir|GET_TOKEN|HttpClient/);
});
