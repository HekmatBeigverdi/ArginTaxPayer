import { readFileSync } from "node:fs";
import assert from "node:assert/strict";
import { test } from "node:test";

const read = path => readFileSync(new URL(path, import.meta.url), "utf8");
const proxy = read("../app/api/invoices/[...segments]/route.ts");
const page = read("../app/(console)/invoices/page.tsx");
const workspace = read("../app/invoices/workspace.tsx");
const css = read("../app/globals.css");
const calendar = read("../app/catalog/persian-date-picker.tsx");
const timePicker = read("../app/components/persian-time-picker.tsx");
const api = proxy;

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
  assert.match(workspace, /sellerDisplayName:chosenTaxpayer\.displayName/);
  assert.match(workspace, /buyerDisplayName:chosenBuyer\.displayName/);
  assert.match(workspace, /buyerPartyId:buyerId/);
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

test("selected taxpayer is the seller and buyer is a scoped searchable directory record", () => {
  assert.match(workspace, /chosenTaxpayer\.displayName/);
  assert.match(workspace, /buyerQuery/);
  assert.match(workspace, /buyerId/);
  assert.match(workspace, /fiscal-participants\?/);
  assert.match(workspace, /type:"buyer",customerId:headerCustomer/);
  assert.doesNotMatch(workspace, /setSeller/);
});
test("partner dashboard defaults to all authorized customers, then customer and taxpayer filters", () => {
  assert.match(page, /isAdmin=\{actor\.roles\.includes\("Level1"\)/);
  assert.match(workspace, /همه مشتریان زیرمجموعه/);
  assert.match(workspace, /filterCustomer/);
  assert.match(workspace, /filterTaxpayer/);
  assert.match(workspace, /\/api\/invoices\/overview/);
  assert.match(api, /"overview"/);
});
test("shared Persian twenty-four-hour time control is available", () => {
  assert.match(workspace, /PersianTimePicker/);
  assert.match(timePicker, /export function PersianTimePicker/);
  assert.match(timePicker, /Array\.from\(\{length:24\}/);
  assert.match(timePicker, /۰۱۲۳۴۵۶۷۸۹/);
  assert.doesNotMatch(workspace, /type="time"/);
});
