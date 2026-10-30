import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('Prepared is state 1 while Reserved is state 3',()=>{
 const states=read('src/ArginTaxPayer.Domain/Invoices/InvoiceDispatchContracts.cs');
 assert.match(states,/Prepared = 1/);assert.match(states,/Reserved = 3/);
});
test('arming checks existing signed outbox, no attempts and never calls HTTP',()=>{
 const service=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10DispatchArmService.cs');
 for(const x of ['InvoiceDispatchReadService','state=3','d.state=1',
  'o.event_type=\'invoice.sandbox.legacy.dispatch.requested\'',
  'NOT EXISTS(SELECT 1 FROM invoice_dispatch_attempts','o.processed_at_utc IS NULL',
  'o.payload_json::jsonb','BeginTransactionAsync','CommitAsync'])
  assert.ok(service.includes(x),x);
 assert.ok(!service.includes('HttpClient'));
});
test('one-click backend arms prepared packet before its single send attempt',()=>{
 const ui=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 const route=read('apps/web/app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/arm/route.ts');
 const service=read('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10OneClickSandboxSubmissionService.cs');
 assert.ok(ui.includes('onClick={()=>void oneClickSend()}'));
 assert.ok(ui.includes('/send-and-inquire'));
 assert.ok(service.includes('if(state=="Prepared")'));
 assert.ok(service.indexOf('arm.ArmAsync')<service.indexOf('worker.ProcessInvoiceAsync'));
 assert.equal((service.match(/worker\.ProcessInvoiceAsync/g)||[]).length,1);
 assert.ok(service.includes('if(state is not ("NotPrepared" or "Prepared" or "Reserved"))'));
 assert.ok(route.includes('isSameLocalOrigin'));
 assert.ok(route.includes('authoritySubmitted:false'));
});

