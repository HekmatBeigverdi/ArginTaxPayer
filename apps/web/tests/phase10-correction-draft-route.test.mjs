import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
import {isSameLocalOrigin} from '../lib/identity-origin.mjs';
const require=createRequire(import.meta.url);
const {NextRequest}=require('next/server');
const id='11111111-1111-1111-1111-111111111111';
const context={params:Promise.resolve({taxpayerId:id,invoiceId:id})};
const routeFile=new URL('../app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/correction-draft/route.ts',import.meta.url);
const line={description:'item',quantity:2,unitPriceRial:100,discountPerUnitRial:0,vatRatePercent:10,officialIdentifierSnapshot:'1234567890123'};
const draft={workspaceId:id,predecessorInvoiceId:id,revision:1,lines:[line],settlementMethod:1,cashAmountRial:220,creditAmountRial:0};
function route(fetch){return loadTsx(routeFile,{globals:{fetch,AbortSignal},modules:{
 '../../../../../../lib/identity':{ACCESS_COOKIE:'access',identityUiEnabled:()=>true,identityApiOrigin:()=>'http://backend'},
 '../../../../../../lib/identity-origin.mjs':{isSameLocalOrigin}
}});}
function request(method,body,headers={}){return new NextRequest('http://localhost:3000/api/test',{method,
 headers:{host:'localhost:3000',origin:'http://localhost:3000',cookie:'access=token',...headers},
 ...(body===undefined?{}:{body:JSON.stringify(body)})});}

test('opening and resuming a correction preserve the editable draft contract',async()=>{
 const calls=[];
 const api=route(async(url,options)=>{calls.push({url,options});return Response.json({code:'draft_loaded',draft:{...draft,privateKey:'secret'},privateKey:'secret'});});
 for(const method of ['POST','GET']){
  const response=await api[method](request(method),context);
  assert.equal(response.status,200);
  const data=await response.json();assert.equal(data.draft.workspaceId,id);assert.deepEqual(data.draft.lines,[line]);
  assert.equal(data.privateKey,undefined);assert.equal(data.draft.privateKey,undefined);
 }
 assert.equal(calls.length,2);
 for(const [i,method] of ['POST','GET'].entries()){
  assert.ok(calls[i].url.endsWith('/dispatch/correction-draft'));
  assert.equal(calls[i].options.method,method);assert.equal(calls[i].options.headers.Authorization,'Bearer token');
 }
});

test('saving forwards editable values and revision, while discard uses DELETE',async()=>{
 const calls=[];
 const api=route(async(url,options)=>{calls.push(options);return Response.json({code:'draft_saved',draft});});
 const body={expectedRevision:1,lines:[{quantity:2,unitPriceRial:150,discountPerUnitRial:0}],settlementMethod:1,cashAmountRial:330,creditAmountRial:0,
  reductionCorrectsOriginalMistake:false,reductionEvidenceReference:null};
 assert.equal((await api.PUT(request('PUT',{...body,ownerId:'untrusted'}),context)).status,200);
 assert.deepEqual(JSON.parse(calls[0].body),body);
 assert.equal(calls[0].method,'PUT');
 assert.equal((await api.DELETE(request('DELETE'),context)).status,200);
 assert.equal(calls[1].method,'DELETE');assert.equal(calls[1].body,undefined);
});

test('draft errors preserve backend conflict codes and reject malformed successful drafts',async()=>{
 const conflict=route(async()=>Response.json({code:'stale_revision'},{status:409}));
 assert.equal((await conflict.POST(request('POST'),context)).status,409);
 assert.equal((await (await conflict.POST(request('POST'),context)).json()).code,'stale_revision');
 const invalid=route(async()=>Response.json({code:'draft_loaded',draft:{...draft,revision:'invalid'}}));
 assert.equal((await invalid.POST(request('POST'),context)).status,502);
});

test('every draft method requires login and a trusted origin before backend access',async()=>{
 let calls=0;
 const api=route(async()=>{calls++;throw Error('unexpected backend request');});
 for(const method of ['GET','POST','PUT','DELETE']){
  assert.equal((await api[method](request(method,undefined,{cookie:''}),context)).status,401);
  assert.equal((await api[method](request(method,undefined,{origin:'https://evil.test'}),context)).status,403);
 }
 assert.equal(calls,0);
});

test('invalid draft save input and bodies on non-save operations are rejected locally',async()=>{
 let calls=0;
 const api=route(async()=>{calls++;throw Error('unexpected backend request');});
 for(const body of [null,{}, {expectedRevision:-1,lines:[]}]){
  assert.equal((await api.PUT(request('PUT',body),context)).status,400);
 }
 for(const method of ['POST','DELETE']){
  assert.equal((await api[method](request(method,{}),context)).status,400);
 }
 assert.equal(calls,0);
});
