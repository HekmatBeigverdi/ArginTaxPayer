import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";
import ts from "typescript";
const read=path=>readFileSync(new URL("../"+path,import.meta.url),"utf8");
const chart=read("app/(console)/dashboard/dashboard-financial-charts.tsx");
const root=read("app/(console)/dashboard/dashboard-analytics.tsx");
const style=read("app/(console)/dashboard/dashboard-design.css");
const proxy=read("app/api/dashboard/[section]/route.ts");
const backend=read("../../src/ArginTaxPayer.Api/Dashboard/Phase15DashboardReadService.cs");
const endpoint=read("../../src/ArginTaxPayer.Api/Dashboard/Phase15DashboardEndpoints.cs");

test("financial chart TSX compiles syntactically",()=>{
 const output=ts.transpileModule(chart,{fileName:"dashboard-financial-charts.tsx",compilerOptions:{
   target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.Preserve
 },reportDiagnostics:true});
 assert.deepEqual((output.diagnostics??[]).filter(x=>x.category===ts.DiagnosticCategory.Error)
  .map(x=>ts.flattenDiagnosticMessageText(x.messageText,"\n")),[]);
});
test("chart is admin-only and root share selector is Level1-only",()=>{
 assert.match(root,/\{admin && <DashboardFinancialCharts/);
 assert.match(chart,/role==="Level1"\?/);
 assert.match(chart,/rootEarnedShareIrr/);
 assert.match(proxy,/section === "financial-trends"\) && !administrator/);
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
 assert.match(chart,/fetch\("\/api\/dashboard\/financial-trends\?period="\+period/);
 assert.match(chart,/cache:"no-store"/);
 assert.match(chart,/controller\.abort\(\)/);
 assert.match(chart,/setData\(null\)/);
 assert.match(chart,/aria-busy="true"/);
 assert.match(chart,/aria-labelledby=\{id\}/);
 assert.match(chart,/<table className=/);
 assert.match(chart,/مانده فروش، وضعیت فعلی/);
 assert.doesNotMatch(chart,/Math\.random|mockData|demoData|method:"POST"/);
});
test("financial chart is responsive and honors active date filter",()=>{
 assert.match(style,/\.argin-dashboard__finance-tabs/);
 assert.match(style,/\.argin-dashboard__finance-graph/);
 assert.match(style,/max-width:580px/);
 assert.match(root,/period=\{period\}/);
});
