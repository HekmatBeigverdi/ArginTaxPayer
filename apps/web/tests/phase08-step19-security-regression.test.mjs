import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {parseCsv,previewRows} from "../app/invoices/invoice-import-preview.mjs";
import {previewBulkInvoices} from "../app/invoices/bulk-import-preview.mjs";

const source=(path)=>readFileSync(new URL(path,import.meta.url),"utf8");
const bff=source("../app/api/invoices/[...segments]/route.ts");
const api=source("../../../src/ArginTaxPayer.Api/Invoices/InvoiceDraftDevelopmentEndpoints.cs");
const service=source("../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceEntryWorkspaceService.cs");
const wizard=source("../app/invoices/workspace.tsx");
test("invoice writes require authenticated same origin BFF and scoped backend",()=>{
 assert.match(bff,/isSameLocalOrigin/);
 assert.match(bff,/ACCESS_COOKIE/);
 assert.match(bff,/Bearer /);
 assert.match(bff,/invalid_bulk_manifest/);
 assert.match(api,/if \(!Local\(http\)\)/);
 assert.match(service,/scope\.ScopedQuery/);
 assert.match(service,/CustomerPartyLinks/);
 assert.match(service,/expectedRevision|ExpectedRevision/);
});
test("importer cannot bypass explicit confirmation or transmit to tax authority",()=>{
 const ui=source("../app/invoices/bulk-invoice-import-panel.tsx");
 assert.match(ui,/onClick=\{\(\)=>void submit\(\)\}/);
 assert.match(ui,/\/api\/invoices\/workspaces\/bulk/);
 assert.doesNotMatch(ui,/\/api\/taxpayer-system|\/api\/submit-to-tax/);
 assert.match(api,/BulkImportKey = "invoice:"/);
 assert.match(service,/InvoiceEntryWorkspaceRevisions\.Add/);
});
test("existing wizard remains resumable and has no mandatory admin review gate",()=>{
 assert.match(wizard,/InvoiceHistoryPanel/);
 assert.match(wizard,/savedSnapshot/);
 assert.match(wizard,/ExpectedRevision|expectedRevision/);
 assert.doesNotMatch(wizard,/تأیید اجباری ادمین/);
});
test("CSV malformed quoting and duplicate column source mappings are rejected",()=>{
 assert.throws(()=>parseCsv('a,b\n"unterminated'),/نقل‌قول/);
 assert.throws(()=>previewRows([["a","b","c"],["item","1","2"]],
  {description:0,quantity:0,unitPrice:2}),/نگاشت/);
});
test("bulk preview rejects duplicated header names",()=>{
 const headers=["invoiceKey","taxpayerId","buyerPartyId","issueDateUtc","type","pattern",
 "settlementMethod","cashAmountRial","description","quantity","unitPriceRial",
 "discountPerUnitRial","vatRatePercent"];
 const repeated=[...headers];repeated[12]="invoiceKey";
 assert.throws(()=>previewBulkInvoices([repeated,Array(13).fill("x")]),/ستون/);
});
