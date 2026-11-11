import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const read = (p) => readFileSync(new URL('../../../' + p, import.meta.url), 'utf8');

test('annual binding is immutable at database layer', () => {
  const s = read('src/ArginTaxPayer.Infrastructure/Persistence/Migrations/20261031193000_AddPhase12AnnualSubscriptionBinding.cs');
  assert.match(s, /TG_OP <> 'INSERT'/);
  assert.match(s, /s\.kind=1 AND s\.state=2/);
  assert.match(s, /ux_subscription_binding_exception_pending/);
});
test('only the authenticated customer or scoped partner may request and Level1 with MFA may review', () => {
  const s = read('src/ArginTaxPayer.Infrastructure/Persistence/Subscriptions/SubscriptionExceptionReviewService.cs');
  assert.match(s, /plan\.CustomerUserId==actor/);
  assert.match(s, /if\(!customer&&!partner\)throw new UnauthorizedAccessException/);
  assert.match(s, /IdentityRoleNames\.Level1/);
  assert.match(s, /PartnerLevel2UserId\s*==\s*actor/);
  assert.match(s, /IsRootLevel1WithMfaAsync\(actor/);
  assert.doesNotMatch(s, /AnnualSubscriptionBindings\.(Remove|Update)/);
});
test('API cannot silently rebind after exception approval', () => {
  const s = read('src/ArginTaxPayer.Api/Subscriptions/Phase12SubscriptionExceptionEndpoints.cs');
  assert.match(s, /bindingChanged = input\.Approve/);
  assert.match(s, /service\.ReviewAsync\(Bearer\(http\), requestId, input\.Approve, input\.DecisionReason/);
  assert.match(s, /ContainsKey\("Origin"\)/);
  assert.match(s, /RequireRateLimiting/);
});
