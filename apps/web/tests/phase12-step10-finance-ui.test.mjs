import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("Step10 finance page requires signed-in manager and excludes customers", () => {
  const page = read("app/(console)/subscriptions/page.tsx");
  assert.match(page, /currentIdentity\(\)/);
  assert.match(page, /roles\.includes\("Level1"\)/);
  assert.match(page, /roles\.includes\("Level2"\)/);
  assert.match(page, /redirect\("\/dashboard"\)/);
});

test("Step10 API proxy uses cookie identity, allowlisted destinations, local backend", () => {
  const route = read("app/api/phase12/[...parts]/route.ts");
  assert.match(route, /currentIdentity\(\)/);
  assert.match(route, /ACCESS_COOKIE/);
  assert.match(route, /const actions = new Map/);
  assert.match(route, /identityApiOrigin\(\)/);
  assert.match(route, /invalid_origin/);
  assert.match(route, /roles\.includes\("Level1"\)/);
  assert.match(route, /subscriptions\/settlements\/reverse/);
  assert.doesNotMatch(route, /fetch\(request\.nextUrl/);
});

test("Step10 renders five finance sections and never enables PSP settlement from callback", () => {
  const ui = read("app/subscriptions/subscription-workspace.tsx");
  for (const name of ["تعریف پلن", "سفارش مالی", "ثبت دریافت", "گزارش درآمد", "تسویه شرکا"])
    assert.ok(ui.includes(name), name);
  assert.match(ui, /level1 && <form/);
  assert.match(ui, /role="tablist"/);
  assert.match(ui, /fetch\("\/api\/phase12\/subscriptions\/"/);
  assert.doesNotMatch(ui, /api\/payment\/callback/);
});

test("Step10 navigation exposes subscriptions to administrators", () => {
  assert.match(read("app/components/console-shell.tsx"), /path: "\/subscriptions"/);
  assert.match(read("app/(console)/dashboard/page.tsx"), /href: "\/subscriptions"/);
});
