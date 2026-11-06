import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {test} from "node:test";

const read=p=>readFileSync(new URL(`../../../${p}`,import.meta.url),"utf8");

test("legal-successor API chooses Production or the tested legacy Sandbox path from persisted memory",()=>{
  const endpoint=read("src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs");
  const correction=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18CorrectionServiceRouter.cs");
  const cancellation=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18CancellationServiceRouter.cs");
  const salesReturn=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18SalesReturnServiceRouter.cs");
  for(const router of [correction,cancellation,salesReturn]){
    assert.match(router,/memories\.ResolveAsync\(b(?:earer)?,?\s*t(?:axpayerId)?,?\s*i(?:nvoiceId)?/);
    assert.match(router,/binding\.Environment==TaxAuthorityEnvironment\.Production/);
    assert.match(router,/binding\.LegacySandbox/);
    assert.match(router,/classified_sandbox_legal_successor_not_supported/);
  }
  assert.match(endpoint,/Phase18CorrectionServiceRouter service/);
  assert.match(endpoint,/Phase18CancellationServiceRouter service/);
  assert.match(endpoint,/Phase18SalesReturnServiceRouter service/);
});

test("Production legal successor storage never uses Sandbox tables or test database guards",()=>{
  for(const name of ["Correction","Cancellation","SalesReturn"]){
    const source=read(`src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18Production${name}Service.cs`);
    assert.doesNotMatch(source,/sandbox_invoice_|sandbox_authority_|argintaxpayer_test|SandboxInvoiceLegacy/);
    assert.match(source,/authority_invoice_legal_relations/);
    assert.match(source,/Phase18InvoiceSubmissionService|submission\.SendAsync/);
    assert.match(source,/inquiry\.ReadAsync/);
    assert.doesNotMatch(source,/inquiry\.InquireAsync/);
  }
});

test("Production successor reservations require accepted same-memory predecessor evidence",()=>{
  const migration=read("src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261106010000_AddProductionLegalSuccessorRelations.cs");
  const preparation=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ProductionInvoicePreparationService.cs");
  assert.match(migration,/ux_authority_legal_correction_return_claim/);
  assert.match(migration,/predecessor_operation_id/);
  assert.match(migration,/ir\.report_json->>'Status'='SUCCESS'/);
  assert.match(migration,/pr\.fiscal_memory_id=NEW\.fiscal_memory_id/);
  assert.match(preparation,/production_legal_relation_not_verified/);
  assert.match(preparation,/@environment=1/);
  assert.match(migration,/ux_authority_legal_cancel_claim/);
  assert.match(migration,/authority_invoice_correction_workspaces/);
  assert.match(migration,/authority_invoice_sales_return_workspaces/);
});

test("legal successors preserve their source and share V7.9 quantity/edit rules",()=>{
  const correction=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ProductionCorrectionService.cs");
  const cancellation=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ProductionCancellationService.cs");
  const salesReturn=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ProductionSalesReturnService.cs");
  assert.match(correction,/Phase10CorrectionValidation\.AllowedLines/);
  assert.match(correction,/stale_revision/);
  assert.match(correction,/UPDATE authority_invoice_correction_workspaces SET state=2/);
  assert.match(cancellation,/Subject=InvoiceSubject\.Cancellation/);
  assert.match(cancellation,/cancellation_already_requested/);
  assert.match(salesReturn,/Phase10SalesReturnValidation\.AllowedLines/);
  assert.match(salesReturn,/return_quantity_exceeded/);
  assert.match(salesReturn,/UPDATE authority_invoice_sales_return_workspaces SET state=2/);
});
