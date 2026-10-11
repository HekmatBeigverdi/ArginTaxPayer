import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const read = (name) => readFileSync(new URL(`../${name}`, import.meta.url), "utf8");

test("API probing is development-only and restricted to loopback", () => {
  const source = read("lib/api-health.ts");
  assert.match(source, /environment !== "development"/);
  assert.match(source, /"127\.0\.0\.1"/);
  assert.match(source, /"localhost"/);
  assert.match(source, /url\.username/);
  assert.match(source, /url\.password/);
});

test("health probes use bounded time and suppress diagnostics", () => {
  const source = read("lib/api-health.ts");
  assert.match(source, /AbortSignal\.timeout\(1500\)/);
  assert.match(source, /cache: "no-store"/);
  assert.match(source, /return "unavailable"/);
  assert.match(source, /body\.status === "Healthy"/);
});

test("frontend does not expose health service via client-side variable", () => {
  const source = read("lib/api-health.ts");
  const page = read("app/page.tsx");
  assert.doesNotMatch(source + page, /NEXT_PUBLIC_API|localStorage|dangerouslySetInnerHTML/);
  assert.match(page, /getApiHealth/);
});
