import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import ts from "typescript";
import React from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {loadTsx} from "./helpers/load-tsx.mjs";
const read=path=>readFileSync(new URL("../"+path,import.meta.url),"utf8");
const client=read("app/(console)/dashboard/dashboard-analytics.tsx");
const page=read("app/(console)/dashboard/page.tsx");
const css=read("app/(console)/dashboard/dashboard-design.css");

test("all shared Phase15 widgets compile as TSX without syntax diagnostics",()=>{
 const output=ts.transpileModule(client,{fileName:"dashboard-analytics.tsx",compilerOptions:{
   target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.Preserve
 },reportDiagnostics:true});
 assert.deepEqual((output.diagnostics||[]).filter(x=>x.category===ts.DiagnosticCategory.Error)
   .map(x=>ts.flattenDiagnosticMessageText(x.messageText,"\n")),[]);
});
test("dashboard page visibly mounts role-scoped UI and its styles",()=>{
 assert.match(page,/import DashboardAnalytics/);
 assert.match(page,/import "\.\/dashboard-design\.css"/);
 assert.match(page,/<DashboardAnalytics role=\{/);
 assert.match(page,/level1 \? "Level1" : level2 \? "Level2" : "Customer"/);
 assert.match(page,/admin \? adminModules : customerModules/);
});
function dashboard(role,fetch) {
 const state=[],effects=[];
 let slot=0;
 const Component=loadTsx(new URL("../app/(console)/dashboard/dashboard-analytics.tsx",import.meta.url),{
  globals:{fetch,AbortController},modules:{
   "next/link":{default:({href,children,...props})=>React.createElement("a",{href,...props},children)},
   react:{...React,useMemo:fn=>fn(),useEffect:fn=>effects.push(fn),useState(initial){
    const index=slot++;
    if(!(index in state))state[index]=typeof initial==="function"?initial():initial;
    return [state[index],value=>{state[index]=typeof value==="function"?value(state[index]):value;}];
   }}
  }
 }).default;
 return {state,effects,tree(){slot=0;return Component({role});},
  render(){return renderToStaticMarkup(this.tree());}};
}
const overview=role=>({schemaVersion:1,role,asOfUtc:"2026-11-03T10:00:00Z",invoiceDrafts:2,
 invoiceStatus:{logicalInvoiceCount:5,accepted:3,acceptedCancellation:0,rejected:1,processing:0,
 awaitingInquiry:0,unknown:1,prepared:0,attention:0,unclassified:0,successRatePercent:60},subscription:null});
const finance=role=>({schemaVersion:1,role,currency:"IRR",salesGrossIrr:9000,
 collectionsInPeriodIrr:3000,salesOutstandingIrr:6000,rootEarnedShareIrr:2000,partnerEarnedShareIrr:1000});
const flush=()=>new Promise(resolve=>setImmediate(resolve));

test("dashboard requests scoped read-only APIs and renders role-specific KPIs",async()=>{
 for(const role of ["Customer","Level1","Level2"]){
  const calls=[];
  const view=dashboard(role,async(url,options)=>{
   calls.push({url,options});
   const section=url.split("/").at(-1).split("?")[0];
   return Response.json({overview:overview(role),trends:{schemaVersion:1,buckets:[]},attention:{items:[]},finance:finance(role)}[section]);
  });
  view.render();view.effects[0]();await flush();
  assert.deepEqual(calls.map(call=>call.url),["overview","trends","attention",...(role==="Customer"?[]:["finance"])]
   .map(section=>`/api/dashboard/${section}?period=30d`));
  assert.ok(calls.every(call=>call.options.cache==="no-store"&&!call.options.method));
  const html=view.render();
  assert.ok(html.includes(role==="Customer"?"پیش‌نویس‌های فعال":"فروش اشتراک"));
  if(role!=="Customer")assert.ok(html.includes(role==="Level1"?"سهم مدیر سطح ۱":"سهم همکاری شما"));
 }
});

test("changing period clears old data, failure hides KPIs, and cleanup aborts requests",async()=>{
 let fail=false;const calls=[];
 const view=dashboard("Customer",async(url,options)=>{
  calls.push({url,...options});
  if(fail)return Response.json({},{status:503});
  return Response.json(url.includes("overview")?overview("Customer"):url.includes("trends")?{schemaVersion:1,buckets:[]}:{items:[]});
 });
 view.render();const previousCleanup=view.effects[0]();await flush();
 assert.ok(view.render().includes('aria-label="شاخص‌های کلیدی"'));
 const filter=view.tree().props.children[0].props.children[1];
 filter.props.setPeriod("7d");
 previousCleanup();
 view.render();
 fail=true;
 const cleanup=view.effects.at(-1)();
 assert.equal(view.state[0],"7d");
 assert.ok(calls.at(-1).url.endsWith("?period=7d"));
 for(const section of view.state.slice(2,6)){
  assert.equal(section.status,"loading");assert.equal(section.data,null);
 }
 await flush();
 const html=view.render();
 assert.ok(html.includes('role="alert"'));
 assert.ok(!html.includes('class="argin-dashboard__kpi-value"'));
 cleanup();assert.equal(calls.at(-1).signal.aborted,true);
});
test("chart has textual accessible alternative and correct timeline semantics",()=>{
 assert.match(client,/role="img"\s+aria-label=/);
 assert.match(client,/آخرین روزهای دارای فعالیت/);
 assert.match(client,/تأییدها بر مبنای تاریخ صدور/);
 assert.match(client,/status\.unknown/);
 assert.match(client,/DashboardPeriodFilter/);
});
test("scoped CSS styles are actually integrated and responsive",()=>{
 assert.match(css,/\.argin-dashboard__composition/);
 assert.match(css,/\.argin-dashboard__quick/);
 assert.match(css,/max-width:640px/);
 assert.match(css,/:focus-visible/);
 assert.match(css,/--argin-primary/);
});

test("slow charts and failed attention do not block role-specific KPI cards or quick actions",async()=>{
 for(const role of ["Customer","Level1","Level2"]){
  const view=dashboard(role,async url=>{
   if(url.includes("trends")||(role!=="Customer"&&url.includes("overview")))
    return new Promise(()=>{});
   if(url.includes("attention"))return Response.json({},{status:503});
   return Response.json(url.includes("overview")?overview(role):finance(role));
  });
  view.render();const cleanup=view.effects[0]();await flush();
  const html=view.render();
  assert.ok(html.includes('class="argin-dashboard__kpi-value"'),role);
  assert.ok(html.includes('class="argin-dashboard__skeleton"'),role);
  assert.ok(html.includes('role="alert"'),role);
  assert.ok(html.includes('aria-label="اقدامات پرکاربرد"'),role);
  cleanup();
 }
});
