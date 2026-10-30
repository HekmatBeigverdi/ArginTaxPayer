import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {dirname,resolve} from "node:path";
import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");
const read=p=>readFileSync(resolve(root,p),"utf8");
test("printed tax identity comes from reserved 22-character fiscal taxid, never UUID",()=>{
 const backend=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoicePrintPreviewService.cs");
 const preview=read("apps/web/app/invoices/invoice-formal-preview.tsx");
 assert.ok(backend.includes("FROM sandbox_invoice_fiscal_reservations"));
 assert.ok(backend.includes("uniqueInvoiceId=fiscalTaxId"));
 assert.ok(!backend.includes('uniqueInvoiceId=invoiceId.ToString("D")'));
 assert.ok(preview.includes("شناسه یکتای مالیاتی"));
 assert.ok(preview.includes("در انتظار تخصیص"));
});
test("invoice list wires the completion callback and refreshes when closing detail modal",()=>{
 const list=read("apps/web/app/invoices/workspace.tsx");
 assert.ok(list.includes("onCompleted={()=>void reload()}"));
 assert.ok(list.includes("setDetail(null);void reload();"));
 // Completion after acknowledgement and inquiry failure is exercised by phase10-dispatch-arm-contract.
});
test("subject labels correspond to the four legal subject enums",()=>{
 const domain=read("src/ArginTaxPayer.Domain/Invoices/InvoiceSubjects.cs");
 const labels=read("apps/web/app/invoices/invoice-subject-badge.tsx");
 for(const name of ["Original","Correction","Cancellation","SalesReturn"]){
  assert.ok(domain.includes(name+" = "));
  assert.ok(labels.includes(name+':'));
 }
});
