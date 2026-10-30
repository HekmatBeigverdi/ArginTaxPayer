import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";

const ui = readFileSync(new URL("../app/invoices/workspace.tsx", import.meta.url), "utf8");
const css = readFileSync(new URL("../app/globals.css", import.meta.url), "utf8");

test("initial taxpayer load cannot dismiss invoice overview loader prematurely", () => {
  const segment = ui.split('getJson<Taxpayer[]>("/api/taxpayers/list")')[1].split("const reload = useCallback")[0];
  assert.doesNotMatch(segment, /setLoading\(false\)/);
  assert.match(ui, /if \(requestId === requestSeq\.current\) setLoading\(false\)/);
  assert.match(ui, /setLoading\(true\);\s*const timer = window\.setTimeout/);
});

test("invoice table uses existing shared argin spinner while waiting", () => {
  assert.match(ui, /argin-loader argin-loader-overlay invoice-register__loading/);
  assert.match(ui, /argin-loader-spinner/);
  assert.doesNotMatch(ui, /invoice-table-skeleton/);
});

test("search expands and help button opens real tooltip", () => {
  assert.match(ui, /setSearchHelpOpen\(v=>!v\)/);
  assert.match(ui, /role="tooltip"/);
  assert.match(ui, /aria-expanded=\{searchHelpOpen\}/);
  assert.match(css, /\.invoice-register__search input#invoice-list-search/);
  assert.match(css, /max-width:none/);
});

test("invoice loading overlay clips the full table viewport", () => {
  assert.match(ui, /invoice-register__table-stage.*aria-busy=\{loading\}/s);
  assert.match(css, /\.invoice-register__table-stage\[aria-busy=\"true\"\]/);
  assert.match(css, /overflow:hidden/);
  assert.match(css, /\.invoice-register__loading[^}]*height:100%/s);
});
