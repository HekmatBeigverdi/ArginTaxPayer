import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {invoiceTemplateXlsx} from "../app/invoices/invoice-template.mjs";
const ui=readFileSync(new URL("../app/invoices/invoice-import-panel.tsx",import.meta.url),"utf8");
test("sample Excel is a ZIP and contains expected import columns",()=>{
 const bytes=invoiceTemplateXlsx();
 assert.equal(bytes[0],80);assert.equal(bytes[1],75);
 const content=new TextDecoder().decode(bytes);
 for(const field of ["description","quantity","unitPrice","discount","vatRate"])
  assert.ok(content.includes(field),field);
 assert.ok(content.includes("InvoiceLines"));
});
test("uploader reuses profile dropzone and allows mouse keyboard and drag drop",()=>{
 assert.match(ui,/console-logo-dropzone/);
 assert.match(ui,/console-upload-status/);
 assert.match(ui,/onDragOver/);
 assert.match(ui,/onDrop/);
 assert.match(ui,/onKeyDown/);
 assert.match(ui,/\/api\/invoices\/template/);
});
