import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {spawnSync} from "node:child_process";
import {test} from "node:test";

const read=p=>readFileSync(new URL("../../../"+p,import.meta.url),"utf8");
const ubuntu=read("scripts/phase15-step17-ubuntu-acceptance.sh");
const mac=read("scripts/phase15-step16-integrated-acceptance.sh");
const ignore=read(".gitignore");
const smoke=read("scripts/phase15-step16-role-smoke.mjs");

test("Ubuntu Step17 shell syntax is valid before any remote execution",()=>{
 const r=spawnSync("bash",["-n"],{input:ubuntu,encoding:"utf8"});
 assert.equal(r.status,0,r.stderr);
});
test("fail closed on wrong OS, branch, dirty files and missing mac SHA",()=>{
 assert.match(ubuntu,/uname -s/);
 assert.match(ubuntu,/VERSION_ID.*24\.04/);
 assert.match(ubuntu,/phase\/15-role-based-dashboard/);
 assert.match(ubuntu,/git status --porcelain --untracked-files=no/);
 assert.match(ubuntu,/PHASE15_MAC_ACCEPTED_SHA/);
 assert.match(ubuntu,/sha.*!=.*mac_sha/);
 assert.match(ubuntu,/^\s*fail\(\)/m);
 assert.match(ubuntu,/git rev-parse HEAD/);
});
test("Ubuntu acceptance checks the exact macOS test matrix",()=>{
 for(const cmd of ["dotnet restore ArginTaxPayer.sln",
  "dotnet build ArginTaxPayer.sln -c Release --no-restore",
  "dotnet test ArginTaxPayer.sln -c Release --no-restore",
  "npm ci","npm test","npm run typecheck","npm run build",
  "phase15-step16-integrated-acceptance.test.mjs",
  "phase15-step03-dashboard-proxy.test.mjs",
  "phase15-step15-query-performance.test.mjs"])
  assert.ok(ubuntu.includes(cmd),cmd);
 assert.ok(mac.includes("phase15-step16-integrated-acceptance.test.mjs"));
 assert.ok(mac.includes("dotnet test ArginTaxPayer.sln"));
});
test("Ubuntu gate must leave QA, API and Sandbox manual and block release",()=>{
 for(const v of ["MANUAL_PENDING","live-role-access","financial-parity",
  "invoice-domain-regression","database-plan-check","environment-configuration",
  "release-and-merge BLOCKED","manualAcceptance","release"])
  assert.ok(ubuntu.includes(v),v);
 assert.doesNotMatch(ubuntu,/dotnet ef database update|git merge|gh release create|git tag|sudo systemctl|curl .*sandbox/);
 assert.match(ubuntu,/umask 077/);
 assert.match(ubuntu,/artifacts\/phase15-step17/);
 assert.ok(ignore.includes("artifacts/phase15-step17/"));
 assert.ok(smoke.includes('method:"GET"'));
});
test("same-SHA handoff does not silently accept old Mac SHA after new code",()=>{
 assert.ok(ubuntu.includes('[[ "$sha" == "$mac_sha" ]]'));
 assert.ok(ubuntu.includes('PHASE15_MAC_ACCEPTED_SHA'));
 assert.ok(ubuntu.includes('record immutable-sha'));
 assert.ok(ubuntu.includes('"automaticFailures":%d'));
});
