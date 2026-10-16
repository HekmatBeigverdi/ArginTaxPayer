import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import { test } from "node:test";
const read = (path) => readFileSync(new URL("../"+path,import.meta.url),"utf8");
test("owner-supplied Argin logo is available as a nonempty PNG", () => {
  for (const path of ["public/brand/argin-logo.png", "app/icon.png",
    "public/favicon.png", "public/apple-touch-icon.png"]) {
    const file = new URL("../"+path,import.meta.url);
    assert.ok(existsSync(file),path+" missing");
    const data = readFileSync(file);
    assert.ok(data.length > 300);
    assert.equal(data.subarray(0,8).toString("hex"),"89504e470d0a1a0a");
  }
});
test("same logo is used for login, sidebar, header and metadata", () => {
  assert.match(read("app/layout.tsx"), /icons:/);
  assert.match(read("app/layout.tsx"), /\/brand\/argin-logo\.png/);
  assert.match(read("app/identity/page.tsx"), /argin-login-logo/);
  assert.match(read("app/components/console-shell.tsx"), /argin-sidebar-logo/);
  assert.match(read("app/components/console-shell.tsx"), /argin-header-logo/);
  assert.match(read("app/globals.css"), /\.argin-brand-logo\{/);
});
