import test from "node:test";import assert from "node:assert/strict";
import {readFileSync} from "node:fs";import {resolve,dirname} from "node:path";import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");
const read=p=>readFileSync(resolve(root,p),"utf8");
test("lineage is tenant scoped and tax ids originate in reserved fiscal identifiers",()=>{
 const source=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoiceLineageReadService.cs");
 assert.match(source,/scope.ScopedQuery/);
 assert.match(source,/d.customer_user_id=@owner/);
 assert.match(source,/sandbox_invoice_fiscal_reservations/);
 assert.match(source,/sandbox_authority_inquiry_reports/);
 assert.match(source,/state is 4 or 5 or 6/);
});
test("list row chain action is conditional; modal uses read-only API",()=>{
 const list=read("apps/web/app/invoices/workspace.tsx");
 const modal=read("apps/web/app/invoices/invoice-lineage-modal.tsx");
 assert.ok(list.includes("onClick={()=>setLineage(item)}"));
 assert.ok(list.includes('"INDETERMINATE","SENDING"'));
 assert.ok(modal.includes("/lineage"));
 assert.ok(modal.includes('cache:"no-store"'));
 assert.ok(modal.includes("شناسه مالیاتی"));
});
test("BFF checks session and makes only one scoped GET request",()=>{
 const s=read("apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/lineage/route.ts");
 assert.ok(s.includes("ACCESS_COOKIE"));
 assert.ok(s.includes("dispatch/lineage"));
 assert.equal((s.match(/await fetch\(/g)||[]).length,1);
});
