import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const script=readFileSync("../../scripts/phase10-step21-macos-acceptance.sh","utf8");
const gitignore=readFileSync("../../.gitignore","utf8");
test("step21 evidence pins macOS branch SHA and no dirty worktree",()=>{
  assert.match(script,/uname -s/);
  assert.match(script,/Darwin/);
  assert.match(script,/phase\/10-invoice-submission/);
  assert.match(script,/git rev-parse HEAD/);
  assert.match(script,/git status --porcelain/);
  assert.match(script,/exact-sha-and-clean-worktree/);
  assert.match(gitignore,/^artifacts\/phase10-step21-\*\/$/m);
});
test("step21 executes build, test, readonly database and live service readiness checks",()=>{
  assert.match(script,/dotnet build ArginTaxPayer.sln --no-restore/);
  assert.match(script,/dotnet test ArginTaxPayer.sln --no-restore/);
  assert.match(script,/npm test/);
  assert.match(script,/npm run typecheck/);
  assert.match(script,/npm run build/);
  assert.match(script,/phase10-step20-db-audit.sh/);
  assert.match(script,/health\/ready/);
  assert.match(script,/127\.0\.0\.1:3000/);
  assert.match(script,/manifest.tsv/);
});
test("step21 cannot silently pass missing manual, backup or secret checks",()=>{
  assert.match(script,/MANUAL_PENDING/);
  assert.match(script,/backup-restore-isolated-rehearsal/);
  assert.match(script,/operator_acceptance=PENDING/);
  assert.match(script,/no-credentials-in-logs/);
  assert.match(script,/no-invoice-submit-network/);
  assert.doesNotMatch(script,/dotnet ef database update|pg_restore\s+--dbname|\bDROP TABLE\b|\bgit push\b|GET_TOKEN|SEND_INVOICE/);
});
