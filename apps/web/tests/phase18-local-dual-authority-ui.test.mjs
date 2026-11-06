import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
const require=createRequire(import.meta.url);
const React=require('react');
const {renderToStaticMarkup}=require('react-dom/server');
const {NextRequest}=require('next/server');
const id='11111111-1111-1111-1111-111111111111';
const context={params:Promise.resolve({taxpayerId:id,invoiceId:id})};
const report=environment=>({environment,invoiceState:'Finalized',hasImmutableSnapshot:true,invoiceRevision:1,
 sandboxFiscalMemoryId:environment==='Sandbox'?id:null,fiscalMemoryId:environment==='Production'?id:null,
 readyForLiveSend:false,submissionEnabled:false,signedPacketAvailable:false,fiscalAccepted:false,blockingCodes:[]});
function route(path,payload){
 return loadTsx(new URL('../app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/'+path,import.meta.url),{
  modules:{'../../../../../../lib/identity':{ACCESS_COOKIE:'access',identityApiOrigin:()=>'http://backend',identityUiEnabled:()=>true},
   '../../../../../lib/identity':{ACCESS_COOKIE:'access',identityApiOrigin:()=>'http://backend',identityUiEnabled:()=>true}},
  globals:{AbortSignal,fetch:async()=>new Response(JSON.stringify(payload),{status:200})}
 });
}
const request=()=>new NextRequest('http://localhost:3000/api/test',{headers:{cookie:'access=token'}});
function render(environment,status='NotPrepared',authorityStatus=null){
 let state=0;
 const component=loadTsx(new URL('../app/invoices/invoice-send-readiness-panel.tsx',import.meta.url),{
  modules:{react:{...React,useEffect:()=>{},useCallback:fn=>fn,useRef:value=>({current:value}),
   useState(initial){const i=state++;return [i===0?report(environment):i===6?{status,environment,submissionEnabled:false,fiscalAccepted:false,referenceNumber:'REF'}:
    i===10&&authorityStatus?{scope:id+'/'+id,referenceNumber:'REF',result:{status:authorityStatus,errors:[],warnings:[],canPrepareCorrection:false,observedAtUtc:'2026-11-05T00:00:00Z'}}:initial,()=>{}];}}}
 });
 return renderToStaticMarkup(React.createElement(component.default,{invoiceId:id,taxpayerId:id,isWorkspace:false,hideHistory:true}));
}
test('readiness proxy preserves each verified environment and its memory binding',async()=>{
 for(const environment of ['Production','Sandbox']){
  const response=await route('readiness/route.ts',report(environment)).GET(request(),context);
  assert.equal(response.status,200,environment);
  const body=await response.json();
  assert.equal(body.environment,environment);
  if(environment==='Production')assert.equal(body.fiscalMemoryId,id);
 }
});
test('readiness proxy rejects Production without a real binding and mixed memory environments',async()=>{
 for(const payload of [{...report('Production'),fiscalMemoryId:null},
  {...report('Production'),sandboxFiscalMemoryId:id},{...report('Sandbox'),fiscalMemoryId:id},
  {...report('Production'),fiscalMemoryId:'bad-id'}]){
  assert.equal((await route('readiness/route.ts',payload).GET(request(),context)).status,502);
 }
});
test('dispatch proxy preserves Production instead of relabeling it Mock',async()=>{
 const response=await route('route.ts',{environment:'Production',status:'Acknowledged',submissionEnabled:false,fiscalAccepted:false}).GET(request(),context);
 assert.equal((await response.json()).environment,'Production');
});
test('Production and Sandbox both expose the owner-driven one-click action',()=>{
 for(const environment of ['Production','Sandbox']){
  const html=render(environment);
  assert.ok(html.includes('ارسال به سامانه مؤدیان و استعلام'),environment);
 }
 assert.ok(!render('Unverified').includes('ارسال به سامانه مؤدیان و استعلام'));
});
test('Production receipt exposes reference inquiry without Sandbox labels',()=>{
 const html=render('Production','Acknowledged','SUCCESS');
 assert.ok(html.includes('استعلام شماره رهگیری'));
 assert.ok(html.includes('Production'));
 assert.ok(!html.includes('نتیجه موفق در Sandbox'));
 assert.ok(!html.includes('این نتیجه مربوط به Sandbox است'));
 assert.ok(!html.includes('وضعیت ارسال Sandbox'));
 assert.ok(!html.includes('ارسال به سامانه مؤدیان و استعلام'));
});
test('Sandbox success retains its explicit test-environment explanation',()=>{
 const html=render('Sandbox','Acknowledged','SUCCESS');
 assert.ok(html.includes('نتیجه موفق در Sandbox'));
 assert.ok(html.includes('این نتیجه مربوط به Sandbox است'));
});
test('invoice memory options make Production selectable and unclassified metadata unavailable',()=>{
 let state=0;
 const component=loadTsx(new URL('../app/invoices/workspace.tsx',import.meta.url),{
  modules:{react:{...React,useEffect:()=>{},useCallback:fn=>fn,useMemo:fn=>fn(),useRef:value=>({current:value}),
   useState(initial){const i=state++;const values={0:[{id,displayName:'Seller',status:1}],1:id,5:false,10:true,
    21:{taxpayer:{id,displayName:'Seller',normalizedIdentifier:'14012072010'},memories:[
     {id:'production-memory',memoryIdentifier:'REAL01',status:2,authorityEnvironment:1},
     {id:'old-memory',memoryIdentifier:'OLD001',status:2,authorityEnvironment:null}],sandboxMemories:[{id:'sandbox-memory',memoryIdentifier:'TEST01',environment:'Sandbox'}]}};
    return [Object.hasOwn(values,i)?values[i]:typeof initial==='function'?initial():initial,()=>{}];}}}
 });
 const html=renderToStaticMarkup(React.createElement(component.default,{actorId:id,isAdmin:false}));
 assert.match(html,/<option value="production-memory">REAL01 — Production/);
 assert.match(html,/<option value="old-memory" disabled="">OLD001/);
 assert.match(html,/<option value="sandbox:sandbox-memory">TEST01 — Sandbox/);
});

test('both environments retain signed-packet and blocker-free readiness gates',async()=>{
 for(const environment of ['Production','Sandbox']){
  const ready={...report(environment),readyForLiveSend:true,submissionEnabled:true,signedPacketAvailable:true};
  assert.equal((await route('readiness/route.ts',ready).GET(request(),context)).status,200);
  for(const change of [{signedPacketAvailable:false},{blockingCodes:['invoice.transport.disabled']},
   {readyForLiveSend:false},{submissionEnabled:false}]){
   assert.equal((await route('readiness/route.ts',{...ready,...change}).GET(request(),context)).status,502);
  }
 }
});

test('modern Sandbox keeps its regular fiscalMemoryId without a legacy Sandbox binding',async()=>{
 const modern={...report('Sandbox'),fiscalMemoryId:id,sandboxFiscalMemoryId:null};
 const response=await route('readiness/route.ts',modern).GET(request(),context);
 assert.equal(response.status,200);const body=await response.json();
 assert.equal(body.environment,'Sandbox');assert.equal(body.fiscalMemoryId,id);assert.equal(body.sandboxFiscalMemoryId,null);
});
