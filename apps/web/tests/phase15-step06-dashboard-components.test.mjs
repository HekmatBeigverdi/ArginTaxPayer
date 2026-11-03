import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import ts from "typescript";
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
test("components request real scoped APIs, no fake KPI numbers or write calls",()=>{
 for(const route of ["overview","trends","attention","finance"])assert.ok(client.includes('query("'+route+'"'));
 assert.match(client,/fetch\("\/api\/dashboard\/"\+section/);
 assert.match(client,/cache:"no-store"/);
 assert.match(client,/if\(admin\)tasks\.push\(query\("finance"/);
 assert.doesNotMatch(client,/method\s*:\s*"(?:POST|PUT|DELETE)"/);
 assert.doesNotMatch(client,/Math\.random|setInterval|demoData|mockData/i);
});
test("error state cannot falsely show previous period numbers",()=>{
 assert.match(client,/setState\("loading"\);setOverview\(null\);setTrend\(null\);setFinance\(null\);setAttention\(\[\]\)/);
 assert.match(client,/Promise\.all\(tasks\)/);
 assert.match(client,/setState\("error"\)/);
 assert.match(client,/status:"error",data:null/);
 assert.match(client,/if\\(admin\\)query\\("finance"/);
 assert.match(client,/setOverviewState\\(waiting\\(\\)\\)/);
 assert.match(client,/setTrendState\\(waiting\\(\\)\\)/);
 assert.match(client,/dashboard_unavailable/);
 assert.match(client,/controller\.abort\(\)/);
});
test("chart has textual accessible alternative and correct timeline semantics",()=>{
 assert.match(client,/role="img" aria-label=/);
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
