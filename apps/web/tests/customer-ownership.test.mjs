import { readFileSync } from "node:fs";
import assert from "node:assert/strict";
import { test } from "node:test";

const panel = readFileSync(new URL("../app/account/customer-ownership-console.tsx", import.meta.url), "utf8");
const proxy = readFileSync(new URL("../app/api/customer-owners/[...path]/route.ts", import.meta.url), "utf8");
const taxpayers = readFileSync(new URL("../app/taxpayers/taxpayer-workspace.tsx", import.meta.url), "utf8");

test("partner management acts on customer accounts rather than fiscal taxpayer IDs", () => {
  assert.match(panel, /customerUserId/);
  assert.match(panel, /partnerLevel2UserId/);
  assert.match(panel, /expectedRevision: current.revision/);
  assert.match(panel, /approvedByLevel1UserId/);
  assert.doesNotMatch(taxpayers, /DelegateConsole/);
  assert.match(taxpayers, /customerUserId: newCustomerId/);
});
test("root transfer and history use safe server proxy rather than browser bearer", () => {
  assert.match(proxy, /ACCESS_COOKIE/);
  assert.match(proxy, /isSameLocalOrigin/);
  assert.match(proxy, /identityUiEnabled/);
  assert.match(proxy, /Cache-Control/);
  assert.match(proxy, /path\[1\] === "transfer"/);
  assert.match(proxy, /path\[1\] === "history"/);
  assert.doesNotMatch(panel, /localStorage|document\.cookie/);
});
