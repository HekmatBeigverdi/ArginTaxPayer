import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const read = (name) => readFileSync(new URL("../" + name, import.meta.url), "utf8");

test("catalog page is server authenticated and Persian RTL", () => {
  const page = read("app/(console)/catalog/page.tsx");
  assert.match(read("app/(console)/layout.tsx"), /identityUiEnabled\(\)/);
  assert.match(page, /currentIdentity\(\)/);
  assert.match(read("app/(console)/layout.tsx"), /redirect\("\/identity"\)/);
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
  assert.match(source, /PersianDatePicker/);
  assert.match(source, /catalog-split-layout/);
  assert.match(source, /ConsoleLoader/);
  assert.match(source, /customerLabel/);
  assert.match(source, /\/api\/customer-owners\/accounts/);
  assert.match(source, /"products"/);
  assert.match(source, /"services"/);
  assert.match(source, /"official"/);
  assert.match(source, /catalog-editor-panel/);
  assert.doesNotMatch(source, /argin-modal-backdrop/);
  assert.match(read("app/components/console-shell.tsx"), /path: "\/catalog"/);
  assert.match(read("app/catalog/unit-combobox.tsx"), /جست‌وجوی واحد/);
  assert.match(source, /CatalogUnitCombobox/);
});

test("RTL workspace has small-screen, keyboard and bidi safeguards", () => {
  const css = read("app/globals.css");
  assert.match(css, /\.console-catalog-page\{/);
  assert.match(css, /\.catalog-unit-options\{/);
  assert.match(css, /\.catalog-split-layout\{/);
  assert.match(css, /\.ui-persian-date__popover\{/);
  assert.match(css, /@media\(max-width:650px\)/);
  assert.match(css, /unicode-bidi:isolate/);
});

test("Persian date picker reuses ArginAccounting Jalali input contract", () => {
  const component = read("app/catalog/persian-date-picker.tsx");
  assert.match(component, /persianToGregorianIso/);
  assert.match(component, /gregorianIsoToPersian/);
  assert.match(component, /fa-IR-u-ca-persian-nu-latn/);
});

test("catalog split editor never inherits viewport-sized old modal width", () => {
  const css = read("app/globals.css");
  assert.doesNotMatch(css, /\.catalog-form-dialog\{max-width:530px;width:calc\(100vw - 28px\)/);
  assert.match(css, /\.catalog-form-dialog\{max-width:100%;width:100%;min-width:0/);
  assert.match(css, /catalog-split-layout/);
});
test("unnamed accounts excluded and Persian calendar opens upwards", () => {
  const page = read("app/catalog/workspace.tsx");
  const css = read("app/globals.css");
  assert.match(page, /accountName\(a\)\.length>0/);
  assert.doesNotMatch(page, /مشتری بدون نام/);
  assert.match(css, /ui-persian-date__popover\{top:auto;bottom:calc\(100% \+ 7px\)/);
});
