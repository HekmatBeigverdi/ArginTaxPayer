import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { runInNewContext } from "node:vm";
import { test } from "node:test";
import ts from "typescript";

const require=createRequire(import.meta.url);
const {NextRequest}=require("next/server");
const source=readFileSync(new URL("../app/api/dashboard/[section]/route.ts",import.meta.url),"utf8");

function handler(roles=["Level1"],loggedIn=true) {
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
      return Response.json({schemaVersion:1,role:roles[0]});
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
