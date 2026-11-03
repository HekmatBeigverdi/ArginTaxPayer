import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {createRequire} from "node:module";
import {runInNewContext} from "node:vm";
import {test} from "node:test";
import ts from "typescript";

const require=createRequire(import.meta.url);
const {NextRequest}=require("next/server");
const read=path=>readFileSync(new URL("../"+path,import.meta.url),"utf8");
const readRoot=path=>readFileSync(new URL("../../../"+path,import.meta.url),"utf8");
const route=read("app/api/dashboard/[section]/route.ts");
const dashboard=read("app/(console)/dashboard/dashboard-analytics.tsx");
const center=read("app/(console)/dashboard/dashboard-action-center.tsx");
const subscription=read("app/(console)/dashboard/dashboard-subscription-watch.tsx");
const financial=read("app/(console)/dashboard/dashboard-financial-charts.tsx");
const root=read("app/(console)/dashboard/root-dashboard-details.tsx");
const partner=read("app/(console)/dashboard/partner-dashboard-details.tsx");
const css=read("app/(console)/dashboard/dashboard-design.css");
const backend=readRoot("src/ArginTaxPayer.Api/Dashboard/Phase15DashboardReadService.cs");
const endpoints=readRoot("src/ArginTaxPayer.Api/Dashboard/Phase15DashboardEndpoints.cs");
const runner=readRoot("scripts/phase15-step15-dashboard-load.mjs");
const smoke=readRoot("scripts/phase15-step16-role-smoke.mjs");
const acceptance=readRoot("scripts/phase15-step16-integrated-acceptance.sh");

function proxy({roles=["Customer"],loggedIn=true,cookie=true,upstreamStatus=200,networkFailure=false}={}){
 const transpiled=ts.transpileModule(route,{fileName:"route.ts",
  compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.CommonJS},reportDiagnostics:true});
 const errors=(transpiled.diagnostics??[]).filter(d=>d.category===ts.DiagnosticCategory.Error);
 assert.deepEqual(errors.map(d=>ts.flattenDiagnosticMessageText(d.messageText,"\n")),[]);
 const exported={},calls=[];
 runInNewContext(transpiled.outputText,{
  exports:exported,URL,URLSearchParams,AbortSignal,Response,
  require(name){
   if(name==="next/headers")return {cookies:async()=>({get:()=>cookie?{value:"not-a-real-test-token"}:undefined})};
   if(name.endsWith("/lib/identity"))return {
    ACCESS_COOKIE:"token",identityUiEnabled:()=>true,
    currentIdentity:async()=>loggedIn?{userId:"test-actor",roles}:null,
    identityApiOrigin:()=> "http://127.0.0.1:15089"
   };
   return require(name);
  },
  fetch:async(url,options)=>{
   calls.push({url:String(url),options});
   if(networkFailure)throw new Error("controlled connection failure");
   if(upstreamStatus!==200)return new Response(null,{status:upstreamStatus});
   return Response.json({schemaVersion:1,role:roles[0]});
  }
 });
 const get=(section,query="")=>exported.GET(
  new NextRequest("http://localhost:3000/api/dashboard/"+section+query),
  {params:Promise.resolve({section})});
 return {get,calls};
}
test("role matrix: no commercial or cross-role dashboard disclosure through the proxy",async()=>{
 const restricted=["finance","financial-trends","subscriptions-watch","root","partner"];
 for(const section of restricted){
  const h=proxy({roles:["Customer"]});
  const response=await h.get(section);
  assert.equal(response.status,403,section);
  assert.equal(h.calls.length,0,section);
  assert.equal(response.headers.get("cache-control"),"private, no-store");
 }
 for(const role of ["Level1","Level2"]){
  const h=proxy({roles:[role]});
  const forbidden=role==="Level1"?"partner":"root";
  assert.equal((await h.get(forbidden)).status,403);
  assert.equal(h.calls.length,0);
  const authorized=role==="Level1"?"root":"partner";
  assert.equal((await h.get(authorized)).status,200);
  assert.equal(h.calls.length,1);
  assert.equal(h.calls[0].options.cache,"no-store");
  assert.equal(h.calls[0].options.method,"GET");
 }
});
test("pre-forwarding authorization and filter validation: zero foreign calls",async()=>{
 const anonymous=proxy({roles:["Level1"],loggedIn:false});
 assert.equal((await anonymous.get("overview")).status,401);
 assert.equal(anonymous.calls.length,0);
 const noCookie=proxy({roles:["Level1"],cookie:false});
 assert.equal((await noCookie.get("overview")).status,401);
 assert.equal(noCookie.calls.length,0);
 const rootUser=proxy({roles:["Level1"]});
 for(const section of ["root","financial-trends","subscriptions-watch","partner"]){
  for(const q of ["?partnerUserId=00000000-0000-0000-0000-000000000001",
                 "?taxpayerId=00000000-0000-0000-0000-000000000001"]){
   const response=await rootUser.get(section,q);
   assert.ok([400,403].includes(response.status),section+q);
  }
 }
 assert.equal(rootUser.calls.length,0);
 assert.equal((await rootUser.get("unknown-section")).status,404);
 assert.equal((await rootUser.get("overview","?period=30d&period=7d")).status,400);
 assert.equal((await rootUser.get("overview","?role=Level1")).status,400);
 assert.equal(rootUser.calls.length,0);
});
test("server-origin failure and upstream denial never become successful JSON",async()=>{
 for(const upstreamStatus of [401,403,404,429,500,503]){
  const h=proxy({roles:["Level1"],upstreamStatus});
  const response=await h.get("overview","?period=30d");
  assert.equal(response.status,upstreamStatus>=500?503:upstreamStatus);
  assert.equal(response.headers.get("cache-control"),"private, no-store");
  assert.equal(h.calls.length,1);
 }
 const offline=proxy({roles:["Customer"],networkFailure:true});
 const response=await offline.get("overview");
 assert.equal(response.status,503);
 assert.equal(response.headers.get("cache-control"),"private, no-store");
});
test("all main dashboard role components have a valid TSX syntax tree",()=>{
 for(const [file,source] of Object.entries({dashboard,center,subscription,financial,root,partner})){
  const output=ts.transpileModule(source,{fileName:file+".tsx",reportDiagnostics:true,
   compilerOptions:{target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.Preserve}});
  assert.deepEqual((output.diagnostics??[]).filter(d=>d.category===ts.DiagnosticCategory.Error)
   .map(d=>ts.flattenDiagnosticMessageText(d.messageText,"\n")),[],file);
 }
});
test("independent errors, a11y, reduced motion and lazy-loaded secondary panels",()=>{
 assert.ok(dashboard.includes('aria-expanded={open}'));
 assert.ok(dashboard.includes('aria-controls={id}'));
 assert.ok(dashboard.includes('open ? children : null'));
 assert.ok(dashboard.includes('DashboardKpiSkeleton'));
 assert.ok(dashboard.includes('DashboardState kind="error"'));
 assert.ok(dashboard.includes('role === "Level1" && <DashboardFold id="root-details"'));
 assert.ok(dashboard.includes('role === "Level2" && ('));
 assert.ok(dashboard.includes('admin && <DashboardFold id="finance-details"'));
 assert.ok(dashboard.includes("setOverviewState(waiting())"));
 assert.ok(dashboard.includes("setFinanceState(waiting())"));
 assert.ok(dashboard.includes("controller.abort()"));
 assert.ok(center.includes("بخشی از اطلاعات دریافت نشد"));
 assert.ok(center.includes('tasks.slice(0,5)'));
 assert.ok(css.includes("prefers-reduced-motion:reduce"));
 assert.ok(!dashboard.includes('method:"POST"'));
 assert.ok(!center.includes('method:"POST"'));
});
test("commercial and fiscal read models retain owner authorization and provenance",()=>{
 for(const term of ["scoped", "customer_commercial_owners", "AuthorizeAsync(bearer",
  "Classify(", "InvoiceSql", "RootOverviewAsync", "PartnerOverviewAsync", "SubscriptionWatchAsync"])
  assert.ok(backend.includes(term),term);
 assert.match(backend,/if \(!actor\.Level1 \|\| actor\.Role != "Level1"\)/);
 assert.match(backend,/if \(actor\.Role != "Level2"\)/);
 assert.match(backend,/WHEN e\.kind=2 THEN -e\.amount_irr/);
 assert.match(backend,/a\.root_user_id=\{0\}/);
 assert.match(backend,/a\.partner_user_id=\{0\}/);
 assert.match(endpoints,/IPAddress\.IsLoopback/);
 assert.match(endpoints,/RequireRateLimiting/);
 assert.ok(!dashboard.includes("localStorage"));
 assert.ok(!dashboard.includes("sessionStorage"));
});
test("non-mutating bounded API benchmark cannot silently become a send workflow",()=>{
 assert.ok(runner.includes('method:"GET"'));
 assert.ok(runner.includes('redirect:"manual"'));
 assert.ok(runner.includes('cache:"no-store"'));
 assert.ok(runner.includes('rounds>20'));
 assert.ok(runner.includes('concurrency>4'));
 assert.ok(!runner.includes('method:"POST"'));
 assert.ok(!runner.includes('JSON.stringify(body)'));
});

