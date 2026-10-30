import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const api=readFileSync("../../src/ArginTaxPayer.Api/Taxpayers/SandboxFiscalMemoryDevelopmentEndpoints.cs","utf8");
const store=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Signing/SandboxFiscalMemoryMetadataService.cs","utf8");
const migration=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261024103000_AddSandboxFiscalMemoryMetadata.cs","utf8");
const custodyMigration=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261024170000_AddSandboxEncryptedCustodyColumns.cs","utf8");
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
  assert.match(store,/ON CONFLICT\(customer_user_id,taxpayer_id\) DO UPDATE SET/);
  assert.match(store,/private_envelope_json IS NULL/);
  assert.match(store,/createdReader\.DisposeAsync\(\)/);
  assert.doesNotMatch(store,/INSERT INTO fiscal_memories|UPDATE signing_key_versions/);
});
test("only validated public PEM and sandbox code are submitted via BFF",()=>{
  assert.match(bff,/memoryIdentifier","publicKeyPem/);
  assert.match(bff,/isSameLocalOrigin/);
  assert.match(bff,/ACCESS_COOKIE/);
  assert.match(bff,/const taxpayerId=rawTaxpayerId\.trim\(\)/);
  assert.match(bff,/const uuid=\/\^\[0-9a-f\]\{8\}-\[0-9a-f\]\{4\}-\[0-9a-f\]\{4\}-\[0-9a-f\]\{4\}-\[0-9a-f\]\{12\}\$\/i;/);
  assert.match(store,/ImportFromPem/);
  assert.match(store,/ExportSubjectPublicKeyInfo/);
  const registerMemory=ui.match(/async function registerMemory\(\)[\s\S]*?\n  \}\n\n  async function generateKeys/);
  assert.ok(registerMemory,"registerMemory function must remain present");
  assert.match(registerMemory[0],/publicKeyPem:publicKey/);
  assert.match(registerMemory[0],/storePrivateKeyPem\(privateKey\)/);
  assert.match(registerMemory[0],/setNotice\("شناسه و کلید عمومی Sandbox با موفقیت ثبت شد\."\)/);
  assert.doesNotMatch(registerMemory[0],/privateKeyPem:/);
  assert.match(ui,/sessionStorage/);
  assert.match(ui,/invalid_sandbox_public_key/);
  assert.match(ui,/registrationError\(response\.status,value\)/);
  assert.match(ui,/registered!==null&&custodyReady/);
  assert.doesNotMatch(ui,/disabled=\{!generated\|\|!validIdentifier\|\|!!registered\|\|saving\|\|reading\}/);
  assert.match(ui,/setTimeout\(\(\)=>download\("argin-sandbox-private\.pem",s\),/);
  assert.match(ui,/downloadablePrivateKey/);
  assert.match(ui,/privateDownloadUrl/);
  assert.match(ui,/data:application\/x-pem-file/);
  assert.match(ui,/download="argin-sandbox-private\.pem"/);
  assert.match(ui,/دانلود Private Key همین نشست/);
});
test("live sandbox token endpoint remains fail-closed",()=>{
  assert.match(api,/sandbox_authority_api_not_verified/);
  assert.match(api,/authenticationConfirmed=false/);
  assert.match(api,/StatusCodes|statusCode:503/);
  assert.doesNotMatch(api,/GET_TOKEN|tp\.tax\.gov\.ir|HttpClient/);
  assert.match(ui,/تست اتصال واقعی Sandbox/);
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
  assert.match(custodyMigration,/ADD COLUMN private_envelope_json text NULL/);
  assert.match(custodyMigration,/ADD COLUMN origin_operation_id uuid NULL/);
  assert.match(custodyMigration,/ADD COLUMN signing_key_version_id uuid NULL/);
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

test("generated Sandbox private key can be stored without a second file upload",()=>{
  assert.match(ui,/const pem=privateKey\|\|\(privateFile\?await privateFile\.text\(\):""\)/);
  assert.doesNotMatch(ui,/disabled=\{!registered\|\|custodyReady\|\|storing\|\|!privateFile\}/);
  assert.match(ui,/!privateKey&&!privateFile/);
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
