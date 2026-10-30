import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
const require=createRequire(import.meta.url);
const React=require('react');
const {renderToStaticMarkup}=require('react-dom/server');
function modal(initial=[]){
 const state=[null,'',0,null,'',true];Object.assign(state,initial);
 let slot=0;
 const Component=loadTsx(new URL('../app/invoices/invoice-lineage-modal.tsx',import.meta.url),{
  modules:{react:{...React,useEffect:()=>{},useState(value){
   const index=slot++;return [state[index]??value,next=>{state[index]=typeof next==='function'?next(state[index]):next;}];
  }}}
 }).default;
 return {html:renderToStaticMarkup(React.createElement(Component,{taxpayerId:'taxpayer',invoiceId:'invoice',onClose:()=>{}})),state};
}
test('initial lineage wait announces loading and shows a decorative skeleton',()=>{
 const {html}=modal();
 assert.match(html,/aria-busy="true"/);
 assert.match(html,/argin-loader-spinner/);
 assert.match(html,/invoice-lineage-skeleton/);
 assert.match(html,/در حال دریافت زنجیره/);
});
test('loaded empty lineage stops displaying its loading state',()=>{
 const {html}=modal([{hasDispatchAttempt:false,hasChanges:false,nodes:[]},'',0,null,'',false]);
 assert.doesNotMatch(html,/invoice-lineage-skeleton|argin-loader-spinner/);
 assert.match(html,/هنوز سابقه ارسال/);
});
test('lineage failure provides retry without leaving the loader visible',()=>{
 const {html}=modal([null,'دریافت زنجیره صورتحساب ناموفق بود.',0,null,'',false]);
 assert.doesNotMatch(html,/invoice-lineage-skeleton/);
 assert.match(html,/تلاش دوباره/);
});
test('refresh keeps existing chain visible and announces the refresh',()=>{
 const {html}=modal([{hasDispatchAttempt:true,hasChanges:true,nodes:[]},'',0,null,'',true]);
 assert.match(html,/در حال به‌روزرسانی زنجیره/);
 assert.doesNotMatch(html,/invoice-lineage-skeleton/);
});
