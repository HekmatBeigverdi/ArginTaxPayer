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
  assert.match(workspace, /className="console-table"/);
  assert.match(workspace, /invoice-register__toolbar/);
  assert.match(workspace, /<table/);
  assert.match(css, /\.console-table th/);
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

test("wizard persists editable private drafts and supports later resumption", () => {
  assert.match(workspace, /saveWorkspace\(/);
  assert.match(workspace, /getJson<SavedEntry>/);
  assert.match(workspace, /\/api\/invoices\/workspaces/);
  assert.match(workspace, /expectedRevision:editing\?\.revision/);
  assert.match(workspace, /ویرایش و ادامه/);
  assert.match(workspace, /ذخیره و ادامه بعداً/);
  assert.match(workspace, /ذخیره و ثبت خریدار/);
  assert.match(workspace, /lastCompletedStep:completeStep/);
  assert.match(workspace, /buyerPartyId:buyerId \|\| null/);
  assert.match(workspace, /lines:rows\.map/);
  assert.match(workspace, /بدون ارسال مالیاتی/);
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
  assert.match(workspace, /\/api\/invoices\/workspaces/);
  assert.match(api, /"overview"/);
});
test("shared Persian twenty-four-hour time control is available", () => {
  assert.match(workspace, /PersianTimePicker/);
  assert.match(timePicker, /export function PersianTimePicker/);
  assert.match(timePicker, /Array\.from\(\{length:24\}/);
  assert.match(timePicker, /۰۱۲۳۴۵۶۷۸۹/);
  assert.doesNotMatch(workspace, /type="time"/);
});

test("accessible buyer combobox supports keyboard selection and resolves selected Party GUID", () => {
  assert.match(workspace, /role="combobox"/);
  assert.match(workspace, /role="listbox"/);
  assert.match(workspace, /aria-activedescendant/);
  assert.match(workspace, /ArrowDown/);
  assert.match(workspace, /ArrowUp/);
  assert.match(workspace, /setBuyerId\(b\.id\)/);
  assert.doesNotMatch(workspace, /<input id="invoice-seller"/);
});
test("all authorized customers are paged server-side", () => {
  assert.match(workspace, /listPageSize = 50/);
  assert.match(workspace, /query\.set\("page",\s*String\(listPage\)\)/);
  assert.match(workspace, /صفحه قبل|قبلی/);
  assert.match(workspace, /صفحه بعد|بعدی/);
  assert.match(workspace, /همه مشتریان زیرمجموعه/);
});

test("invoice list reuses the shared dense console table and pagination layout", () => {
  assert.match(workspace, /className="console-table"/);
  assert.match(workspace, /console-table-scroll/);
  assert.match(workspace, /console-table-footer/);
  assert.match(workspace, /console-row-action/);
  assert.match(css, /\.console-page \.invoice-register \.console-table/);
});

test("catalog uses searchable combobox, not a list of product cards", () => {
  const picker = read("../app/invoices/searchable-catalog-picker.tsx");
  assert.match(workspace, /<SearchableCatalogPicker/);
  assert.match(picker, /role="combobox"/);
  assert.match(picker, /role="listbox"/);
  assert.match(picker, /onQueryChange/);
  assert.match(picker, /ArrowDown/);
  assert.match(picker, /ArrowUp/);
  assert.doesNotMatch(workspace, /invoice-register__catalog">/);
});

test("shared console density applies within modal and rows", () => {
  assert.match(css, /\.console-page \.invoice-register__dialog-body/);
  assert.match(css, /\.console-page \.invoice-register__item/);
  assert.match(css, /\.console-page \.invoice-register__fields/);
  assert.match(css, /\.invoice-register__catalog-options/);
});
