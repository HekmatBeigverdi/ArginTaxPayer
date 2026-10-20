import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {parseCsv,previewRows,MAX_IMPORT_ROWS} from "../app/invoices/invoice-import-preview.mjs";
const map={description:0,quantity:1,unitPrice:2,discount:3,vatRate:4};
test("quoted CSV, commas, escaped quotes and newlines",()=>{
 const rows=parseCsv('description,quantity,unitPrice,discount,vatRate\n"paper, A4",2,100,0,10\n"item ""quoted""",1,50,0,0\n');
 assert.equal(rows.length,3);
 assert.equal(rows[1][0],"paper, A4");
 assert.equal(rows[2][0],'item "quoted"');
 assert.equal(previewRows(rows,map).every(x=>x.errors.length===0),true);
});
test("invalid rows explain which input failed",()=>{
 const rows=[["description","quantity","unitPrice","discount","vatRate"],["", "-1","x","0","0"],["ok","1","50","51","10"]];
 const result=previewRows(rows,map);
 assert.equal(result.length,2);
 assert.ok(result[0].errors.length>0);
 assert.match(result[1].errors.join(" "),/تخفیف/);
});
test("duplicate mappings and huge inputs rejected",()=>{
 assert.throws(()=>previewRows([["a","b"],["x","1"]],{description:0,quantity:0,unitPrice:1}),/ستون/);
 const rows=[["a","b"],...Array.from({length:MAX_IMPORT_ROWS+1},()=>["A","1"])];
 assert.throws(()=>previewRows(rows,map),/۵۰۰/);
});
test("import UI never posts file contents directly to server",()=>{
 const ui=readFileSync(new URL("../app/invoices/invoice-import-panel.tsx",import.meta.url),"utf8");
 const page=readFileSync(new URL("../app/invoices/workspace.tsx",import.meta.url),"utf8");
 assert.match(ui,/onImport\(previews\.items\.map/);
 assert.match(page,/setRows\(current => current\.concat\(imported\.map/);
 assert.doesNotMatch(ui,/fetch\(/);
});
