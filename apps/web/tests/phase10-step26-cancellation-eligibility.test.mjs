import test from "node:test";import assert from "node:assert/strict";import {readFileSync} from "node:fs";import {dirname,resolve} from "node:path";import {fileURLToPath} from "node:url";const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");const read=x=>readFileSync(resolve(root,x),"utf8");
test("cancellation guard checks a confirmed predecessor with distinct fiscal ID",()=>{
 const s=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10CancellationEligibilityService.cs");
 for(const x of ['scope.ScopedQuery','AuthorityResult?.Status!="SUCCESS"','sandbox_invoice_legal_relations','sandbox_invoice_fiscal_reservations','cancellation_already_requested'])assert.ok(s.includes(x),x);
});
test("failed cancellation attempts are released for retry",()=>{
 const service=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10SandboxCancellationService.cs");
 const eligibility=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10CancellationEligibilityService.cs");
 assert.match(service,/authority_inquiry_reports/);
 assert.match(service,/SET state=3/);
 assert.match(eligibility,/authority_inquiry_reports/);
});
test("cancellation migration protects immutable predecessor link and active claims",()=>{
 const s=read("src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261029121500_AddSandboxLegalInvoiceRelations.cs");
 for(const x of ["FOREIGN KEY","predecessor_invoice_id<>successor_invoice_id","ux_sandbox_legal_active_cancellation","idempotency_key"])assert.ok(s.includes(x),x);
});
test("UI shows preflight and routes confirmed cancellation to dedicated backend",()=>{
 const s=read("apps/web/app/invoices/invoice-formal-preview.tsx");
 assert.ok(s.includes("/cancellation-eligibility"));
 assert.ok(s.includes("در حال بررسی سند مرجع"));
 assert.ok(s.includes("/cancel-and-inquire"));
});
