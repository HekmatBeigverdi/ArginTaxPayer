import test from 'node:test';
import assert from 'node:assert/strict';
import {loadTsx} from './helpers/load-tsx.mjs';

function helper(fetch){
 return loadTsx(new URL('../lib/session-fetch.ts',import.meta.url),{globals:{fetch}}).fetchWithSessionRefresh;
}
const url='/api/invoice-dispatch/taxpayer/invoice/correction-draft';
const input={method:'PUT',body:JSON.stringify({expectedRevision:3,lines:[{quantity:2,unitPriceRial:50,discountPerUnitRial:0}]})};

test('expired access cookie refreshes the session and saves the same draft values',async()=>{
 const requests=[];let calls=0;
 const fetch=helper(async(path,options)=>{
  requests.push({path,options});calls++;
  return new Response(null,{status:calls===1?401:200});
 });
 const response=await fetch(url,input);
 assert.equal(response.status,200);
 assert.deepEqual(requests.map(x=>x.path),[url,'/api/identity/refresh',url]);
 assert.equal(requests[1].options.method,'POST');
 assert.equal(requests[1].options.credentials,'same-origin');
 assert.equal(requests[2].options.body,input.body);
 assert.equal(requests[2].options.method,'PUT');
});

test('concurrent expired draft requests share one refresh to avoid token replay',async()=>{
 let release;let refreshes=0;let attempts=0;
 const refreshed=new Promise(resolve=>{release=resolve;});
 const fetch=helper(async(path)=>{
  if(path==='/api/identity/refresh'){refreshes++;await refreshed;return new Response(null,{status:200});}
  attempts++;return new Response(null,{status:attempts<=2?401:200});
 });
 const first=fetch(url,input);const second=fetch(url,input);
 await new Promise(resolve=>setImmediate(resolve));
 assert.equal(refreshes,1);release();
 assert.deepEqual((await Promise.all([first,second])).map(x=>x.status),[200,200]);
});

test('revoked sessions stop at refresh failure and authorization denials never retry',async()=>{
 for(const status of [401,403,409,500]){
  const requests=[];
  const fetch=helper(async(path)=>{requests.push(path);return new Response(null,{status:path==='/api/identity/refresh'?401:status});});
  const response=await fetch(url,input);
  assert.equal(response.status,status);
  assert.deepEqual(requests,status===401?[url,'/api/identity/refresh']:[url]);
 }
});

test('a repeated authentication failure is returned without a retry loop',async()=>{
 const requests=[];
 const fetch=helper(async(path)=>{requests.push(path);return new Response(null,{status:path==='/api/identity/refresh'?200:401});});
 assert.equal((await fetch(url,input)).status,401);
 assert.equal(requests.length,3);
});
