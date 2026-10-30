import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
const require=createRequire(import.meta.url);
const React=require('react');
const id='11111111-1111-1111-1111-111111111111';
const line={description:'item',quantity:2,unitPriceRial:100,discountPerUnitRial:0,vatRatePercent:10,officialIdentifierSnapshot:'1234567890123'};
const draft={workspaceId:id,predecessorInvoiceId:id,revision:1,lines:[line],settlementMethod:1,cashAmountRial:220,creditAmountRial:0};
function findSend(element){
 if(!element||typeof element!=='object')return null;
 if(Array.isArray(element))return element.map(findSend).find(Boolean);
 if(element.type==='button'&&(element.props['aria-label']==='ذخیره تغییرات و ارسال اصلاحیه'||(element.props.className==='identity-primary'&&String(element.props.children).includes('ارسال'))))return element;
 return findSend(element.props?.children);
}
async function send({saveStatus=200,sendStatus=200,sendCode='acknowledged',inquiryStatus=200,cancel=false,sendResult={}}={}){
 const state=[draft,[{quantity:2,unitPriceRial:150,discountPerUnitRial:0}],1,220,0,false,'',false,'','', 'items'];
 let index=0;let finish;let changed=0;const requests=[];
 const finished=new Promise(resolve=>{finish=resolve;});
 const component=loadTsx(new URL('../app/invoices/invoice-correction-modal.tsx',import.meta.url),{
  globals:{AbortSignal,window:{confirm:()=>true},fetch:async(url,options)=>{
   requests.push({url,options});
   if(url.endsWith('/inquiry-by-reference'))return Response.json({authorityResult:{status:'SUCCESS'},safeCode:'inquiry_success'},{status:inquiryStatus});
   if(options.method==='PUT')return Response.json(saveStatus===200?{code:'draft_saved',draft:{...draft,revision:2,lines:[{...line,unitPriceRial:150}]}}:{code:'stale_revision'},{status:saveStatus});
   return Response.json({code:sendCode,...(sendStatus===200?{invoiceId:id,status:'Acknowledged',referenceNumber:'REF-1'}:{}),...sendResult},{status:sendStatus});
  }},
  modules:{react:{...React,useEffect:()=>{},useCallback:fn=>fn,useState(initial){
   const slot=index++;return [state[slot]??initial,value=>{
    state[slot]=typeof value==='function'?value(state[slot]):value;
    if(slot===7&&value===false)finish();
   }];
  }}}
 });
 const props={invoiceId:id,taxpayerId:id,onClose:()=>assert.fail('must keep editor open'),onChanged:()=>changed++};
 const render=()=>{index=0;return component.default(props);};
 const view=component.default({invoiceId:id,taxpayerId:id,onClose:()=>assert.fail('must keep editor open'),onChanged:()=>changed++});
 findSend(view).props.onClick();
 assert.equal(requests.length,0,'confirmation must precede persistence and submission');
 const confirmation=findConfirmation(render());assert.ok(confirmation,'action toast is shown');
 if(cancel){confirmation.props.onCancel();return {state,requests,changed};}
 await confirmation.props.onConfirm();await finished;
 return {state,requests,changed};
}

test('sending an edited correction saves the current form before submitting once',{timeout:3000},async()=>{
 const result=await send();
 assert.deepEqual(result.requests.map(x=>x.options.method),['PUT','POST','POST']);
 assert.ok(result.requests[0].url.endsWith('/correction-draft'));
 const saved=JSON.parse(result.requests[0].options.body);
 assert.equal(saved.expectedRevision,1);assert.equal(saved.lines[0].unitPriceRial,150);
 assert.ok(result.requests[1].url.endsWith('/correction-send-and-inquire'));
 assert.ok(result.changed>=1);assert.equal(result.state[8],'');
});

test('a failed save prevents submission and retains the edited form',{timeout:3000},async()=>{
 const result=await send({saveStatus:409});
 assert.deepEqual(result.requests.map(x=>x.options.method),['PUT']);
 assert.equal(result.state[0],draft);assert.equal(result.state[1][0].unitPriceRial,150);
 assert.doesNotMatch(result.state[8],/نتیجه ارسال نیازمند بررسی/);
 assert.equal(result.state[9],'');assert.equal(result.changed,0);
});

test('an unchanged correction is explained as a validation refusal, not an uncertain send',{timeout:3000},async()=>{
 const result=await send({sendStatus:400,sendCode:'no_changes_to_correct'});
 assert.match(result.state[8],/تغییری/);
 assert.doesNotMatch(result.state[8],/نتیجه ارسال نیازمند بررسی|no_changes_to_correct/);
 assert.ok(result.state[0]);assert.equal(result.state[9],'');assert.equal(result.changed,0);
});

function findConfirmation(element){
 if(!element||typeof element!=='object')return null;
 if(Array.isArray(element))return element.map(findConfirmation).find(Boolean);
 if(element.props?.confirmLabel==='تأیید و ارسال')return element;
 return findConfirmation(element.props?.children);
}
test('dismissing the confirmation toast sends nothing',async()=>{
 const result=await send({cancel:true});assert.equal(result.requests.length,0);
});
test('failed authority inquiry retains the receipt and never repeats submission',async()=>{
 const result=await send({inquiryStatus:503});
 assert.equal(result.requests.filter(x=>x.url.endsWith('/correction-send-and-inquire')).length,1);
 assert.ok(result.requests[2].url.includes('/'+id+'/inquiry-by-reference'));
 assert.equal(result.state[0],null);
 assert.match(result.state[8],/استعلام/);
 assert.equal(result.state[13].referenceNumber,'REF-1');
});
test('uncertain submission blocks another submission and asks for reconciliation',async()=>{
 const result=await send({sendStatus:503,sendCode:'correction_api_unavailable'});
 assert.equal(result.requests.length,2);
 assert.equal(result.state[14],true);
 assert.match(result.state[8],/بدون استعلام/);
});

test('a created but indeterminate correction keeps its structured receipt',async()=>{
 const result=await send({sendStatus:409,sendCode:'dispatch_requires_reconciliation',sendResult:{invoiceId:id,status:'Indeterminate',referenceNumber:null}});
 assert.equal(result.state[13]?.invoiceId,id);
 assert.equal(result.state[13]?.status,'Indeterminate');
 assert.equal(result.state[14],true);assert.equal(result.state[0],null);
 assert.equal(result.requests.length,2);
 assert.doesNotMatch(result.state[8],new RegExp(id));
});
