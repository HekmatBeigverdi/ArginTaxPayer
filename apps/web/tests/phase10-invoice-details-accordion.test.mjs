import test from "node:test";import assert from "node:assert/strict";import {readFileSync} from "node:fs";import {resolve,dirname} from "node:path";import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");const read=p=>readFileSync(resolve(root,p),"utf8");
test("one-open-at-a-time accordion opens formal invoice by default",()=>{
 const ui=read("apps/web/app/invoices/workspace.tsx");
 assert.equal((ui.match(/name="invoice-detail-sections"/g)||[]).length,4);
 assert.ok(ui.includes('name="invoice-detail-sections" open'));
 assert.ok(ui.includes('hideHistory={true}'));
});
test("successful invoice has inquiry without redundant preparation and yellow banner",()=>{
 const p=read("apps/web/app/invoices/invoice-send-readiness-panel.tsx");
 assert.ok(p.includes('dispatch?.status!=="Acknowledged"&&<div'));
 assert.ok(p.includes('dispatch?.status!=="Acknowledged"&&<button'));
 assert.ok(!p.includes('invoice-validation__warning'));
});
test("legal actions hidden for non-success and rejection offers correction draft",()=>{
 const preview=read("apps/web/app/invoices/invoice-formal-preview.tsx");
 const inquiry=read("apps/web/app/invoices/invoice-send-readiness-panel.tsx");
 assert.ok(preview.includes('authorityStatus==="SUCCESS"&&<div'));
 assert.ok(inquiry.includes('authorityResult.status==="FAILED"&&authorityResult.canPrepareCorrection'));
 assert.ok(inquiry.includes("بازگشت به پیش‌نویس اصلاحی"));
});
test("history section has neutral treatment and no nested accordion",()=>{
 const history=read("apps/web/app/invoices/invoice-dispatch-history.tsx");
 assert.ok(history.includes('background:"transparent"'));
 assert.ok(!history.includes('className="invoice-calculation-audit"'));
 assert.ok(!history.includes("<summary>تاریخچه ارسال"));
 assert.ok(history.includes('aria-label="جزئیات تاریخچه ارسال"'));
 assert.ok(history.includes('maxHeight:230'));
 assert.ok(history.includes('className="console-table"'));
 assert.ok(!history.includes("return <details"));
});
