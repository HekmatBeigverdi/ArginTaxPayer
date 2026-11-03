import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

import React from "react";
import {renderToStaticMarkup} from "react-dom/server";
import {loadTsx} from "./helpers/load-tsx.mjs";

function find(node,predicate) {
  if(!node||typeof node!=="object")return null;
  if(Array.isArray(node))return node.map(child=>find(child,predicate)).find(Boolean);
  return predicate(node)?node:find(node.props?.children,predicate);
}
const order={orderId:"order-1",customerUserId:"customer-1",customerDisplayName:"مشتری نمونه",
  planKind:2,invoiceUnitLimit:100,issuedAtUtc:"2026-11-02T12:00:00Z",grossIrr:1000,
  collectedIrr:0,isActivated:false};
function workspace(states,fetch) {
  let slot=0;
  return loadTsx(new URL("../app/subscriptions/subscription-workspace.tsx",import.meta.url),{
    globals:{fetch},modules:{"./subscription-workspace.css":{},"./partner-settlement-workspace.css":{},
      "./plan-catalog":{default:()=>null},
      "./collection-history-table":{default:()=>React.createElement("div",null,"سابقه دریافت")},
      "./sms-topup-action":{default:({customerUserId})=>React.createElement("button",{"data-customer":customerUserId},"شارژ با SEP")},
      react:{...React,useRef:value=>({current:value}),useCallback:fn=>fn,useEffect:()=>{},
        useState(initial){const i=slot++;return [Object.hasOwn(states,i)?states[i]:initial,()=>{}];}}}
  }).default;
}

const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("Step10 finance page requires signed-in manager and excludes customers", () => {
  const page = read("app/(console)/subscriptions/page.tsx");
  assert.match(page, /currentIdentity\(\)/);
  assert.match(page, /roles\.includes\("Level1"\)/);
  assert.match(page, /roles\.includes\("Level2"\)/);
  assert.match(page, /redirect\("\/dashboard"\)/);
});

test("Step10 API proxy uses cookie identity, allowlisted destinations, local backend", () => {
  const route = read("app/api/phase12/[...parts]/route.ts");
  assert.match(route, /currentIdentity\(\)/);
  assert.match(route, /ACCESS_COOKIE/);
  assert.match(route, /const actions = new Map/);
  assert.match(route, /identityApiOrigin\(\)/);
  assert.match(route, /invalid_origin/);
  assert.match(route, /roles\.includes\("Level1"\)/);
  assert.match(route, /subscriptions\/settlements\/reverse/);
  assert.doesNotMatch(route, /fetch\(request\.nextUrl/);
});

test("Finance workspace approves orders and records collections through authenticated server actions",async()=>{
  const calls=[];
  const Component=workspace({15:[order],17:false,18:order.orderId,
    19:{[order.orderId]:{amount:"250",reference:"RECEIPT-1",channel:2}}},async(url,options)=>{
    calls.push({url,options});
    return Response.json(options.method==="GET"?{items:[order],totalCount:1}:{ok:true});
  });
  const view=Component({level1:true});
  const approve=find(view,node=>node.type==="button"&&node.props.children==="تأیید و فعال‌سازی");
  assert.ok(approve);approve.props.onClick();
  await new Promise(resolve=>setImmediate(resolve));
  const form=find(view,node=>node.type==="form");assert.ok(form);
  form.props.onSubmit({preventDefault(){}});
  await new Promise(resolve=>setImmediate(resolve));
  const writes=calls.filter(call=>call.options.method==="POST");
  assert.deepEqual(writes.map(call=>call.url),[
    "/api/phase12/subscriptions/paid-orders/activate",
    "/api/phase12/subscriptions/collections/manual"
  ]);
  assert.deepEqual(JSON.parse(writes[0].options.body),{orderId:"order-1"});
  assert.deepEqual(JSON.parse(writes[1].options.body),{
    orderId:"order-1",customerUserId:"customer-1",channel:2,amountIrr:250,receiptReference:"RECEIPT-1"
  });
  assert.ok(writes.every(call=>call.options.credentials==="same-origin"));
});

test("Step10 navigation exposes subscriptions to administrators", () => {
  assert.match(read("app/components/console-shell.tsx"), /path: "\/subscriptions"/);
  assert.match(read("app/(console)/dashboard/page.tsx"), /href: "\/subscriptions"/);
});

test("Finance SMS report shows usage totals and offers SEP top-up for the selected customer",()=>{
  const Component=workspace({0:"sms",9:{items:[{dispatchId:"dispatch-1",purpose:"otp",
    customerUserId:"customer-1",customerContact:"09120000000",partnerLevel2UserId:null,
    providerCostIrr:100,customerChargeIrr:200,recordedAtUtc:"2026-11-02T12:00:00Z",status:"sent"}],
    totalCount:1,providerCostTotalIrr:100,customerChargeTotalIrr:200},10:false},()=>{
      throw Error("render must not submit a payment");
    });
  const html=renderToStaticMarkup(React.createElement(Component,{level1:true}));
  for(const label of ["تعریف پلن","ثبت دریافت","گزارش درآمد","تسویه شرکا","گزارش پیامک",
    "هزینه واقعی سرویس","جمع کسر از اعتبار کاربران","09120000000","شارژ با SEP"])
    assert.ok(html.includes(label),label);
  assert.ok(html.includes('data-customer="customer-1"'));
});

test("Plan catalog has backend-backed compact search, modal and protected CRUD",()=>{
  const catalog=read("app/subscriptions/plan-catalog.tsx");
  for(const term of ["جست‌وجوی پلن","افزودن پلن","مشاهده","ویرایش","حذف","role=\"dialog\"","DELETE","PUT","بازخوانی"])
    assert.ok(catalog.includes(term),term);
  const proxy=read("app/api/phase12/[...parts]/route.ts");
  assert.match(proxy,/request.method === "GET"/);
  assert.match(proxy,/request.method as "PUT"\s*\|\s*"DELETE"/);
});
