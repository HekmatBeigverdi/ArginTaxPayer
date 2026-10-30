import test from "node:test";import assert from "node:assert/strict";
import {readFileSync} from "node:fs";import {dirname,resolve} from "node:path";import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../../..");
const read=p=>readFileSync(resolve(root,p),"utf8");
const sdk="references/tax-sdk/modern-2.0.32/tax-collect-data-sdk-dotnet-tax-collect-data-sdk-2.0.32/TaxCollectData.Library/Dto/";
test("archived SDK header and project invoice mapper share subject and predecessor tax ID",()=>{
 const header=read(sdk+"HeaderDto.cs");
 const invoice=read(sdk+"InvoiceDto.cs");
 const mapper=read("src/ArginTaxPayer.Domain/Invoices/Phase10InvoiceWireMapper.cs");
 const projector=read("src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyFrozenInvoiceProjector.cs");
 assert.match(header,/int ins \{/);
 assert.match(header,/string\? irtaxid \{/);
 assert.match(invoice,/HeaderDto Header/);
 assert.match(mapper,/ins = \(int\)source.Subject/);
 assert.match(mapper,/irtaxid = source.ReferenceTaxId/);
 assert.match(projector,/InvoiceSubject.Correction/);
});
test("correction migration uses active unique index and scoped reference",()=>{
 const m=read("src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261030140000_AddSandboxCorrectionWorkspaces.cs");
 assert.ok(m.includes("predecessor_fiscal_tax_id"));
 assert.ok(m.includes("ux_correction_active_predecessor"));
 assert.ok(m.includes("FOREIGN KEY"));
});
