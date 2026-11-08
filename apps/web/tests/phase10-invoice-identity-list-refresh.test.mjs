import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {dirname,resolve} from "node:path";
import {fileURLToPath} from "node:url";
import {createRequire} from "node:module";
import {runInNewContext} from "node:vm";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");
const read=p=>readFileSync(resolve(root,p),"utf8");
const require=createRequire(import.meta.url);
test("printed tax identity comes from reserved 22-character fiscal taxid, never UUID",()=>{
 const backend=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoicePrintPreviewService.cs");
 const preview=read("apps/web/app/invoices/invoice-formal-preview.tsx");
 assert.ok(backend.includes("FROM sandbox_invoice_fiscal_reservations"));
 assert.ok(backend.includes("uniqueInvoiceId=fiscalTaxId"));
 assert.ok(!backend.includes('uniqueInvoiceId=invoiceId.ToString("D")'));
 assert.ok(preview.includes("شناسه منحصربه‌فرد مالیاتی"));
 assert.ok(preview.includes("در انتظار تخصیص"));
});
test("invoice list wires the completion callback and refreshes when closing detail modal",()=>{
 const list=read("apps/web/app/invoices/workspace.tsx");
 assert.ok(list.includes("onCompleted={()=>void reload()}"));
 assert.ok(list.includes("setDetail(null);setAutoOpenDispatchFor(\"\");void reload();"));
 // Completion after acknowledgement and inquiry failure is exercised by phase10-dispatch-arm-contract.
});
test("return action targets the latest correctable chain node and excludes cancellation",()=>{
 const list=read("apps/web/app/invoices/workspace.tsx");
 const preview=read("apps/web/app/invoices/invoice-formal-preview.tsx");
 assert.ok(list.includes('["Original","Correction","SalesReturn"].includes(item.subject)'));
 assert.ok(list.includes('invoiceId={returnTarget.effectiveInvoiceId??returnTarget.id}'));
 assert.ok(preview.includes('["Original","Correction","SalesReturn"].includes(subject??"")'));
 assert.ok(preview.includes('const canPrepareSalesReturn=!!onSalesReturn'));
});
test("return preparation is available on sent nonterminal correction and return nodes",()=>{
 const ts=require("typescript");const React=require("react");
 const {renderToStaticMarkup}=require("react-dom/server");
 const source=read("apps/web/app/invoices/invoice-formal-preview.tsx");
 const actions=source.slice(source.indexOf("function AuthorityActions("),source.indexOf("export default function InvoiceFormalPreview"));
 const exports={};
 runInNewContext(ts.transpileModule(actions+"\nexports.Actions=AuthorityActions;",{
  compilerOptions:{jsx:ts.JsxEmit.ReactJSX,module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}
 }).outputText,{exports,require,React,actionsStyle:{}});
 for(const subject of ["Original","Correction","SalesReturn"]){
  for(const authorityStatus of ["ACKNOWLEDGED","SUCCESS","PENDING","UNKNOWN","FAILED","INDETERMINATE","SENDING"]){
   const props={subject,authorityStatus,onSalesReturn:()=>{},onCancel:()=>{},onCorrect:()=>{}};
   const html=renderToStaticMarkup(React.createElement(exports.Actions,props));
   assert.ok(html.includes(authorityStatus==="FAILED"&&subject==="SalesReturn"?"ویرایش و تلاش مجدد برگشت":"برگشت از فروش"),subject+" "+authorityStatus);
  }
 }
 const terminal={subject:"Cancellation",authorityStatus:"SUCCESS",onSalesReturn:()=>{},onCancel:()=>{},onCorrect:()=>{}};
 assert.ok(!renderToStaticMarkup(React.createElement(exports.Actions,terminal)).includes("برگشت از فروش"));
});
test("subject labels correspond to the four legal subject enums",()=>{
 const domain=read("src/ArginTaxPayer.Domain/Invoices/InvoiceSubjects.cs");
 const labels=read("apps/web/app/invoices/invoice-subject-badge.tsx");
 for(const name of ["Original","Correction","Cancellation","SalesReturn"]){
  assert.ok(domain.includes(name+" = "));
  assert.ok(labels.includes(name+':'));
 }
});
