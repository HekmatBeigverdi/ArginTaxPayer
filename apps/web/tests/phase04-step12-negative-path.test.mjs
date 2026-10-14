import { readFileSync } from "node:fs";
import assert from "node:assert/strict";
import { test } from "node:test";

const usersPage = readFileSync(new URL("../app/users/page.tsx", import.meta.url), "utf8");
const users = readFileSync(new URL("../app/users/users-workspace.tsx", import.meta.url), "utf8");
const bff = readFileSync(new URL("../app/api/customer-owners/[...path]/route.ts", import.meta.url), "utf8");
const old = readFileSync(new URL("../app/taxpayers/page.tsx", import.meta.url), "utf8");

test("root and partner workspace requires current identity before rendering", () => {
  assert.match(usersPage, /currentIdentity\(\)/);
  assert.match(usersPage, /!isLevel1 && !isLevel2/);
  assert.match(usersPage, /redirect\("\/identity"\)/);
});
test("protected BFF reads HttpOnly cookie server-side and fails closed", () => {
  assert.match(bff, /ACCESS_COOKIE/);
  assert.match(bff, /identityUiEnabled\(\)/);
  assert.match(bff, /isSameLocalOrigin/);
  assert.match(bff, /redirect: "manual"/);
  assert.match(bff, /Cache-Control/);
  assert.match(bff, /uuid\.test\(path\[0\]\)/);
  assert.doesNotMatch(users, /document\.cookie|localStorage|Authorization: "Bearer"/);
});
test("legacy taxpayer menu redirects to customer-first page", () => {
  assert.match(old, /redirect\("\/users"\)/);
});
test("UI cannot grant ownership via GET and uses revision/reason", () => {
  assert.match(users, /method: "POST"/);
  assert.match(users, /expectedRevision: selected\.revision/);
  assert.match(users, /explanation: transferReason\.trim\(\)/);
  assert.match(users, /partnerLevel2UserId: target \|\| null/);
  assert.match(users, /users-dialog/);
  assert.match(users, /close\(\)/);
});
