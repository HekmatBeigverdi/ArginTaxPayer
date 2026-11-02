import test from 'node:test';
import assert from 'node:assert/strict';
import React from 'react';
import {createRequire} from 'node:module';
import {renderToStaticMarkup} from 'react-dom/server';
import {loadTsx} from './helpers/load-tsx.mjs';

const paid={orderId:'paid',customerDisplayName:'مشتری تسویه‌شده',customerContact:null,
 partnerDisplayName:'مدیر فروش',planCode:'طلایی',planKind:1,invoiceUnitLimit:null,
 issuedAtUtc:'2026-11-02T12:00:00Z',grossIrr:1000,collectedIrr:1000,isActivated:true};
const report={items:[paid],totalGrossIrr:6000,totalCollectedIrr:1500,
 totalOutstandingIrr:4500,totalCount:31,page:1,pageSize:10,partners:[{partnerUserId:"11111111-1111-1111-1111-111111111111",displayName:"ادمین نمونه"}]};
function load(reportState=null,effects=[],updates=[]){
 let slot=0;
 return loadTsx(new URL('../app/subscriptions/plan-sales-report.tsx',import.meta.url),{
  modules:{react:{...React,useState(initial){const i=slot++;return [i===3?reportState:i===4?reportState===null:initial,value=>updates.push({slot:i,value})];},
   useEffect(effect){effects.push(effect);}}}
 }).default;
}
test('sales report retains fully paid sales, partner name and full-period totals',()=>{
 const Component=load(report);
 const html=renderToStaticMarkup(React.createElement(Component,{request:async()=>report}));
 for(const text of ['مشتری تسویه‌شده','مدیر فروش','طلایی','تسویه کامل','۱٬۵۰۰','۴٬۵۰۰','ماه جاری','سال جاری','صفحهٔ ۱ از ۴'])
  assert.ok(html.includes(text),text);
});
test('default report requests the current month and suppresses a result after effect cleanup',async()=>{
 const effects=[];const calls=[];const updates=[];
 const Component=load(null,effects,updates);
 Component({request:async path=>{calls.push(path);return report;}});
 assert.equal(effects.length,1);
 const cleanup=effects[0]();
 const initialUpdates=updates.length;
 cleanup();
 await new Promise(resolve=>setImmediate(resolve));
 assert.equal(calls[0],'sales-report?period=month&page=1&pageSize=10');
 assert.equal(updates.length,initialUpdates,'no state is updated after cleanup');
});

const {NextRequest}=createRequire(import.meta.url)('next/server');
function proxy(fetch,roles=['Level2']){
 return loadTsx(new URL('../app/api/phase12/[...parts]/route.ts',import.meta.url),{
  globals:{fetch,AbortSignal,URLSearchParams},modules:{
   'next/headers':{cookies:async()=>({get:()=>({value:'session-token'})})},
   '../../../../lib/identity':{ACCESS_COOKIE:'access',currentIdentity:async()=>({roles}),
    identityUiEnabled:()=>true,identityApiOrigin:()=>'http://backend'}
  }
 });
}
const context={params:Promise.resolve({parts:['subscriptions','sales-report']})};
test('sales proxy forwards validated yearly period and pagination using session identity',async()=>{
 const calls=[];
 const route=proxy(async(url,options)=>{calls.push({url,options});return Response.json(report);});
 const response=await route.GET(new NextRequest('http://localhost/api/phase12/subscriptions/sales-report?period=year&page=2&pageSize=10&partnerUserId=11111111-1111-1111-1111-111111111111&customerUserId=foreign'),context);
 assert.equal(response.status,200);
 assert.equal(calls[0].url,'http://backend/api/dev/subscriptions/sales-report?page=2&pageSize=10&partnerUserId=11111111-1111-1111-1111-111111111111&period=year');
 assert.equal(calls[0].options.headers.Authorization,'Bearer session-token');
 assert.equal(response.headers.get('cache-control'),'no-store');
});
test('sales proxy rejects unknown periods and customers before querying backend',async()=>{
 let calls=0;
 const fetch=async()=>{calls++;return Response.json(report);};
 const request=new NextRequest('http://localhost/api/phase12/subscriptions/sales-report?period=week');
 assert.equal((await proxy(fetch).GET(request,context)).status,400);
 assert.equal((await proxy(fetch,['Customer']).GET(request,context)).status,403);
 assert.equal(calls,0);
});

test('sales table and its column headings are visible during the initial request',()=>{
 const Component=load();
 const html=renderToStaticMarkup(React.createElement(Component,{request:async()=>report}));
 assert.ok(html.includes('<table'));
 assert.ok(html.includes('تاریخ فروش'));
 assert.ok(html.includes('در حال دریافت گزارش فروش'));
 assert.match(html,/<tbody><tr><td colSpan="9"/);
});

test('sales refresh returns to a valid last page when the previous page disappears',async()=>{
 const updates=[],effects=[];let slot=0;
 const Component=loadTsx(new URL('../app/subscriptions/plan-sales-report.tsx',import.meta.url),{
  modules:{react:{...React,useState(initial){const i=slot++;return [i===1?2:initial,value=>updates.push({slot:i,value})];},
   useEffect(effect){effects.push(effect);}}}
 }).default;
 Component({request:async()=>({...report,items:[],totalCount:10})});
 effects[0]();
 await new Promise(resolve=>setImmediate(resolve));
 assert.equal(updates.find(x=>x.slot===1)?.value,1);
 assert.ok(!updates.some(x=>x.slot===3&&x.value?.items?.length===0),'invalid empty page is not displayed');
});

test('sales sends selected Level 2 filter to the server and renders its options',async()=>{
 const effects=[],calls=[];let slot=0;
 const partner='11111111-1111-1111-1111-111111111111';
 const Component=loadTsx(new URL('../app/subscriptions/plan-sales-report.tsx',import.meta.url),{
  modules:{react:{...React,useState(initial){const i=slot++;return [i===3?report:i===4?false:i===6?partner:i===7?report.partners:initial,()=>{}];},
   useEffect(effect){effects.push(effect);}}}
 }).default;
 const html=renderToStaticMarkup(React.createElement(Component,{request:async path=>{calls.push(path);return report;}}));
 assert.ok(html.includes('همه ادمین‌ها'));
 assert.ok(html.includes('ادمین نمونه'));
 effects[0]();
 await new Promise(resolve=>setImmediate(resolve));
 assert.equal(calls[0],`sales-report?period=month&page=1&pageSize=10&partnerUserId=${partner}`);
});
