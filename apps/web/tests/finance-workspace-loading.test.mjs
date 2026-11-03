import test from 'node:test';
import assert from 'node:assert/strict';
import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {loadTsx} from './helpers/load-tsx.mjs';

function workspace({tab='collections',loading=false,effects=[],updates=[],fetch=async()=>{throw Error('unexpected request');}}={}) {
 let slot=0;
 return loadTsx(new URL('../app/subscriptions/subscription-workspace.tsx',import.meta.url),{
  globals:{fetch},modules:{
   './subscription-workspace.css':{},'./partner-settlement-workspace.css':{},'./plan-catalog':{default:()=>null},
   '../invoices/invoice-action-toast':{default:()=>null},
   './plan-sales-report':{default:()=>React.createElement('div',null,'گزارش فروش')},
   react:{...React,useRef:value=>({current:value}),useCallback:fn=>fn,useEffect:effect=>effects.push(effect),useState(initial){
    const i=slot++;
    const value=i===0?tab:i===17?loading:i===15?[{orderId:'old',customerUserId:'customer',
     customerDisplayName:'ردیف قبلی',customerContact:null,partnerDisplayName:'مدیر فروش',
     planKind:1,invoiceUnitLimit:null,grossIrr:1000,collectedIrr:0,
     issuedAtUtc:'2026-11-02T12:00:00Z',isActivated:true}]:initial;
    return [value,next=>updates.push({slot:i,value:next})];
   }}
  }
 }).default;
}
test('collections loading replaces stale rows within the table body',()=>{
 const Component=workspace({loading:true});
 const html=renderToStaticMarkup(React.createElement(Component,{level1:true}));
 assert.ok(html.includes('در حال دریافت درخواست‌ها'));
 assert.ok(!html.includes('ردیف قبلی'));
 assert.match(html,/<tbody><tr><td colSpan="9"/);
});
test('revenue tab renders sales without allocation UI or background settlement/ledger requests',async()=>{
 const effects=[];let requests=0;
 const Component=workspace({tab:'revenue',effects,fetch:async()=>{requests++;return Response.json({});}});
 const html=renderToStaticMarkup(React.createElement(Component,{level1:true}));
 assert.ok(!html.includes('گزارش تخصیص'));
 for(const effect of effects)effect();
 await new Promise(resolve=>setImmediate(resolve));
 assert.equal(requests,0);
});

for(const [tab,stateSlot] of [['collections',15]])
test(`an older ${tab} request cannot overwrite the most recent page response`,async()=>{
 const effects=[],updates=[],resolvers=[];
 const Component=workspace({tab,effects,updates,fetch:()=>new Promise(resolve=>resolvers.push(resolve))});
 Component({level1:true});
 effects[0]();effects[0]();
 resolvers[1](Response.json({items:[{orderId:'newer'}],totalCount:20}));
 await new Promise(resolve=>setImmediate(resolve));
 resolvers[0](Response.json({items:[{orderId:'older'}],totalCount:10}));
 await new Promise(resolve=>setImmediate(resolve));
 const latest=updates.filter(x=>x.slot===stateSlot).at(-1).value;
 assert.equal((tab==='collections'?latest:latest.items)[0].orderId,'newer');
});
