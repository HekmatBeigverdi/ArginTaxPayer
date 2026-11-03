import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";
import ts from "typescript";
import React from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {createRequire} from "node:module";
import {loadTsx} from "./helpers/load-tsx.mjs";
const read=path=>readFileSync(new URL("../"+path,import.meta.url),"utf8");
const chart=read("app/(console)/dashboard/dashboard-financial-charts.tsx");
const root=read("app/(console)/dashboard/dashboard-analytics.tsx");
const style=read("app/(console)/dashboard/dashboard-design.css");
const backend=read("../../src/ArginTaxPayer.Api/Dashboard/Phase15DashboardReadService.cs");
const endpoint=read("../../src/ArginTaxPayer.Api/Dashboard/Phase15DashboardEndpoints.cs");

test("financial chart TSX compiles syntactically",()=>{
 const output=ts.transpileModule(chart,{fileName:"dashboard-financial-charts.tsx",compilerOptions:{
   target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.Preserve
 },reportDiagnostics:true});
 assert.deepEqual((output.diagnostics??[]).filter(x=>x.category===ts.DiagnosticCategory.Error)
  .map(x=>ts.flattenDiagnosticMessageText(x.messageText,"\n")),[]);
});
test("chart is admin-only and root share selector is Level1-only",async()=>{
 assert.match(root,/\{admin && <DashboardFold id="finance-details"/);
 assert.match(root,/<DashboardFinancialCharts/);
 const {NextRequest}=createRequire(import.meta.url)("next/server");
 for(const role of ["Customer","Level1","Level2"]){
  const calls=[];
  const route=loadTsx(new URL("../app/api/dashboard/[section]/route.ts",import.meta.url),{
   globals:{URL,AbortSignal,fetch:async(url,options)=>{
    calls.push({url:url.toString(),options});return Response.json({schemaVersion:1,role});
   }},
   modules:{"next/headers":{cookies:async()=>({get:()=>({value:"session-token"})})},
    "../../../../lib/identity":{ACCESS_COOKIE:"access",identityUiEnabled:()=>true,
     currentIdentity:async()=>({roles:[role]}),identityApiOrigin:()=>"http://backend"}}
  });
  const response=await route.GET(new NextRequest("http://localhost/api/dashboard/financial-trends?period=30d"),
   {params:Promise.resolve({section:"financial-trends"})});
  assert.equal(response.status,role==="Customer"?403:200,role);
  assert.equal(calls.length,role==="Customer"?0:1,role);
  if(role==="Customer")continue;
  assert.equal(calls[0].url,"http://backend/api/dev/dashboard/financial-trends?period=30d");
  assert.equal(calls[0].options.headers.Authorization,"Bearer session-token");

  const Component=loadTsx(new URL("../app/(console)/dashboard/dashboard-financial-charts.tsx",import.meta.url),{
   modules:{react:{...React,useEffect:()=>{},useState:initial=>[initial,()=>{}]}}
  }).default;
  const html=renderToStaticMarkup(React.createElement(Component,{role,period:"30d"}));
  assert.equal(html.includes("سهم مدیر اصلی"),role==="Level1",role);
  assert.ok(html.includes("وصول وجه"));
 }
 assert.match(endpoint,/"financial-trends"=>await service\.FinancialTrendsAsync/);
 assert.match(backend,/AuthorizeAsync\(bearer,true,ct\)/);
 assert.match(backend,/actor\.Level1\?x\.RootEarnedShareIrr:null/);
});
test("server scopes before grouping and uses distinct event clocks",()=>{
 for(const name of ["customer_commercial_owners","co.partner_level2_user_id={3}",
  "o.root_admin_user_id={3}","o.partner_user_id={3}","a.root_user_id={3}",
  "a.partner_user_id={3}","GROUP BY event_day","Asia/Tehran"])
  assert.ok(backend.includes(name),name);
 for(const clock of ["o.issued_at_utc","c.verified_at_utc","a.posted_at_utc","e.recorded_at_utc"])
  assert.ok(backend.includes(clock+" AT TIME ZONE 'Asia/Tehran'"),clock);
 assert.match(backend,/WHEN e.kind=1 THEN e.amount_irr ELSE -e.amount_irr/);
});
test("chart is read-only, loading independently with accessible table and no pretend money",()=>{
 assert.match(chart,/fetch\("\/api\/dashboard\/financial-trends\?period="\s*\+\s*period/);
 assert.match(chart,/cache:\s*"no-store"/);
 assert.match(chart,/controller\.abort\(\)/);
 assert.match(chart,/setData\(null\)/);
 assert.match(chart,/aria-busy="true"/);
 assert.match(chart,/aria-labelledby=\{titleId\}/);
 assert.match(chart,/<table className=/);
 assert.match(chart,/مانده فروش، وضعیت فعلی/);
 assert.doesNotMatch(chart,/Math\.random|mockData|demoData|method:"POST"/);
});
test("financial chart is responsive and honors active date filter",()=>{
 assert.match(style,/\.argin-dashboard__finance-tabs/);
 assert.match(style,/\.argin-dashboard__finance-graph/);
 assert.match(style,/max-width:580px/);
 assert.match(root,/period=\{dateQuery\}/);
});
