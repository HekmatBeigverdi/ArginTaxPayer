import assert from "node:assert/strict";
import { test } from "node:test";
import { isSameLocalOrigin } from "../lib/identity-origin.mjs";

test("same-origin requests support each local hostname", () => {
  for (const host of ["127.0.0.1:3000", "localhost:3000", "[::1]:3000"]) {
    assert.equal(isSameLocalOrigin(`http://${host}`, host, "http:"), true);
  }
});

test("missing, malformed, cross-origin and non-local requests fail closed", () => {
  for (const origin of [null, "null", "", "http://evil.invalid", "http://localhost:3000",
    "http://127.0.0.1:3001", "https://127.0.0.1:3000", "http://127.0.0.1:3000/path"]) {
    assert.equal(isSameLocalOrigin(origin, "127.0.0.1:3000", "http:"), false);
  }
  for (const host of [null, "", "evil.invalid", "localhost.evil.invalid:3000",
    "evil@localhost:3000", "localhost:3000/path", "localhost:99999"]) {
    assert.equal(isSameLocalOrigin("http://localhost:3000", host, "http:"), false);
  }
  assert.equal(isSameLocalOrigin("http://localhost:3000", "localhost:3000", "https:"), false);
});
