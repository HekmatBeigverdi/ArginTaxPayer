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
test("legal actions hidden for non-success and rejection offers correction draft",()=>{
 const preview=read("apps/web/app/invoices/invoice-formal-preview.tsx");
 const inquiry=read("apps/web/app/invoices/invoice-send-readiness-panel.tsx");
 assert.ok(preview.includes('if(authorityStatus!=="SUCCESS" || subject?.toLowerCase()==="cancellation")return null;'));
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
test('legal actions render only for exact authoritative SUCCESS on a non-cancellation invoice',()=>{
 const ts=require('typescript');const React=require('react');
 const {renderToStaticMarkup}=require('react-dom/server');
 const source=read('apps/web/app/invoices/invoice-formal-preview.tsx');
 const actions=source.slice(source.indexOf('function AuthorityActions('),source.indexOf('export default function InvoiceFormalPreview'));
 const exports={};
 runInNewContext(ts.transpileModule(actions+'\nexports.Actions=AuthorityActions;',{
  compilerOptions:{jsx:ts.JsxEmit.ReactJSX,module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}
 }).outputText,{exports,require,React,actionsStyle:{}});
 for(const authorityStatus of [undefined,'SUCCESS','Acknowledged','ACKNOWLEDGED','FAILED','PENDING','UNKNOWN','success']){
  for(const subject of ['Original','Cancellation','cancellation']){
   const html=renderToStaticMarkup(React.createElement(exports.Actions,{authorityStatus,subject,onCancel:()=>{}}));
   assert.equal(html.includes('>ابطال</button>'),authorityStatus==='SUCCESS'&&subject==='Original',String(authorityStatus)+' '+subject);
   if(authorityStatus==='SUCCESS'&&subject==='Original'){
    assert.match(html,/disabled=""[^>]*>اصلاح<\/button>/);
    assert.match(html,/disabled=""[^>]*>برگشت از فروش<\/button>/);
   }else assert.equal(html,'');
  }
 }
});
