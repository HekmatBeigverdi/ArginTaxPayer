import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const web = readFileSync(new URL("../app/invoices/workspace.tsx", import.meta.url),"utf8");
const proxy = readFileSync(new URL("../app/api/invoices/[...segments]/route.ts", import.meta.url),"utf8");
const service = readFileSync(new URL("../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceEntryWorkspaceService.cs", import.meta.url),"utf8");
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


test("type 2 general sale disables buyer details and backend rejects them",()=>{
  assert.match(web,/buyerDetailsNotRequired = invoiceType === 2 && pattern === 1/);
  assert.match(web,/disabled=\{buyerDetailsNotRequired\}/);
  assert.match(web,/buyerPartyId:buyerDetailsNotRequired \? null : buyerId \|\| null/);
  assert.match(service,/input\.Type == 2 && input\.Pattern == 1 &&\s*\(input\.BuyerPartyId is not null \|\| !string\.IsNullOrWhiteSpace\(input\.BuyerDisplayName\)\)/);
});

test("type 1 contracting keeps contract number in dynamic required fields",()=>{
  assert.match(web,/requiredFields/);
  assert.match(web,/contractNumber/);
  assert.match(service,/input\.Type == 1 && input\.Pattern == 4 && !Digits\(input\.ContractNumber,12\)/);
});
