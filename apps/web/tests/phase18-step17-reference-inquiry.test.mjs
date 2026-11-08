import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {resolve} from 'node:path';

const root=fileURLToPath(new URL('../../../',import.meta.url));
const src=readFileSync(resolve(root,
'src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase18ScopedLegacyReferenceInquiryService.cs'),'utf8');

test('reference-only inquiry routes from persisted environment and acknowledged receipt',()=>{
  for(const needle of [
    'gate.ResolveAsync(operationId, customerUserId',
    'INQUIRY_BY_REFERENCE_NUMBER',
    'r.authority_reference',
    'd.state=5 AND r.receipt_kind=1',
    'd.authority_environment=@environment',
    'a.attempt_number=1',
    'keys.RecoverAsync(customerUserId,taxpayerId',
    'tokens.RequestVerifiedEndpointAsync',
    'HttpMethod.Post,endpoint',
    'Phase10InquiryReconciliation.Evaluate'
  ]) assert.ok(src.includes(needle),needle);
  assert.match(src,/AllowAutoRedirect=false/);
  assert.match(src,/MaxResponseContentBufferSize=262144/);
  assert.match(src,/new InvoiceReconciliationIdentity\(operationId/);
  assert.doesNotMatch(src,/normal-enqueue|INVOICE\.V01",Retry=false|SendPreparedAsync/);
});

test('reference inquiry cannot claim fiscal SUCCESS or trigger resend',()=>{
  assert.match(src,/verdict.SafeCode,verdict.MatchedReference/);
  assert.doesNotMatch(src,/invoice_dispatch_operations\s+SET|AppendInitialReceiptAsync|Retry=true|requeue|Thread.Sleep/);
});
