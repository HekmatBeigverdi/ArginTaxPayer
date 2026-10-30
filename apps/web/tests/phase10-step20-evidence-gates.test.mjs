import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const cmd=readFileSync("../../scripts/phase10-step20-macos-gate.sh","utf8");
const audit=readFileSync("../../scripts/phase10-step20-db-audit.sql","utf8");
const auditRunner=readFileSync("../../scripts/phase10-step20-db-audit.sh","utf8");

test("Step20 runner binds to macOS and exact branch/commit without any migration or live invoice send",()=>{
  assert.match(cmd,/uname -s/);
  assert.match(cmd,/Darwin/);
  assert.match(cmd,/phase\/10-invoice-submission/);
  assert.match(cmd,/git rev-parse HEAD/);
  assert.match(cmd,/dotnet build ArginTaxPayer.sln --no-restore/);
  assert.match(cmd,/dotnet test ArginTaxPayer.sln --no-restore/);
  assert.match(cmd,/npm run typecheck/);
  assert.match(cmd,/npm run build/);
  assert.match(cmd,/npm test/);
  assert.match(cmd,/manifest\.tsv/);
  assert.doesNotMatch(cmd,/dotnet ef database update|pg_restore |curl .*SEND|git push|git checkout --/);
});
test("Step20 DB evidence is readonly and rejects foreign databases",()=>{
  assert.match(auditRunner,/Database'\)=='argintaxpayer_test'/);
  assert.match(auditRunner,/127\.0\.0\.1/);
  assert.match(auditRunner,/15432/);
  assert.match(auditRunner,/default_transaction_read_only=on/);
  assert.match(audit,/BEGIN READ ONLY/);
  assert.match(audit,/__EFMigrationsHistory/);
  assert.match(audit,/trg_phase07_invoice_history_guard/);
  assert.match(audit,/trg_p10_attempts_immutable/);
  assert.match(audit,/trg_p10_receipts_immutable/);
  assert.doesNotMatch(audit,/\b(?:INSERT|UPDATE|DELETE|TRUNCATE|DROP|ALTER)\s+(?:INTO\s+|TABLE\s+)?(?:public\.)?invoice_/i);
});
