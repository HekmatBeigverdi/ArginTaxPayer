import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const panel=readFileSync(new URL("../app/invoices/invoice-dispatch-panel.tsx",import.meta.url),"utf8");
const workspace=readFileSync(new URL("../app/invoices/workspace.tsx",import.meta.url),"utf8");
const css=readFileSync(new URL("../app/globals.css",import.meta.url),"utf8");

test("step17 dispatch panel uses scoped BFF status and guarded send",()=>{
  assert.match(panel,/\/api\/invoice-dispatch\//);
  assert.match(panel,/method:"GET"/);
  assert.match(panel,/method:"POST"/);
  assert.match(panel,/credentials:"same-origin"/);
  assert.match(panel,/cache:"no-store"/);
  assert.match(panel,/encodeURIComponent\(taxpayerId\)/);
  assert.match(panel,/encodeURIComponent\(invoiceId\)/);
});
test("step17 enables only guarded Sandbox sending",()=>{
  assert.match(panel,/typeof value\.submissionEnabled!=="boolean"/);
  assert.match(panel,/inquiryEnabled!==false/);
  assert.match(panel,/fiscalAccepted!==false/);
  assert.match(panel,/value\.environment==="Mock"\|\|value\.environment==="Sandbox"/);
  assert.match(panel,/data\.status!=="Reserved"/);
  assert.match(panel,/title="ارسال به Sandbox/);
  assert.match(panel,/disabled\s+title="سرویس استعلام/);
  assert.match(panel,/محیط \{data\?\.environment/);
});
test("invoice details use scoped one-click readiness and dedicated history",()=>{
  assert.match(workspace,/import InvoiceSendReadinessPanel/);
  assert.match(workspace,/<InvoiceSendReadinessPanel key=\{"readiness:"\+detail\.id\}/);
  assert.match(workspace,/invoiceId=\{detail\.effectiveInvoiceId \?\? detail\.id\} taxpayerId=\{detail\.taxpayerId\}/);
  assert.match(workspace,/isWorkspace=\{detail\.kind === "workspace"\} hideHistory=\{true\}/);
  assert.match(workspace,/<InvoiceDispatchHistory taxpayerId=\{detail\.taxpayerId\}/);
  assert.match(css,/\.invoice-dispatch-ui__grid/);
});
