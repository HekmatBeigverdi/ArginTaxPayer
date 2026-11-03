import test from 'node:test';
import assert from 'node:assert/strict';
import React from 'react';
import {renderToStaticMarkup} from 'react-dom/server';
import {loadTsx} from './helpers/load-tsx.mjs';
const load=()=>loadTsx(new URL('../app/subscriptions/partner-settlement-workspace.tsx',import.meta.url),{modules:{'./partner-settlement-workspace.css':{}}});
test('initial settlement workspace requires a partner and shows loading',()=>{
 const Component=load().default;
 const html=renderToStaticMarkup(React.createElement(Component,{level1:true,request:async()=>[]}));
 assert.match(html,/انتخاب همکار/);assert.match(html,/در حال دریافت/);assert.match(html,/disabled/);
});
test('voucher prints immutable identity, percentage, signed direction and escaped receipt text',()=>{
 const {SettlementVoucher}=load();
 const html=renderToStaticMarkup(React.createElement(SettlementVoucher,{detail:{batch:{receiptNumber:'V1',rootDisplayName:'Root',partnerDisplayName:'Partner',createdAtUtc:'2026-11-02',reference:'<script>bad</script>',note:'note',totalCollectedIrr:100,totalCostIrr:30,rootPaidCostIrr:10,cashIrr:100,bankIrr:0,totalPartnerShareIrr:21,totalRootShareIrr:49,netToRootIrr:-10},items:[{collectionId:'receipt',customerDisplayName:'<Customer>',customerContact:'phone',planKind:2,invoiceUnitLimit:100,planCode:'P',partnerBasisPoints:3000,grossIrr:100,approvedCostIrr:30,rootPaidCostIrr:10,partnerShareIrr:21,rootShareIrr:49,netToRootIrr:59,previousNetToRootIrr:69,deltaNetToRootIrr:-10,deltaCollectedIrr:0,deltaCostIrr:30,deltaPartnerShareIrr:-9,deltaRootShareIrr:-21,channel:1,receiptReference:'ref'}]}}));
 assert.match(html,/V1/);assert.match(html,/۳۰/);assert.match(html,/درصد/);assert.match(html,/پرداخت سطح یک به همکار/);assert.match(html,/&lt;Customer&gt;/);assert.ok(!html.includes('<script>'));assert.match(html,/۱۰۰/);assert.match(html,/تغییر هزینه/);
});
function controlled(states={}){
 const effects=[],updates=[],requests=[],pending=[];let slot=0;
 const Component=loadTsx(new URL('../app/subscriptions/partner-settlement-workspace.tsx',import.meta.url),{modules:{'./partner-settlement-workspace.css':{},react:{...React,useRef:value=>({current:value}),useEffect:fn=>effects.push(fn),useState(initial){const index=slot++;return [Object.hasOwn(states,index)?states[index]:initial,next=>updates.push({index,next})];}}}}).default;
 const request=path=>{requests.push(path);return new Promise((resolve,reject)=>pending.push({resolve,reject}));};
 return {Component,effects,updates,requests,pending,request};
}
test('history requests real database pages and ignores a late superseded response',async()=>{
 const h=controlled({1:false,2:'partner-id',4:3});h.Component({level1:true,request:h.request});
 h.effects[2]();h.effects[2]();assert.equal(h.requests[0],'settlements/batches?partnerUserId=partner-id&page=3');
 h.pending[1].resolve({items:[{receiptNumber:'new'}]});await new Promise(setImmediate);
 h.pending[0].resolve({items:[{receiptNumber:'old'}]});await new Promise(setImmediate);
 assert.equal(h.updates.filter(x=>x.index===3).at(-1).next.items[0].receiptNumber,'new');
});
test('preview requests selected partner and database page, suppressing superseded data',async()=>{
 const h=controlled({1:false,2:'partner-id',6:'preview',7:2});h.Component({level1:false,request:h.request});
 h.effects[3]();h.effects[3]();assert.equal(h.requests[0],'settlements/preview?partnerUserId=partner-id&page=2');
 h.pending[1].resolve({previewFingerprint:'fresh'});await new Promise(setImmediate);
 h.pending[0].resolve({previewFingerprint:'stale'});await new Promise(setImmediate);
 assert.equal(h.updates.filter(x=>x.index===8).at(-1).next.previewFingerprint,'fresh');
});
test('Level2 has preview visibility without rate, expense or transfer mutation controls',()=>{
 const h=controlled({1:false,2:'partner-id',6:'preview',8:{items:[],totalCount:0,missingRateCount:0,pendingGatewayCount:1,legacyConflictCount:0,canRegister:false,totalCollectedIrr:0,cashIrr:0,bankIrr:0,totalCostIrr:0,rootPaidCostIrr:0,totalPartnerShareIrr:0,totalRootShareIrr:0,netToRootIrr:0}});
 const html=renderToStaticMarkup(React.createElement(h.Component,{level1:false,request:h.request}));
 assert.match(html,/پیش‌نمایش تسویه/);assert.match(html,/درگاه با مالک نامشخص/);
 assert.ok(!html.includes('ثبت نرخ برای وصول'));assert.ok(!html.includes('تأیید انتقال و ثبت رسید'));assert.ok(!html.includes('تأیید و ثبت هزینه'));
});
test('immutable detail reads request a real server page and ignore late responses',async()=>{
 const h=controlled({1:false,2:'partner-id',6:'batch-id',7:4});h.Component({level1:true,request:h.request});
 h.effects[3]();h.effects[3]();assert.equal(h.requests[0],'settlements/batch?batchId=batch-id&page=4');
 h.pending[1].resolve({batch:{receiptNumber:'fresh'}});await new Promise(setImmediate);
 h.pending[0].resolve({batch:{receiptNumber:'stale'}});await new Promise(setImmediate);
 assert.equal(h.updates.filter(x=>x.index===9).at(-1).next.batch.receiptNumber,'fresh');
});
test('rate editor uses actual percentages and converts fractional values only for the terms API',async()=>{
 const h=controlled({1:false,2:'partner-id',6:'preview',14:'12.34'}),calls=[];
 const tree=h.Component({level1:true,request:async(...args)=>{calls.push(args);return {};}});
 const nodes=[];function visit(node){if(Array.isArray(node)){node.forEach(visit);return;}if(!React.isValidElement(node))return;nodes.push(node);visit(node.props.children);}visit(tree);
 const form=nodes.find(node=>node.type==='form');
 const html=renderToStaticMarkup(tree);assert.match(html,/سهم ادمین سطح ۲ \(%\)/);assert.match(html,/step="0.01"/);assert.match(html,/max="100"/);
 form.props.onSubmit({preventDefault(){}});await new Promise(setImmediate);
 assert.equal(calls[0][0],'settlements/terms');assert.equal(calls[0][1],'PUT');assert.equal(calls[0][2].partnerBasisPoints,1234);
});
test('loading modal retains column headings and shared finance spinner in the table body',()=>{
 const h=controlled({1:false,2:'partner-id',6:'preview',10:true});
 const html=renderToStaticMarkup(React.createElement(h.Component,{level1:true,request:h.request}));
 assert.match(html,/کاربر \/ تماس/);assert.match(html,/درصد همکار/);assert.match(html,/<td colSpan="7"><p class="phase12-finance-loading" role="status"/);assert.match(html,/<span aria-hidden="true"/);
});
