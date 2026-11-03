import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";
import ts from "typescript";
const read=p=>readFileSync(new URL("../"+p,import.meta.url),"utf8");
const page=read("app/(console)/dashboard/dashboard-analytics.tsx");
const watch=read("app/(console)/dashboard/dashboard-subscription-watch.tsx");
const backend=read("../../src/ArginTaxPayer.Api/Dashboard/Phase15DashboardReadService.cs");
const endpoints=read("../../src/ArginTaxPayer.Api/Dashboard/Phase15DashboardEndpoints.cs");
const proxy=read("app/api/dashboard/[section]/route.ts");
const css=read("app/(console)/dashboard/dashboard-design.css");

test("subscription watch compiles as TSX",()=>{
 const r=ts.transpileModule(watch,{fileName:"dashboard-subscription-watch.tsx",compilerOptions:{
   target:ts.ScriptTarget.ES2022,module:ts.ModuleKind.ESNext,jsx:ts.JsxEmit.Preserve
 },reportDiagnostics:true});
 assert.deepEqual((r.diagnostics??[]).filter(x=>x.category===ts.DiagnosticCategory.Error)
   .map(x=>ts.flattenDiagnosticMessageText(x.messageText,"\n")),[]);
});
test("watchlist only mounts for administrator roles",()=>{
 assert.ok(page.includes('admin && <DashboardFold id="subscription-details"'));
 assert.ok(page.includes('<DashboardSubscriptionWatch'));
 assert.ok(page.includes('role === "Level1" ? "Level1" : "Level2"'));
 assert.ok(watch.includes("data.role!==role"));
 assert.ok(watch.includes('fetch("/api/dashboard/subscriptions-watch"'));
 assert.ok(watch.includes('cache:"no-store"'));
 assert.ok(watch.includes("controller.abort()"));
});
test("backend filters currently owned clients before subscription enumeration",()=>{
 assert.ok(backend.includes('public async Task<SubscriptionWatch> SubscriptionWatchAsync'));
 assert.ok(backend.includes('AuthorizeAsync(bearer,true,ct)'));
 assert.ok(backend.includes('co.root_level1_user_id={0}'));
 assert.ok(backend.includes('co.partner_level2_user_id={0}'));
 assert.ok(backend.includes('JOIN identity_users u ON u.id=sub.customer_user_id AND u.is_active=true'));
 assert.ok(backend.includes('LEFT JOIN subscription_credit_accounts'));
 assert.ok(backend.includes('sub.activated_at_utc<={1} AND sub.expires_at_utc>{1}'));
 assert.ok(backend.includes('sub.state=2'));
 assert.ok(backend.includes('ROW_NUMBER() OVER (PARTITION BY sub.customer_user_id'));
 assert.ok(backend.includes('GREATEST(COALESCE(c.available_units,0),0)'));
 assert.ok(backend.includes('Take(10)'));
});
test("restricted endpoint rejects arbitrary filter selectors and does not disclose customer contact",()=>{
 assert.ok(endpoints.includes('"subscriptions-watch"=>await service.SubscriptionWatchAsync'));
 assert.ok(proxy.includes('section === "subscriptions-watch" && query.size > 0'));
 assert.ok(proxy.includes('section === "finance" || section === "financial-trends" || section === "subscriptions-watch"'));
 const dto=backend.split('public sealed record SubscriptionWatchItem(')[1]?.split(');')[0];
 assert.ok(dto);
 assert.doesNotMatch(dto,/Email|Mobile|CustomerUserId|RootShare/);
 assert.doesNotMatch(watch,/method:"POST"|Math.random|mockData|demoData/);
});
test("renewal overview has clear as-of semantics, bounded results and proper loading",()=>{
 for(const x of ["expiringWithinSevenDays","expiringWithinThirtyDays","exhaustedQuantityPlans",
   "activeSubscriptionCount","remainingInvoices","remainingDays"])assert.ok(watch.includes("."+x));
 assert.ok(watch.includes("مستقل از فیلتر تاریخ"));
 assert.ok(watch.includes("aria-busy=\"true\""));
 assert.ok(watch.includes("role=\"alert\""));
 assert.ok(css.includes(".argin-dashboard__watch-stats"));
 assert.ok(css.includes("max-width:500px"));
});
