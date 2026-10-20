import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const ui=readFileSync(new URL("../app/invoices/workspace.tsx",import.meta.url),"utf8");
const server=readFileSync(new URL("../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceEntryWorkspaceService.cs",import.meta.url),"utf8");

test("completed final step rejects unspecified terms on server and UI",()=>{
 assert.match(server,/input.LastCompletedStep == 2/);
 assert.match(server,/input.SettlementMethod == InvoiceDraftSettlementMethod.Unknown/);
 assert.match(server,/input.CashAmountRial is null \|\| input.CreditAmountRial is null/);
 assert.match(ui,/completeStep===2 && settlement===0/);
 assert.match(ui,/settlementError \|\| settlement===0/);
});
test("unfinished steps remain resumable and mixed settlement is not accepted with zero leg",()=>{
 assert.match(ui,/step===2 && settlement===0 \? 1 : step/);
 assert.match(server,/CashAndCredit => cashRial.Value > 0 && creditRial.Value > 0/);
});
