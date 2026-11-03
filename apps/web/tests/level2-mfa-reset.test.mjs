import { readFileSync } from 'node:fs';
import assert from 'node:assert/strict';
import { test } from 'node:test';
const read = path => readFileSync(new URL(path, import.meta.url), 'utf8');
const view = read('../app/users/users-workspace.tsx');
const proxy = read('../app/api/identity/[...path]/route.ts');
const listing = read('../app/api/customer-owners/[...path]/route.ts');
test('Level1 software users offer a server role filter and an explicit confirmed MFA reset', () => {
  assert.match(view, /ادمین‌های سطح ۲/);
  assert.match(view, /params\.set\("role"/);
  assert.match(view, /<th scope="col">ریست MFA<\/th>/);
  assert.match(view, /aria-labelledby="mfa-reset-heading"/);
  assert.match(view, /targetUserId: resetTarget\.customerUserId/);
  assert.match(view, /\/api\/identity\/mfa\/level2\/reset/);
  assert.match(view, /PendingLevel2/);
  assert.match(view, /level1 &&/);
  assert.doesNotMatch(view, /09192424119|09303492171/);
});
test('reset and role queries pass through authenticated same-origin proxies', () => {
  assert.match(proxy, /operations\.add\("mfa\/level2\/reset"\)/);
  assert.match(proxy, /"mfa\/level2\/reset": "mfa\/level2\/reset"/);
  assert.match(proxy, /"mfa\/level2\/reset"[^\n]+&& !token/);
  assert.match(proxy, /isSameLocalOrigin/);
  assert.match(listing, /"ownership", "role"/);
});
