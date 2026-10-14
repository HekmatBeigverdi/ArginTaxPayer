import { readFileSync } from "node:fs";
import assert from "node:assert/strict";
import { test } from "node:test";

const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("both admin roles get a dashboard and customer has its own module menu", () => {
  const page = read("app/(console)/dashboard/page.tsx");
  assert.match(page, /actor\.roles\.includes\("Level1"\)/);
  assert.match(page, /actor\.roles\.includes\("Level2"\)/);
  assert.match(page, /actor\.roles\.includes\("Customer"\)/);
  assert.match(page, /admin \? adminModules : customerModules/);
  assert.match(page, /کاربران نرم‌افزار/);
  assert.match(page, /خریداران و مؤدیان/);
  assert.match(page, /تراکنش‌های مالی/);
  assert.match(page, /"\/parties"/);
});

test("login remains locked across successful navigation rather than reenabling submit", () => {
  const flow = read("app/identity/identity-flow.tsx");
  assert.match(flow, /className="auth-navigation-lock"/);
  assert.match(flow, /disabled=\{pending \|\| navigating\}/);
  assert.match(flow, /leavingForAccount = true;setNavigating\(true\);router\.replace\("\/dashboard"\)/);
  assert.match(flow, /if \(!leavingForAccount\) setPending\(false\)/);
  assert.doesNotMatch(flow, /identity-main-tabs" role="tablist"/);
  assert.doesNotMatch(flow, /className="auth-choice" role="group"/);
});

test("software users and parties are reachable by navigation and selectable customer", () => {
  const workspace = read("app/parties/buyer-taxpayer-directory.tsx");
  assert.match(workspace, /api\\/customer-owners\\/accounts/);
  assert.match(workspace, /console-directory-filters/);
  assert.match(workspace, /onSubmit=\\{addTaxpayer\\}/);
  assert.match(workspace, /buyer-new/);
  assert.match(workspace, /taxpayer-connection/);
});

test("dashboard mobile layout and Persian font fallbacks exist", () => {
  const css = read("app/globals.css");
  assert.match(css, /Vazirmatn/);
  assert.match(css, /\.app-module-grid/);
  assert.match(css, /max-width:780px/);
  assert.match(css, /\.auth-navigation-lock/);
});

test("authenticated landing and identity screen redirect to dashboard", () => {
  const home = read("app/page.tsx");
  const identity = read("app/identity/page.tsx");
  assert.match(home, /redirect\("\/dashboard"\)/);
  assert.match(identity, /redirect\("\/dashboard"\)/);
});
