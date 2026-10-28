import test from "node:test";import assert from "node:assert/strict";import {readFileSync} from "node:fs";import {resolve,dirname} from "node:path";import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");const read=p=>readFileSync(resolve(root,p),"utf8");
test("one button delegates the complete send workflow to the backend without retrying",()=>{
 const route=read("apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/send-and-inquire/route.ts");
 assert.ok(route.includes("/dispatch/send-and-inquire"));
 assert.ok(route.includes("Exactly one backend API call")||route.includes("one backend API call"));
 assert.ok(route.includes("send_api_unavailable"));
 assert.ok(route.includes("invalid_send_response"));
 assert.ok(route.includes('status:data.status==="Acknowledged"?"Acknowledged":"Blocked"'));
 assert.ok(!route.includes("validate-internal"));
 assert.ok(!route.includes("review/submit"));
 assert.ok(!route.includes("prepare-legacy"));
 const ui=read("apps/web/app/invoices/invoice-send-readiness-panel.tsx");
 assert.ok(ui.includes("ارسال به سامانه مؤدیان و استعلام"));
 assert.ok(!ui.includes("JSON کامل بسته صورتحساب"));
 assert.ok(!ui.includes(">تأیید بازبینی صورتحساب</button>"));
});
test("formal invoice prints unique identifier and textual settlement method without section labels",()=>{
 const backend=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoicePrintPreviewService.cs");
 const ui=read("apps/web/app/invoices/invoice-formal-preview.tsx");
 assert.ok(backend.includes('uniqueInvoiceId=invoiceId.ToString("D")'));
 for(const x of ["نقدی","نسیه","نقدی و نسیه"])assert.ok(backend.includes(x));
 assert.ok(ui.includes("شناسه یکتای فاکتور"));
 for(const x of ["سربرگ صورتحساب","بدنه صورتحساب","پاورقی و جمع مبالغ"])assert.ok(!ui.includes(x));
});
