import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const read = (file) => readFileSync(new URL(`../${file}`, import.meta.url), "utf8");

test("root layout explicitly declares Persian language and RTL direction", () => {
  const source = read("app/layout.tsx");
  assert.match(source, /<html lang="fa" dir="rtl">/);
});

test("foundation page links to implemented identity flows and marks planned modules", () => {
  const source = read("app/page.tsx");
  assert.match(source, /href="\/identity"/);
  assert.match(source, /href="\/users"/);
  assert.match(source, /در دست طراحی/);
});

test("enabled identity UI routes root visitors to login or dashboard", () => {
  const source = read("app/page.tsx");
  assert.match(source, /if \(identityUiEnabled\(\)\) \{[\s\S]*if \(actor\) redirect\("\/dashboard"\);[\s\S]*redirect\("\/identity"\);[\s\S]*\}/);
});

test("frontend shell is independent of live tenant, database and payment credentials", () => {
  const manifest = JSON.parse(read("package.json"));
  assert.equal(manifest.private, true);
  assert.equal(manifest.scripts.build, "next build");
  assert.doesNotMatch(read("app/page.tsx"), /process\.env|fetch\(|localStorage/);
});
