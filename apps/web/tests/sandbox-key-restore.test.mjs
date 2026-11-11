import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
const require=createRequire(import.meta.url);const {NextRequest}=require('next/server');
const id='11111111-1111-1111-1111-111111111111';
const memoryId='22222222-2222-2222-2222-222222222222';
const payload={memoryId,privateKeyPem:'-----BEGIN PRIVATE KEY-----\n'+'a'.repeat(200),expectedRevision:3,acceptServerCustody:true};
function harness(){const calls=[];return {calls,route:loadTsx(new URL('../app/api/sandbox-memory/[taxpayerId]/private-key/restore/route.ts',import.meta.url),{
 modules:{'../../../../../../lib/identity':{ACCESS_COOKIE:'access',identityApiOrigin:()=>'http://backend',identityUiEnabled:()=>true}},
 globals:{AbortSignal,TextDecoder,fetch:async(url,options)=>{calls.push({url,options:{...options,body:new TextDecoder().decode(options.body)}});return new Response(JSON.stringify({environment:'Sandbox',data:{keyCustodyReady:true,revision:4,privateKeyPem:'secret'},privateKeyPem:'secret'}),{status:200});}}
})};}
const request=(body,origin='http://localhost:3000')=>new NextRequest('http://localhost:3000/api/test',{method:'POST',headers:{host:'localhost:3000',origin,cookie:'access=token','content-type':'application/json'},body:JSON.stringify(body)});
test('Sandbox restore forwards only explicit same-key recovery inputs and drops upstream secrets',async()=>{
 const h=harness();const response=await h.route.POST(request(payload),{params:Promise.resolve({taxpayerId:id})});
 assert.equal(response.status,200);const body=await response.json();assert.equal(body.keyCustodyReady,true);assert.equal(body.revision,4);
 assert.equal(JSON.stringify(body).includes('secret'),false);assert.equal(h.calls[0].url,`http://backend/api/dev/taxpayers/${id}/sandbox-memory/private-key/restore`);
 assert.deepEqual(JSON.parse(h.calls[0].options.body),payload);
});
test('restore rejects cross-origin, absent consent, stale/malformed revision and extra fields before upstream I/O',async()=>{
 for(const [body,origin,status] of [[payload,'https://evil.example',403],[{...payload,acceptServerCustody:false},undefined,400],[{...payload,expectedRevision:0},undefined,400],[{...payload,other:'x'},undefined,400]]){
  const h=harness();const response=await h.route.POST(request(body,origin),{params:Promise.resolve({taxpayerId:id})});assert.equal(response.status,status);assert.equal(h.calls.length,0);
 }
});
