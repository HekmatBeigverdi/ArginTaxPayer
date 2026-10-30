import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync,existsSync} from 'node:fs';
import {createRequire} from 'node:module';
import {runInNewContext} from 'node:vm';
import ts from 'typescript';
import {isSameLocalOrigin} from '../lib/identity-origin.mjs';
import {loadTsx} from './helpers/load-tsx.mjs';
const require=createRequire(import.meta.url);
const {NextRequest}=require('next/server');
const id='11111111-1111-1111-1111-111111111111';
const context={params:Promise.resolve({taxpayerId:id,invoiceId:id})};
const report={status:'FAILED',errors:[{code:'E1',message:'<script>unsafe</script>'}],warnings:[],canPrepareCorrection:true,observedAtUtc:'2026-10-26T12:00:00Z'};
const payload=()=>({safeCode:'inquiry_failed',referenceNumber:'REF_1',fiscalAccepted:false,authoritySubmitted:false,authorityResult:structuredClone(report)});
function route(name,body,check=()=>{},status=200){
 const path=new URL('../app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/'+name+'/route.ts',import.meta.url);
 assert.ok(existsSync(path),'route exists');
 const exports={};
 runInNewContext(ts.transpileModule(readFileSync(path,'utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS,target:ts.ScriptTarget.ES2022}}).outputText,{
 exports,AbortSignal,TextEncoder,Uint8Array,Response,
 fetch:async(url,options)=>{check(url,options);return new Response(typeof body==='string'?body:JSON.stringify(body),{status});},
 require(name){
 if(name.endsWith('/lib/identity'))return {ACCESS_COOKIE:'access',identityUiEnabled:()=>true,identityApiOrigin:()=>'http://backend'};
 if(name.endsWith('/lib/identity-origin.mjs'))return {isSameLocalOrigin};
 return require(name);
 }});
 return exports;
}
function request(method='POST',{token=true,origin='http://localhost:3000',body}={}){
 return new NextRequest('http://localhost:3000/api/test',{method,headers:{host:'localhost:3000',origin,...(token?{cookie:'access=token'}:{})},...(body!==undefined?{body}:{})});
}
test('GET persisted and POST inquiry preserve bounded report and safe flags',async()=>{
 for(const method of ['GET','POST']){
 const r=route('inquiry-by-reference',payload(),(url,options)=>{
 assert.ok(url.endsWith('/dispatch/inquiry-by-reference'));assert.equal(options.method,method);
 assert.equal(options.headers.Authorization,'Bearer token');});
 assert.equal(typeof r[method],'function');
 const response=await r[method](request(method),context);
 assert.equal(response.status,200);assert.equal(response.headers.get('cache-control'),'no-store');
 assert.deepEqual(await response.json(),payload());
 }
 assert.equal((await route('inquiry-by-reference',payload()).POST(request('POST',{body:''}),context)).status,200);
});
test('inquiry rejects malformed reports and oversized responses',async()=>{
 for(const change of [
 p=>p.authorityResult.status='OTHER',p=>p.authorityResult.errors=null,
 p=>p.authorityResult.errors=[null],p=>p.authorityResult.warnings=[{code:1,message:'x'}],
 p=>p.authorityResult.errors=Array(101).fill({code:'E',message:'x'}),
 p=>p.authorityResult.errors[0].code='x'.repeat(81),
 p=>p.authorityResult.errors[0].message='x'.repeat(1001),
 p=>p.authorityResult.canPrepareCorrection='true',p=>p.authorityResult.observedAtUtc=42,
 p=>p.fiscalAccepted=true,p=>p.authoritySubmitted=true]){
 const p=payload();change(p);
 assert.equal((await route('inquiry-by-reference',p).POST(request(),context)).status,502);
 }
 assert.equal((await route('inquiry-by-reference',' '.repeat(300001)).POST(request(),context)).status,502);
 assert.equal((await route('inquiry-by-reference','{').POST(request(),context)).status,502);
});
test('nullable reports and maximum allowed lists are supported',async()=>{
 for(const authorityResult of [null,{...report,errors:Array(100).fill({code:'x'.repeat(80),message:'x'.repeat(1000)}),warnings:Array(100).fill({code:'W',message:'warning'})}]){
 assert.equal((await route('inquiry-by-reference',{...payload(),authorityResult}).POST(request(),context)).status,200);
 }
});
test('correction validates UUID and safe flag, forwards auth and refuses cross-origin or payload',async()=>{
 const r=route('correction-draft',{workspaceId:id,authoritySubmitted:false},(url,options)=>{
 assert.ok(url.endsWith('/dispatch/correction-draft'));assert.equal(options.method,'POST');
 assert.equal(options.headers.Authorization,'Bearer token');});
 assert.deepEqual(await (await r.POST(request(),context)).json(),{workspaceId:id,authoritySubmitted:false,issueDateAdjusted:false});
 assert.equal((await r.POST(request('POST',{body:''}),context)).status,200);
 assert.equal((await r.POST(request('POST',{origin:'https://evil.test'}),context)).status,403);
 assert.equal((await r.POST(request('POST',{token:false}),context)).status,401);
 assert.equal((await r.POST(request('POST',{body:'{}'}),context)).status,400);
 for(const p of [{workspaceId:'javascript:bad',authoritySubmitted:false},{workspaceId:id,authoritySubmitted:true},
  {workspaceId:id,authoritySubmitted:false,issueDateAdjusted:'true'},null]){
 assert.equal((await route('correction-draft',p).POST(request(),context)).status,502);
 }
});
test('UI loads persisted report, renders text and offers only explicitly authorized failed correction',()=>{
 const ui=readFileSync(new URL('../app/invoices/invoice-send-readiness-panel.tsx',import.meta.url),'utf8');
 assert.match(ui,/loadAuthorityReport/);
 assert.ok(ui.includes('authorityResult.status==="FAILED"&&authorityResult.canPrepareCorrection'));
 assert.ok(ui.includes('/invoices?workspaceId='));
 assert.ok(ui.includes('{item.message}'));
 assert.ok(!ui.includes('dangerouslySetInnerHTML'));
 const workspace=readFileSync(new URL('../app/invoices/workspace.tsx',import.meta.url),'utf8');
 assert.ok(workspace.includes('get("workspaceId")'));
 assert.ok(workspace.includes('editDraft({id:workspaceId})'));
});
test('inquiry auth, upstream access denial, unknown states and projected fields remain safe',async()=>{
 for(const method of ['GET','POST']){
 const r=route('inquiry-by-reference',payload());
 assert.equal((await r[method](request(method,{token:false}),context)).status,401);
 assert.equal((await r[method](request(method,{origin:'https://evil.test'}),context)).status,403);
 for(const status of [401,403,404]){
 assert.equal((await route('inquiry-by-reference',{},()=>{},status)[method](request(method),context)).status,status);
 }
 }
 for(const status of ['PENDING','UNKNOWN','SUCCESS']){
 const p=payload();p.authorityResult.status=status;p.authorityResult.secret='private';
 const response=await route('inquiry-by-reference',p).GET(request('GET'),context);
 const body=await response.json();
 assert.equal(body.authorityResult.canPrepareCorrection,false);
 assert.equal(body.authorityResult.secret,undefined);
 assert.equal(body.fiscalAccepted,false);
 }
});
test('rendered authority messages are escaped and only failed authorized reports expose correction',()=>{
 const React=require('react');
 const {renderToStaticMarkup}=require('react-dom/server');
 for(const status of ['FAILED','SUCCESS','PENDING','UNKNOWN']){
 for(const allowed of [true,false]){
 let stateIndex=0;
 const component=loadTsx(new URL('../app/invoices/invoice-send-readiness-panel.tsx',import.meta.url),{modules:{react:{...React,
 useState(initial){const i=stateIndex++;return [i===10?{scope:id+'/'+id,result:{...report,status,canPrepareCorrection:allowed},referenceNumber:'REF_1'}:initial,()=>{}];},
 useCallback:fn=>fn,useEffect:()=>{},useRef:value=>({current:value})}}});
 const html=renderToStaticMarkup(React.createElement(component.default,{invoiceId:id,taxpayerId:id,isWorkspace:false,hideHistory:true}));
 assert.ok(html.includes('&lt;script&gt;unsafe&lt;/script&gt;'));
 assert.ok(!html.includes('<script>unsafe</script>'));
 assert.equal(html.includes('بازگشت به پیش‌نویس اصلاحی'),status==='FAILED'&&allowed);
 if(status==='PENDING'||status==='UNKNOWN')assert.ok(html.includes('ارسال را تکرار نکنید'));
 }
 }
});

test('correction preserves the date-adjustment flag while projecting private fields away',async()=>{
 for(const adjusted of [true,false]){
  const response=await route('correction-draft',{
   workspaceId:id,authoritySubmitted:false,issueDateAdjusted:adjusted,privateKey:'must-not-leak'
  }).POST(request(),context);
  assert.equal(response.status,200);
  assert.deepEqual(await response.json(),{workspaceId:id,authoritySubmitted:false,issueDateAdjusted:adjusted});
 }
});
