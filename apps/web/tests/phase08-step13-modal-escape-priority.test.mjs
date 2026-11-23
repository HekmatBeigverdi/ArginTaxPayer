import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const modal=readFileSync(new URL("../app/invoices/workspace.tsx",import.meta.url),"utf8");
const picker=readFileSync(new URL("../app/invoices/searchable-catalog-picker.tsx",import.meta.url),"utf8");
test("Modal document Escape handler gives catalog priority",()=>{
  const handler=modal.slice(modal.indexOf("const onKey = (event: KeyboardEvent)"),modal.indexOf('document.addEventListener("keydown", onKey)'));
  assert.match(handler,/querySelector\('\[data-invoice-catalog-open="true"\]'\)/);
  assert.ok(handler.indexOf("invoice-catalog-dismiss") < handler.indexOf("setModal(false)"));
});
test("Picker exposes open flag and handles document-triggered dismiss",()=>{
  const select=readFileSync(new URL("../app/components/searchable-select.tsx",import.meta.url),"utf8");
  assert.match(modal,/data-invoice-catalog-open="true"/);
  assert.match(select,/event\.stopImmediatePropagation\(\);closeMenu\(\)/);
});
