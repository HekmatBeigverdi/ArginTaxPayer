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

test("successful login remains busy until protected page navigation", () => {
  const flow = read("app/identity/identity-flow.tsx");
  assert.match(flow, /setNavigating\(true\)/);
  assert.match(flow, /disabled=\{pending \|\| navigating(?: \|\| [^}]+)?\}/);
  assert.match(flow, /در حال ورود به پنل/);
});

test("administrator customer form requires role-gated UI and authenticated BFF operation", () => {
  const account = read("app/account/page.tsx");
  const form = read("app/account/admin-create-customer.tsx");
  const proxy = read("app/api/identity/[...path]/route.ts");
  assert.match(account, /role === "Level1" \|\| role === "Level2"/);
  assert.match(form, /admin\/customers/);
  assert.match(form, /ایمیل \(اختیاری\)/);
  assert.match(proxy, /"admin\/customers"/);
  assert.match(proxy, /httpOnly: true/);
});

test("OTP sign-up shows existing-user conflict and live 3-minute resend countdown", () => {
  const flow = read("app/identity/identity-flow.tsx");
  const proxy = read("app/api/identity/[...path]/route.ts");
  assert.match(flow, /response.status === 409/);
  assert.match(flow, /mobile_already_registered/);
  assert.match(flow, /response.status === 429/);
  assert.match(flow, /result.retryAfterSeconds/);
  assert.match(flow, /otp-countdown/);
  assert.match(flow, /setInterval/);
  assert.match(flow, /disabled=\{pending \|\| navigating \|\| \(phoneOtp/);
  assert.match(proxy, /response.status === 429/);
  assert.match(proxy, /retryAfterSeconds: remaining/);
  assert.match(proxy, /response.status === 409/);
});
