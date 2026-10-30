import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const backend=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10InvoiceDispatchHistoryService.cs","utf8");
const api=readFileSync("../../src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs","utf8");
const bff=readFileSync("app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/history/route.ts","utf8");
const ui=readFileSync("app/invoices/invoice-dispatch-history.tsx","utf8");
const workspace=readFileSync("app/invoices/workspace.tsx","utf8");
const startup=readFileSync("../../src/ArginTaxPayer.Api/Program.cs","utf8");
test("history service verifies owner, taxpayer and invoice before scoped evidence reads",()=>{
  assert.match(backend,/scope\.ResolveAsync/);
  assert.match(backend,/scope\.ScopedQuery/);
  assert.match(backend,/db\.InvoiceDocuments\.AsNoTracking/);
  assert.match(backend,/customer_user_id=@owner AND taxpayer_id=@taxpayer AND invoice_id=@invoice/);
  assert.match(backend,/WHERE operation_id=@operation/);
  assert.match(backend,/LIMIT 30/);
  assert.doesNotMatch(backend,/POST|HttpClient|SendAsync|private_envelope_json/);
});
test("history never claims signed preview, fiscal accepted or live sending",()=>{
  assert.match(backend,/new\("Sandbox",false,false,false/);
  assert.match(backend,/Signed packet bytes are deliberately not read/);
  assert.match(api,/MapGet\("\/history"/);
  assert.match(startup,/AddScoped<Phase10InvoiceDispatchHistoryService>/);
  assert.match(bff,/signedPreviewAvailable:false/);
  assert.match(bff,/fiscalAccepted:false/);
  assert.match(bff,/submissionEnabled:false/);
  assert.match(bff,/ACCESS_COOKIE/);
  assert.doesNotMatch(bff,/export async function POST/);
});
test("current history UI reads attempts and receipts without submitting or claiming acceptance",()=>{
  assert.match(ui,/disabled=\{pending\}/);
  assert.match(ui,/attempts\.map/);
  assert.match(ui,/receipts\.map/);
  assert.match(ui,/history\.state === "Indeterminate"/);
  assert.match(ui,/credentials: "same-origin"/);
  assert.match(ui,/cache: "no-store"/);
  assert.doesNotMatch(ui,/method:\s*"POST"/);
  assert.match(workspace,/InvoiceDispatchHistory/);
});

import {createRequire} from "node:module";
import {runInNewContext} from "node:vm";
const require=createRequire(import.meta.url);
const ts=require("typescript");

// Execute the production route; replace only Next's response wrapper and upstream I/O.
async function readHistory(overrides={},requestOverrides={}){
  const requests=[];
  const payload={
    environment:"Sandbox",submissionEnabled:false,fiscalAccepted:false,
    signedPreviewAvailable:false,invoiceRevision:2,state:"Acknowledged",
    lastUpdatedAtUtc:"2026-10-29T10:00:00Z",
    attempts:[{number:1,startedAtUtc:"2026-10-29T10:00:00Z",endedAtUtc:null,result:"acknowledged"}],
    receipts:[{kind:"InitialReceipt",observedAtUtc:"2026-10-29T10:00:00Z",hasAuthorityReference:true}],
    recoveryAction:"RefreshOnly",recoveryCode:"invoice.recovery.refresh_only",
    mayRetry:false,mayInquire:false,...overrides
  };
  const context={exports:{},AbortSignal,
    require:name=>{
      if(name==="next/server")return {NextResponse:{json:(body,options={})=>({body,status:options.status??200,headers:options.headers})}};
      if(name.endsWith("/lib/identity"))return {ACCESS_COOKIE:"access",identityUiEnabled:()=>true,identityApiOrigin:()=>"http://127.0.0.1:5000"};
      throw Error("Unexpected import: "+name);
    },
    fetch:async(url,options)=>{requests.push({url,options});return {status:200,ok:true,text:async()=>JSON.stringify(payload)};}
  };
  runInNewContext(ts.transpileModule(bff,{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText,context);
  const response=await context.exports.GET({cookies:{get:()=>({value:"session-token"})},...requestOverrides},{params:Promise.resolve({taxpayerId:"11111111-1111-1111-1111-111111111111",invoiceId:"22222222-2222-2222-2222-222222222222"})});
  return {response,requests};
}

test("history route accepts actual Sandbox evidence and returns bounded read-only data",async()=>{
  const {response,requests}=await readHistory({providerRaw:"secret",attempts:[{number:1,startedAtUtc:"2026-10-29T10:00:00Z",endedAtUtc:null,result:"untrusted-provider-text"}]});
  assert.equal(response.status,200);
  assert.equal(response.body.environment,"Sandbox");
  assert.equal(response.body.attempts[0].result,"Recorded");
  assert.equal(response.body.receipts[0].hasAuthorityReference,true);
  assert.equal(response.body.providerRaw,undefined);
  assert.equal(response.body.submissionEnabled,false);
  assert.equal(response.body.fiscalAccepted,false);
  assert.equal(response.body.signedPreviewAvailable,false);
  assert.equal(response.headers["Cache-Control"],"no-store");
  assert.equal(requests.length,1);
  assert.equal(requests[0].url,"http://127.0.0.1:5000/api/dev/taxpayers/11111111-1111-1111-1111-111111111111/invoices/22222222-2222-2222-2222-222222222222/dispatch/history");
  assert.equal(requests[0].options.method,"GET");
  assert.equal(requests[0].options.headers.Authorization,"Bearer session-token");
});

test("history route rejects other environments, active controls and oversized evidence",async()=>{
  for(const invalid of [{environment:"Production"},{environment:"Mock"},
    {submissionEnabled:true},{fiscalAccepted:true},{signedPreviewAvailable:true},
    {mayRetry:true},{mayInquire:true},{attempts:Array(31).fill({})},{receipts:Array(31).fill({})}]){
    const {response}=await readHistory(invalid);
    assert.equal(response.status,502,JSON.stringify(invalid));
  }
});

test("history route rejects unauthenticated reads before contacting backend",async()=>{
  const {response,requests}=await readHistory({}, {cookies:{get:()=>undefined}});
  assert.equal(response.status,401);
  assert.equal(requests.length,0);
});
