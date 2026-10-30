import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {resolve,dirname} from "node:path";
import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");
const read=p=>readFileSync(resolve(root,p),"utf8");
test("correction draft has independent immutable successor and scoped revision controls",()=>{
 const x=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10SandboxCorrectionService.cs");
 for(const term of ["scope.ScopedQuery","ExpectedRevision","revision=revision+1",
 "Phase10CorrectionValidation.AllowedLines","InvoiceDocumentRevisions.Add",
 "InvoiceSubject.Correction","sandbox_invoice_legal_relations","sender.SendAsync"])
 assert.ok(x.includes(term),term);
});
test("V7.9 correction mapper emits legal subject with fiscal predecessor",()=>{
 const x=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyFrozenInvoiceProjector.cs");
 assert.ok(x.includes("InvoiceSubject.Correction"));
 assert.ok(x.includes('root.GetProperty("referenceTaxId")'));
 const mapper=read("src/ArginTaxPayer.Domain/Invoices/Phase10InvoiceWireMapper.cs");
 assert.ok(mapper.includes("irtaxid = source.ReferenceTaxId"));
});
test("correction BFF validates same-origin, one backend call, and scopes tenant",()=>{
 for(const action of ["correction-draft","correction-send-and-inquire"]){
  const x=read("apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/"+action+"/route.ts");
  assert.ok(x.includes("isSameLocalOrigin"));assert.ok(x.includes("ACCESS_COOKIE"));
  assert.equal((x.match(/await fetch\(/g)||[]).length,1);
  assert.ok(x.includes("taxpayerId")&&x.includes("invoiceId"));
 }
});
test("UI has save, abandon, resume, and locked historical VAT",()=>{
 const form=read("apps/web/app/invoices/invoice-correction-modal.tsx");
 const list=read("apps/web/app/invoices/workspace.tsx");
 assert.ok(form.includes("ذخیره پیش‌نویس"));
 assert.ok(form.includes("انصراف از اصلاح"));
 assert.ok(form.includes("در حال صدور و ارسال"));
 assert.ok(form.includes("line.vatRatePercent"));
 assert.ok(list.includes("ادامه ویرایش"));
 assert.ok(list.includes("correctionInProgress"));
});
