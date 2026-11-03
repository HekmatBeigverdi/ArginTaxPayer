import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";
import ts from "typescript";
import {createRequire} from "node:module";
import {loadTsx} from "./helpers/load-tsx.mjs";

const read=(path)=>readFileSync(new URL("../"+path,import.meta.url),"utf8");
const root=read("app/(console)/dashboard/dashboard-analytics.tsx");
const partner=read("app/(console)/dashboard/partner-dashboard-details.tsx");
const css=read("app/(console)/dashboard/dashboard-design.css");
const backend=read("../../src/ArginTaxPayer.Api/Dashboard/Phase15DashboardReadService.cs");
const endpoint=read("../../src/ArginTaxPayer.Api/Dashboard/Phase15DashboardEndpoints.cs");

test("Level2 UI and dashboard TSX transpile cleanly",()=>{
 for(const source of [partner,root]){
   const result=ts.transpileModule(source,{fileName:"dashboard.tsx",compilerOptions:{
     target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.Preserve
   },reportDiagnostics:true});
   assert.deepEqual((result.diagnostics??[]).filter(x=>x.category===ts.DiagnosticCategory.Error)
     .map(x=>ts.flattenDiagnosticMessageText(x.messageText,"\n")),[]);
 }
});

test("partner analytics is mounted only for Level2, never Level1 or customer",()=>{
 assert.match(root,/role === "Level2" && \(/);
 assert.match(root,/<PartnerDashboardDetails/);
 assert.doesNotMatch(partner,/rootShare|RootEarnedShare|ApprovedCostIrr/);
 assert.match(partner,/role!=="Level2"/);
 assert.match(partner,/cache:"no-store"/);
 assert.match(partner,/controller\.abort\(\)/);
 assert.match(partner,/setData\(null\)/);
});

test("partner only backend DTO has no privileged root share or unrelated totals",()=>{
 const dto=backend.split("public sealed record PartnerOverview(")[1]?.split(");")[0];
 assert.ok(dto,"PartnerOverview record exists");
 assert.doesNotMatch(dto,/RootShare|RootEarned|ApprovedCost|RootUserId/);
 assert.match(dto,/ActiveCustomerCount/);
 assert.match(dto,/ExpiringSubscriptionCount/);
 assert.match(dto,/LifetimePartnerOutstandingIrr/);
 assert.match(backend,/actor\.Role != "Level2"/);
 assert.match(backend,/o\.partner_level2_user_id=\{0\}/);
 assert.match(backend,/a\.partner_user_id=\{0\}/);
 assert.match(backend,/JOIN owned own ON own\.customer_user_id=ord\.customer_user_id/);
 assert.match(backend,/WHEN e\.kind=2 THEN -e\.amount_irr/);
});

function partnerProxy(roles) {
 const calls=[];
 const route=loadTsx(new URL("../app/api/dashboard/[section]/route.ts",import.meta.url),{
  globals:{URL,AbortSignal,fetch:async(url,options)=>{
   calls.push({url:url.toString(),options});
   return Response.json({schemaVersion:1,role:"Level2"});
  }},
  modules:{
   "next/headers":{cookies:async()=>({get:()=>({value:"session-token"})})},
   "../../../../lib/identity":{ACCESS_COOKIE:"access",identityUiEnabled:()=>true,
    currentIdentity:async()=>({roles}),identityApiOrigin:()=>"http://backend"}
  }
 });
 const {NextRequest}=createRequire(import.meta.url)("next/server");
 return {calls,get(query=""){
  return route.GET(new NextRequest("http://localhost/api/dashboard/partner"+query),
   {params:Promise.resolve({section:"partner"})});
 }};
}

test("partner endpoint denies role misuse at both server and proxy",async()=>{
 assert.match(endpoint,/"partner"=>await service\.PartnerOverviewAsync/);
 assert.match(backend,/actor\.Role != "Level2"/);

 for(const roles of [[],["Customer"],["Level1"],["Level1","Level2"],["Level2","Level1"]]){
  const proxy=partnerProxy(roles);
  const response=await proxy.get();
  assert.equal(response.status,403,JSON.stringify(roles));
  assert.equal(proxy.calls.length,0,"unauthorized roles never reach the backend");
 }

 const proxy=partnerProxy(["Level2"]);
 for(const query of ["?partnerUserId=another-partner","?taxpayerId=another-taxpayer",
  "?period=30d","?from=2026-11-01","?to=2026-11-03","?period=",
  "?partnerUserId=","?period=7d&period=30d","?role=Level1"]){
  const response=await proxy.get(query);
  assert.equal(response.status,400,query);
  assert.equal((await response.json()).error,"invalid_filter",query);
 }
 assert.equal(proxy.calls.length,0,"filtered snapshots never reach the backend");

 const response=await proxy.get();
 assert.equal(response.status,200);
 assert.equal(response.headers.get("cache-control"),"private, no-store");
 assert.equal(proxy.calls.length,1);
 assert.equal(proxy.calls[0].url,"http://backend/api/dev/dashboard/partner");
 assert.equal(proxy.calls[0].options.headers.Authorization,"Bearer session-token");
});

test("UI uses real numbers and excludes fabricated profit",()=>{
 for(const field of ["activeCustomerCount","activeSubscriptionCount","expiringSubscriptionCount",
   "lifetimePartnerEarnedIrr","lifetimePartnerSettledIrr","lifetimePartnerOutstandingIrr",
   "collectionsInPeriodIrr","partnerSettlementNetIrr"])
   assert.ok(partner.includes("."+field),field);
 assert.match(partner,/سود و زیان حسابداری نیست/);
 assert.match(partner,/۳۰ روز آینده/);
 assert.match(partner,/financeState==="loading"/);
 assert.match(partner,/aria-busy="true"/);
 assert.doesNotMatch(partner,/Math\.random|demoData|mockData|method:"POST"/);
});

test("partner view is responsive and does not stretch side panels",()=>{
 assert.ok(css.includes(".argin-dashboard__partner-grid"));
 assert.ok(css.includes(".argin-dashboard__partner-finance"));
 assert.ok(css.includes("max-width:950px"));
 assert.ok(css.includes("max-width:580px"));
 assert.ok(css.includes("align-items:start"));
});
