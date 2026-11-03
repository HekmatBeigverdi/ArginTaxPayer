import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";
const read=path=>readFileSync(new URL("../../"+path,import.meta.url),"utf8");
const service=read("src/ArginTaxPayer.Api/Dashboard/Phase15DashboardReadService.cs");
const sql=read("scripts/phase15-step15-query-plan-audit.sql");
const runner=read("scripts/phase15-step15-dashboard-load.mjs");
const doc=read("docs/phases/phase-15-role-based-dashboard.md");

test("recent list pushes LIMIT to SQL after canonical latest-state ordering",()=>{
 const recent=service.split("public async Task<Recent> RecentAsync")[1]?.split("private static Alert ToAlert")[0];
 assert.ok(recent);
 assert.match(recent,/ReadInvoices\(actor,range,taxpayer,ct,maxRows:10\)/);
 assert.match(recent,/rows\.Select\(ToAlert\)/);
 assert.match(service,/InvoiceSql \+ "\\nLIMIT \{7\}"/);
 assert.match(service,/actor\.Role == "Customer", maxRows\.Value/);
 const query=service.split('private const string InvoiceSql = """')[1]?.split('""";')[0];
 assert.ok(query);
 assert.match(query,/WHERE rn=1/);
 assert.match(query,/ORDER BY "IssuedAtUtc" DESC,"RootId" DESC/);
 assert.match(query,/c\.depth<100 AND NOT s\.id=ANY\(c\.visited\)/);
});
test("do not silently limit financial, overview, trend and attention calculations",()=>{
 for(const section of ["Overview","Trends","Attention"]){
  const method=service.split("public async Task<"+section+"> "+section+"Async")[1];
  assert.ok(method,section);
  assert.ok(method.split("public async Task<")[0].includes("ReadInvoices(actor,range,taxpayer,ct)"),section);
 }
 assert.ok(service.includes("public async Task<Overview> OverviewAsync"));
});
test("audit must be test-db-gated and read-only with explain buffers",()=>{
 assert.match(sql,/\\set ON_ERROR_STOP on/);
 assert.match(sql,/BEGIN TRANSACTION READ ONLY/);
 assert.match(sql,/current_database\(\) = 'argintaxpayer_test'/);
 assert.match(sql,/SET LOCAL statement_timeout = '15s'/);
 assert.match(sql,/EXPLAIN \(ANALYZE, BUFFERS, TIMING OFF, SUMMARY ON\)/);
 assert.match(sql,/phase15_sale_cohort/);
 assert.match(sql,/phase15_verified_collections/);
 assert.match(sql,/phase15_partner_scope/);
 assert.match(sql,/phase15_expiring_subscriptions/);
 assert.match(sql,/ROLLBACK;/);
 assert.doesNotMatch(sql,/\b(?:CREATE|DROP|ALTER|UPDATE|DELETE|INSERT|TRUNCATE)\s+(?:TABLE|INDEX|INTO|FROM|\w+\s+SET)\b/i);
});
test("bounded runner is loopback-only and never saves token or payload",()=>{
 assert.match(runner,/127\.0\.0\.1/);
 assert.match(runner,/PHASE15_BEARER_TOKEN/);
 assert.match(runner,/rounds>20/);
 assert.match(runner,/concurrency>4/);
 assert.match(runner,/method:"GET"/);
 assert.match(runner,/cache:"no-store"/);
 assert.match(runner,/performance\.now\(\)/);
 assert.match(runner,/p95Ms/);
 assert.match(runner,/AbortController/);
 assert.match(runner,/writeFileSync\(output,JSON\.stringify\(result/);
 assert.doesNotMatch(runner,/console\.log\(token\)|JSON\.stringify\(body\)|method:"POST"/);
});
test("documentation retains a single phase file",()=>{
 assert.ok(doc.includes("STEP 14"));
});
