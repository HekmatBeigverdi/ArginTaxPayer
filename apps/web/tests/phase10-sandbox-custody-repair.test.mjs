import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const old=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261024103000_AddSandboxFiscalMemoryMetadata.cs","utf8");
const repair=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261024170000_AddSandboxEncryptedCustodyColumns.cs","utf8");
const helper=readFileSync("../../scripts/phase10-sandbox-custody-repair.zsh","utf8");

test("already-applied original migration remains metadata only",()=>{
  assert.doesNotMatch(old,/private_envelope_json|signing_key_version_id|origin_operation_id/);
  assert.match(old,/CREATE TABLE sandbox_fiscal_memories/);
});
test("repair is strictly additive and isolated to test DB",()=>{
  assert.match(repair,/argintaxpayer_test/);
  assert.match(repair,/ALTER TABLE public\.sandbox_fiscal_memories/);
  assert.match(repair,/ADD COLUMN private_envelope_json text NULL/);
  assert.match(repair,/ADD COLUMN signing_key_version_id uuid NULL/);
  assert.match(repair,/ADD COLUMN origin_operation_id uuid NULL/);
  assert.match(repair,/gen_random_uuid\(\)/);
  assert.match(repair,/ALTER COLUMN signing_key_version_id SET NOT NULL/);
  assert.match(repair,/ALTER COLUMN origin_operation_id SET NOT NULL/);
  assert.match(repair,/partially exist/);
  assert.doesNotMatch(repair,/DELETE FROM|DROP TABLE|TRUNCATE|ALTER TABLE fiscal_memories/);
});
test("operator repair requires backup SQL review and explicit opt-in",()=>{
  assert.match(helper,/pg_dump -Fc/);
  assert.match(helper,/pg_restore --list/);
  assert.match(helper,/dotnet ef migrations script/);
  assert.match(helper,/approval.*APPLY/);
  assert.match(helper,/\[\[ "\$approval" == 'APPLY' \]\]/);
  assert.match(helper,/count\(\*\)/);
  assert.match(helper,/argintaxpayer_test/);
});
