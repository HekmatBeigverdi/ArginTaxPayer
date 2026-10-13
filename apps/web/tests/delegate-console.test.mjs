import { readFileSync } from "node:fs";
import assert from "node:assert/strict";
import { test } from "node:test";

const ui = readFileSync(new URL("../app/taxpayers/delegate-console.tsx", import.meta.url), "utf8");
const api = readFileSync(new URL("../app/api/delegations/[...path]/route.ts", import.meta.url), "utf8");

test("delegate UI includes grant, revision and revocation reason", () => {
  assert.match(ui, /delegateUserId/);
  assert.match(ui, /expectedRevision/);
  assert.match(ui, /revocationReason/);
});
test("gateway uses session, no-store and URL allowlisting", () => {
  assert.match(api, /ACCESS_COOKIE/);
  assert.match(api, /identityUiEnabled/);
  assert.match(api, /isSameLocalOrigin/);
  assert.match(api, /Cache-Control/);
});
