import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {resolve,dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../../..');
const source=p=>readFileSync(resolve(root,p),'utf8');
test('invoice owner can approve review without admin MFA',()=>{
 const s=source('src/ArginTaxPayer.Infrastructure/Persistence/Invoices/InvoiceReviewCommandService.cs');
 assert.match(s,/bool authorized = isCustomer \|\| isRoot \|\| isPartner/);
 assert.match(s,/principal.ActorId == invoice.CustomerUserId/);
 assert.match(s,/CustomerTaxpayerMemberships/);
 assert.match(s,/InvoiceReviewPolicy.CanApply/);
});
test('UI does not assert mandatory manager approval',()=>{
 const s=source('apps/web/app/invoices/invoice-send-readiness-panel.tsx');
 assert.ok(!s.includes('تأیید بازبینی (مدیر)'));
 assert.ok(!s.includes('تأیید توسط مدیر دارای MFA'));
});
