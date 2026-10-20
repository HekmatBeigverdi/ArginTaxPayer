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
  assert.match(workspace, /مرور نهایی و تسویه/);
  assert.match(workspace, /step === 0/);
  assert.match(workspace, /step === 1/);
  assert.match(workspace, /step === 2/);
  assert.match(workspace, /setRows\(current => current\.concat/);
  assert.match(workspace, /setRows\(current\s*=>\s*current\.filter/);
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
  assert.match(workspace, /lastCompletedStep:persistedStep/);
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

test("Next session proxy only supports scoped reads and development draft writes", () => {
  assert.match(proxy, /identityUiEnabled\(\)/);
  assert.match(proxy, /ACCESS_COOKIE/);
  assert.match(proxy, /isSameLocalOrigin/);
  assert.match(proxy, /unsupported_invoice_fields/);
  for (const field of ["invoiceDescription", "settlementMethod", "cashAmountRial", "creditAmountRial"])
    assert.match(proxy, new RegExp('"' + field + '"'));
  assert.doesNotMatch(proxy, /export async function (PUT|PATCH|DELETE)/);
});

test("selected taxpayer is the seller and buyer is a scoped searchable directory record", () => {
  assert.match(workspace, /مؤدی صادرکننده \/ فروشنده/);
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

test("final stage has compact financial summary and saved invoice description", () => {
  assert.match(workspace, /مرور کلی صورتحساب/);
  assert.match(workspace, /مجموع مبلغ قبل از تخفیف/);
  assert.match(workspace, /مجموع تخفیفات/);
  assert.match(workspace, /مجموع مبلغ پس از تخفیف/);
  assert.match(workspace, /جمع صورتحساب/);
  assert.match(workspace, /توضیحات صورتحساب/);
  assert.match(workspace, /maxLength=\{1500\}/);
  assert.match(workspace, /invoiceDescription:invoiceDescription.trim\(\) \|\| null/);
  assert.match(workspace, /setInvoiceDescription\(saved.invoiceDescription \?\? ""\)/);
});
test("final stage settlement permits unknown, cash, credit and mixed with constrained sums", () => {
  assert.match(workspace, /<option value=\{0\}>نامعلوم<\/option>/);
  assert.match(workspace, /<option value=\{1\}>نقدی<\/option>/);
  assert.match(workspace, /<option value=\{2\}>نسیه<\/option>/);
  assert.match(workspace, /<option value=\{3\}>نقدی و نسیه<\/option>/);
  assert.match(workspace, /settlementMethod:settlement/);
  assert.match(workspace, /cashAmountRial:cash/);
  assert.match(workspace, /creditAmountRial:credit/);
  assert.match(workspace, /setSettlement\(\(saved.settlementMethod \?\? 0\) as Settlement\)/);
  assert.match(workspace, /settlementError/);
  assert.match(css, /invoice-register__settlement/);
  assert.match(css, /invoice-register__final-grid/);
});

test("Step21 scoped validation panel is explicitly not a fiscal approval", () => {
  const panel=read("../app/invoices/invoice-validation-panel.tsx");
  assert.match(workspace, /<InvoiceValidationPanel/);
  assert.match(panel, /\/api\/invoices\/workspaces\/.*\/validation/);
  assert.match(panel, /\/api\/invoices\/references\//);
  assert.match(panel, /isSubmissionAuthorized/);
  assert.match(panel, /مجوز ارسال مالیاتی صادر نمی‌کند/);
  assert.match(panel, /disabled>/);
  assert.match(panel, /fieldPath/);
  assert.match(panel, /revision!==revision/);
  assert.match(panel, /dirty/);
});
test("Next proxy safely allowlists GET validation and scoped references", () => {
  assert.match(proxy, /segments\[2\] === "validation"/);
  assert.match(proxy, /segments\[0\] === "references"/);
  assert.match(proxy, /"invoiceDescription","settlementMethod","cashAmountRial","creditAmountRial"/);
  assert.doesNotMatch(proxy, /export async function (PATCH|PUT|DELETE)/);
});
