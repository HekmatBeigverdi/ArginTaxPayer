import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const panel=readFileSync("app/invoices/invoice-dispatch-panel.tsx","utf8");
const workspace=readFileSync("app/invoices/workspace.tsx","utf8");
const css=readFileSync("app/globals.css","utf8");

test("step17 dispatch panel uses scoped BFF read-only status",()=>{
  assert.match(panel,/\/api\/invoice-dispatch\//);
  assert.match(panel,/method:"GET"/);
  assert.match(panel,/credentials:"same-origin"/);
  assert.match(panel,/cache:"no-store"/);
  assert.match(panel,/encodeURIComponent\(taxpayerId\)/);
  assert.match(panel,/encodeURIComponent\(invoiceId\)/);
  assert.doesNotMatch(panel,/method:"POST"/);
});
test("step17 cannot enable sandbox or production sending in UI",()=>{
  assert.match(panel,/submissionEnabled!==false/);
  assert.match(panel,/inquiryEnabled!==false/);
  assert.match(panel,/fiscalAccepted!==false/);
  assert.match(panel,/value\.environment!=="Mock"/);
  assert.match(panel,/disabled\s+title="ارسال/);
  assert.match(panel,/disabled\s+title="سرویس استعلام/);
  assert.match(panel,/محیط Mock/);
});
test("step17 uses existing invoice details without adding new data mutations",()=>{
  assert.match(workspace,/import InvoiceDispatchPanel/);
  assert.match(workspace,/<InvoiceDispatchPanel key=\{detail\.id\}/);
  assert.match(workspace,/taxpayerId=\{detail\.taxpayerId\}/);
  assert.match(workspace,/مشاهده و وضعیت ارسال/);
  assert.match(css,/\.invoice-dispatch-ui__grid/);
  assert.match(css,/max-width:520px/);
});
