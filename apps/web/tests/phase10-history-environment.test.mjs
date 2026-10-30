import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
const require=createRequire(import.meta.url);
const React=require('react');
const {renderToStaticMarkup}=require('react-dom/server');
const payload=()=>({environment:'Sandbox',submissionEnabled:false,fiscalAccepted:false,
 signedPreviewAvailable:false,invoiceRevision:1,state:'Acknowledged',lastUpdatedAtUtc:null,
 attempts:[],receipts:[],recoveryAction:'RefreshOnly',recoveryCode:'invoice.recovery.refresh_only',
 mayRetry:false,mayInquire:false});

function panel(body){
 let received=null,error='';
 const callbacks=[],requests=[];
 const component=loadTsx(new URL('../app/invoices/invoice-dispatch-history-panel.tsx',import.meta.url),{
  modules:{react:{...React,useEffect:()=>{},useCallback:fn=>{callbacks.push(fn);return fn;},
   useState(initial){return [initial,value=>{
    if(initial===null)received=value;
    if(initial==='')error=value;
   }];}}},
  globals:{fetch:async(url,options)=>{requests.push({url,options});return {ok:true,json:async()=>body};}}
 });
 component.default({taxpayerId:'taxpayer',invoiceId:'invoice',isWorkspace:false});
 return {refresh:callbacks[0],requests,received:()=>received,error:()=>error};
}

test('history panel accepts current Sandbox evidence using only a scoped GET',async()=>{
 const view=panel(payload());
 await view.refresh();
 assert.equal(view.received()?.state,'Acknowledged');
 assert.equal(view.error(),'');
 assert.equal(view.requests.length,1);
 assert.equal(view.requests[0].url,'/api/invoice-dispatch/taxpayer/invoice/history');
 assert.equal(view.requests[0].options.method,'GET');
 assert.equal(view.requests[0].options.credentials,'same-origin');
});

test('history panel rejects incompatible environments and unsafe action flags',async()=>{
 for(const change of [{environment:'Mock'},{environment:'Production'},
  {submissionEnabled:true},{fiscalAccepted:true},{signedPreviewAvailable:true},
  {mayRetry:true},{mayInquire:true}]){
  const view=panel({...payload(),...change});
  await view.refresh();
  assert.equal(view.received(),null);
  assert.notEqual(view.error(),'');
 }
});

test('history panel labels persisted evidence as Sandbox',()=>{
 let index=0;
 const component=loadTsx(new URL('../app/invoices/invoice-dispatch-history-panel.tsx',import.meta.url),{
  modules:{react:{...React,useEffect:()=>{},useCallback:fn=>fn,
   useState(initial){return [index++===0?payload():initial,()=>{}];}}}
 });
 const html=renderToStaticMarkup(React.createElement(component.default,{
  taxpayerId:'taxpayer',invoiceId:'invoice',isWorkspace:false
 }));
 assert.ok(html.includes('Sandbox'));
 assert.ok(!html.includes('Mock'));
});
