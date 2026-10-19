import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const ui=readFileSync(new URL("../app/invoices/workspace.tsx",import.meta.url),"utf8");
const proxy=readFileSync(new URL("../app/api/invoices/[...segments]/route.ts",import.meta.url),"utf8");
test("seller scope determines fiscal memory choices",()=>{
  assert.match(ui,/\/api\/invoices\/header-options\/.*taxpayerId/);
  assert.match(proxy,/\/api\/dev\/invoices\/header-options\//);
  assert.match(ui,/m\.id===memoryId/);
  assert.match(ui,/fiscalMemoryId:memoryId \|\| null/);
});
test("buyer must be selected rather than free-typed, and memory resets on seller change",()=>{
  assert.match(ui,/buyerQuery\.trim\(\) && !buyerId/);
  assert.match(ui,/setTaxpayerId\(e\.target\.value\);setMemoryId\(""/);
  assert.match(ui,/setMemoryId\(saved\.fiscalMemoryId \?\? ""\)/);
});
