import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const read=p=>readFileSync(new URL("../"+p,import.meta.url),"utf8");

import React from "react";
import {createRequire} from "node:module";
import {loadTsx} from "./helpers/load-tsx.mjs";

function find(node, predicate) {
 if (!node || typeof node !== "object") return null;
 if (Array.isArray(node)) return node.map(child => find(child,predicate)).find(Boolean);
 return predicate(node) ? node : find(node.props?.children,predicate);
}

test("Customer cash checkout submits selected plan and official-invoice preference without client pricing",async()=>{
 const calls=[];
 let slot=0;
 const plan={planId:"plan-1",kind:2,invoiceUnitLimit:100,validityDays:365,priceIrr:5000};
 const component=loadTsx(new URL("../app/customer-plans/customer-plans.tsx",import.meta.url),{
  globals:{crypto:{randomUUID:()=>"new-operation"},AbortController,fetch:async(url,options)=>{
   calls.push({url,options});return Response.json({orderId:"order-1"});
  }},
  modules:{react:{...React,useEffect:()=>{},useMemo:fn=>fn(),useState(initial){
   const index=slot++;
   return [{0:[plan],1:false,5:plan.planId,6:true,7:"operation-1"}[index]??initial,()=>{}];
  }}}
 }).default;
 const view=component();
 const button=find(view,node=>node.type==="button"&&node.props.className?.includes("customer-order-summary__submit"));
 assert.equal(button.props.disabled,false);
 await button.props.onClick();
 // The click handler deliberately returns void; let the async request finish.
 await new Promise(resolve=>setImmediate(resolve));
 assert.equal(calls.length,1);
 assert.equal(calls[0].url,"/api/customer-checkout");
 assert.equal(calls[0].options.method,"POST");
 assert.deepEqual(JSON.parse(calls[0].options.body),{
  planId:"plan-1",wantsOfficialInvoice:true,operationKey:"operation-1"
 });
 const online=find(view,node=>node.props?.className==="customer-payment-unavailable");
 assert.equal(online.props["aria-disabled"],"true");
 assert.equal(online.props.onClick,undefined);
});

test("Customer checkout proxy forwards cash requests with authenticated identity",async()=>{
 const {NextRequest}=createRequire(import.meta.url)("next/server");
 const calls=[];
 const route=loadTsx(new URL("../app/api/customer-checkout/route.ts",import.meta.url),{
  globals:{AbortSignal,fetch:async(url,options)=>{calls.push({url,options});return Response.json({orderId:"order-1"});}},
  modules:{"next/headers":{cookies:async()=>({get:()=>({value:"session-token"})})},
   "../../../lib/identity":{ACCESS_COOKIE:"access",currentIdentity:async()=>({roles:["Customer"]}),
    identityApiOrigin:()=>"http://backend",identityUiEnabled:()=>true}}
 });
 const body=JSON.stringify({planId:"plan-1",wantsOfficialInvoice:true,operationKey:"operation-1"});
 const request=origin=>new NextRequest("http://localhost/api/customer-checkout",{
  method:"POST",headers:{origin},body
 });
 assert.equal((await route.POST(request("https://foreign.test"))).status,403);
 assert.equal(calls.length,0);
 assert.equal((await route.POST(request("http://localhost"))).status,200);
 assert.equal(calls.length,1);
 assert.equal(calls[0].url,"http://backend/api/dev/subscriptions/customer-checkout/cash");
 assert.equal(calls[0].options.headers.Authorization,"Bearer session-token");
 assert.equal(calls[0].options.body,body);
});
test("Customer only access to checkout page",()=>{
 const s=read("app/(console)/customer-plans/page.tsx");
 assert.match(s,/currentIdentity/);
 assert.match(s,/roles.includes\("Customer"\)/);
});
test("Subscription view derives from authenticated identity",()=>{
 const source=read("app/api/customer-subscription/route.ts");
 assert.match(source,/ACCESS_COOKIE/);
 assert.match(source,/customer-overview\/me/);
});
