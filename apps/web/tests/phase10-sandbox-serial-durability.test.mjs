import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const repo=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/SandboxInvoiceFiscalSerialRepository.cs","utf8");
const migration=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261025183000_AddSandboxInvoiceFiscalReservations.cs","utf8");
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
