import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
import {isSameLocalOrigin} from '../lib/identity-origin.mjs';
const require=createRequire(import.meta.url);const React=require('react');const {NextRequest}=require('next/server');
const id='11111111-1111-1111-1111-111111111111';const successor='22222222-2222-2222-2222-222222222222';
function findResume(node){
 if(!node||typeof node!=='object')return null;
 if(Array.isArray(node))return node.map(findResume).find(Boolean);
 if(node.type==='button'&&String(node.props.children).includes('ادامه ارسال همین سند'))return node;
 return findResume(node.props?.children);
}
for(const subject of ['Cancellation','SalesReturn'])test(`${subject}: resuming uses the existing successor ID and never repeats the original action`,async()=>{
 const node={invoiceId:successor,subject,fiscalTaxId:null,issuedAtUtc:'2026-11-15T00:00:00Z',dispatchState:'NotPrepared',authorityReference:null,authorityStatus:null,sentAtUtc:null,canResumeSubmission:true};
 const state=[{hasDispatchAttempt:true,hasChanges:true,nodes:[node]}];let slot=0;let done;const finished=new Promise(r=>done=r);const requests=[];
 const Component=loadTsx(new URL('../app/invoices/invoice-lineage-modal.tsx',import.meta.url),{globals:{AbortSignal,fetch:async(url,options)=>{requests.push({url,options});return Response.json({code:'acknowledged',invoiceId:successor,status:'Acknowledged'});}},modules:{react:{...React,useEffect(){},useRef:v=>({current:v}),useState(initial){const i=slot++;if(!(i in state))state[i]=initial;return [state[i],value=>{state[i]=typeof value==='function'?value(state[i]):value;if(i===3&&value===null)done();}];}}}}).default;
 const view=Component({taxpayerId:id,invoiceId:id,onClose(){}});const button=findResume(view);assert.ok(button);button.props.onClick();await finished;
 assert.equal(requests.length,1);assert.equal(requests[0].url,`/api/invoice-dispatch/${id}/${successor}/resume-unprepared`);assert.equal(requests[0].options.method,'POST');
});
function route(fetch){return loadTsx(new URL('../app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/resume-unprepared/route.ts',import.meta.url),{globals:{fetch,AbortSignal},modules:{
 '../../../../../../lib/identity':{ACCESS_COOKIE:'access',identityUiEnabled:()=>true,identityApiOrigin:()=>'http://backend'},'../../../../../../lib/identity-origin.mjs':{isSameLocalOrigin}
}});}
const request=(origin='http://localhost:3000',cookie='access=token')=>new NextRequest('http://localhost:3000/api/test',{method:'POST',headers:{host:'localhost:3000',origin,cookie}});
const context={params:Promise.resolve({taxpayerId:id,invoiceId:successor})};
test('resume proxy preserves the exact successor and strips unrelated upstream data',async()=>{
 const calls=[];const api=route(async(url,options)=>{calls.push({url,options});return Response.json({code:'acknowledged',invoiceId:successor,status:'Acknowledged',privateKey:'secret'});});
 const response=await api.POST(request(),context);assert.equal(response.status,200);const body=await response.json();assert.equal(body.invoiceId,successor);assert.equal(body.privateKey,undefined);assert.ok(calls[0].url.endsWith(`/${successor}/dispatch/resume-unprepared`));
});
test('resume proxy refuses cross-origin and unauthenticated requests before backend I/O',async()=>{
 let calls=0;const api=route(async()=>{calls++;return Response.json({});});
 assert.equal((await api.POST(request('https://other.invalid'),context)).status,403);
 assert.equal((await api.POST(request('http://localhost:3000',''),context)).status,401);
 assert.equal(calls,0);
});
