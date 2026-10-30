import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
const require=createRequire(import.meta.url);
const React=require('react');
const {renderToStaticMarkup}=require('react-dom/server');

for(const [subject,label,tone] of [['Original','اصلی','original'],['Correction','اصلاحیه','correction'],['SalesReturn','برگشت','return'],['ReturnFromSale','برگشت','return']]){
 test('invoice subject badge: '+subject,()=>{
  const Badge=loadTsx(new URL('../app/invoices/invoice-subject-badge.tsx',import.meta.url)).default;
  const html=renderToStaticMarkup(React.createElement(Badge,{subject}));
  assert.ok(html.includes(label));assert.ok(html.includes('invoice-subject-badge--'+tone));
 });
}
function find(element,predicate){
 if(!element||typeof element!=='object')return null;
 if(Array.isArray(element))return element.map(x=>find(x,predicate)).find(Boolean)??null;
 if(predicate(element))return element;
 return find(element.props?.children,predicate);
}
function actions(props){
 let slot=0;
 const Preview=loadTsx(new URL('../app/invoices/invoice-formal-preview.tsx',import.meta.url),{
  modules:{react:{...React,useEffect:()=>{},useState(initial){return [slot++===0?{}:initial,()=>{}];}}}
 }).default;
 const tree=Preview({taxpayerId:'taxpayer',invoiceId:'latest-invoice',reference:'root-code',...props});
 const node=find(tree,x=>x.type?.name==='AuthorityActions');
 return node?node.type(node.props):null;
}
for(const subject of ['Original','SalesReturn']){
 test('preview return button opens the shared return action: '+subject,()=>{
  let opened=0;
  const tree=actions({subject,authorityStatus:'SUCCESS',onSalesReturn:()=>opened++});
  const button=find(tree,x=>x.type==='button'&&x.props.children==='برگشت از فروش');
  assert.ok(button);assert.ok(!button.props.disabled);assert.equal(typeof button.props.onClick,'function');
  button.props.onClick();assert.equal(opened,1);
 });
}
for(const props of [{subject:'Original',authorityStatus:'PENDING'},{subject:'Correction',authorityStatus:'SUCCESS'},{subject:'Cancellation',authorityStatus:'SUCCESS'},{subject:'Original',authorityStatus:'SUCCESS',readOnly:true}]){
 test('preview excludes unavailable return action: '+JSON.stringify(props),()=>{
  assert.equal(find(actions({...props,onSalesReturn:()=>assert.fail('must not run')}),x=>x.type==='button'&&x.props.children==='برگشت از فروش'),null);
 });
}
