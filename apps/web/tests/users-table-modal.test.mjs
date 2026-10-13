import { readFileSync } from "node:fs";
import assert from "node:assert/strict";
import { test } from "node:test";

const view = readFileSync(new URL("../app/users/users-workspace.tsx", import.meta.url), "utf8");
const css = readFileSync(new URL("../app/globals.css", import.meta.url), "utf8");
const api = readFileSync(new URL("../app/api/customer-owners/[...path]/route.ts", import.meta.url), "utf8");

test("software customers are a data table, not top-level taxpayer cards", () => {
  assert.match(view, /<table className="users-table">/);
  assert.match(view, /<thead>/);
  assert.match(view, /<tbody>/);
  assert.match(view, /customerUserId/);
  assert.match(view, /taxpayerCount/);
});
test("pagination is coupled to search and ownership filtering", () => {
  assert.match(view, /PAGE_SIZES = \[10, 20, 50\]/);
  assert.match(view, /Math\.ceil\(filtered\.length \/ pageSize\)/);
  assert.match(view, /filtered\.slice/);
  assert.match(view, /setPage\(1\)/);
  assert.match(view, /setPage\(p => p \+ 1\)/);
  assert.match(view, /setPage\(p => p - 1\)/);
});
test("native accessible dialog loads only one customer's taxpayers", () => {
  assert.match(view, /<dialog className="users-dialog"/);
  assert.match(view, /showModal\(\)/);
  assert.match(view, /aria-labelledby="user-detail-heading"/);
  assert.match(view, /\/taxpayers/);
  assert.match(view, /onClose=\{close\}/);
  assert.match(css, /users-dialog::backdrop/);
});
test("Level1 can review legacy owners and approve partner attribution with CAS and reason", () => {
  assert.match(view, /expectedRevision: selected\.revision/);
  assert.match(view, /partnerLevel2UserId: target \|\| null/);
  assert.match(view, /transferReason\.trim/);
  assert.match(view, /ثبت اولیه مالکیت کاربر قدیمی/);
  assert.match(view, /transferHistory/);
  assert.match(view, /\/api\/customer-owners\/partners/);
  assert.match(view, /partners.map/);
  assert.match(view, /ادمین سطح ۱ — مشتری مستقیم/);
  assert.match(api, /isSameLocalOrigin/);
  assert.doesNotMatch(view, /document\.cookie|localStorage|Authorization: "Bearer"/);
});
