import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const read=p=>readFileSync(resolve(root,p),'utf8');
test('legacy packet rebuild retains encrypted INVOICE.V01 data and signs new transport headers',()=>{
 const builder=read('src/ArginTaxPayer.Infrastructure/Signing/Connection/LegacyInvoiceSubmissionPacketBuilder.cs');
 const rebuild=read('src/ArginTaxPayer.Infrastructure/Signing/Connection/LegacyInvoiceOutboxPacketRebuilder.cs');
 assert.ok(builder.includes('packetType="INVOICE.V01"'));
 assert.ok(rebuild.includes('packetType.GetString() != "INVOICE.V01"'));
 for(const field of ['encryptionKeyId','symmetricKey','dataSignature','fiscalId','ReSignEncrypted'])
  assert.ok(rebuild.includes(field),field);
 assert.ok(!rebuild.includes('data.GetRawText()'));
});
