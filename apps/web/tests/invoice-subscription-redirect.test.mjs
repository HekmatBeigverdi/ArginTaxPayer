import test from 'node:test';
import assert from 'node:assert/strict';
import {loadTsx} from './helpers/load-tsx.mjs';

const helper = new URL('../lib/invoice-subscription.ts', import.meta.url);
test('subscription denials navigate to purchase while unrelated failures stay on the invoice', () => {
 const navigations=[];
 const {redirectForInvoiceSubscription}=loadTsx(helper,{globals:{window:{location:{assign:path=>navigations.push(path)}}}});
 for(const code of ['subscription_required','subscription_expired','subscription_exhausted']){
  assert.equal(redirectForInvoiceSubscription({error:code}),true);
  assert.equal(redirectForInvoiceSubscription({code}),true);
 }
 for(const body of [{error:'send_api_unavailable'},{code:'predecessor_not_accepted'},null,{error:'subscription_evil',redirectUrl:'https://evil.test'}])
  assert.equal(redirectForInvoiceSubscription(body),false);
 assert.equal(navigations.length,6);
 assert.ok(navigations.every(path=>path==='/customer-plans'));
});

import {createRequire} from 'node:module';
import {isSameLocalOrigin} from '../lib/identity-origin.mjs';
const require=createRequire(import.meta.url);
const {NextRequest}=require('next/server');
const React=require('react');
const id='11111111-1111-1111-1111-111111111111';
for(const action of ['send-and-inquire','cancel-and-inquire','correction-send-and-inquire','sales-return-send-and-inquire']){
 test(`${action} preserves subscription rejection from the backend without retry`,async()=>{
  let calls=0;
  const route=loadTsx(new URL(`../app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/${action}/route.ts`,import.meta.url),{
   globals:{fetch:async()=>{calls++;return Response.json({error:'subscription_exhausted',code:'subscription_exhausted'},{status:409});},AbortSignal},
   modules:{'../../../../../../lib/identity':{ACCESS_COOKIE:'access',identityUiEnabled:()=>true,identityApiOrigin:()=>'http://backend'},
    '../../../../../../lib/identity-origin.mjs':{isSameLocalOrigin}}
  });
  const request=new NextRequest('http://localhost:3000/api/test',{method:'POST',headers:{host:'localhost:3000',origin:'http://localhost:3000',cookie:'access=token'}});
  const response=await route.POST(request,{params:Promise.resolve({taxpayerId:id,invoiceId:id})});
  assert.equal(response.status,409);
  const body=await response.json();assert.equal(body.code??body.error,'subscription_exhausted');
  assert.equal(calls,1);
 });
}

for(const [name,draftSuffix,sendSuffix] of [
 ['invoice-correction-modal.tsx','correction-draft','correction-send-and-inquire'],
 ['invoice-sales-return-modal.tsx','sales-return-draft','sales-return-send-and-inquire']]){
 test(`${name} saves edits then redirects on exhaustion without inquiry or locking the draft`,async()=>{
  const line={description:'item',quantity:2,unitPriceRial:100,discountPerUnitRial:0,vatRatePercent:10,officialIdentifierSnapshot:'1234567890123'};
  const draft={workspaceId:id,predecessorInvoiceId:id,revision:1,lines:[line],originalQuantities:[3],settlementMethod:1,cashAmountRial:220,creditAmountRial:0};
  const state=[draft,[line],1,220,0,false,'',false,'','', 'items'];let index=0;
  const requests=[],navigations=[];
  const component=loadTsx(new URL(`../app/invoices/${name}`,import.meta.url),{
   globals:{AbortSignal,window:{location:{assign:path=>navigations.push(path)}},fetch:async(url,options)=>{
    requests.push({url,options});
    if(options.method==='PUT')return Response.json({code:'draft_saved',draft:{...draft,revision:2}});
    return Response.json({code:'subscription_exhausted'},{status:409});
   }},
   modules:{react:{...React,useEffect:()=>{},useCallback:fn=>fn,useState(initial){const slot=index++;return [state[slot]??initial,value=>{state[slot]=typeof value==='function'?value(state[slot]):value;}];}}}
  });
  const props={invoiceId:id,taxpayerId:id,onClose:()=>assert.fail('must preserve draft'),onChanged:()=>{}};
  const find=(node,predicate)=>{
   if(!node||typeof node!=='object')return null;
   if(Array.isArray(node))return node.map(x=>find(x,predicate)).find(Boolean);
   return predicate(node)?node:find(node.props?.children,predicate);
  };
  const render=()=>{index=0;return component.default(props);};
  const view=render();
  const button=find(view,n=>n.type==='button'&&(String(n.props['aria-label']??'').includes('ارسال')||n.props.className==='identity-primary'&&String(n.props.children).includes('ارسال')));
  assert.ok(button);button.props.onClick();
  const confirmation=find(render(),n=>n.props?.confirmLabel==='تأیید و ارسال');assert.ok(confirmation);
  await confirmation.props.onConfirm();
  assert.deepEqual(navigations,['/customer-plans']);
  assert.equal(requests.length,2);assert.ok(requests[0].url.endsWith('/'+draftSuffix));assert.ok(requests[1].url.endsWith('/'+sendSuffix));
  assert.equal(state[0].revision,2);assert.notEqual(state[14],true);
 });
}
