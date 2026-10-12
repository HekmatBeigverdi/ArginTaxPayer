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

test("login form supports MFA challenge, recovery code, and accessible errors", () => {
  const form = read("app/identity/identity-forms.tsx");
  assert.match(form, /\/api\/identity\/mfa\/challenge/);
  assert.match(form, /\/api\/identity\/mfa\/complete/);
  assert.match(form, /useRecoveryCode: recovery/);
  assert.match(form, /role="alert"/);
  assert.match(form, /minLength=\{view === "register" \? 8/);
});
