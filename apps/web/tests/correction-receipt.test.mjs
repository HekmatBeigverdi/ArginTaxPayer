import test from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {loadTsx} from './helpers/load-tsx.mjs';
const require=createRequire(import.meta.url);
const React=require('react');
const {renderToStaticMarkup}=require('react-dom/server');
const Receipt=loadTsx(new URL('../app/invoices/invoice-correction-receipt.tsx',import.meta.url)).default;
for(const [status,label] of [[null,'در انتظار استعلام سازمان'],['PENDING','در انتظار بررسی سازمان'],['SUCCESS','تأیید شده توسط سازمان'],['FAILED','رد شده توسط سازمان']]){
 test('receipt separates submission from authority result: '+status,()=>{
  const html=renderToStaticMarkup(React.createElement(Receipt,{receipt:{invoiceId:'internal-id',status:'Acknowledged',referenceNumber:'REF-<unsafe>',authorityStatus:status}}));
  assert.match(html,/رسید ارسال دریافت شد/);assert.ok(html.includes(label));
  assert.ok(html.includes('<bdi dir="ltr">REF-&lt;unsafe&gt;</bdi>'));
  assert.ok(html.includes('<details>'));assert.doesNotMatch(html,/Acknowledged|نامشخص|<unsafe>/);
  if(status==='SUCCESS')assert.match(html,/data-outcome="success"/);
  else assert.doesNotMatch(html,/نتیجه موفق استعلام/);
 });
}
