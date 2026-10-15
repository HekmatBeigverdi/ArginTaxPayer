import { readFileSync } from "node:fs";
import assert from "node:assert/strict";
import { test } from "node:test";
const legacyPage = readFileSync(new URL("../app/taxpayers/page.tsx", import.meta.url), "utf8");
const page = readFileSync(new URL("../app/(console)/users/page.tsx", import.meta.url), "utf8");
const workspace = readFileSync(new URL("../app/users/users-workspace.tsx", import.meta.url), "utf8");
const proxy = readFileSync(new URL("../app/api/customer-owners/[...path]/route.ts", import.meta.url), "utf8");

test("old taxpayer admin URL redirects to customer-first users screen", () => {
  assert.match(legacyPage, /redirect\("\/users"\)/);
  assert.match(page, /کاربران نرم‌افزار/);
  assert.match(page, /currentIdentity\(\)/);
  assert.match(page, /!isLevel1 && !isLevel2/);
});
test("software users screen is separate from fiscal participant records", () => {
  assert.match(workspace, /\/api\/customer-owners\/accounts/);
  assert.match(workspace, /customerUserId/);
  assert.doesNotMatch(workspace, /مؤدیان وابسته به این کاربر/);
  assert.doesNotMatch(workspace, /\/api\/customer-owners\/[^"]+\/taxpayers/);
  assert.match(workspace, /خریداران و مؤدیان در فهرست مالیاتی مستقل/);
  assert.match(workspace, /نیازمند تعیین مالکیت/);
});
test("server-proxy retains secure session and operation allowlist", () => {
  assert.match(proxy, /ACCESS_COOKIE/);
  assert.match(proxy, /isSameLocalOrigin/);
  assert.match(proxy, /path\[0\] === "accounts"/);
  assert.match(proxy, /path\[1\] === "taxpayers"/);
  assert.match(proxy, /Cache-Control/);
  assert.doesNotMatch(workspace, /localStorage|document\.cookie|Authorization: "Bearer"/);
});
test("UI does not invent subscription limits or enable blind taxpayer creation", () => {
  assert.match(workspace, /این پنجره فقط اطلاعات حساب ورود و انتساب تجاری را مدیریت می‌کند/);
  assert.doesNotMatch(workspace, /\/api\/taxpayers\/new/);
});
