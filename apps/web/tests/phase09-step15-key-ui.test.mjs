import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read = path => readFileSync(new URL(`../${path}`, import.meta.url), "utf8");

test("Step15 key page is Persian RTL and keeps private material out of the UI", () => {
  const page = read("app/keys/key-management-workspace.tsx");
  assert.match(page, /dir="rtl"/);
  assert.match(page, /کلید خصوصی هرگز/);
  assert.match(page, /اثر انگشت عمومی/);
  assert.doesNotMatch(page, /privateKey|private_key|secret|password|privateMaterial|private_material/i);
});

test("Step15 actions remain disabled until the secure API gate is wired", () => {
  const page = read("app/keys/key-management-workspace.tsx");
  assert.match(page, /تولید کلید جدید/);
  assert.match(page, /ورود کلید موجود/);
  assert.match(page, /درخواست چرخش/);
  assert.equal((page.match(/<button[^>]+disabled/g) ?? []).length, 3);
});

test("Step15 route uses the authenticated console layout", () => {
  const page = read("app\/(console)/keys/page.tsx");
  assert.match(page, /KeyManagementWorkspace/);
});
