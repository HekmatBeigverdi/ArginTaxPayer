import {strict as assert} from "node:assert";
import {readFileSync} from "node:fs";
import {test} from "node:test";
const read=p=>readFileSync(new URL("../"+p,import.meta.url),"utf8");

test("calculation audit BFF allowlists organization error codes",()=>{
 const route=read("app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/calculation-audit/route.ts");
 for(const code of ["0204501","0304401","0200201","00010","00026"])assert.match(route,new RegExp(code));
 assert.match(route,/calculation-audit/);assert.match(route,/response_too_large/);
});

test("invoice readiness UI exposes a guarded calculation resend action",()=>{
 const ui=read("app/invoices/invoice-send-readiness-panel.tsx");
 assert.match(ui,/بررسی محاسبات و آماده‌سازی ارسال مجدد/);
 assert.match(ui,/calculation-audit/);
 assert.match(ui,/Acknowledged/);assert.match(ui,/Indeterminate/);
 assert.match(ui,/canResend/);
});

test("legacy resend shows the exact wire JSON and requires review before sending",()=>{
 const ui=read("app/invoices/invoice-send-readiness-panel.tsx");
 const api=read("app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/prepare-legacy/route.ts");
 const service=read("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyInvoicePreparationService.cs");
 assert.match(service,/WirePreviewJson/);
 assert.match(api,/wirePreviewJson/);
 assert.match(ui,/مقادیر نهایی ارسالی/);
 assert.match(ui,/کپی JSON/);
 assert.match(ui,/reviewedWirePreview/);
 assert.match(ui,/!reviewedWirePreview/);
});
