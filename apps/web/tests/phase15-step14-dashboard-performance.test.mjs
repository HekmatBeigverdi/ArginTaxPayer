import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";
import ts from "typescript";

const read=path=>readFileSync(new URL("../"+path,import.meta.url),"utf8");
const page=read("app/(console)/dashboard/dashboard-analytics.tsx");
const center=read("app/(console)/dashboard/dashboard-action-center.tsx");
const detail=read("app/(console)/dashboard/dashboard-subscription-watch.tsx");
const proxy=read("app/api/dashboard/[section]/route.ts");
const css=read("app/(console)/dashboard/dashboard-design.css");

test("Step14 shared dashboard components transpile as TSX",()=>{
 for(const source of [page,center,detail]){
   const compiled=ts.transpileModule(source,{fileName:"dashboard.tsx",
     compilerOptions:{module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.Preserve,
       target:ts.ScriptTarget.ES2022},reportDiagnostics:true});
   assert.deepEqual((compiled.diagnostics||[])
     .filter(d=>d.category===ts.DiagnosticCategory.Error)
     .map(d=>ts.flattenDiagnosticMessageText(d.messageText,"\n")),[]);
 }
});

test("only one watch endpoint request site exists across all three components",()=>{
 const hits=[page,center,detail]
  .map(code=>(code.match(/fetch\("\/api\/dashboard\/subscriptions-watch"/g)||[]).length);
 assert.deepEqual(hits,[1,0,0]);
 assert.ok(page.includes('if(!admin||!watchEnabled)return'));
 assert.ok(page.includes('onOpen={enableWatch}'));
 assert.ok(page.includes('const [watchEnabled,setWatchEnabled]=useState(false)'));
 assert.ok(page.includes('const [watchRetry,setWatchRetry]=useState(0)'));
 assert.ok(page.includes('role!==role'));
 assert.ok(page.includes('watchData?.role===role?watchData:null'));
});

test("two consumers share the exact parent-owned snapshot and retry callback",()=>{
 assert.ok(page.includes('watchState={scopedWatchStatus} watch={scopedWatch} onRetryWatch={retryWatch}'));
 assert.ok(page.includes('status={scopedWatchStatus} watch={scopedWatch} onRetry={retryWatch}'));
 assert.ok(center.includes("watchState:WatchLoadState"));
 assert.ok(center.includes("watch:SubscriptionWatch|null"));
 assert.ok(center.includes("watch?.role===role"));
 assert.ok(detail.includes("watch?.role===role"));
 assert.ok(detail.includes("status:WatchLoadState"));
 assert.ok(detail.includes("onClick={onRetry}"));
 assert.ok(!center.includes("useEffect"));
 assert.ok(!detail.includes("useEffect"));
});

test("the subscription call has no persistent, cross-user or global cache",()=>{
 assert.ok(page.includes('cache:"no-store"'));
 assert.ok(page.includes("controller.abort()"));
 assert.ok(page.includes('setWatchData(null)'));
 assert.ok(page.includes('value.role!==role'));
 assert.ok(page.includes('!controller.signal.aborted'));
 assert.ok(!page.includes("localStorage"));
 assert.ok(!page.includes("sessionStorage"));
 assert.ok(!center.includes("localStorage"));
 assert.ok(!detail.includes("localStorage"));
 assert.ok(proxy.includes('"Cache-Control": "private, no-store"'));
});

test("collapsed reports are lazy and accessible",()=>{
 assert.ok(page.includes("const [open,setOpen]=useState(false)"));
 assert.ok(page.includes("if(!open)onOpen?.()"));
 assert.ok(page.includes("open ? children : null"));
 assert.ok(page.includes("aria-controls={id}"));
 assert.ok(page.includes("aria-expanded={open}"));
 assert.ok(css.includes(".argin-dashboard__fold-trigger"));
 assert.ok(css.includes("prefers-reduced-motion:reduce"));
});

test("period-specific data preserves abortable requests and no-store semantics",()=>{
 for(const name of ["overview","trends","attention","finance"])
   assert.ok(page.includes('query("'+name+'", period, controller.signal, customRange)'));
 assert.ok(page.includes("return () => controller.abort()"));
 assert.ok(page.includes("setOverviewState(waiting())"));
 assert.ok(page.includes("setFinanceState(waiting())"));
 assert.ok(page.includes("setSelectedKpi(null)"));
 assert.ok(!page.includes("Promise.all(tasks)"));
});
