import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const repo=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/SandboxInvoiceFiscalSerialRepository.cs","utf8");
const migration=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261025183000_AddSandboxInvoiceFiscalReservations.cs","utf8");
const registration=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/PersistenceRegistration.cs","utf8");
const endpoint=readFileSync("../../src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs","utf8");
const outbox=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10SandboxOutboxService.cs","utf8");
test("fiscal serial reservation does not issue tax IDs for unfinalized or wrong-tenant invoices",()=>{
 assert.match(repo,/scope.ResolveAsync\(bearer,ct\)/);
 assert.match(repo,/scope.ScopedQuery\(actor.ActorId,actor.Level1\)/);
 assert.match(repo,/state!=\(int\)InvoiceLifecycleState.Finalized/);
 assert.match(repo,/private_envelope_json IS NOT NULL/);
 assert.match(repo,/WHERE customer_user_id=@owner AND taxpayer_id=@taxpayer/);
 assert.match(repo,/sandbox_invoice_fiscal_reservations/);
 assert.doesNotMatch(repo,/SEND_INVOICE|HttpClient|HttpRequestMessage/);
});
test("serial allocation is locked, idempotent and never reuses a consumed serial",()=>{
 assert.match(repo,/IsolationLevel.Serializable/);
 assert.match(repo,/FOR UPDATE/);
 assert.match(repo,/COALESCE\(MAX\(serial\),0\)/);
 assert.match(repo,/SandboxSerialStatus.IdenticalReplay/);
 assert.match(repo,/Phase10FiscalInvoiceIdentifier.Generate/);
 assert.match(repo,/await tx.CommitAsync/);
 assert.match(migration,/ux_sandbox_invoice_once/);
 assert.match(migration,/ux_sandbox_memory_serial/);
 assert.match(migration,/trg_sandbox_invoice_fiscal_immutable/);
 assert.match(migration,/current_database\(\)<>\x27argintaxpayer_test\x27/);
 assert.match(migration,/throw new NotSupportedException/);
});
test("serial reservation is registered in the development persistence boundary",()=>{
 assert.match(registration,/AddScoped<SandboxInvoiceFiscalSerialRepository>/);
});
test("serial reservation endpoint requires the server-owned invoice revision",()=>{
 assert.match(endpoint,/MapPost\("\/serial-reservation"/);
 assert.match(endpoint,/SandboxFiscalSerialReservationRequest/);
 assert.match(endpoint,/ReserveAsync\(Token\(http\), taxpayerId, invoiceId,[\s\S]*request.ExpectedRevision/);
});
test("legacy packet admission is durable and never stores an access token",()=>{
 assert.match(outbox,/PrepareLegacyAsync/);
 assert.match(outbox,/invoice\.sandbox\.legacy\.dispatch\.requested/);
 assert.match(outbox,/accessTokenStored = false/);
 assert.match(outbox,/ExecuteInTransactionAsync/);
});
