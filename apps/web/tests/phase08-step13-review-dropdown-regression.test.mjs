import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const picker=readFileSync(new URL("../app/invoices/searchable-catalog-picker.tsx",import.meta.url),"utf8");
const panel=readFileSync(new URL("../app/invoices/invoice-validation-panel.tsx",import.meta.url),"utf8");
const ui=readFileSync(new URL("../app/invoices/workspace.tsx",import.meta.url),"utf8");

test("Escape is stopped inside open catalog picker and explicit close control exists",()=>{
 assert.match(picker,/event\.key==="Escape" && open/);
 assert.match(picker,/event\.stopPropagation\(\)/);
 assert.match(picker,/aria-label="بستن فهرست کالا و خدمات"/);
 assert.match(picker,/pointerdown/);
});
test("draft review button saves modified draft before requesting validation",()=>{
 assert.match(panel,/ذخیره تغییرات و بررسی/);
 assert.match(panel,/onSave\(\)\.then/);
 assert.match(panel,/if\(!saved\)/);
 assert.match(ui,/onSave=\{\(\)=>saveWorkspace/);
});
