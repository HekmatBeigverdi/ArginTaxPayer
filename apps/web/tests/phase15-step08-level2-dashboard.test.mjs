import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";
import ts from "typescript";

const read=(path)=>readFileSync(new URL("../"+path,import.meta.url),"utf8");
const root=read("app/(console)/dashboard/dashboard-analytics.tsx");
const partner=read("app/(console)/dashboard/partner-dashboard-details.tsx");
const css=read("app/(console)/dashboard/dashboard-design.css");
const proxy=read("app/api/dashboard/[section]/route.ts");
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

test("partner endpoint denies role misuse at both server and proxy",()=>{
 assert.match(endpoint,/"partner"=>await service\.PartnerOverviewAsync/);
 assert.match(proxy,/"recent", "partner"/);
 assert.match(proxy,/section === "partner" && \(actor\.roles\.includes\("Level1"\)/);
 assert.match(proxy,/!actor\.roles\.includes\("Level2"\)/);
 assert.ok(proxy.includes('section === "partner" && query.size > 0'));
 assert.match(proxy,/section === "partner"\)\) return unavailable\(400/);
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
