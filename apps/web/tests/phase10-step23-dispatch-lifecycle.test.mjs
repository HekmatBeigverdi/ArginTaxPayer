import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const readiness=readFileSync(new URL("../app/invoices/invoice-send-readiness-panel.tsx",import.meta.url),"utf8");
const service=readFileSync(new URL("../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LiveSendReadinessService.cs",import.meta.url),"utf8");
const bff=readFileSync(new URL("../app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/readiness/route.ts",import.meta.url),"utf8");
const history=readFileSync(new URL("../app/invoices/invoice-dispatch-history-panel.tsx",import.meta.url),"utf8");

test("dispatch lifecycle validates server-owned revision and Sandbox memory context",()=>{
  assert.match(service,/i\.Revision/);
  assert.match(service,/sandboxFiscalMemoryId/);
  assert.match(bff,/invoiceRevision/);
  assert.match(bff,/sandboxFiscalMemoryId/);
  assert.match(readiness,/Number\.isSafeInteger\(data\.invoiceRevision\)/);
  assert.match(readiness,/typeof data\.sandboxFiscalMemoryId!=="string"/);
});

test("owner confirmation and inquiry remain explicit, bounded and browser-safe",()=>{
  assert.match(history,/checked=\{consent\}/);
  assert.match(history,/استعلام/);
  assert.match(history,/mayInquire/);
  assert.doesNotMatch(history,/method:"POST"|fetch\([^)]*POST/);
});
