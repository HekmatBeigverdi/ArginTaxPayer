import test from "node:test";import assert from "node:assert/strict";import {readFileSync} from "node:fs";import {dirname,resolve} from "node:path";import {fileURLToPath} from "node:url";const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");const read=p=>readFileSync(resolve(root,p),"utf8");
test("accepted fiscal invoice exposes cancellation confirmation without mutating predecessor",()=>{
 const ui=read("apps/web/app/invoices/invoice-formal-preview.tsx");
 assert.ok(ui.includes('authorityStatus!=="SUCCESS"'));
 assert.ok(ui.includes('onClick={onCancel}'));
 assert.ok(ui.includes('setCancelOpen(true)'));
 assert.ok(ui.includes('role="dialog"'));
 assert.ok(ui.includes('غیرقابل بازگشت'));
 assert.ok(ui.includes('invoice?.uniqueInvoiceId'));
 assert.ok(ui.includes("/cancel-and-inquire"));
 assert.ok(ui.includes("disabled={!cancelEligible||cancelChecking||cancelSubmitting}"));
});
test("cancellation submission is wired to authenticated backend endpoint",()=>{
 const api=read("src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs");
 assert.ok(api.includes('MapPost("/cancel-and-inquire"'));
});
