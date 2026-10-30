import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
import {isSameLocalOrigin} from '../lib/identity-origin.mjs';

const require=createRequire(import.meta.url);
const {NextRequest}=require('next/server');
const id='11111111-1111-1111-1111-111111111111';
const context={params:Promise.resolve({taxpayerId:id,invoiceId:id})};
const routeFile=new URL('../app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/correction-send-and-inquire/route.ts',import.meta.url);

function loadRoute(fetch){
 return loadTsx(routeFile,{
  globals:{fetch,AbortSignal},
  modules:{
   '../../../../../../lib/identity':{
    ACCESS_COOKIE:'access',identityUiEnabled:()=>true,identityApiOrigin:()=>'http://backend'
   },
   '../../../../../../lib/identity-origin.mjs':{isSameLocalOrigin}
  }
 });
}
function request({token=true,origin='http://localhost:3000',body}={}){
 return new NextRequest('http://localhost:3000/api/test',{
  method:'POST',headers:{host:'localhost:3000',origin,...(token?{cookie:'access=token'}:{})},
  ...(body===undefined?{}:{body})
 });
}

test('correction send forwards once to the submit endpoint and returns the receipt',async()=>{
 const calls=[];
 const route=loadRoute(async(url,options)=>{
  calls.push({url,options});
  return Response.json({decision:0,code:'acknowledged',invoiceId:id,status:'Acknowledged',
   referenceNumber:'REF-1',inquiryCode:null,privateField:'not for the browser'});
 });
 const response=await route.POST(request(),context);
 assert.equal(response.status,200);
 assert.equal(calls.length,1);
 assert.equal(calls[0].url,`http://backend/api/dev/taxpayers/${id}/invoices/${id}/dispatch/correction-send-and-inquire`);
 assert.equal(calls[0].options.headers.Authorization,'Bearer token');
 assert.equal(calls[0].options.method,'POST');
 assert.equal(calls[0].options.cache,'no-store');
 assert.equal(response.headers.get('cache-control'),'no-store');
 assert.deepEqual(await response.json(),{code:'acknowledged',invoiceId:id,status:'Acknowledged',referenceNumber:'REF-1',inquiryCode:null});
});

test('correction send denies missing login, foreign origin and bodies before calling backend',async()=>{
 let calls=0;
 const route=loadRoute(async()=>{calls++;throw Error('must not reach backend');});
 for(const [options,status] of [[{token:false},401],[{origin:'https://evil.test'},403],[{body:'{}'},400]]){
  assert.equal((await route.POST(request(options),context)).status,status);
 }
 assert.equal(calls,0);
});

test('correction send preserves created invoice identity on backend conflict',async()=>{
 const route=loadRoute(async()=>Response.json({decision:4,code:'dispatch_requires_reconciliation',invoiceId:id,status:'Indeterminate'},{status:409}));
 const response=await route.POST(request(),context);
 assert.equal(response.status,409);
 assert.deepEqual(await response.json(),{code:'dispatch_requires_reconciliation',invoiceId:id,status:'Indeterminate',referenceNumber:null,inquiryCode:null});
});

test('correction send rejects malformed upstream responses and does not retry network errors',async()=>{
 for(const body of [null,[],{},'{', ' '.repeat(4097)]){
  const route=loadRoute(async()=>new Response(typeof body==='string'?body:JSON.stringify(body)));
  assert.equal((await route.POST(request(),context)).status,502);
 }
 let calls=0;
 const route=loadRoute(async()=>{calls++;throw Error('connection lost');});
 const response=await route.POST(request(),context);
 assert.equal(response.status,503);
 assert.equal(calls,1);
 assert.deepEqual(await response.json(),{error:'correction_api_unavailable'});
});
