import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {dirname,resolve} from "node:path";
import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");
const read=p=>readFileSync(resolve(root,p),"utf8");
test("step23 reference inquiry can only inspect Acknowledged dispatch with persisted reference",()=>{
 const api=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyReferenceInquiryService.cs");
 for(const required of ["d.state=5","receipt.receipt_kind=1","receipt.authority_reference IS NOT NULL",
   "ReferenceNumber=new[]{referenceNumber}","INQUIRY_BY_REFERENCE_NUMBER",
   "Phase10InquiryReconciliation.Evaluate","LegacyAuthorityInquiryReportParser.Parse",
   "AppendReferenceInquiryEvidenceAsync","LegacyAuthorityReportRepository(db).AppendAsync"])
  assert.ok(api.includes(required),required);
 assert.ok(!api.includes("normal-enqueue"));
 assert.ok(!api.includes("BeginSendAsync"));
});
test("step23 report is append-only and scoped to taxpayer and owner",()=>{
 const migration=read("src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261026110000_AddLegacyAuthorityInquiryReports.cs");
 const repo=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/LegacyAuthorityReportRepository.cs");
 for(const required of ["sandbox_authority_inquiry_reports","argintaxpayer_test",
    "sandbox_authority_reports_immutable","BEFORE UPDATE OR DELETE"])
  assert.ok(migration.includes(required),required);
 for(const required of ["d.customer_user_id=@owner","d.taxpayer_id=@taxpayer",
    "d.invoice_id=@invoice","d.state=5","r.authority_reference=@reference"])
  assert.ok(repo.includes(required),required);
});
test("step23 UI and BFF expose reports without treating inquiry as legal fiscal acceptance",()=>{
 const ui=read("apps/web/app/invoices/invoice-send-readiness-panel.tsx");
 const bff=read("apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/inquiry-by-reference/route.ts");
 const backend=read("src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs");
 assert.ok(ui.includes('dispatch?.status==="Acknowledged"'));
 assert.ok(ui.includes("inquireByReference"));
 assert.ok(ui.includes("authorityResult"));
 assert.ok(bff.includes("isSameLocalOrigin"));
 assert.ok(bff.includes("fiscalAccepted:false"));
 assert.ok(bff.includes("canPrepareCorrection"));
 assert.ok(backend.includes('group.MapGet("/inquiry-by-reference"'));
 assert.ok(backend.includes('group.MapPost("/inquiry-by-reference"'));
});
