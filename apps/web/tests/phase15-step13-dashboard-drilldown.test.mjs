import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";
import ts from "typescript";

const read=p=>readFileSync(new URL("../"+p,import.meta.url),"utf8");
const dashboard=read("app/(console)/dashboard/dashboard-analytics.tsx");
const css=read("app/(console)/dashboard/dashboard-design.css");
const finance=read("app/(console)/dashboard/dashboard-financial-charts.tsx");
const root=read("app/(console)/dashboard/root-dashboard-details.tsx");
const proxy=read("app/api/dashboard/[section]/route.ts");
const api=read("../../src/ArginTaxPayer.Api/Dashboard/Phase15DashboardReadService.cs");

test("Step13 dashboard and dependent chart components transpile without syntax diagnostics",()=>{
 for(const source of [dashboard,finance,root]){
  const result=ts.transpileModule(source,{fileName:"dashboard.tsx",compilerOptions:{
   module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.Preserve,target:ts.ScriptTarget.ES2022
  },reportDiagnostics:true});
  assert.deepEqual((result.diagnostics??[]).filter(d=>d.category===ts.DiagnosticCategory.Error)
   .map(d=>ts.flattenDiagnosticMessageText(d.messageText,"\n")),[]);
 }
});

test("KPI selection is a real keyboard-operable button and uses only scoped totals",()=>{
 assert.ok(dashboard.includes("function DashboardKpiDetails"));
 assert.ok(dashboard.includes('aria-expanded={Boolean(selected)}'));
 assert.ok(dashboard.includes('onClick={onSelect}'));
 assert.ok(dashboard.includes('selectedKpi===index'));
 assert.ok(dashboard.includes('setSelectedKpi(selectedKpi===index?null:index)'));
 assert.ok(dashboard.includes('finance.salesGrossIrr'));
 assert.ok(dashboard.includes('finance.salesCollectedForCohortIrr'));
 assert.ok(dashboard.includes('overview.invoiceStatus.unknown'));
 assert.ok(dashboard.includes('overview.invoiceStatus.acceptedCancellation'));
 assert.ok(dashboard.includes('href={admin?"/subscriptions":"/invoices"}'));
 assert.ok(!dashboard.includes("Math.random"));
 assert.ok(!dashboard.includes('method:"POST"'));
});

test("custom date filter has strict client validation and uses validated server period contract",()=>{
 assert.ok(dashboard.includes('type CustomRange = {from:string;to:string}'));
 assert.ok(dashboard.includes('period==="custom"'));
 assert.ok(dashboard.includes('new Date(start).toISOString().slice(0,10)!==from'));
 assert.ok(dashboard.includes('(end-start)/86400000>365'));
 assert.ok(dashboard.includes('start>end||to>today'));
 assert.ok(dashboard.includes('onSubmit={apply}'));
 assert.ok(dashboard.includes('type="date"'));
 assert.ok(dashboard.includes('max={today}'));
 assert.ok(api.includes('case "custom":'));
 assert.ok(api.includes('end.DayNumber - start.DayNumber > 365'));
 assert.ok(proxy.includes('const allowedKeys = new Set(["period", "from", "to"'));
});

test("custom period reaches every period-sensitive endpoint and never changes snapshot feeds",()=>{
 for(const section of ["overview","trends","attention","finance"])
  assert.ok(dashboard.includes('query("'+section+'", period, controller.signal, customRange)'),section);
 assert.ok(dashboard.includes('period={dateQuery}'));
 assert.ok(dashboard.includes('<RootDashboardDetails period={dateQuery} />'));
 assert.ok(dashboard.includes('setSelectedKpi(null);setCustomRange({from,to});setPeriod("custom")'));
 assert.ok(dashboard.includes('currentPeriodLabel'));
 assert.ok(!dashboard.includes('<DashboardSubscriptionWatch role={role === "Level1" ? "Level1" : "Level2"} period={dateQuery}'));
});

test("compact responsive period controls and KPI details remain accessible",()=>{
 assert.ok(css.includes(".argin-dashboard__custom-range"));
 assert.ok(css.includes(".argin-dashboard__kpi-interactive"));
 assert.ok(css.includes(".argin-dashboard__kpi-detail"));
 assert.ok(css.includes("max-width:540px"));
 assert.ok(dashboard.includes('<details className="argin-dashboard__custom-range"'));
 assert.ok(dashboard.includes('aria-label="بستن جزئیات شاخص"'));
 assert.ok(dashboard.includes("aria-label={label"));
});
