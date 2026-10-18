import { readFileSync, existsSync } from "node:fs";
import { test } from "node:test";
import assert from "node:assert/strict";

const root = new URL("../", import.meta.url);
const read = path => readFileSync(new URL(path, root), "utf8");
const pathExists = path => existsSync(new URL(path, root));

test("Phase07 domain and security suites remain present", () => {
  for (const name of [
    "Phase07InvoiceFinancialTests",
    "Phase07MonetaryTaxRulesTests",
    "Phase07CancellationInvoiceTests",
    "Phase07CorrectionInvoiceTests",
    "Phase07SalesReturnInvoiceTests",
    "Phase07InvoiceConcurrencyIdempotencyTests",
    "Phase07InvoicePersistenceMetadataTests",
    "Phase07FiscalParticipantTenantIsolationTests",
    "Phase07ResumableInvoiceDraftTests",
    "Phase07InvoiceValidationReferenceUiTests",
    "Phase07ComprehensiveRegressionTests"
  ]) assert.ok(pathExists("tests/ArginTaxPayer.Tests/" + name + ".cs"), name);
});

test("Phase03-06 existing suites are retained for backward compatibility", () => {
  for (const path of [
    "scripts/phase-03-step-10-verify.zsh",
    "scripts/phase04-step13-verify.zsh",
    "scripts/phase05-step15-verify.zsh",
    "scripts/phase06-step15-auth-smoke.py"
  ]) assert.ok(pathExists(path), path);
});

test("invoice proxy is a narrow authenticated and origin checked allowlist", () => {
  const proxy = read("apps/web/app/api/invoices/[...segments]/route.ts");
  assert.match(proxy, /identityUiEnabled\(\)/);
  assert.match(proxy, /ACCESS_COOKIE/);
  assert.match(proxy, /isSameLocalOrigin/);
  assert.match(proxy, /const uuid =/);
  assert.match(proxy, /"workspaces"/);
  assert.match(proxy, /"references"/);
  assert.match(proxy, /"validation"/);
  assert.match(proxy, /"invoiceDescription"/);
  assert.match(proxy, /"settlementMethod"/);
  assert.match(proxy, /"cashAmountRial"/);
  assert.match(proxy, /"creditAmountRial"/);
  assert.doesNotMatch(proxy, /export async function (?:DELETE|PUT|PATCH)/);
  assert.doesNotMatch(proxy, /\/api\/.*(?:submit|issue|send|finalize)/i);
});

test("development API does not register transmission or issuance", () => {
  const endpoints = read("src/ArginTaxPayer.Api/Invoices/InvoiceDraftDevelopmentEndpoints.cs");
  assert.match(endpoints, /IPAddress\.IsLoopback/);
  assert.match(endpoints, /InvoiceValidationReferenceService/);
  assert.match(endpoints, /InvoiceEntryWorkspaceService/);
  assert.match(endpoints, /\/api\/dev\/invoice-references/);
  assert.doesNotMatch(endpoints, /MapPost\("[^"]*(?:send|submit|issue|finalize)/i);
});

test("workspace read and write operations are tenant scoped and CAS guarded", () => {
  const service = read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceEntryWorkspaceService.cs");
  assert.match(service, /scope\.ScopedQuery/);
  assert.match(service, /CustomerPartyLinks/);
  assert.match(service, /l\.CustomerUserId == access\.Item2/);
  assert.match(service, /c\.CustomerUserId == access\.Item2/);
  assert.match(service, /x\.Revision == input\.ExpectedRevision/);
  assert.match(service, /ExecuteUpdateAsync/);
  assert.match(service, /InvoiceDraftSettlementPolicy\.IsConsistent/);
  assert.doesNotMatch(service, /SubmitAsync|TransmitAsync|AuthorityAccepted/);
});

test("validation references remain unverified and outside fiscal issuance", () => {
  const service = read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceValidationReferenceService.cs");
  const component = read("apps/web/app/invoices/invoice-validation-panel.tsx");
  assert.match(service, /false,null/);
  assert.match(service, /scope\.ScopedQuery/);
  assert.match(service, /false,findings\.AsReadOnly\(\)/);
  assert.match(component, /disabled>/);
  assert.match(component, /isSubmissionAuthorized/);
  assert.match(component, /dirty/);
});

test("invoice wizard preserves compact modal, catalog search, settlement and resume", () => {
  const ui = read("apps/web/app/invoices/workspace.tsx");
  const catalog = read("apps/web/app/invoices/searchable-catalog-picker.tsx");
  assert.match(ui, /<InvoiceValidationPanel/);
  assert.match(ui, /<SearchableCatalogPicker/);
  assert.match(ui, /expectedRevision:editing\?\.revision/);
  assert.match(ui, /invoiceDescription:invoiceDescription\.trim\(\) \|\| null/);
  assert.match(ui, /settlementMethod:settlement/);
  assert.match(ui, /ذخیره و ادامه بعداً/);
  assert.match(ui, /ویرایش و ادامه/);
  assert.match(ui, /console-table-footer/);
  assert.match(catalog, /role="combobox"/);
  assert.match(catalog, /role="listbox"/);
});

test("additive migrations keep prior tenant isolation and resumable drafts", () => {
  const migrationPaths = [
    "src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261018090000_ScopeFiscalParticipantsPerCustomer.cs",
    "src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261018150000_AddResumableInvoiceEntryWorkspaces.cs"
  ];
  for (const path of migrationPaths) assert.ok(pathExists(path), path);
  const owner = read(migrationPaths[0]);
  const entry = read(migrationPaths[1]);
  assert.match(owner, /ux_parties_owner_kind_identifier/);
  assert.match(owner, /ux_taxpayer_profiles_customer_kind_identifier/);
  assert.match(entry, /invoice_entry_workspaces/);
  assert.match(entry, /revision bigint/);
});
