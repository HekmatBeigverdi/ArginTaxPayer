import test from "node:test";import assert from "node:assert/strict";import {readFileSync} from "node:fs";import {resolve,dirname} from "node:path";import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");const read=p=>readFileSync(resolve(root,p),"utf8");
test("one-open-at-a-time accordion opens formal invoice by default",()=>{
 const ui=read("apps/web/app/invoices/workspace.tsx");
 assert.equal((ui.match(/name="invoice-detail-sections"/g)||[]).length,4);
 assert.ok(ui.includes('name="invoice-detail-sections" className="fiscal-section invoice-detail-section" open'));
 assert.ok(ui.includes('hideHistory={true}'));
});
test("successful invoice has inquiry without redundant preparation and yellow banner",()=>{
 const p=read("apps/web/app/invoices/invoice-send-readiness-panel.tsx");
 assert.ok(p.includes('dispatch?.status!=="Acknowledged"&&<div'));
 assert.ok(p.includes('dispatch?.status!=="Acknowledged"&&<button'));
 assert.ok(!p.includes('invoice-validation__warning'));
});
test("edit and cancellation need success while return preparation follows a sent chain node",()=>{
 const preview=read("apps/web/app/invoices/invoice-formal-preview.tsx");
 const inquiry=read("apps/web/app/invoices/invoice-send-readiness-panel.tsx");
 assert.ok(preview.includes('const canEdit=authorityStatus==="SUCCESS"'));
 assert.ok(preview.includes('const canPrepareSalesReturn=!!onSalesReturn'));
 assert.ok(inquiry.includes('const canPrepareRejectedDraft=authorityResult?.status==="FAILED"'));
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

test("invoice accordion visually matches buyer seller dossier",()=>{
 const ui=read("apps/web/app/invoices/workspace.tsx");
 const css=read("apps/web/app/globals.css");
 assert.equal((ui.match(/className="fiscal-section invoice-detail-section"/g)||[]).length,4);
 assert.equal((ui.match(/className="fiscal-section-content invoice-detail-section__content"/g)||[]).length,4);
 for(const token of [".fiscal-section > summary",".invoice-detail-section > summary","background:#f0f7f6","border-radius:8px"])
  assert.ok(css.includes(token),token);
});

import {createRequire} from 'node:module';
import {runInNewContext} from 'node:vm';
const require=createRequire(import.meta.url);
test('return action remains available on sent nonterminal nodes; cancellation stays terminal',()=>{
 const ts=require('typescript');const React=require('react');
 const {renderToStaticMarkup}=require('react-dom/server');
 const source=read('apps/web/app/invoices/invoice-formal-preview.tsx');
 const actions=source.slice(source.indexOf('function AuthorityActions('),source.indexOf('export default function InvoiceFormalPreview'));
 const exports={};
 runInNewContext(ts.transpileModule(actions+'\nexports.Actions=AuthorityActions;',{
  compilerOptions:{jsx:ts.JsxEmit.ReactJSX,module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}
 }).outputText,{exports,require,React,actionsStyle:{}});
 const returnStatuses=['ACKNOWLEDGED','SUCCESS','FAILED','PENDING','UNKNOWN','INDETERMINATE','SENDING'];
 for(const authorityStatus of [undefined,'SUCCESS','Acknowledged','ACKNOWLEDGED','FAILED','PENDING','UNKNOWN','INDETERMINATE','SENDING','success']){
  for(const subject of ['Original','Correction','SalesReturn','Cancellation']){
   const returnEligible=returnStatuses.includes(authorityStatus)&&['Original','Correction','SalesReturn'].includes(subject);
   const editEligible=authorityStatus==='SUCCESS'&&subject!=='Cancellation';
   const props={authorityStatus,subject,onSalesReturn:()=>{},onCancel:()=>{},onCorrect:()=>{}};
   const html=renderToStaticMarkup(React.createElement(exports.Actions,props));
   assert.equal(html.includes('>برگشت از فروش</button>'),returnEligible,String(authorityStatus)+' '+subject);
   assert.equal(html.includes('>ابطال</button>'),editEligible,String(authorityStatus)+' '+subject);
   assert.equal(html.includes('>اصلاح</button>'),editEligible,String(authorityStatus)+' '+subject);
  }
 }
});
