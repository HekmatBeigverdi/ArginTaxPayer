import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const policy=readFileSync("../../src/ArginTaxPayer.Application/Invoices/Phase10InvoiceRecoveryAdvice.cs","utf8");
const service=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoiceDispatchHistoryService.cs","utf8");
const bff=readFileSync("app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/history/route.ts","utf8");
const ui=readFileSync("app/invoices/invoice-dispatch-history-panel.tsx","utf8");
const endpoint=readFileSync("../../src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs","utf8");

test("terminal, unknown and transient dispatch statuses never authorize blind retry",()=>{
 assert.match(policy,/InvoiceRecoveryNextAction.AwaitReconciliation/);
 assert.match(policy,/InvoiceRecoveryNextAction.NoFurtherSend/);
 assert.match(policy,/InvoiceRecoveryNextAction.ManualReview/);
 assert.match(policy,/false,false,false,false/);
 assert.doesNotMatch(policy,/SendAsync|HttpClient|UpdateAsync/);
});
test("recovery advice is returned only from scoped read-only dispatch history",()=>{
 assert.match(service,/InvoiceDocuments.AsNoTracking/);
 assert.match(service,/Phase10InvoiceRecoveryAdvice/);
 assert.match(service,/new\("Mock",false,false,false/);
 assert.match(service,/next.MayRetry,next.MayInquire/);
 assert.match(endpoint,/MapGet\("\/history"/);
 assert.match(bff,/mayRetry:false,mayInquire:false/);
 assert.match(bff,/row.mayRetry!==false\|\|row.mayInquire!==false/);
 assert.doesNotMatch(bff,/export async function POST/);
});
test("UI shows bounded recovery advice and protects against duplicate clicks",()=>{
 assert.match(ui,/AwaitReconciliation/);
 assert.match(ui,/نتیجه قطعی نیست/);
 assert.match(ui,/Refresh فقط اطلاعات ثبت‌شده/);
 assert.match(ui,/disabled=\{isWorkspace\|\|busy\}/);
 assert.match(ui,/className="identity-primary" disabled/);
 assert.doesNotMatch(ui,/method:"POST"|\.post\(/);
});
