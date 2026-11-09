import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';

const require=createRequire(import.meta.url);
const React=require('react');
const draft={workspaceId:'workspace',predecessorInvoiceId:'invoice',revision:7,
 lines:[{description:'paper',quantity:1,unitPriceRial:100000,discountPerUnitRial:0,vatRatePercent:0}],
 remainingQuantities:[2],settlementMethod:1,cashAmountRial:100000,creditAmountRial:0};

for(const resumeDraft of [false,true]){
 test(resumeDraft?'continuing a saved correction retains the edited one unit':'starting a correction shows the remaining two units',async()=>{
  const state=[];const effects=[];let slot=0;
  const Component=loadTsx(new URL('../app/invoices/invoice-correction-modal.tsx',import.meta.url),{
   globals:{fetch:async()=>Response.json({code:'existing_draft',draft})},
   modules:{react:{...React,useCallback:fn=>fn,useEffect:fn=>effects.push(fn),useState(initial){
    const index=slot++;
    if(!(index in state))state[index]=initial;
    return [state[index],next=>{state[index]=typeof next==='function'?next(state[index]):next;}];
   }}}
  }).default;
  Component({taxpayerId:'taxpayer',invoiceId:'invoice',resumeDraft,onClose(){},onChanged(){}});
  const cleanup=effects[0]();
  await new Promise(setImmediate);
  assert.equal(state[1][0].quantity,resumeDraft?1:2);
  assert.equal(state[0].lines[0].quantity,1,'saved draft is preserved until the user saves');
  cleanup();
 });
}
