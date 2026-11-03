import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";
import ts from "typescript";
import React from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {loadTsx} from "./helpers/load-tsx.mjs";
const read=p=>readFileSync(new URL("../"+p,import.meta.url),"utf8");
const root=read("app/(console)/dashboard/dashboard-analytics.tsx");
const center=read("app/(console)/dashboard/dashboard-action-center.tsx");
const css=read("app/(console)/dashboard/dashboard-design.css");

test("new action center transpiles and has a visible initial header",()=>{
  const r=ts.transpileModule(center,{fileName:"dashboard-action-center.tsx",compilerOptions:{
    target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.Preserve
  },reportDiagnostics:true});
  assert.deepEqual((r.diagnostics??[]).filter(x=>x.category===ts.DiagnosticCategory.Error)
    .map(x=>ts.flattenDiagnosticMessageText(x.messageText,"\n")),[]);
  const Component=loadTsx(new URL("../app/(console)/dashboard/dashboard-action-center.tsx",import.meta.url),{
    modules:{"next/link":{default:({href,children})=>React.createElement("a",{href},children)}}
  }).default;
  const html=renderToStaticMarkup(React.createElement(Component,{
    role:"Customer",attentionStatus:"loading",attention:[],onRetry:()=>{}
  }));
  assert.ok(html.includes("مرکز پیگیری و اقدامات ضروری"));
  assert.ok(html.includes("در حال بررسی"));
  assert.ok(html.includes("در حال بررسی موارد مهم"));
});

test("attention is ordered by priority and links only to authenticated existing routes",()=>{
  assert.match(center,/prio\(a\.status\)-prio\(b\.status\)/);
  assert.match(center,/sort\(\(a,b\)=>\(a\.priority==="urgent"/);
  assert.match(center,/tasks\.slice\(0,5\)/);
  assert.ok(center.includes('href:"/invoices"'));
  assert.ok(center.includes('href:"/subscriptions"'));
  assert.ok(!center.includes("fetch(item.link)"));
  assert.doesNotMatch(center,/method:\s*"POST"|inquire|resend|autoRenew/i);
});

test("subscription watch is queried for admins only, with no leaked response on failure",()=>{
  assert.ok(root.includes("if(!admin||!watchEnabled)return"));
  assert.ok(root.includes('fetch("/api/dashboard/subscriptions-watch"'));
  assert.ok(root.includes('cache:"no-store"'));
  assert.ok(root.includes("controller.abort()"));
  assert.ok(root.includes("setWatchData(null)"));
  assert.ok(root.includes('value.role!==role'));
  assert.ok(center.includes('watchState==="error"'));
  assert.ok(center.includes("تعداد موارد ممکن است کامل نباشد"));
});

test("heavy reports are collapsed by default and independently expandable",()=>{
  assert.ok(root.includes('function DashboardFold('));
  assert.ok(root.includes('const [open,setOpen]=useState(false)'));
  assert.ok(root.includes('aria-expanded={open}'));
  assert.ok(root.includes('aria-controls={id}'));
  assert.ok(root.includes('{open ? children : null}'));
  assert.ok(root.includes('<DashboardActionCenter role={role}'));
  for(const id of ["finance-details","subscription-details","root-details","partner-details","invoice-details"])
    assert.ok(root.includes('id="'+id+'"'),id);
  assert.ok(css.includes(".argin-dashboard__fold-trigger"));
  assert.ok(css.includes(".argin-dashboard__action-center"));
  assert.ok(css.includes("prefers-reduced-motion:reduce"));
});
