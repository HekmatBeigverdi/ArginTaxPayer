import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {resolve,dirname} from "node:path";
import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");
const read=p=>readFileSync(resolve(root,p),"utf8");
test("one-click cancel has separate immutable successor and fiscal reference",()=>{
 const service=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10SandboxCancellationService.cs");
 for(const name of ["InvoiceSubject.Cancellation","InvoiceDocumentRevisions.Add","sandbox_invoice_legal_relations","referenceTaxId","submission.SendAsync","argintaxpayer_test"])
  assert.ok(service.includes(name),name);
 assert.ok(service.includes(":attempt:"));
 assert.ok(service.includes("LegacyAuthorityReportRepository"));
 assert.ok(service.includes("confirmed_at_utc"));
});
test("cancellation transaction is executed through the configured EF retry strategy",()=>{
 const service=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10SandboxCancellationService.cs");
 assert.match(service,/CreateExecutionStrategy\(\)\.ExecuteAsync/);
});
test("backend cancel route and BFF are wired with same-origin protection",()=>{
 const api=read("src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs");
 const bff=read("apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/cancel-and-inquire/route.ts");
 assert.ok(api.includes('MapPost("/cancel-and-inquire"'));
 assert.ok(bff.includes("isSameLocalOrigin"));
 assert.equal((bff.match(/await fetch\(/g)||[]).length,1);
});
test("cancellation wire preserves predecessor monetary totals and only changes subject/reference",()=>{
 const factory=read("src/ArginTaxPayer.Domain/Invoices/Phase10CancellationWireFactory.cs");
 const projector=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyFrozenInvoiceProjector.cs");
 assert.ok(factory.includes("InvoiceSubject.Cancellation"));
 assert.ok(factory.includes("totalBeforeDiscountRial"));
 assert.ok(factory.includes("totalBillRial"));
 assert.doesNotMatch(factory,/Array\.Empty<InvoiceWireLine>\(\).*0,0,0,0,0/s);
 assert.ok(projector.includes("referenceTaxId"));
 assert.ok(projector.includes("financial.BeforeDiscountRial"));
 assert.ok(projector.includes("Phase10CancellationWireFactory.Project"));
});
