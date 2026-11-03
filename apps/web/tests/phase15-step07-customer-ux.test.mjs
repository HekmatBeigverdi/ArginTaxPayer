import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";
import ts from "typescript";

const read=(path)=>readFileSync(new URL("../"+path,import.meta.url),"utf8");
const client=read("app/(console)/dashboard/dashboard-analytics.tsx");
const css=read("app/(console)/dashboard/dashboard-design.css");
const page=read("app/(console)/dashboard/page.tsx");

test("Step07 TSX transpiles and visible dashboard remains mounted",()=>{
  const compiled=ts.transpileModule(client,{fileName:"dashboard-analytics.tsx",compilerOptions:{
    target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.Preserve
  },reportDiagnostics:true});
  assert.deepEqual((compiled.diagnostics??[]).filter(x=>x.category===ts.DiagnosticCategory.Error)
    .map(x=>ts.flattenDiagnosticMessageText(x.messageText,"\n")),[]);
  assert.ok(page.includes("<DashboardAnalytics role="));
  assert.ok(page.includes("./dashboard-design.css"));
});

test("independent requests settle separately and abort obsolete periods",()=>{
  for(const section of ["overview","trends","attention","finance"]) {
    assert.ok(client.includes('query("'+section+'"'));
  }
  assert.ok(client.includes('if(admin)query("finance"'));
  assert.ok(client.includes("controller.abort()"));
  assert.ok(client.includes("if(!controller.signal.aborted)"));
  assert.ok(!client.includes("Promise.all(tasks)"));
  for(const state of ["OverviewState","TrendState","AttentionState","FinanceState"]) {
    assert.ok(client.includes("set"+state+"(waiting())"),state);
    assert.ok(client.includes("set"+state+'({status:"error",data:null})'),state);
  }
  assert.ok(client.includes('cache:"no-store"'));
});

test("dashboard stays structurally visible during loading with SVG waiting per region",()=>{
  assert.ok(client.includes("function DashboardSkeleton"));
  assert.ok(client.includes("function DashboardKpiSkeleton"));
  assert.ok(client.includes('variant="chart"'));
  assert.ok(client.includes('variant="list"'));
  assert.ok(client.includes('aria-busy="true"'));
  assert.ok(client.includes("useId()"));
  assert.ok(client.includes('role="status"'));
  assert.ok(client.includes("DashboardPeriodFilter period={period}"));
  assert.ok(!client.includes('state!=="ready"?<DashboardState'));
});

test("quick actions render immediately and remain content height",()=>{
  assert.ok(client.includes('title="دسترسی سریع"'));
  assert.ok(client.includes('className="argin-dashboard__side"'));
  assert.ok(css.includes(".argin-dashboard__side>.argin-dashboard__card {height:auto;min-height:0}"));
  assert.ok(css.includes("align-items:start"));
  assert.ok(css.includes("align-self:start"));
  assert.ok(client.includes('href="/invoices"'));
  assert.ok(client.includes('href="/parties"'));
});

test("attention is bounded to five, preserves safe invoice links and show-more",()=>{
  assert.ok(client.includes("attentionState.data?.slice(0,5)"));
  assert.ok(client.includes("مشاهده همه صورتحساب‌ها"));
  assert.ok(client.includes('href="/invoices"'));
  assert.ok(!client.includes("Math.random"));
  assert.ok(!client.includes('method:"POST"'));
});

test("reduced motion and live error handling preserve accessibility",()=>{
  assert.ok(css.includes("@keyframes argin-dashboard-skeleton"));
  assert.ok(css.includes("prefers-reduced-motion:reduce"));
  assert.ok(client.includes("DashboardState kind=\"error\""));
  assert.ok(client.includes("aria-live=\"polite\""));
  assert.ok(client.includes('aria-label="در حال دریافت اطلاعات از سرور"'));
  assert.ok(client.includes('status:"error",data:null'));
});
