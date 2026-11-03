import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";
import ts from "typescript";

const read=(path)=>readFileSync(new URL("../"+path,import.meta.url),"utf8");
const root=read("app/(console)/dashboard/dashboard-analytics.tsx");
const level1=read("app/(console)/dashboard/root-dashboard-details.tsx");
const level2=read("app/(console)/dashboard/partner-dashboard-details.tsx");
const css=read("app/(console)/dashboard/dashboard-design.css");
const proxy=read("app/api/dashboard/[section]/route.ts");
const backend=read("../../src/ArginTaxPayer.Api/Dashboard/Phase15DashboardReadService.cs");
const endpoint=read("../../src/ArginTaxPayer.Api/Dashboard/Phase15DashboardEndpoints.cs");

test("Level1-specific component and shared dashboard transpile",()=>{
 for(const source of [level1,root]){
   const result=ts.transpileModule(source,{fileName:"root-dashboard-details.tsx",compilerOptions:{
     target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.Preserve
   },reportDiagnostics:true});
   assert.deepEqual((result.diagnostics??[]).filter(x=>x.category===ts.DiagnosticCategory.Error)
     .map(x=>ts.flattenDiagnosticMessageText(x.messageText,"\n")),[]);
 }
});
test("Level1-only root widget stays out of Level2 and Customer views",()=>{
 assert.match(root,/role === "Level1" && <DashboardFold id="root-details"/);
 assert.match(root,/<RootDashboardDetails period=\{dateQuery\}/);
 assert.match(root,/role === "Level2" && \(/);
 assert.match(root,/import RootDashboardDetails/);
 assert.doesNotMatch(level2,/RootDashboardDetails|LifetimePartnerEarnedIrr/);
 assert.match(proxy,/section === "root" && !actor\.roles\.includes\("Level1"\)/);
 assert.match(endpoint,/"root"=>await service\.RootOverviewAsync/);
 assert.match(backend,/if \(!actor\.Level1 \|\| actor\.Role != "Level1"\)/);
});
test("root aggregates are scoped before every collection, allocation and settlement sum",()=>{
 assert.match(backend,/co\.root_level1_user_id=\{0\}/);
 assert.match(backend,/ord\.root_admin_user_id=\{0\}/);
 assert.match(backend,/a\.root_user_id=\{0\}/);
 assert.match(backend,/JOIN owned own ON own\.customer_user_id=ord\.customer_user_id/);
 assert.match(backend,/WHEN e\.kind=2 THEN -e\.amount_irr/);
 assert.match(backend,/LIMIT 10/);
 assert.match(backend,/COUNT\(DISTINCT partner_level2_user_id\)/);
});
test("root view uses only actual receipt and subscription values with separate time semantics",()=>{
 for(const name of ["activeCustomerCount","activeSubscriptionCount","expiringSubscriptionCount",
 "activePartnerCount","lifetimePartnerEarnedIrr","lifetimePartnerSettledIrr",
 "lifetimePartnerOutstandingIrr","verifiedCollectionsInPeriodIrr","partnerEarnedShareInPeriodIrr"])
   assert.ok(level1.includes("."+name),name);
 assert.match(level1,/مستقل از فیلتر تاریخ/);
 assert.match(level1,/بدون استفاده از مبالغ صورتحساب مالیاتی/);
 assert.match(level1,/در بازه انتخابی/);
 assert.doesNotMatch(level1,/mockData|demoData|Math\.random|method:"POST"/);
});
test("root aggregates are read-only, no-store, abort old periods and never include foreign filter",()=>{
 assert.match(level1,/fetch\("\/api\/dashboard\/root\?period="\+period/);
 assert.match(level1,/cache:"no-store"/);
 assert.match(level1,/controller\.abort\(\)/);
 assert.match(level1,/setCurrent\(\{state:"loading",data:null\}\)/);
 assert.match(proxy,/section === "root" && \(query\.has\("taxpayerId"\) \|\| query\.has\("partnerUserId"\)\)/);
 assert.match(level1,/aria-busy="true"/);
});
test("root and partner widgets remain compact on smaller screens",()=>{
 assert.ok(css.includes(".argin-dashboard__root-metrics"));
 assert.ok(css.includes(".argin-dashboard__root-panels"));
 assert.ok(css.includes(".argin-dashboard__root-partner-bar"));
 assert.ok(css.includes("max-width:1100px"));
 assert.ok(css.includes("max-width:580px"));
});
