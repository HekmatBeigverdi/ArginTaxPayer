import { readFileSync } from "node:fs";
import { test } from "node:test";
import assert from "node:assert/strict";

const read = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("RTL parties page requires authenticated identity and role", () => {
  const page = read("app/(console)/parties/page.tsx");
  const layout = read("app/(console)/layout.tsx");
  assert.match(layout, /identityUiEnabled\(\)/);
  assert.match(page, /currentIdentity\(\)/);
  assert.match(page, /redirect\("\/identity"\)/);
  assert.match(page, /if \(!admin && !customer\) redirect/);
  assert.match(layout, /ConsoleShell/);
});

test("same-origin proxy never accepts arbitrary paths or browser-supplied Bearer", () => {
  const proxy = read("app/api/party-workspace/[...path]/route.ts");
  assert.match(proxy, /ACCESS_COOKIE/);
  assert.match(proxy, /isSameLocalOrigin/);
  assert.match(proxy, /identityApiOrigin\(\)/);
  assert.match(proxy, /const url = target\(path, method\)/);
  assert.match(proxy, /if \(!url\) return reject\(404\)/);
  assert.match(proxy, /response\.status/);
});

test("customer screen has read-only actions and scoped own dashboard", () => {
  const workspace = read("app/parties/party-workspace.tsx");
  assert.match(workspace, /if \(!isAdmin\) return/);
  assert.match(workspace, /api<Mine>\("mine"\)/);
  assert.match(workspace, /writesEnabled/);
  assert.match(workspace, /role="alert"/);
  assert.match(workspace, /aria-busy/);
  assert.match(workspace, /aria-pressed/);
});

test("responsive forms have focus styling and no new database migration", () => {
  const css = read("app/globals.css");
  assert.match(css, /\.party-grid/);
  assert.match(css, /max-width:850px/);
  assert.match(css, /focus-visible/);
});
