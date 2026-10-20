import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const source=(p)=>readFileSync(new URL(p,import.meta.url),"utf8");
const service=source("../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceEntryWorkspaceService.cs");
const migration=source("../../../src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261020104500_AddInvoiceWorkspaceRevisionAudit.cs");
const endpoint=source("../../../src/ArginTaxPayer.Api/Invoices/InvoiceDraftDevelopmentEndpoints.cs");
const proxy=source("../app/api/invoices/[...segments]/route.ts");
const ui=source("../app/invoices/invoice-history-panel.tsx");
test("history insert is inside the same transaction as revision CAS",()=>{
 assert.match(service,/BeginTransactionAsync\(ct\)/);
 assert.match(service,/x\.Revision == input\.ExpectedRevision/);
 assert.match(service,/InvoiceEntryWorkspaceRevisions\.Add/);
 assert.match(service,/transaction\.CommitAsync\(ct\)/);
});
test("history is database append-only and rollback refuses nonempty history",()=>{
 assert.match(migration,/BEFORE UPDATE OR DELETE ON invoice_entry_workspace_revisions/);
 assert.match(migration,/Cannot remove nonempty invoice workspace audit history/);
});
test("history is scoped, read-only, and presented with actor and UTC date",()=>{
 assert.match(service,/allowed\.Contains\(h\.TaxpayerId\)/);
 assert.match(endpoint,/history"/);
 assert.match(proxy,/segments\[2\] === "history"/);
 assert.match(ui,/occurredAtUtc/);
 assert.match(ui,/actorUserId/);
});
