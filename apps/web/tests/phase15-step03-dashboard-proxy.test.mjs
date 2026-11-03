import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { runInNewContext } from "node:vm";
import { test } from "node:test";
import ts from "typescript";

const require=createRequire(import.meta.url);
const {NextRequest}=require("next/server");
const source=readFileSync(new URL("../app/api/dashboard/[section]/route.ts",import.meta.url),"utf8");

function handler(roles=["Level1"],loggedIn=true,backendStatus=200) {
  const js=ts.transpileModule(source,{compilerOptions:{
    module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022
  },reportDiagnostics:true});
  assert.deepEqual(js.diagnostics.filter(x=>x.category===ts.DiagnosticCategory.Error),[]);
  const exports={};const calls=[];
  runInNewContext(js.outputText,{
    exports,URL,URLSearchParams,AbortSignal,Response,
    require(name) {
      if(name==="next/headers")return {cookies:async()=>({get:()=>({value:"token"})})};
      if(name.endsWith("/lib/identity"))return {
        ACCESS_COOKIE:"token", identityUiEnabled:()=>true,
        currentIdentity:async()=>loggedIn?{userId:"id",roles}:null,
        identityApiOrigin:()=>"http://127.0.0.1:15089"
      };
      return require(name);
    },
    fetch:async (url,options)=>{
      calls.push({url:url.toString(),options});
      return backendStatus===200 ? Response.json({schemaVersion:1,role:roles[0]})
        : new Response(null,{status:backendStatus});
    }
  });
  return {GET:exports.GET,calls};
}
const request=(url)=>new NextRequest("http://localhost:3000"+url);
const call=(h,section,query="")=>h.GET(request("/api/dashboard/"+section+query),
    {params:Promise.resolve({section})});

test("an ordinary user cannot read finance or another partner's metrics",async()=>{
  const h=handler(["Customer"]);
  assert.equal((await call(h,"finance")).status,403);
  assert.equal((await call(h,"overview","?partnerUserId=abc")).status,403);
  assert.equal(h.calls.length,0);
});

test("signed-out actor is rejected before forwarding",async()=>{
  const h=handler(["Level1"],false);
  assert.equal((await call(h,"overview")).status,401);
  assert.equal(h.calls.length,0);
});

test("admin read goes through fixed local endpoint and no-store bearer",async()=>{
  const h=handler(["Level1"]);
  const response=await call(h,"finance","?period=30d");
  assert.equal(response.status,200);
  assert.equal(h.calls.length,1);
  assert.equal(h.calls[0].url,"http://127.0.0.1:15089/api/dev/dashboard/finance?period=30d");
  assert.equal(h.calls[0].options.headers.Authorization,"Bearer token");
  assert.equal(h.calls[0].options.cache,"no-store");
});

test("unrecognized sections and duplicated or foreign parameters are blocked",async()=>{
  const h=handler(["Level1"]);
  assert.equal((await call(h,"debug")).status,404);
  assert.equal((await call(h,"overview","?role=Level1")).status,400);
  assert.equal((await call(h,"overview","?period=30d&period=7d")).status,400);
  assert.equal(h.calls.length,0);
});

test("Level2 cannot supply a different partner filter",async()=>{
  const h=handler(["Level2"]);
  assert.equal((await call(h,"finance","?partnerUserId=123")).status,403);
  assert.equal(h.calls.length,0);
});

test("backend denial never becomes a successful dashboard result",async()=>{
  for(const status of [401,403,404,503]) {
    const h=handler(["Level2"],true,status);
    const response=await call(h,"overview","?period=30d");
    assert.equal(response.status,status);
    assert.equal(response.headers.get("cache-control"),"private, no-store");
  }
});
test("finance customer response contains no root or partner financial payload",async()=>{
  const h=handler(["Customer"]);
  const response=await call(h,"finance","?period=7d");
  assert.equal(response.status,403);
  const body=await response.text();
  assert.ok(!body.includes("RootEarnedShareIrr"));
  assert.ok(!body.includes("SalesGrossIrr"));
  assert.equal(h.calls.length,0);
});
test("a finance request may not include a taxpayer selector",async()=>{
  const h=handler(["Level1"]);
  assert.equal((await call(h,"finance","?taxpayerId=123")).status,400);
  assert.equal(h.calls.length,0);
});

