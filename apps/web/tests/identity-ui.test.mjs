import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const read = (path) => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("identity and account pages use Persian RTL and server-side identity checks", () => {
  const login = read("app/identity/page.tsx");
  const account = read("app/account/page.tsx");
  const root = read("app/layout.tsx");
  assert.match(root, /lang="fa" dir="rtl"/);
  assert.match(login, /currentIdentity\(\)/);
  assert.match(login, /redirect\("\/account"\)/);
  assert.match(account, /if \(!actor\) redirect\("\/identity"\)/);
  assert.match(account, /currentIdentity\(\)/);
});

test("identity proxy fails closed outside development and keeps credentials HttpOnly", () => {
  const proxy = read("app/api/identity/[...path]/route.ts");
  const server = read("lib/identity.ts");
  assert.match(server, /NODE_ENV === "development"/);
  assert.match(server, /ARGIN_IDENTITY_UI_ENABLED/);
  assert.match(server, /\["127\.0\.0\.1", "localhost", "::1"\]/);
  assert.match(proxy, /httpOnly: true/);
  assert.match(proxy, /sameSite: "strict"/);
  assert.match(proxy, /sameOrigin\(req\)/);
  assert.match(proxy, /identityUiEnabled\(\)/);
  assert.match(proxy, /operations\.has\(operation\)/);
  assert.doesNotMatch(proxy, /console\.log/);
});

test("protected account provides logout, MFA, and owned-session controls", () => {
  const account = read("app/account/account-controls.tsx");
  assert.match(account, /\/api\/identity\/logout/);
  assert.match(account, /\/api\/identity\/sessions\/revoke/);
  assert.match(account, /\/api\/identity\/mfa\/enroll/);
  assert.match(account, /\/api\/identity\/mfa\/confirm/);
  assert.match(account, /setRecoveryCodes\(\[\]\)/);
});

test("unified identity screen has two top-level tabs, OTP choice and recovery path", () => {
  const page = read("app/identity/page.tsx");
  const flow = read("app/identity/identity-flow.tsx");
  const proxy = read("app/api/identity/[...path]/route.ts");
  assert.match(page, /<IdentityFlow \/>/);
  assert.match(flow, /role="tablist"/);
  assert.match(flow, />ورود<\/button>/);
  assert.match(flow, />ثبت‌نام<\/button>/);
  assert.match(flow, /role="group" aria-label="روش ورود"/);
  assert.match(flow, /رمز عبور را فراموش کرده‌اید/);
  assert.match(flow, /mfa\/challenge/);
  assert.match(flow, /mfa\/complete/);
  assert.match(flow, /useRecoveryCode:recovery/);
  assert.match(flow, /phone\/request/);
  assert.match(flow, /register\/complete/);
  assert.match(flow, /login\/complete/);
  assert.match(flow, /reset\/complete/);
  assert.match(flow, /role="alert"/);
  assert.match(proxy, /"phone\/login\/complete"/);
  assert.match(proxy, /httpOnly: true/);
});
