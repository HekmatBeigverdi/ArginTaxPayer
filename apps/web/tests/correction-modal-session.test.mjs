import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
const require=createRequire(import.meta.url);
const React=require('react');
const id='11111111-1111-1111-1111-111111111111';
const line={description:'item',quantity:2,unitPriceRial:100,discountPerUnitRial:0,vatRatePercent:10,officialIdentifierSnapshot:'1234567890123'};
const draft={workspaceId:id,predecessorInvoiceId:id,revision:1,lines:[line],settlementMethod:1,cashAmountRial:220,creditAmountRial:0};

function findSaveButton(element){
 if(!element||typeof element!=='object')return null;
 if(Array.isArray(element))return element.map(findSaveButton).find(Boolean);
 if(element.type==='button'&&element.props['aria-label']==='ذخیره پیش‌نویس')return element;
 return findSaveButton(element.props?.children);
}

for(const canRefresh of [true,false]){
 test(`draft save ${canRefresh?'recovers the expired session':'keeps edits when session recovery fails'}`,{timeout:3000},async()=>{
  const edits=[{quantity:2,unitPriceRial:150,discountPerUnitRial:0}];
  const state=[draft,edits,1,220,0,false,'',false,'','', 'items'];
  const requests=[];let index=0;let finish;
  const saved=new Promise(resolve=>{finish=resolve;});
  const component=loadTsx(new URL('../app/invoices/invoice-correction-modal.tsx',import.meta.url),{
   globals:{fetch:async(url,options)=>{
    requests.push({url,options});
    if(url==='/api/identity/refresh')return Response.json({status:canRefresh?'authenticated':'denied'},{status:canRefresh?200:401});
    if(requests.length===1)return Response.json({error:'unauthorized'},{status:401});
    return Response.json({code:'draft_saved',draft:{...draft,revision:2,lines:[{...line,unitPriceRial:150}]}});
   }},
   modules:{react:{...React,useCallback:fn=>fn,useEffect:()=>{},useState(initial){
    const slot=index++;return [state[slot]??initial,value=>{
     state[slot]=typeof value==='function'?value(state[slot]):value;
     if(slot===7&&value===false)finish();
    }];
   }}}
  });
  const view=component.default({taxpayerId:id,invoiceId:id,onClose:()=>assert.fail('must keep modal open'),onChanged:()=>{}});
  const button=findSaveButton(view);assert.ok(button);button.props.onClick();await saved;
  assert.equal(requests[0].options.method,'PUT');
  assert.equal(JSON.parse(requests[0].options.body).lines[0].unitPriceRial,150);
  assert.equal(state[7],false);
  if(canRefresh){
   assert.equal(requests.length,3);assert.equal(state[0].revision,2);
   assert.match(state[9],/ذخیره شد/);assert.equal(state[8],'');
  }else{
   assert.equal(requests.length,2);assert.equal(state[0],draft);assert.equal(state[1],edits);
   assert.match(state[8],/تغییرات فرم حفظ شده‌اند/);
   assert.doesNotMatch(state[8],/unauthorized/);
  }
 });
}
