import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
import {isSameLocalOrigin} from '../lib/identity-origin.mjs';
const require=createRequire(import.meta.url);
const {NextRequest}=require('next/server');
const id='11111111-1111-1111-1111-111111111111';
const context={params:Promise.resolve({taxpayerId:id,invoiceId:id})};
const line={description:'item',quantity:1,unitPriceRial:100,discountPerUnitRial:0,vatRatePercent:10,officialIdentifierSnapshot:'1234567890123'};
const draft={workspaceId:id,predecessorInvoiceId:id,revision:1,lines:[line],invoiceType:2,settlementMethod:0,cashAmountRial:0,creditAmountRial:0,originalQuantities:[2]};
const request=(method,body)=>new NextRequest('http://localhost:3000/api/test',{method,headers:{host:'localhost:3000',origin:'http://localhost:3000',cookie:'access=token'},...(body===undefined?{}:{body:JSON.stringify(body)})});
for(const operation of ['correction','sales-return']){
 function route(fetch){return loadTsx(new URL(`../app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/${operation}-draft/route.ts`,import.meta.url),{globals:{fetch,AbortSignal},modules:{
  '../../../../../../lib/identity':{ACCESS_COOKIE:'access',identityUiEnabled:()=>true,identityApiOrigin:()=>'http://backend'},
  '../../../../../../lib/identity-origin.mjs':{isSameLocalOrigin}
 }});}
 test(`${operation}: Type2 draft metadata and empty settlement survive open, save and resume`,async()=>{
  const calls=[];const api=route(async(url,options)=>{calls.push(options);return Response.json({code:'draft_loaded',draft});});
  for(const method of ['POST','GET','PUT']){
   const body=method==='PUT'?{expectedRevision:1,lines:[{quantity:1,unitPriceRial:100,discountPerUnitRial:0}],settlementMethod:0,cashAmountRial:0,creditAmountRial:0}:undefined;
   const response=await api[method](request(method,body),context);
   assert.equal(response.status,200);
   const data=await response.json();assert.equal(data.draft.invoiceType,2);assert.equal(data.draft.settlementMethod,0);
  }
  const saved=JSON.parse(calls[2].body);assert.equal(saved.settlementMethod,0);assert.equal(saved.cashAmountRial,0);assert.equal(saved.creditAmountRial,0);
 });
 test(`${operation}: malformed invoice type and nonempty Type2 settlement are rejected from upstream`,async()=>{
  for(const change of [{invoiceType:3},{settlementMethod:1},{cashAmountRial:10},{creditAmountRial:10}]){
   const api=route(async()=>Response.json({code:'draft_loaded',draft:{...draft,...change}}));
   assert.equal((await api.GET(request('GET'),context)).status,502);
  }
 });
}
