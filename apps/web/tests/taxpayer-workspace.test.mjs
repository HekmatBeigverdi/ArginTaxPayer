import { readFileSync } from "node:fs";
import assert from "node:assert/strict";
import { test } from "node:test";

const page = readFileSync(new URL("../app/taxpayers/page.tsx", import.meta.url), "utf8");
const workspace = readFileSync(new URL("../app/taxpayers/taxpayer-workspace.tsx", import.meta.url), "utf8");
const proxy = readFileSync(new URL("../app/api/taxpayers/[...path]/route.ts", import.meta.url), "utf8");

test("workspace route rejects unauthenticated or unauthorized roles", () => {
  assert.match(page, /currentIdentity\(\)/);
  assert.match(page, /redirect\("\/identity"\)/);
  assert.match(page, /!level1 && !level2/);
});
test("browser never receives bearer or reads session cookie", () => {
  assert.match(proxy, /ACCESS_COOKIE/);
  assert.match(proxy, /Authorization: "Bearer " \+ token/);
  assert.doesNotMatch(workspace, /document\.cookie|localStorage|Authorization: "Bearer"/);
  assert.match(proxy, /isSameLocalOrigin/);
  assert.match(proxy, /identityUiEnabled\(\)/);
});
test("BFF has strict allowlist, same-origin writes and no-store responses", () => {
  assert.match(proxy, /validPath\(path, method\)/);
  assert.match(proxy, /method !== "GET"/);
  assert.match(proxy, /Cache-Control": "no-store"/);
  assert.match(proxy, /uuid\.test/);
});
test("workspace displays permission errors, uses revisions and never simulates authority success", () => {
  assert.match(workspace, /response\.status === 404/);
  assert.match(workspace, /expectedRevision: details\.revision/);
  assert.match(workspace, /تست واقعی ارتباط با سازمان مالیاتی/);
  assert.match(workspace, /"\/list"/);
  assert.match(workspace, /"\/new"/);
});
