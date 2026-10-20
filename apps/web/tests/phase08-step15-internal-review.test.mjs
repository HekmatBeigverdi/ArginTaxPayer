import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const page=readFileSync(new URL("../app/invoices/workspace.tsx",import.meta.url),"utf8");
const api=readFileSync(new URL("../app/api/invoices/[...segments]/route.ts",import.meta.url),"utf8");
const backend=readFileSync(new URL("../../../src/ArginTaxPayer.Api/Invoices/InvoiceDraftDevelopmentEndpoints.cs",import.meta.url),"utf8");
const service=readFileSync(new URL("../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceEntryWorkspaceService.cs",import.meta.url),"utf8");
test("invoice workspace remains independently editable without administrator review UI",()=>{
  assert.doesNotMatch(page,/setReviewId\(item\.id\)/);
  assert.match(page,/ویرایش و ادامه/);
});
test("mandatory approval route is not exposed and cannot block saving",()=>{
  assert.doesNotMatch(api,/segments\[2\] === "review"/);
  assert.doesNotMatch(backend,/MapPost\("\/api\/dev\/invoice-workspaces\/\{id:guid\}\/review"/);
  assert.doesNotMatch(service,/if \(reviewBeforeEdit is "InReview" or "Approved"\)/);
});