test("three-account role probe is bounded, localhost-only, read-only and redacts payloads",()=>{
 assert.ok(smoke.includes("PHASE15_LEVEL1_TOKEN"));
 assert.ok(smoke.includes("PHASE15_LEVEL2_TOKEN"));
 assert.ok(smoke.includes("PHASE15_CUSTOMER_TOKEN"));
 assert.ok(smoke.includes('new Set(actors.map(([,token])=>token)).size!==3'));
 assert.ok(smoke.includes('method:"GET"'));
 assert.ok(smoke.includes('redirect:"manual"'));
 assert.ok(smoke.includes('cache:"no-store"'));
 assert.ok(smoke.includes('"127.0.0.1"'));
 assert.ok(smoke.includes("root"));
 assert.ok(smoke.includes("partner"));
 assert.ok(smoke.includes("subscriptions-watch"));
 assert.ok(!smoke.includes("console.log(token)"));
 assert.ok(!smoke.includes("console.log(payload)"));
});
test("macOS runner is fail-closed and emits same-SHA private evidence with manual gates",()=>{
 assert.ok(acceptance.includes('uname -s'));
 assert.ok(acceptance.includes('phase/15-role-based-dashboard'));
 assert.ok(acceptance.includes('git status --porcelain --untracked-files=no'));
 assert.ok(acceptance.includes('git rev-parse HEAD'));
 assert.ok(acceptance.includes('umask 077'));
 assert.ok(acceptance.includes('dotnet test ArginTaxPayer.sln'));
 assert.ok(acceptance.includes('npm test'));
 assert.ok(acceptance.includes('npm run typecheck'));
 assert.ok(acceptance.includes('npm run build'));
 assert.ok(acceptance.includes("MANUAL_PENDING"));
 assert.ok(acceptance.includes('"manualAcceptance":"PENDING"'));
 assert.ok(!acceptance.includes('dotnet ef database update'));
});
