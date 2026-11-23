import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const root="../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/";
const policy=readFileSync(`${root}InvoiceParticipantIdentityPolicy.cs`,"utf8");
const production=readFileSync(`${root}Phase18ProductionInvoicePreparationService.cs`,"utf8");
const sandbox=readFileSync(`${root}Phase10LegacyInvoicePreparationService.cs`,"utf8");
const panel=readFileSync("app/invoices/invoice-send-readiness-panel.tsx","utf8");

test("invoice preparation reports which participant identity is missing",()=>{
  assert.match(policy,/seller_economic_identifier_missing/);
  assert.match(policy,/buyer_identity_missing/);
  assert.match(production,/InvoiceParticipantIdentityPolicy\.MissingCode\(seller,buyer,!type2\)/);
  assert.match(sandbox,/InvoiceParticipantIdentityPolicy\.MissingCode\(seller,buyer,!type2\)/);
  assert.match(panel,/seller_economic_identifier_missing:"کد اقتصادی فروشنده/);
  assert.match(panel,/buyer_identity_missing:"شناسه خریدار/);
});
