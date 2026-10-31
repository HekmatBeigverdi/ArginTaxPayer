// Phase 11 Step 03 — regression contract for accepted Phase 10 inquiry paths.
// Offline checks only; no Sandbox calls, no invoice sending, no DB writes.
import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';

const load = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const reference = load('../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyReferenceInquiryService.cs');
const uid = load('../../../src/ArginTaxPayer.Infrastructure/Persistence/Invoices/Phase10LegacyUidInquiryService.cs');
const reconciler = load('../../../src/ArginTaxPayer.Application/Invoices/Phase10InquiryReconciliation.cs');
const refRoute = load('../app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/inquiry-by-reference/route.ts');
const uidRoute = load('../app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/inquiry-by-uid/route.ts');

test('reference inquiry only selects acknowledged invoicing and queries using receipt reference', () => {
  assert.match(reference, /d\.state=5/);
  assert.match(reference, /receipt\.authority_reference/);
  assert.match(reference, /INQUIRY_BY_REFERENCE_NUMBER/);
  assert.match(reference, /AppendReferenceInquiryEvidenceAsync/);
  assert.doesNotMatch(reference, /BeginSendAsync|SendPreparedAsync|ProcessInvoiceAsync/);
});
test('UID inquiry targets indeterminate operations without original packet retransmission', () => {
  assert.match(uid, /d\.state=6/);
  assert.match(uid, /INQUIRY_BY_UID/);
  assert.match(uid, /AppendInquiryEvidenceAsync/);
  assert.doesNotMatch(uid, /BeginSendAsync|SendPreparedAsync|ProcessInvoiceAsync/);
});
test('correlation is conservative and may not authorize a resend or fiscal acceptance', () => {
  for (const expression of [/MayAutomaticallyResend => false/, /IsFiscalAccepted => false/, /ProvesNotDelivered => false/]) {
    assert.match(reconciler, expression);
  }
  assert.match(reconciler, /ConflictingEvidence/);
  assert.match(reconciler, /NoConclusiveResult/);
});
test('both BFF inquiry routes require scoped session and do not accept invoice payloads', () => {
  for (const route of [refRoute, uidRoute]) {
    assert.match(route, /isSameLocalOrigin/);
    assert.match(route, /ACCESS_COOKIE/);
    assert.match(route, /payload_not_supported/);
    assert.match(route, /identityApiOrigin\(\)/);
    assert.match(route, /fiscalAccepted:false,authoritySubmitted:false/);
    assert.doesNotMatch(route, /send-and-inquire|cancel-and-inquire|correction-send-and-inquire|sales-return-send-and-inquire/);
  }
});
