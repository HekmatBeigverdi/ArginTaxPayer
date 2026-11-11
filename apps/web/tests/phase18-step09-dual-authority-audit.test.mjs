import { fileURLToPath } from "node:url";
import { resolve } from "node:path";
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

// Source-only boundary audit. This intentionally DOES NOT claim dual-authority
// routing exists. Tests must keep known blockers visible until real fixes land.
const repoRoot = fileURLToPath(new URL("../../../", import.meta.url));
const read = (relative) => readFileSync(resolve(repoRoot, relative), "utf8");
const root = 'src/ArginTaxPayer.Infrastructure/';
const inv = root + 'Persistence/Invoices/';
const signing = root + 'Persistence/Signing/';
const paths = {
  fiscal: root + 'Persistence/Taxpayers/FiscalMemory.cs',
  sandbox: signing + 'SandboxFiscalMemoryMetadataService.cs',
  readiness: inv + 'Phase10LiveSendReadinessService.cs',
  submission: inv + 'Phase10OneClickSandboxSubmissionService.cs',
  prepare: inv + 'Phase10LegacyInvoicePreparationService.cs',
  arm: inv + 'Phase10DispatchArmService.cs',
  worker: inv + 'Phase10LegacyOutboxWorker.cs',
  uid: inv + 'Phase10LegacyUidInquiryService.cs',
  reference: inv + 'Phase10LegacyReferenceInquiryService.cs',
  cancellation: inv + 'Phase10SandboxCancellationService.cs',
  correction: inv + 'Phase10SandboxCorrectionService.cs',
  returns: inv + 'Phase10SandboxSalesReturnService.cs',
  key: root + 'Signing/Protection/KeyProtectionContracts.cs',
};

test('existing persisted fiscal memories and dedicated Sandbox metadata are distinct', () => {
  const fiscal = read(paths.fiscal);
  const sandbox = read(paths.sandbox);
  assert.match(fiscal, /class FiscalMemory/);
  assert.match(sandbox, /sandbox_fiscal_memories/);
  assert.match(sandbox, /customer_user_id=@owner/);
  assert.match(sandbox, /taxpayer_id=@taxpayer/);
});

test('Sandbox send and legal-child flows remain explicitly Sandbox-only', () => {
  assert.match(read(paths.submission), /info.Environment!="Sandbox"/);
  assert.match(read(paths.prepare), /env.GetString\(\)!="Sandbox"/);
  for (const name of ['cancellation','correction','returns']) {
    const source = read(paths[name]);
    assert.match(source, /"environment"\]\s*!=\s*"Sandbox"/);
  }
});

test('Sandbox inquiry is pinned to Sandbox operation and Sandbox memory', () => {
  for (const name of ['uid','reference']) {
    const source = read(paths[name]);
    assert.match(source, /JOIN sandbox_fiscal_memories m ON m.id=d.fiscal_memory_id/);
    assert.match(source, /invoice\.sandbox\.legacy\.dispatch\.requested/);
    assert.match(source, /TryResolveSandboxBase\(/);
  }
});

test('legacy Sandbox workflows admit only isolated test or explicitly approved hosted production databases', () => {
  assert.match(read(paths.sandbox), /HostedSandboxDeployment\.IsAllowed/);
  for (const name of ['prepare','arm','cancellation','correction','returns']) {
    assert.match(read(paths[name]), /SandboxDatabasePolicy\.IsAllowed/,
      name + ' must use the shared deployment admission policy');
  }
  const policy = read('src/ArginTaxPayer.Infrastructure/Configuration/SandboxDatabasePolicy.cs');
  assert.match(policy, /databaseName == "argintaxpayer_test"/);
  assert.match(policy, /databaseName == "argintaxpayer_prod"/);
  assert.match(policy, /HostedSandboxDeployment\.IsAllowed/);
});

test('persisted fiscal memory has an explicit nullable authority environment, while dispatch binding remains blocked', () => {
  const fiscal = read(paths.fiscal);
  assert.match(fiscal, /FiscalConnectionEnvironment\? AuthorityEnvironment/);
  const registration = read(root + 'Persistence/Taxpayers/FiscalMemoryManagementService.cs');
  assert.match(registration, /request\?\.AuthorityEnvironment is not/);
  const migration = read(root + 'Persistence/Migrations/20261104220000_AddFiscalMemoryAuthorityEnvironment.cs');
  assert.match(migration, /nullable: true/);
  assert.match(migration, /authority_environment IS NULL OR authority_environment IN \(1, 2\)/);
  assert.match(read(paths.readiness), /sandboxFiscalMemoryId/);
});

test('all legal send operations remain fail-closed by default on Production service unit', () => {
  const unit = read('ops/phase18-systemd/argintaxpayer-api.service');
  assert.match(unit, /Environment=ArginTaxPayer__EnableOperationalEndpoints=false/);
  assert.match(unit, /Environment=SandboxInvoiceLegacy__Enabled=false/);
  assert.match(unit, /Environment=SandboxInvoiceLegacy__OperatorApprovedEndpoint=false/);
  const web = read('ops/phase18-systemd/argintaxpayer-web.service');
  assert.match(web, /Environment=ARGIN_PRODUCTION_UI_ENABLED=false/);
});

test('key encryption distinguishes Sandbox binding without reading global deployment mode', () => {
  const key = read(paths.key);
  assert.match(key, /bool Sandbox = false/);
  assert.match(key, /argin:key:sandbox:v1/);
});

test('existing worker protects retry/indeterminate states', () => {
  const worker = read(paths.worker);
  assert.match(worker, /[Ii]ndeterminate|[Ii]ndeterminat/);
  assert.match(worker, /invoice\.sandbox\.legacy\.dispatch\.requested/);
});
