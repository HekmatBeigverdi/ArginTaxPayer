import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {resolve,dirname} from "node:path";
import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");
const read=p=>readFileSync(resolve(root,p),"utf8");
test("step24 history is scoped and shows attempts and receipts without sending",()=>{
 const history=read("apps/web/app/invoices/invoice-dispatch-history.tsx");
 const parent=read("apps/web/app/invoices/invoice-send-readiness-panel.tsx");
 const bff=read("apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/history/route.ts");
 const api=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoiceDispatchHistoryService.cs");
 for(const field of ["attempts","receipts","Indeterminate","no-store","invoiceId","taxpayerId"])
  assert.ok(history.includes(field),field);
 assert.ok(parent.includes("<InvoiceDispatchHistory taxpayerId={taxpayerId} invoiceId={invoiceId}/>"));
 assert.ok(bff.includes('row.environment!=="Sandbox"'));
 assert.ok(api.includes('new("Sandbox",false,false,false,'));
 assert.ok(!history.includes('method:"POST"'));
 assert.ok(!history.includes("normal-enqueue"));
});