test("only Level2 can request the unfiltered partner overview",async()=>{
  for(const roles of [["Customer"],["Level1"],["Level1","Level2"]]){
    const h=handler(roles);
    assert.equal((await call(h,"partner")).status,403);
    assert.equal(h.calls.length,0);
  }
  const partner=handler(["Level2"]);
  const response=await call(partner,"partner");
  assert.equal(response.status,200);
  assert.equal(partner.calls.length,1);
  assert.equal(partner.calls[0].url,"http://127.0.0.1:15089/api/dev/dashboard/partner");
  assert.equal(partner.calls[0].options.headers.Authorization,"Bearer token");
  assert.equal(partner.calls[0].options.cache,"no-store");
});
test("Level2 partner snapshot rejects any date or identity filter",async()=>{
  const partner=handler(["Level2"]);
  for(const params of ["?period=30d","?partnerUserId=1","?taxpayerId=1","?from=2026-11-01","?to=2026-11-03"]){
    assert.equal((await call(partner,"partner",params)).status,400,params);
  }
  assert.equal(partner.calls.length,0);
});

test("Level1 root summary only forwards for Level1 and never Level2 or Customer",async()=>{
  for (const roles of [["Customer"],["Level2"]]) {
    const h=handler(roles);
    assert.equal((await call(h,"root","?period=30d")).status,403);
    assert.equal(h.calls.length,0);
  }
  const h=handler(["Level1"]);
  assert.equal((await call(h,"root","?period=30d")).status,200);
  assert.equal(h.calls.length,1);
  assert.equal(h.calls[0].url,"http://127.0.0.1:15089/api/dev/dashboard/root?period=30d");
  assert.equal(h.calls[0].options.headers.Authorization,"Bearer token");
  assert.equal(h.calls[0].options.cache,"no-store");
  assert.equal((await call(h,"root","?partnerUserId=1")).status,400);
  assert.equal((await call(h,"root","?taxpayerId=1")).status,400);
  assert.equal(h.calls.length,1);
});

test("financial trends is denied to Customer and refuses tenant selectors",async()=>{
  const customer=handler(["Customer"]);
  assert.equal((await call(customer,"financial-trends","?period=30d")).status,403);
  assert.equal(customer.calls.length,0);
  const level2=handler(["Level2"]);
  assert.equal((await call(level2,"financial-trends","?period=30d")).status,200);
  assert.equal(level2.calls[0].url,
    "http://127.0.0.1:15089/api/dev/dashboard/financial-trends?period=30d");
  for(const query of ["?partnerUserId=1","?taxpayerId=1"])
    assert.equal((await call(level2,"financial-trends",query)).status,400);
  assert.equal(level2.calls.length,1);
  const level1=handler(["Level1"]);
  assert.equal((await call(level1,"financial-trends","?period=7d")).status,200);
  assert.equal(level1.calls.length,1);
});

test("subscription watch is admin only and rejects selectors on the proxy",async()=>{
  const customer=handler(["Customer"]);
  assert.equal((await call(customer,"subscriptions-watch")).status,403);
  assert.equal(customer.calls.length,0);
  for(const roles of [["Level1"],["Level2"]]){
    const h=handler(roles);
    const response=await call(h,"subscriptions-watch");
    assert.equal(response.status,200);
    assert.equal(h.calls[0].url,"http://127.0.0.1:15089/api/dev/dashboard/subscriptions-watch");
    assert.equal(h.calls[0].options.cache,"no-store");
    for(const filter of ["?period=30d","?partnerUserId=1","?taxpayerId=1","?from=2026-11-01"])
      assert.equal((await call(h,"subscriptions-watch",filter)).status,400,filter);
    assert.equal(h.calls.length,1);
  }
});

test("custom Tehran date window is forwarded unchanged only to read-only supported reports",async()=>{
  const value="?period=custom&from=2026-10-01&to=2026-10-31";
  for(const roles of [["Customer"],["Level2"],["Level1"]]){
    const h=handler(roles);
    for(const section of ["overview","trends","attention",...(roles[0]==="Customer"?[]:["finance","financial-trends"])]){
      const response=await call(h,section,value);
      assert.equal(response.status,200,section);
      assert.equal(h.calls.at(-1).url,"http://127.0.0.1:15089/api/dev/dashboard/"+section+value);
      assert.equal(h.calls.at(-1).options.cache,"no-store");
    }
  }
  const h=handler(["Level1"]);
  assert.equal((await call(h,"root",value)).status,200);
  assert.equal((await call(h,"subscriptions-watch",value)).status,400);
  assert.equal((await call(h,"partner",value)).status,403);
});
