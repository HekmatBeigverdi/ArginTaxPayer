import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
const require=createRequire(import.meta.url);const React=require('react');
const {renderToStaticMarkup}=require('react-dom/server');
const draft={invoiceType:2,workspaceId:'workspace',predecessorInvoiceId:'invoice',revision:1,settlementMethod:0,cashAmountRial:0,creditAmountRial:0,
 lines:[{description:'paper',quantity:2,unitPriceRial:100,discountPerUnitRial:0,vatRatePercent:0}],remainingQuantities:[2],originalQuantities:[2]};
function buttons(node){
 if(!node||typeof node!=='object')return [];
 if(Array.isArray(node))return node.flatMap(buttons);
 return [...(node.type==='button'?[node]:[]),...buttons(node.props?.children)];
}
for(const operation of ['correction','sales-return']){
 for(const invoiceType of [1,2]){
  test(`${operation}: Type ${invoiceType} editor exposes only its applicable settlement fields and saves the right contract`,async()=>{
   const file=new URL(`../app/invoices/invoice-${operation}-modal.tsx`,import.meta.url);
   const names=[...readFileSync(file,'utf8').matchAll(/const\s+\[([A-Za-z]\w*),\s*\w+\]\s*=\s*useState/g)].map(m=>m[1]);
   const state={draft:{...draft,invoiceType},entries:[{quantity:1,unitPriceRial:100,discountPerUnitRial:0}],stage:'items'};
   const requests=[];let slot=0;
   const Component=loadTsx(file,{globals:{fetch:async(url,options)=>{requests.push({url,options});return Response.json({code:'draft_saved',draft:{...draft,invoiceType,revision:2}});}},modules:{react:{...React,useEffect:()=>{},useCallback:fn=>fn,useState(initial){
    const name=names[slot++];return [Object.hasOwn(state,name)?state[name]:typeof initial==='function'?initial():initial,value=>{state[name]=typeof value==='function'?value(state[name]):value;}];
   }}}}).default;
   const props={taxpayerId:'taxpayer',invoiceId:'invoice',onClose(){},onChanged(){}};
   const view=Component(props);const html=renderToStaticMarkup(view);
   const tabs=buttons(view).filter(b=>b.props['aria-pressed']!==undefined);
   assert.equal(tabs.some(b=>String(b.props.children).includes('تسویه')),invoiceType===1);
   if(invoiceType===2)assert.match(html,/نوع دوم نقدی/);
   const save=buttons(view).find(b=>b.props['aria-label']==='ذخیره پیش‌نویس');assert.ok(save);save.props.onClick();
   await new Promise(setImmediate);
   assert.equal(requests.length,1);
   const payload=JSON.parse(requests[0].options.body);
   assert.equal(payload.settlementMethod,invoiceType===2?0:1);
   if(invoiceType===2){assert.equal(payload.cashAmountRial,0);assert.equal(payload.creditAmountRial,0);}
  });
 }
}
