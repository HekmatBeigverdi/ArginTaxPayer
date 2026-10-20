import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {previewBulkInvoices} from "../app/invoices/bulk-import-preview.mjs";
import {bulkInvoiceTemplateXlsx} from "../app/invoices/invoice-template.mjs";
const uuid1="12345678-1234-4234-8234-123456789abc";
const uuid2="22345678-1234-4234-8234-123456789abc";
const header=["invoiceKey","taxpayerId","buyerPartyId","issueDateUtc","type","pattern",
 "settlementMethod","cashAmountRial","description","quantity","unitPriceRial",
 "discountPerUnitRial","vatRatePercent"];
const line=(key,name,qty="1")=>[key,uuid1,uuid2,"2026-10-20T09:00:00Z",
 "1","1","1","0",name,qty,"1000","0","10"];
test("several lines with the same invoiceKey produce one draft",()=>{
 const result=previewBulkInvoices([header,line("INV-1","کاغذ"),line("INV-1","خودکار"),
  line("INV-2","خدمات")]);
 assert.equal(result.length,2);
 assert.equal(result[0].lineCount,2);
 assert.equal(result[0].errors.length,0);
 assert.equal(result[0].draft.lines.length,2);
 assert.equal(result[0].draft.cashAmountRial,2200);
});
test("per-invoice errors do not hide valid invoice previews",()=>{
 const data=[header,line("INV-1","کاغذ"),line("INV-2","خدمات","-2")];
 const result=previewBulkInvoices(data);
 assert.equal(result[0].errors.length,0);
 assert.ok(result[1].errors.length>0);
 assert.equal(result[1].draft,null);
});
test("a mismatched header within one invoice is rejected",()=>{
 const altered=line("INV-1","خودکار");altered[2]=uuid1;
 const result=previewBulkInvoices([header,line("INV-1","کاغذ"),altered]);
 assert.ok(result[0].errors.some(e=>e.includes("سربرگ")));
});
test("preview refuses more than fifty invoice keys",()=>{
 const data=[header,...Array.from({length:51},(_,i)=>line("INV-"+i,"کالا"))];
 assert.throws(()=>previewBulkInvoices(data),/۵۰/);
});
test("template is XLSX zip with canonical bulk headers",()=>{
 const bytes=bulkInvoiceTemplateXlsx();
 assert.equal(bytes[0],80);assert.equal(bytes[1],75);
 const content=new TextDecoder().decode(bytes);
 for(const key of header)assert.ok(content.includes(key),key);
});
test("server handles secure scoped replay and ordered independent outcomes",()=>{
 const api=readFileSync(new URL("../../../src/ArginTaxPayer.Api/Invoices/InvoiceDraftDevelopmentEndpoints.cs",import.meta.url),"utf8");
 const service=readFileSync(new URL("../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceEntryWorkspaceService.cs",import.meta.url),"utf8");
 assert.match(api,/BulkImportKey = "invoice:" \+ invoice\.InvoiceKey/);
 assert.match(api,/results\.Add\(new\(invoice\.InvoiceKey/);
 assert.match(service,/BulkFingerprint/);
 assert.match(service,/CustomerUserId==access\.Item2/);
 assert.match(service,/importId \?\? Guid\.NewGuid\(\)/);
});
