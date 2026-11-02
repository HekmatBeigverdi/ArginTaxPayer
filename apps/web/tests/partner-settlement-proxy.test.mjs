import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
const {NextRequest}=createRequire(import.meta.url)('next/server');
const partner='11111111-1111-1111-1111-111111111111';
const batch='22222222-2222-2222-2222-222222222222';
function load(roles,fetch){return loadTsx(new URL('../app/api/phase12/[...parts]/route.ts',import.meta.url),{
 globals:{fetch,AbortSignal,URLSearchParams},modules:{
  'next/headers':{cookies:async()=>({get:()=>({value:'opaque-token'})})},
  '../../../../lib/identity':{ACCESS_COOKIE:'access',identityUiEnabled:()=>true,
   currentIdentity:async()=>({roles}),identityApiOrigin:()=>'http://backend'}
 }
});}
const context=action=>({params:Promise.resolve({parts:['subscriptions','settlements',action]})});
test('settlement history and snapshots forward only allowed query keys and retain session identity',async()=>{
 for(const [action,query,expected] of [
  ['batches',`partnerUserId=${partner}&page=2&arbitrary=secret`,`page=2&partnerUserId=${partner}`],
  ['print',`batchId=${batch}&arbitrary=secret`,`batchId=${batch}`]
 ]){
  const calls=[];const route=load(['Level2'],async(url,options)=>{calls.push({url,options});return Response.json({items:[]});});
  const response=await route.GET(new NextRequest(`http://localhost/api/phase12/subscriptions/settlements/${action}?${query}`),context(action));
  assert.equal(response.status,200);
  assert.equal(calls[0].url,`http://backend/api/dev/subscriptions/settlements/${action}?${expected}`);
  assert.equal(calls[0].options.headers.Authorization,'Bearer opaque-token');
 }
});
test('only Level1 may record a batch, approve expense or configure rates',async()=>{
 for(const [action,method] of [['batches','POST'],['expenses','POST'],['terms','PUT']]){
  let calls=0;
  const fetch=async()=>{calls++;return Response.json({batchId:batch});};
  const make=()=>new NextRequest(`http://localhost/api/phase12/subscriptions/settlements/${action}`,{
   method,headers:{origin:'http://localhost','Content-Type':'application/json'},body:JSON.stringify({partnerUserId:partner})
  });
  assert.equal((await load(['Level2'],fetch)[method](make(),context(action))).status,403);
  assert.equal(calls,0);
  assert.equal((await load(['Level1'],fetch)[method](make(),context(action))).status,200);
  assert.equal(calls,1);
 }
});
