import test from "node:test";
import assert from "node:assert/strict";
import {normalizeInvoiceIdentifier} from "../app/invoices/invoice-identifier-digits.mjs";
test("invoice identifiers accept local digits without losing leading zeros or hiding invalid characters", () => {
  for (const text of ["۱۲۳۴", "١٢٣٤", "1234"]) assert.equal(normalizeInvoiceIdentifier(text), "1234");
  assert.equal(normalizeInvoiceIdentifier(" ۰۰۱۲ "), "0012");
  assert.equal(normalizeInvoiceIdentifier("۱۲-۳۴"), "12-34");
});
