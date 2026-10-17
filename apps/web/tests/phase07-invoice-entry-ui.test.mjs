import { readFileSync } from "node:fs";
import assert from "node:assert/strict";
import { test } from "node:test";

const read = path => readFileSync(new URL(path, import.meta.url), "utf8");
const proxy = read("../app/api/invoices/[...segments]/route.ts");
const page = read("../app/(console)/invoices/page.tsx");
const workspace = read("../app/invoices/workspace.tsx");
const css = read("../app/globals.css");
const calendar = read("../app/catalog/persian-date-picker.tsx");

test("invoice register is an authenticated, RTL console view", () => {
  assert.match(page, /currentIdentity\(\)/);
  assert.match(page, /dir="rtl"/);
  assert.match(workspace, /invoice-register__table/);
  assert.match(workspace, /invoice-register__toolbar/);
  assert.match(workspace, /<table/);
  assert.match(css, /\.invoice-register__table :is\(th,td\)/);
});

test("only compact invoice list and toolbar render when add dialog is closed", () => {
  assert.match(workspace, /\{modal && <div className="invoice-register__overlay"/);
  assert.match(workspace, /\{detail && <div className="invoice-register__overlay"/);
  assert.match(workspace, /افزودن صورتحساب/);
  assert.match(workspace, /aria-modal="true"/);
  assert.match(workspace, /setModal\(false\)/);
});

test("wizard has header, repeatable item rows and totals sections", () => {
  assert.match(workspace, /سربرگ صورتحساب/);
  assert.match(workspace, /اقلام صورتحساب/);
  assert.match(workspace, /جمع‌بندی صورتحساب/);
  assert.match(workspace, /step === 0/);
  assert.match(workspace, /step === 1/);
  assert.match(workspace, /step === 2/);
  assert.match(workspace, /setRows\(current => current\.concat/);
  assert.match(workspace, /setRows\(current => current\.filter/);
  assert.match(workspace, /vatRate/);
  assert.match(workspace, /calculated\.total/);
});

test("draft remains honest about persisted fields and fiscal submission", () => {
  assert.match(workspace, /issueDateUtc: issued\.toISOString\(\)/);
  assert.match(workspace, /sellerDisplayName:seller\.trim\(\)/);
  assert.match(workspace, /buyerDisplayName:buyer\.trim\(\) \|\| null/);
  assert.match(workspace, /ذخیره فقط سربرگ پیش‌نویس/);
  assert.match(workspace, /ردیف‌های آماده‌شده و فوتر ذخیره نخواهند شد/);
  assert.match(workspace, /بدون ارسال مالیاتی/);
  assert.doesNotMatch(workspace, /fetch\(["']http/);
  assert.doesNotMatch(workspace, /localStorage|document\.cookie/);
});

test("calendar has Persian conversion and modal-safe floating positioning", () => {
  assert.match(workspace, /PersianDatePicker id="invoice-date".*floating/);
  assert.match(calendar, /fa-IR-u-ca-persian-nu-latn/);
  assert.match(calendar, /date\.toISOString\(\)\.slice\(0, 10\) !== value/);
  assert.match(calendar, /floatingPosition/);
  assert.match(calendar, /position: "fixed"/);
  assert.match(css, /\.invoice-register \.ui-persian-date__weekdays/);
  assert.match(css, /\.invoice-register \.ui-persian-date__days/);
  assert.match(css, /\.invoice-register \.ui-persian-date__popover/);
});

test("Next session proxy only supports scoped reads and header writes", () => {
  assert.match(proxy, /identityUiEnabled\(\)/);
  assert.match(proxy, /ACCESS_COOKIE/);
  assert.match(proxy, /isSameLocalOrigin/);
  assert.match(proxy, /unsupported_invoice_fields/);
  assert.doesNotMatch(proxy, /export async function (PUT|PATCH|DELETE)/);
});