// Execute the actual UI callback so duplicate-send guards are behavioral contracts.
import {createRequire} from 'node:module';
import {runInNewContext} from 'node:vm';
const require=createRequire(import.meta.url);
const ts=require('typescript');
function oneClick(overrides={},callback="oneClickSend"){
 const source=read('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 const deferStart=source.indexOf('const deferInquiry=useCallback(');
 const oneClickStart=source.indexOf('const oneClickSend=useCallback(');
 const start=source.indexOf('const '+callback+'=useCallback(');
 const end=source.indexOf(callback==='oneClickSend'?'const readyExisting=':'const inquireByUid=',start);
 const code=(source.slice(deferStart,oneClickStart)+source.slice(start,end))
  .replace('const deferInquiry=','exports.defer=')
  .replace('const '+callback+'=','exports.send=');
 const calls=[];const notices=[];const errors=[];let completed=0;
 const context={exports:{},useCallback:fn=>fn,actionBusy:false,isWorkspace:false,
  dispatchLoading:false,dispatch:{status:'NotPrepared'},taxpayerId:'taxpayer/scope',invoiceId:'invoice/scope',
  inquiryController:{current:null},AbortController,AbortSignal,authorityLabels:{SUCCESS:"موفق",FAILED:"رد شده",PENDING:"در انتظار بررسی",UNKNOWN:"نامشخص"},authoritySequence:{current:0},inquiryDetailsRef:{current:{open:false}},
  setDispatch:()=>{},setAuthority:()=>{},setAuthorityError:()=>{},authorityController:{current:new AbortController()},window:{setTimeout:()=>1,clearTimeout:()=>{}},
  setSendProgress:()=>{},setActionBusy:()=>{},setError:x=>errors.push(x),setNotice:x=>notices.push(typeof x==='function'?x(notices.at(-1)??''):x),
  loadDispatch:async()=>{},refresh:async()=>{},loadReview:async()=>{},onCompleted:()=>completed++,
  fetch:async(url,options)=>{calls.push({url,options});return {ok:true,json:async()=>({status:'Acknowledged',referenceNumber:'REF'})};},
  ...overrides};
 runInNewContext(ts.transpileModule(code,{compilerOptions:{target:ts.ScriptTarget.ES2022}}).outputText,context);
 return {send:context.exports.send,defer:context.exports.defer,calls,notices,errors,completed:()=>completed};
}
test('one-click callback blocks started, uncertain and acknowledged operations',async()=>{
 for(const status of ['Indeterminate','Sending','Acknowledged']){
  const action=oneClick({dispatch:{status}});await action.send();assert.equal(action.calls.length,0,status);
 }
 for(const guard of ['actionBusy','isWorkspace','dispatchLoading']){
  const action=oneClick({[guard]:true});await action.send();assert.equal(action.calls.length,0,guard);
 }
});
test('one-click callback sends one scoped authenticated request and refreshes acknowledged result',async()=>{
 const action=oneClick();await action.send();
 assert.equal(action.calls.length,2);
 assert.equal(action.calls[1].url,'/api/invoice-dispatch/taxpayer%2Fscope/invoice%2Fscope/inquiry-by-reference');
 assert.equal(action.calls[0].url,'/api/invoice-dispatch/taxpayer%2Fscope/invoice%2Fscope/send-and-inquire');
 assert.equal(action.calls[0].options.method,'POST');
 assert.equal(action.calls[0].options.credentials,'same-origin');
 assert.equal(action.calls[0].options.cache,'no-store');
 assert.equal(action.completed(),1);
});
test('uncertain one-click result never retries and never signals completion',async()=>{
 let requests=0;
 const action=oneClick({fetch:async()=>{requests++;return {ok:true,json:async()=>({status:'Blocked',safeCode:'indeterminate'})};}});
 await action.send();assert.equal(requests,1);assert.equal(action.completed(),0);
 assert.ok(action.errors.some(x=>x.includes('ارسال را تکرار نکنید')));
});


const reply=(body,ok=true)=>({ok,status:ok?200:503,json:async()=>body});
const successReport={status:'SUCCESS',errors:[],warnings:[],canPrepareCorrection:false,observedAtUtc:'2026-10-30T08:00:00Z'};
test('receipt is shown before a separate slow inquiry completes',async()=>{
 let finish;let started;const stages=[];const reports=[];
 const pending=new Promise(resolve=>{finish=resolve;});
 const inquiryStarted=new Promise(resolve=>{started=resolve;});
 const action=oneClick({setSendProgress:value=>stages.push(value),setAuthority:value=>reports.push(value),
  fetch:async(url)=>{
   if(url.endsWith('/send-and-inquire'))return reply({status:'Acknowledged',referenceNumber:'REF-123'});
   started();return pending;
  }});
 const sending=action.send();await inquiryStarted;
 assert.equal(stages.at(-1)?.stage,'inquiring');
 assert.ok(action.notices.some(x=>x.includes('REF-123')));
 assert.equal(action.errors.filter(Boolean).length,0);
 finish(reply({safeCode:'inquiry_correlated',referenceNumber:'REF-123',authorityResult:successReport}));
 await sending;
 assert.equal(reports.at(-1)?.result.status,'SUCCESS');
 assert.equal(stages.at(-1),null);
});
test('inquiry timeout preserves receipt, stops waiting, and offers a later inquiry',async()=>{
 const stages=[];const inquiryErrors=[];
 const action=oneClick({setSendProgress:value=>stages.push(value),setAuthorityError:x=>inquiryErrors.push(x),
  fetch:async(url)=>{
   if(url.endsWith('/send-and-inquire'))return reply({status:'Acknowledged',referenceNumber:'REF-123'});
   throw new Error('timeout');
  }});
 await action.send();
 assert.equal(stages.at(-1),null);
 assert.equal(action.errors.filter(Boolean).length,0);
 assert.ok(action.notices.at(-1).includes('REF-123'));
 assert.ok(inquiryErrors.some(x=>x.includes('ارسال موفق')));
 assert.equal(action.completed(),1);
});
test('lost send response reconciles the persisted receipt without submitting again',async()=>{
 const requests=[];
 const action=oneClick({fetch:async(url,options)=>{
  requests.push({url,method:options.method});
  if(url.endsWith('/send-and-inquire'))return reply({error:'send_api_unavailable'},false);
  if(options.method==='GET')return reply({status:'Acknowledged',referenceNumber:'REF-RECOVERED'});
  return reply({safeCode:'inquiry_correlated',referenceNumber:'REF-RECOVERED',authorityResult:successReport});
 }});
 await action.send();
 assert.equal(requests.filter(x=>x.url.endsWith('/send-and-inquire')).length,1);
 assert.equal(requests.filter(x=>x.method==='GET').length,1);
 assert.ok(action.notices.some(x=>x.includes('REF-RECOVERED')));
 assert.equal(action.errors.filter(Boolean).length,0);
});
test('lost response with uncertain persisted state never resends or announces success',async()=>{
 for(const status of ['Sending','Indeterminate','Unavailable']){
  const requests=[];
  const action=oneClick({fetch:async(url,options)=>{
   requests.push(options.method);
   return options.method==='POST'?reply({error:'send_api_unavailable'},false):reply({status});
  }});
  await action.send();
  assert.deepEqual(requests,['POST','GET']);
  assert.equal(action.completed(),0);
  assert.ok(action.errors.some(x=>x.includes('ارسال را تکرار نکنید')));
 }
});

test('deferring inquiry releases waiting while preserving the successful send and ignoring late results',async()=>{
 let finish;let started;let inquirySignal;const stages=[];const reports=[];const busy=[];
 const pending=new Promise(resolve=>{finish=resolve;});
 const inquiryStarted=new Promise(resolve=>{started=resolve;});
 const action=oneClick({setSendProgress:value=>stages.push(value),setAuthority:value=>reports.push(value),setActionBusy:value=>busy.push(value),
  fetch:async(url,options)=>{
   if(url.endsWith('/send-and-inquire'))return reply({status:'Acknowledged',referenceNumber:'REF-LATER'});
   inquirySignal=options.signal;started();return pending;
  }});
 const sending=action.send();await inquiryStarted;
 assert.equal(stages.at(-1)?.stage,'inquiring');
 action.defer();
 assert.equal(inquirySignal.aborted,true);
 assert.equal(stages.at(-1),null);
 assert.equal(busy.at(-1),false);
 assert.ok(action.notices.some(x=>typeof x==='string'&&x.includes('REF-LATER')));
 finish(reply({authorityResult:successReport,referenceNumber:'REF-LATER'}));await sending;
 assert.equal(reports.filter(x=>x?.result).length,0);
 assert.equal(action.errors.filter(Boolean).length,0);
 assert.equal(action.completed(),1);
});

test('manual inquiry can be deferred and a later manual inquiry still receives its result',async()=>{
 let finishOld;let finishNew;let started;let requests=0;const stages=[];const reports=[];
 const pendingOld=new Promise(resolve=>{finishOld=resolve;});
 const pendingNew=new Promise(resolve=>{finishNew=resolve;});
 let requestStarted=new Promise(resolve=>{started=resolve;});
 const action=oneClick({dispatch:{status:'Acknowledged',referenceNumber:'REF-MANUAL'},
  setSendProgress:value=>stages.push(value),setAuthority:value=>reports.push(value),
  fetch:async()=>{requests++;started();return requests===1?pendingOld:pendingNew;}
 },'inquireByReference');
 const old=action.send();await requestStarted;action.defer();
 assert.equal(stages.at(-1),null);
 requestStarted=new Promise(resolve=>{started=resolve;});
 const fresh=action.send();await requestStarted;
 finishOld(reply({authorityResult:{...successReport,status:'FAILED'}}));await old;
 assert.equal(stages.at(-1)?.stage,'inquiring');
 assert.equal(reports.filter(x=>x?.result).length,0);
 finishNew(reply({authorityResult:successReport,referenceNumber:'REF-MANUAL'}));await fresh;
 assert.equal(stages.at(-1),null);
 assert.equal(reports.at(-1)?.result.status,'SUCCESS');
 assert.equal(action.errors.filter(Boolean).length,0);
});
