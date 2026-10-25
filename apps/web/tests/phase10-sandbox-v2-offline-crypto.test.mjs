import {test} from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const code=readFileSync("../../src/ArginTaxPayer.Infrastructure/Signing/InvoiceSnapshot/Phase10SandboxInvoicePacketCryptography.cs","utf8");
const fiscal=readFileSync("../../src/ArginTaxPayer.Domain/Invoices/Phase10FiscalInvoiceIdentifier.cs","utf8");
const readiness=readFileSync("../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LiveSendReadinessService.cs","utf8");
const api=readFileSync("../../src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs","utf8");
test("official SDK v2 primitives are genuine RSA/RS256 plus AES-GCM, not fake signatures",()=>{
 assert.match(code,/RSASignaturePadding.Pkcs1/);
 assert.match(code,/RSAEncryptionPadding.OaepSHA256/);
 assert.match(code,/new AesGcm\(cek,16\)/);
 assert.match(code,/RSA-OAEP-256/);
 assert.match(code,/A256GCM/);
 assert.match(code,/issuedCertificate.GetRSAPublicKey\(\)/);
 assert.match(code,/CryptographicOperations.ZeroMemory\(cek\)/);
 assert.doesNotMatch(code,/HttpClient|HttpRequestMessage|new NpgsqlConnection/);
});
test("tax ID requires a real six-character Sandbox fiscal memory and durable serial input",()=>{
 assert.match(fiscal,/sandboxMemoryCode is not \{Length:6\}/);
 assert.match(fiscal,/serial < 1/);
 assert.match(fiscal,/Generate\(string sandboxMemoryCode,long serial,DateTime issueUtc\)/);
 assert.match(fiscal,/ToUnixTimeSeconds\(\)\/86400L/);
});
test("no live send can occur before signature/certificate and durable outbox are integrated",()=>{
 assert.match(readiness,/invoice.serial.durable_reservation_required/);
 assert.match(readiness,/invoice.sandbox.certificate_chain_not_verified/);
 assert.match(readiness,/invoice.v2.protocol_not_verified_for_sandbox/);
 assert.match(readiness,/invoice.sandbox_outbox.not_connected/);
 assert.match(readiness,/environment,false,false,false,false/);
 assert.match(api,/invoice_dispatch_not_enabled/);
});


test("opt-in v2 transport pins Sandbox HTTPS, has no redirects or retries and is not wired to UI",()=>{
 const transport=readFileSync("../../src/ArginTaxPayer.Infrastructure/Signing/Connection/SandboxV2InvoiceHttpTransport.cs","utf8");
 assert.match(transport,/sandboxrc\.tax\.gov\.ir/);
 assert.match(transport,/SandboxInvoiceV2:OperatorApprovedEndpoint/);
 assert.match(transport,/SandboxInvoiceV2:Enabled/);
 assert.match(transport,/new Uri\(endpoint,"api\/v2\/nonce"\)/);
 assert.match(transport,/new Uri\(endpoint,"api\/v2\/invoice"\)/);
 assert.match(transport,/AllowAutoRedirect=false/);
 assert.match(transport,/UseCookies=false/);
 assert.match(transport,/sandbox_v2_outcome_indeterminate/);
 assert.doesNotMatch(transport,/tp\.tax\.gov\.ir|RetryAsync|Task\.Delay/);
 const dispatch=readFileSync("../../src/ArginTaxPayer.Api/Invoices/Phase10InvoiceDispatchDevelopmentEndpoints.cs","utf8");
 assert.match(dispatch,/invoice_dispatch_not_enabled/);
 assert.doesNotMatch(dispatch,/SandboxV2InvoiceHttpTransport/);
});
