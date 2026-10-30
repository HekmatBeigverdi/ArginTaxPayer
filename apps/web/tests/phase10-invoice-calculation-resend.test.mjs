import {strict as assert} from "node:assert";
import {readFileSync} from "node:fs";
import {test} from "node:test";
const read=p=>readFileSync(new URL("../"+p,import.meta.url),"utf8");

test("calculation audit BFF allowlists organization error codes",()=>{
 const route=read("app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/calculation-audit/route.ts");
 for(const code of ["0204501","0304401","0200201","00010","00026"])assert.match(route,new RegExp(code));
 assert.match(route,/calculation-audit/);assert.match(route,/response_too_large/);
});

test("invoice readiness displays bounded calculation findings before explicit send",()=>{
 const ui=read("app/invoices/invoice-send-readiness-panel.tsx");
 assert.match(ui,/خطاهای محاسباتی و فیلدهای اجباری/);
 assert.match(ui,/calculationAudit\.findings\.map/);
 assert.match(ui,/calculation-audit/);
 assert.match(ui,/Number\.isSafeInteger\(data\.invoiceRevision\)/);
 assert.match(ui,/onClick=\{\(\)=>void oneClickSend\(\)\}/);
});

test("one-click preparation validates audited payload before signing and sending",()=>{
 const api=read("app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/prepare-legacy/route.ts");
 const service=read("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyInvoicePreparationService.cs");
 const flow=read("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10OneClickSandboxSubmissionService.cs");
 assert.match(service,/WirePreviewJson/);
 assert.match(api,/wirePreviewJson/);
 assert.match(service,/invoice_calculation_audit_failed/);
 assert.ok(flow.indexOf('preparation.PrepareAsync')<flow.indexOf('worker.ProcessInvoiceAsync'));
 assert.match(flow,/if\(prepared.Decision!=InvoiceApiDecision.Success\)/);
 assert.match(flow,/return Stop\(prepared.SafeCode,prepared.Decision\)/);
});
