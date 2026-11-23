import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const picker=readFileSync(new URL("../app/invoices/searchable-catalog-picker.tsx",import.meta.url),"utf8");
const panel=readFileSync(new URL("../app/invoices/invoice-validation-panel.tsx",import.meta.url),"utf8");
const ui=readFileSync(new URL("../app/invoices/workspace.tsx",import.meta.url),"utf8");

test("Escape is stopped inside open catalog picker and explicit close control exists",()=>{
 assert.match(picker,/<SearchableSelect/);
 const select=readFileSync(new URL("../app/components/searchable-select.tsx",import.meta.url),"utf8");
 assert.match(select,/event\.key!=="Escape"/);
 assert.match(select,/event\.stopPropagation\(\)/);
 assert.match(select,/aria-label="بستن فهرست انتخاب"/);
 assert.match(select,/document\.addEventListener\("pointerdown",outside\)/);
});
test("draft review button saves modified draft before requesting validation",()=>{
 assert.match(panel,/بررسی نهایی/);
 assert.match(panel,/onSave\(\)\.then/);
 assert.match(panel,/if\(!saved\)/);
 assert.match(ui,/onSave=\{\(\)=>saveWorkspace/);
});
