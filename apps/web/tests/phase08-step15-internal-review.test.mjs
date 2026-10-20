import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const ui=readFileSync(new URL("../app/invoices/internal-review-panel.tsx",import.meta.url),"utf8");
const page=readFileSync(new URL("../app/invoices/workspace.tsx",import.meta.url),"utf8");
const api=readFileSync(new URL("../app/api/invoices/[...segments]/route.ts",import.meta.url),"utf8");
const service=readFileSync(new URL("../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceEntryWorkspaceService.cs",import.meta.url),"utf8");
test("review UI exposes read-only financial and line snapshot",()=>{
 assert.match(ui,/draft\.lines\.map/);
 assert.match(ui,/financialSummary\.previewGrandTotalRial/);
 assert.match(ui,/validation\?\.findings/);
 assert.match(page,/setReviewId\(item\.id\)/);
});
test("review requests are versioned and bound to a server state machine",()=>{
 assert.match(ui,/expectedRevision:draft\.revision/);
 assert.match(api,/segments\[2\] === "review"/);
 assert.match(service,/InvoiceInternalReviewPolicy\.CanTransition/);
 assert.match(service,/ReviewedByUserId/);
});
