import test from "node:test";
import assert from "node:assert/strict";
import {invoicePreview} from "../app/invoices/invoice-financial-preview.mjs";
const line=(quantity,unitPrice,discount,vatRate)=>({quantity,unitPrice,discount,vatRate});
test("base 9100, VAT 10 percent, final 10010",()=>{
 assert.deepEqual(invoicePreview([line("1","9100","0","10")]),
  {base:9100,discount:0,vat:910,total:10010});
});
test("line rounding before aggregation, half away from zero",()=>{
 assert.deepEqual(invoicePreview([line("1","0.005","0","0"),line("1","0.005","0","0")]),
  {base:0.02,discount:0,vat:0,total:0.02});
});
test("discount and tax rounded per line using decimal strings",()=>{
 assert.deepEqual(invoicePreview([line("2","50.25","5.25","10")]),
  {base:100.5,discount:10.5,vat:9,total:99});
});
test("validates malformed decimals and excessive discounts",()=>{
 assert.throws(()=>invoicePreview([line("1","123oops","0","10")]));
 assert.throws(()=>invoicePreview([line("1","100","101","10")]));
});
