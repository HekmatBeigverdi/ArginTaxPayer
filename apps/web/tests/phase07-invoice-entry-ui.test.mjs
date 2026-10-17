import { readFileSync } from "node:fs";
import assert from "node:assert/strict";
import { test } from "node:test";

const read = path => readFileSync(new URL(path, import.meta.url), "utf8");
const proxy = read("../app/api/invoices/[...segments]/route.ts");
const page = read("../app/(console)/invoices/page.tsx");
const workspace = read("../app/invoices/workspace.tsx");
const shell = read("../app/components/console-shell.tsx");
const dashboard = read("../app/(console)/dashboard/page.tsx");

test("invoice entry is accessible from role-scoped console and dashboard", () => {
  assert.match(shell, /path: "\/invoices"/);
  assert.match(dashboard, /href: "\/invoices"/);
  assert.match(page, /currentIdentity\(\)/);
  assert.match(page, /redirect\("\/identity"\)/);
  assert.match(page, /dir="rtl"/);
});

test("Next proxy is restricted to GET and POST with scoped invoice paths", () => {
  assert.match(proxy, /identityUiEnabled\(\)/);
  assert.match(proxy, /ACCESS_COOKIE/);
  assert.match(proxy, /isSameLocalOrigin/);
  assert.match(proxy, /segments\.length === 1/);
  assert.match(proxy, /segments\.length === 2/);
  assert.match(proxy, /uuid\.test\(segments\[1\]\)/);
  assert.doesNotMatch(proxy, /export async function (PUT|PATCH|DELETE)/);
  assert.match(proxy, /unsupported_invoice_fields/);
  assert.match(proxy, /Cache-Control/);
});

test("client only submits backend-supported initial draft fields", () => {
  assert.match(workspace, /issueDateUtc: issued\.toISOString\(\)/);
  assert.match(workspace, /sellerDisplayName: seller\.trim\(\)/);
  assert.match(workspace, /buyerDisplayName: buyer\.trim\(\) \|\| null/);
  assert.match(workspace, /invoiceType !== 1 \|\| pattern !== 1 \|\| prepared\.length !== 0/);
  assert.doesNotMatch(workspace, /fetch\(["']http/);
  assert.doesNotMatch(workspace, /localStorage|document\.cookie|Authorization: "Bearer"/);
});

test("draft UI clearly distinguishes preparation from persisted invoice", () => {
  assert.match(workspace, /PersianDatePicker/);
  assert.match(workspace, /api\/taxpayers\/list/);
  assert.match(workspace, /api\/catalog\/customers\//);
  assert.match(workspace, /فقط پیش‌نمایش محلی/);
  assert.match(workspace, /هیچ صورتحسابی به سامانه مؤدیان ارسال نمی‌شود/);
  assert.match(workspace, /به معنی پذیرش یا ثبت در سامانه مؤدیان نیست/);
});

test("search, rescoping, revision display and HTTP refresh are present", () => {
  assert.match(workspace, /setTaxpayerId/);
  assert.match(workspace, /getJson<Invoice\[]>/);
  assert.match(workspace, /aria-pressed/);
  assert.match(workspace, /item\.revision/);
  assert.match(workspace, /بازخوانی/);
});
