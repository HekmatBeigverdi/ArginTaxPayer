#!/usr/bin/env node
// Phase15 Step15: bounded, read-only dashboard smoke/load check.
// Do not log or commit bearer tokens. Run against local development API ONLY.
import {performance} from "node:perf_hooks";
import {writeFileSync} from "node:fs";

const base=process.env.PHASE15_BASE_URL ?? "http://127.0.0.1:15089";
const token=process.env.PHASE15_BEARER_TOKEN;
const rounds=Number(process.env.PHASE15_ROUNDS ?? "6");
const concurrency=Number(process.env.PHASE15_CONCURRENCY ?? "2");
const role=process.env.PHASE15_ROLE ?? "Customer";
const output=process.env.PHASE15_RESULT_FILE;
const allowedRoles=["Customer","Level1","Level2"];

function fail(message){throw new Error(message);}
if (!allowedRoles.includes(role)) fail("PHASE15_ROLE must be Customer, Level1 or Level2.");
if (!Number.isInteger(rounds)||rounds<1||rounds>20) fail("PHASE15_ROUNDS must be 1..20.");
if (!Number.isInteger(concurrency)||concurrency<1||concurrency>4) fail("PHASE15_CONCURRENCY must be 1..4.");
const parsed=new URL(base);
if(parsed.protocol!=="http:" || !["127.0.0.1","localhost","[::1]"].includes(parsed.hostname) ||
 parsed.username || parsed.password || parsed.pathname!=="/")
 fail("Only a local HTTP API on loopback is permitted.");
if(!token || token.length<10) fail("Set PHASE15_BEARER_TOKEN in your shell; never print it.");
const sections=role==="Customer"
 ? ["overview","trends","attention","recent"]
 : role==="Level1"
 ? ["overview","trends","attention","recent","finance","financial-trends","root","subscriptions-watch"]
 : ["overview","trends","attention","recent","finance","financial-trends","partner","subscriptions-watch"];
const tasks=Array.from({length:rounds},()=>sections).flat();
let cursor=0;
const samples=[];

async function worker(){
 while(cursor<tasks.length){
  const section=tasks[cursor++];
  const url=base.replace(/\/$/,"")+"/api/dev/dashboard/"+section+
    (["partner","subscriptions-watch"].includes(section)?"":"?period=30d");
  const controller=new AbortController();
  const timeout=setTimeout(()=>controller.abort(),15000);
  const t=performance.now();
  let status=0,success=false,bytes=0;
  try {
    const response=await fetch(url,{method:"GET",
      headers:{Authorization:"Bearer "+token,Accept:"application/json"},
      cache:"no-store",signal:controller.signal,redirect:"manual"});
    status=response.status;
    const body=await response.text();
    bytes=Buffer.byteLength(body);
    // Status-only validation; do not store or print customer payloads.
    if(response.ok){
      const parsed=JSON.parse(body);
      success=parsed.schemaVersion===1 && typeof parsed==="object";
    }
  }catch{
    status=0;success=false;
  }finally{clearTimeout(timeout);}
  samples.push({section,status,ok:success,ms:Math.round(performance.now()-t),bytes});
 }
}
await Promise.all(Array.from({length:concurrency},worker));
const p=(vals,percent)=>vals[Math.min(vals.length-1,Math.ceil(vals.length*percent)-1)]??0;
const summary=sections.map(section=>{
 const rows=samples.filter(r=>r.section===section);
 const vals=rows.map(r=>r.ms).sort((a,b)=>a-b);
 return {section,requests:rows.length,ok:rows.filter(r=>r.ok).length,
  errors:rows.filter(r=>!r.ok).length,minMs:vals[0]??0,
  p50Ms:p(vals,.50),p95Ms:p(vals,.95),maxMs:vals.at(-1)??0,
  statuses:[...new Set(rows.map(r=>r.status))].sort((a,b)=>a-b)};
});
const result={role,rounds,concurrency,generatedAtUtc:new Date().toISOString(),summary};
console.table(summary);
if(output)writeFileSync(output,JSON.stringify(result,null,2)+"\n",{mode:0o600});
if(summary.some(s=>s.errors>0)){
 console.error("FAIL: one or more authorized, read-only dashboard calls failed or returned an invalid contract.");
 process.exitCode=1;
}else console.log("PASS: authorized, read-only dashboard sample completed.");
