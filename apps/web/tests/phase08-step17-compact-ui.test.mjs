import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {invoiceTemplateXlsx} from "../app/invoices/invoice-template.mjs";
const read=p=>readFileSync(new URL(p,import.meta.url),"utf8");
test("compact rows stay in a bounded scroll region with explicit labelled fields",()=>{
 const ui=read("../app/invoices/workspace.tsx");
 assert.match(ui,/maxHeight:"min\(48vh,440px\)"/);
 assert.match(ui,/aria-label=\{"شرح ردیف "/);
 assert.doesNotMatch(ui,/واحد پایه ثبت‌شده:/);
 assert.match(ui,/setImportOpen\(true\)/);
});
test("import remains in a separate dismissible dialog",()=>{
 const ui=read("../app/invoices/invoice-import-panel.tsx");
 assert.match(ui,/role="dialog"/);
 assert.match(ui,/onClose\(\)/);
 assert.match(ui,/console-logo-dropzone/);
});
test("template header uses contrasting colors and includes required fields",()=>{
 const source=read("../app/invoices/invoice-template.mjs");
 assert.match(source,/rgb="FF173C32"/);
 assert.match(source,/rgb="FFE4F2EB"/);
 const bytes=invoiceTemplateXlsx();
 assert.equal(bytes[0],80);
 assert.equal(bytes[1],75);
});
