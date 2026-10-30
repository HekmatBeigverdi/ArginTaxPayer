import assert from "node:assert/strict";
import {test} from "node:test";
import {readFileSync} from "node:fs";
import {fileURLToPath} from "node:url";
import {resolve,dirname} from "node:path";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"..");
const read=(path)=>readFileSync(resolve(root,path),"utf8");

test("sales-return draft proxy is authenticated and same-origin",()=>{
 const code=read("app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/sales-return-draft/route.ts");
 assert.match(code,/ACCESS_COOKIE/);
 assert.match(code,/isSameLocalOrigin/);
 assert.match(code,/invalid_sales.return_input|invalid_sales_return_input/);
 assert.match(code,/sales-return-draft/);
});
test("submission uses separate one-attempt endpoint",()=>{
 const code=read("app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/sales-return-send-and-inquire/route.ts");
 assert.match(code,/sales-return-send-and-inquire/);
 assert.match(code,/isSameLocalOrigin/);
});
test("return editor does not allow price or VAT changes",()=>{
 const code=read("app/invoices/invoice-sales-return-modal.tsx");
 assert.match(code,/\(\["quantity"\] as const\)/);
 assert.match(code,/setSubmissionLocked\(true\)/);
 assert.match(code,/InvoiceCorrectionReceipt/);
});
test("list exposes return action on accepted invoice and keeps original identity",()=>{
 const code=read("app/invoices/workspace.tsx");
 assert.match(code,/setReturnTarget\(item\)/);
 assert.match(code,/invoiceId=\{returnTarget\.id\}/);
});
