import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read=(p)=>readFileSync(p,"utf8");
const prepare=read("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceWorkspacePromotionService.cs");
const save=read("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceEntryWorkspaceService.cs");
const bff=read("app/api/invoices/[...segments]/route.ts");
const api=read("../../src/ArginTaxPayer.Api/Invoices/InvoiceDraftDevelopmentEndpoints.cs");
const dispatch=read("../../src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs");
const sandbox=read("../../src/ArginTaxPayer.Infrastructure/Signing/Connection/SandboxAuthorityTokenProbe.cs");
const custody=read("../../src/ArginTaxPayer.Infrastructure/Persistence/Signing/SandboxFiscalMemoryMetadataService.cs");
const ui=read("app/invoices/invoice-validation-panel.tsx");

test("Step20: no fiscal invoice can be sent from preparation or its UI",()=>{
  assert.match(prepare,/fiscalSubmissionAuthorized=false/);
  assert.match(prepare,/signedPacketAvailable=false/);
  assert.match(prepare,/State=InvoiceLifecycleState.Draft/);
  assert.doesNotMatch(prepare,/GET_TOKEN|SEND_INVOICE|HttpClient|SendAsync\(/);
  assert.match(dispatch,/invoice_dispatch_not_enabled/);
  assert.match(ui,/result\.signedPacketAvailable!==false/);
  assert.match(ui,/result\.environmentSubmissionEnabled!==false/);
});
test("Step20: stale-revision, duplicate, cross-tenant and concurrent promotion fail closed",()=>{
  for(const code of [
    /scope\.ResolveAsync\(bearer,ct\)/,
    /scope\.ScopedQuery\(actor\.ActorId,actor\.Level1\)/,
    /currentOwner!=draft\.CustomerUserId/,
    /draft\.Revision!=request\.ExpectedRevision/,
    /IsolationLevel\.Serializable/,
    /FOR UPDATE/,
    /InvoiceDocuments\.AsNoTracking/,
    /workspace_already_bound_to_other_document/,
    /official_item_identifier_invalid/,
    /sandbox_memory_not_ready/,
    /customer_user_id=@owner/
  ])assert.match(prepare,code);
  assert.match(save,/FOR UPDATE/);
  assert.match(save,/InvoiceDocuments\.AsNoTracking\(\)\.AnyAsync/);
  assert.match(prepare,/internal_snapshot_already_created/);
  assert.match(bff,/Number\.isSafeInteger\(data\.expectedRevision\)/);
});
test("Step20: credentials cannot fall back to Production during Sandbox test",()=>{
  assert.match(sandbox,/SandboxConnectionTest:Enabled/);
  assert.match(sandbox,/SandboxConnectionTest:OperatorApprovedExperimentalApi/);
  assert.match(sandbox,/uri\.Host != "sandboxrc\.tax\.gov\.ir"/);
  assert.doesNotMatch(sandbox,/https:\/\/tp\.tax\.gov\.ir/);
  assert.match(custody,/Sandbox:true/);
  assert.match(custody,/CryptographicOperations\.ZeroMemory\(pkcs8\)/);
});
test("Step20: HTTP draft preparation requires session, same origin and exact JSON shape",()=>{
  assert.match(bff,/isSameLocalOrigin/);
  assert.match(bff,/ACCESS_COOKIE/);
  assert.match(bff,/Object\.keys\(data\)\.length!==1/);
  assert.match(bff,/invalid_snapshot_revision/);
  assert.match(api,/Local\(http\)/);
  assert.match(api,/InvoiceWorkspacePromotionService/);
  assert.doesNotMatch(prepare,/Console\.WriteLine|ILogger\.LogDebug/);
});
test("Step20: snapshot checks identifier shape and owner item without treating local catalog as authority",()=>{
  assert.match(prepare,/CustomerCatalogItems\.AsNoTracking/);
  assert.match(prepare,/x\.CustomerUserId==draft\.CustomerUserId/);
  assert.match(prepare,/x\.Status==1/);
  assert.match(prepare,/ownerItems\.Count!=itemIds\.Length/);
  assert.match(prepare,/item\.BaseUnitId!=line\.UnitId/);
  assert.match(prepare,/OfficialIdentifierSnapshot is not \{ Length: 13 \} code/);
  assert.match(prepare,/!code\.All\(char\.IsAsciiDigit\)/);
  assert.match(prepare,/official_item_identifier_invalid/);
});
