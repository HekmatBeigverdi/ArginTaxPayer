import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const history = readFileSync(new URL("../app/invoices/invoice-dispatch-history.tsx", import.meta.url), "utf8");
const reference = readFileSync(new URL("../app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/inquiry-by-reference/route.ts", import.meta.url), "utf8");
const uid = readFileSync(new URL("../app/api/invoice-dispatch/[taxpayerId]/[invoiceId]/inquiry-by-uid/route.ts", import.meta.url), "utf8");

test("existing history only displays recorded evidence; no automatic inquiry or resend", () => {
  assert.match(history, /آخرین شاهد ثبت‌شده/);
  assert.match(history, /history\.receipts\.reduce/);
  assert.match(history, /history\.receipts\.length/);
  assert.match(history, /نتیجه درخواست قبلی مشخص نیست/);
  assert.match(history, /ارسال مجدد مجاز/);
  assert.doesNotMatch(history, /method:\s*["']POST["']|send-and-inquire|cancel-and-inquire/);
});
test("scoped history remains a no-store GET under existing endpoints", () => {
  assert.match(history, /historyUrl\(taxpayerId, invoiceId\)/);
  assert.match(history, /cache:\s*["']no-store["']/);
  assert.match(history, /credentials:\s*["']same-origin["']/);
  assert.match(reference, /ACCESS_COOKIE/);
  assert.match(uid, /ACCESS_COOKIE/);
});
