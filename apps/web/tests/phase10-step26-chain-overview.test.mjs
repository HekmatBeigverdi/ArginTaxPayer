import test from "node:test";import assert from "node:assert/strict";
import {readFileSync} from "node:fs";import {dirname,resolve} from "node:path";import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");const read=p=>readFileSync(resolve(root,p),"utf8");
test("main invoice overview hides successor before paging and retains original system code",()=>{
 const s=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceDraftApplicationService.cs");
 assert.ok(s.includes('successor_invoice_id AS "Value"'));
 assert.ok(s.includes("!legalSuccessors.Contains(x.Id)"));
 assert.ok(s.indexOf("!legalSuccessors.Contains(x.Id)")<s.indexOf(".Skip((page - 1) * pageSize)"));
 assert.ok(s.includes("effectiveByRoot.TryGetValue(x.Id,out var effectiveSubject)"));
 assert.ok(s.includes("effectiveByRoot.TryGetValue(x.Id,out var effectiveDate)"));
 assert.ok(s.includes("effectiveByRoot.TryGetValue(x.Id,out var effectiveStatus)"));
 assert.ok(s.includes('referenceCodes.GetValueOrDefault((x.Source == 1 ? "workspace" : "document", x.Id))'));
});
test("lineage includes independently stored related invoices and same-taxpayer scope",()=>{
 const s=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoiceLineageReadService.cs");
 assert.ok(s.includes("sandbox_invoice_legal_relations rel"));
 assert.ok(s.includes("d.id=rel.successor_invoice_id"));
 assert.ok(s.includes("rel.customer_user_id=@owner AND rel.taxpayer_id=@taxpayer"));
 assert.ok(s.includes("nodes.Add(new(next.GetGuid(0)"));
 assert.ok(s.includes("new(attempted,nodes.Count>1"));
});
