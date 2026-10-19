import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const web = readFileSync(new URL("../app/invoices/workspace.tsx", import.meta.url),"utf8");
const proxy = readFileSync(new URL("../app/api/invoices/[...segments]/route.ts", import.meta.url),"utf8");
test("form pattern metadata comes from server rather than hardcoded arrays",()=>{
  assert.match(web,/getJson<FormMetadata>\("\/api\/invoices\/form-metadata"\)/);
  assert.match(web,/formMetadata\?\.entries\.filter/);
  assert.doesNotMatch(web,/const patterns: Record/);
  assert.match(proxy,/\/api\/dev\/invoices\/form-metadata/);
});
test("unavailable pattern metadata disables selectors, and official edition is visible",()=>{
  assert.match(web,/disabled=\{!formMetadata \|\| busy\}/);
  assert.match(web,/formMetadata\.edition/);
  assert.match(web,/verifiedForSubmission/);
});
