import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const readiness=readFileSync("app/invoices/invoice-send-readiness-panel.tsx","utf8");
const service=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LiveSendReadinessService.cs","utf8");
const bff=readFileSync("app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/readiness/route.ts","utf8");
const history=readFileSync("app/invoices/invoice-dispatch-history-panel.tsx","utf8");

test("dispatch lifecycle exposes server-owned revision and Sandbox memory context",()=>{
  assert.match(service,/i\.Revision/);
  assert.match(service,/sandboxFiscalMemoryId/);
  assert.match(bff,/invoiceRevision/);
  assert.match(bff,/sandboxFiscalMemoryId/);
  assert.match(readiness,/نسخه صورتحساب/);
  assert.match(readiness,/حافظه Sandbox/);
});

test("owner confirmation and inquiry remain explicit, bounded and browser-safe",()=>{
  assert.match(history,/checked=\{consent\}/);
  assert.match(history,/استعلام/);
  assert.match(history,/mayInquire/);
  assert.doesNotMatch(history,/method:"POST"|fetch\([^)]*POST/);
});
