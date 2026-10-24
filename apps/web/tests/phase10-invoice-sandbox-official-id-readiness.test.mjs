import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const catalog=readFileSync("app/catalog/workspace.tsx","utf8");
const entry=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceEntryWorkspaceService.cs","utf8");
const ui=readFileSync("app/invoices/workspace.tsx","utf8");
const options=readFileSync("../../src/ArginTaxPayer.Api/Invoices/InvoiceDraftDevelopmentEndpoints.cs","utf8");
const invoiceBff=readFileSync("app/api/invoices/[...segments]/route.ts","utf8");
const validation=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceValidationReferenceService.cs","utf8");
const validationUi=readFileSync("app/invoices/invoice-validation-panel.tsx","utf8");
const dispatch=readFileSync("../../src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs","utf8");

test("internal product code and official 13-digit sstid remain distinct",()=>{
  assert.match(catalog,/کد داخلی \(صرفاً شناسه داخل نرم‌افزار/);
  assert.match(catalog,/نگاشت شناسه رسمی/);
  assert.match(entry,/OfficialIdentifierSnapshot/);
  assert.match(entry,/validOfficialMappings/);
  assert.match(entry,/CatalogOfficialMappings/);
  assert.match(entry,/OfficialGoodsServiceEntries/);
  assert.match(entry,/OfficialIdentifierSnapshot = officialCode/);
  assert.match(ui,/sstid:/);
  assert.match(ui,/شناسه رسمی ۱۳ رقمی/);
});
test("Sandbox selection is owner scoped and never passed as Production memory FK",()=>{
  assert.match(options,/FROM sandbox_fiscal_memories/);
  assert.match(options,/customer_user_id=@owner/);
  assert.match(options,/private_envelope_json IS NOT NULL/);
  assert.match(entry,/input\.FiscalMemoryId\.HasValue && input\.SandboxFiscalMemoryId\.HasValue/);
  assert.match(entry,/WHERE id=@sandbox AND taxpayer_id=@taxpayer AND customer_user_id=@owner/);
  assert.match(entry,/SandboxFiscalMemoryId/);
  assert.match(ui,/value=\{"sandbox:"\+m\.id\}/);
  assert.match(ui,/sandboxFiscalMemoryId:memoryId\.startsWith\("sandbox:"\)/);
  assert.match(ui,/fiscalMemoryId:memoryId&&!memoryId\.startsWith\("sandbox:"\)/);
  assert.match(invoiceBff,/"sandboxFiscalMemoryId"/);
  assert.match(invoiceBff,/uuid\.test\(data\.sandboxFiscalMemoryId\)/);
  assert.match(invoiceBff,/data\.fiscalMemoryId != null && data\.sandboxFiscalMemoryId != null/);
});
test("validation provides specific missing-code diagnostics but never grants invoice dispatch",()=>{
  assert.match(validation,/invoice\.line\.official_identifier_missing/);
  assert.match(validation,/invoice\.line\.official_catalog_missing/);
  assert.match(validation,/invoice\.rules\.not_approved/);
  assert.match(validation,/false,findings\.AsReadOnly\(\)/);
  assert.match(validationUi,/هنوز قطعی و قابل ارسال نیست/);
  assert.match(validationUi,/selectedEnvironment/);
  assert.match(validation,/invoice.memory.required_for_submission/);
  assert.match(dispatch,/invoice_dispatch_not_enabled/);
});
