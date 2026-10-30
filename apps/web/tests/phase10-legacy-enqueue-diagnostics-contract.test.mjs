import test from 'node:test';import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';import {dirname,resolve} from 'node:path';import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
test('legacy enqueue classifies HTTP and packet rejections without leaking payloads',()=>{
const source=readFileSync(resolve(root,'src/ArginTaxPayer.Infrastructure/Signing/Connection/LegacyInvoiceSubmissionHttpTransport.cs'),'utf8');
for(const code of ['sandbox_legacy_http_400','sandbox_legacy_http_404','sandbox_legacy_http_405','sandbox_legacy_http_503','sandbox_legacy_response_errors','sandbox_legacy_packet_rejected','errorCode'])assert.ok(source.includes(code),code);
assert.ok(!source.includes('return new(false, packetError.GetString()'));
});
