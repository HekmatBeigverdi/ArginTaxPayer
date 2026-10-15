import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
const read = (path) => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("sidebar shows persistent accessible content overlay during route navigation", () => {
  const shell = read("app/components/console-shell.tsx");
  const loader = read("app/components/console-loader.tsx");
  assert.match(shell, /setNavigatingTo\(n\.path\)/);
  assert.match(shell, /setNavigatingTo\(null\)/);
  assert.match(shell, /<ConsoleLoader full/);
  assert.match(loader, /role="status"/);
  assert.match(loader, /aria-busy="true"/);
});

test("buyers and taxpayer list uses one scoped aggregate request and never shows premature empty state", () => {
  const directory = read("app/parties/buyer-taxpayer-directory.tsx");
  assert.match(directory, /\/api\/customer-owners\/directory/);
  assert.doesNotMatch(directory, /\/api\/party-workspace\/customers\/\$\{buyer\.customerUserId\}\/parties/);
  assert.doesNotMatch(directory, /\/api\/customer-owners\/\$\{buyer\.customerUserId\}\/taxpayers/);
  assert.match(directory, /setRows\(combined\)/);
  assert.match(directory, /setProgress/);
  assert.match(directory, /loadedOnce && !error && shown\.length === 0/);
  assert.match(directory, /<ConsoleLoader full/);
});

test("customer-owner BFF exposes the aggregate directory route", () => {
  const route = read("app/api/customer-owners/[...path]/route.ts");
  assert.match(route, /path\[0\] === "directory"/);
});

test("buyer and seller tax details map to persisted secure APIs, without pretending successful connection", () => {
  const form = read("app/parties/invoice-participant-form.tsx");
  const bff = read("app/api/party-workspace/[...path]/route.ts");
  const buyer = read("app/parties/party-workspace.tsx");
  const seller = read("app/taxpayers/taxpayer-workspace.tsx");
  assert.match(form, /economicCode/);
  assert.match(form, /postalCode/);
  assert.match(form, /branchCode/);
  assert.match(form, /expectedRevision/);
  assert.match(form, /نوع اول/);
  assert.match(form, /نوع دوم/);
  assert.match(form, /نوع سوم/);
  assert.match(bff, /invoice-details/);
  assert.match(buyer, /role="buyer"/);
  assert.match(seller, /role="seller"/);
  assert.doesNotMatch(form, /localStorage|document\.cookie/);
});
