import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';

const require=createRequire(import.meta.url);
const React=require('react');
function findInquiry(element){
 if(!element||typeof element!=='object')return null;
 if(Array.isArray(element))return element.map(findInquiry).find(Boolean);
 if(element.type==='button'&&element.props.children==='استعلام این سند از سازمان')return element;
 return findInquiry(element.props?.children);
}

for(const success of [true,false]){
 test(success?'successful lineage inquiry notifies the invoice table to refresh':'failed inquiry preserves the table status',async()=>{
  const node={invoiceId:'invoice',subject:'Original',fiscalTaxId:null,issuedAtUtc:'2026-11-09T08:00:00Z',
   dispatchState:'Acknowledged',authorityReference:'REF',authorityStatus:'PENDING',sentAtUtc:null,canResumeSubmission:false};
  const state=[{hasDispatchAttempt:true,hasChanges:false,nodes:[node]}];let slot=0;let changed=0;let done;
  const finished=new Promise(resolve=>{done=resolve;});
  const Component=loadTsx(new URL('../app/invoices/invoice-lineage-modal.tsx',import.meta.url),{
   globals:{fetch:async()=>Response.json(success?{authorityResult:{status:'SUCCESS',errors:[],warnings:[],observedAtUtc:'2026-11-09T09:00:00Z'}}:{error:'unavailable'},{status:success?200:503})},
   modules:{react:{...React,useEffect(){},useRef:value=>({current:value}),useState(initial){
    const index=slot++;
    if(!(index in state))state[index]=initial;
    return [state[index],next=>{
     state[index]=typeof next==='function'?next(state[index]):next;
     if(index===3&&next===null)done();
    }];
   }}}
  }).default;
  const view=Component({taxpayerId:'taxpayer',invoiceId:'invoice',onClose(){},onChanged(){changed++;}});
  const inquiry=findInquiry(view);assert.ok(inquiry);
  inquiry.props.onClick();await finished;
  assert.equal(changed,success?1:0);
  assert.equal(state[2],success?1:0,'the chain and table refresh after a successful inquiry');
 });
}
