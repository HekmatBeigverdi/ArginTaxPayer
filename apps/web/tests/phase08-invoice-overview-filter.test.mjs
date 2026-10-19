import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const path = new URL("../app/api/invoices/[...segments]/route.ts", import.meta.url);
const source = readFileSync(path, "utf8");

test("invoice overview proxy forwards reviewState and search", () => {
  assert.match(source, /segments\[0\] === "overview"/);
  assert.match(source, /searchParams\.get\("reviewState"\)/);
  assert.match(source, /filtered\.set\("reviewState", reviewState\)/);
  assert.match(source, /searchParams\.get\("search"\)/);
  assert.match(source, /filtered\.set\("search", search\.trim\(\)\)/);
});

test("invoice overview proxy validates its query filters", () => {
  assert.match(source, /"ChangesRequested", "Approved", "WorkingDraft"/);
  assert.match(source, /search\.length > 128/);
  assert.match(source, /return error\(400, "invalid_filter"\)/);
});
