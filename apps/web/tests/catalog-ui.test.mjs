import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const read = (name) => readFileSync(new URL("../" + name, import.meta.url), "utf8");

test("catalog page is server authenticated and Persian RTL", () => {
  const page = read("app/catalog/page.tsx");
  assert.match(page, /identityUiEnabled\(\)/);
  assert.match(page, /currentIdentity\(\)/);
  assert.match(page, /redirect\("\/identity"\)/);
  assert.match(page, /dir="rtl"/);
  assert.match(page, /lang="fa"/);
});

test("backend-for-frontend never puts session tokens into browser JS", () => {
  const proxy = read("app/api/catalog/[...segments]/route.ts");
  const client = read("app/catalog/workspace.tsx");
  assert.match(proxy, /ACCESS_COOKIE/);
  assert.match(proxy, /identityUiEnabled\(\)/);
  assert.match(proxy, /identityApiOrigin\(\)/);
  assert.match(proxy, /verifyRoute/);
  assert.match(proxy, /units/);
  assert.match(proxy, /origin_not_allowed/);
  assert.match(proxy, /Cache-Control/);
  assert.match(client, /credentials: "same-origin"/);
  assert.doesNotMatch(client, /localStorage|sessionStorage|ACCESS_COOKIE|Authorization:|NEXT_PUBLIC_/);
});

test("catalog UI guards revision and exposes mapping-history operations", () => {
  const source = read("app/catalog/workspace.tsx");
  assert.match(source, /expectedRevision/);
  assert.match(source, /tax-mappings/);
  assert.match(source, /closedAtUtc/);
  assert.match(source, /aria-selected/);
  assert.match(source, /type="datetime-local"/);
  assert.match(source, /"products"/);
  assert.match(source, /"services"/);
  assert.match(source, /"official"/);
  assert.match(source, /جست‌وجوی واحد/);
  assert.match(source, /انتخاب واحد پایه/);
});

test("RTL workspace has small-screen, keyboard and bidi safeguards", () => {
  const css = read("app/globals.css");
  assert.match(css, /\.catalog-shell\{direction:rtl;text-align:right/);
  assert.match(css, /\.catalog-shell button:focus-visible/);
  assert.match(css, /@media\(max-width:650px\)/);
  assert.match(css, /unicode-bidi:isolate/);
});
