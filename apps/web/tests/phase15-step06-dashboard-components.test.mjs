import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";
import ts from "typescript";
const read=path=>readFileSync(new URL("../"+path,import.meta.url),"utf8");
const client=read("app/(console)/dashboard/dashboard-analytics.tsx");
const page=read("app/(console)/dashboard/page.tsx");
const css=read("app/(console)/dashboard/dashboard-design.css");

test("shared dashboard widgets transpile without TSX syntax errors",()=>{
 const compiled=ts.transpileModule(client,{fileName:"dashboard-analytics.tsx",compilerOptions:{
   target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.Preserve
 },reportDiagnostics:true});
 assert.deepEqual((compiled.diagnostics??[]).filter(x=>x.category===ts.DiagnosticCategory.Error)
   .map(x=>ts.flattenDiagnosticMessageText(x.messageText,"\n")),[]);
});
test("role-based dashboard is mounted without removing earlier feature shortcuts",()=>{
 assert.ok(page.includes('import DashboardAnalytics'));
 assert.ok(page.includes('import "./dashboard-design.css"'));
 assert.ok(page.includes("<DashboardAnalytics role="));
 assert.ok(page.includes("admin ? adminModules : customerModules"));
});
test("live dashboard uses read-only scoped endpoints, finance only for admins",()=>{
 for(const section of ["overview","trends","attention","finance"])
   assert.ok(client.includes('query("'+section+'"'));
 assert.ok(client.includes('if(admin)query("finance"'));
 assert.ok(client.includes('cache:"no-store"'));
 assert.ok(!client.includes('method:"POST"'));
 assert.ok(!client.includes("mockData"));
});
test("independent requests clear prior-period values and support errors",()=>{
 assert.ok(client.includes("setOverviewState(waiting())"));
 assert.ok(client.includes("setTrendState(waiting())"));
 assert.ok(client.includes("setAttentionState(waiting())"));
 assert.ok(client.includes("setFinanceState(waiting())"));
 assert.ok(!client.includes("Promise.all(tasks)"));
 assert.ok(client.includes('status:"error",data:null'));
 assert.ok(client.includes("controller.abort()"));
});
test("chart has textual alternative and accurate meaning",()=>{
 assert.ok(client.includes('role="img" aria-label='));
 assert.ok(client.includes("آخرین روزهای دارای فعالیت"));
 assert.ok(client.includes("تأییدها بر مبنای تاریخ صدور"));
 assert.ok(client.includes("status.unknown"));
 assert.ok(client.includes("DashboardPeriodFilter"));
});
test("dashboard stylesheet is scoped and responsive",()=>{
 assert.ok(css.includes(".argin-dashboard__composition"));
 assert.ok(css.includes(".argin-dashboard__quick"));
 assert.ok(css.includes("max-width:640px"));
 assert.ok(css.includes(":focus-visible"));
 assert.ok(css.includes("--argin-primary"));
});
