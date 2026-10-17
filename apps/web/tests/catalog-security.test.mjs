import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const load = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("catalog browser requests use only same-origin BFF with no session token", () => {
  const client = load("app/catalog/workspace.tsx");
  assert.match(client, /fetch\("\/api\/catalog\//);
  assert.doesNotMatch(client, /localStorage|sessionStorage|Bearer |NEXT_PUBLIC_.*TOKEN/);
});

test("development-only BFF is restricted to catalog route allowlist", () => {
  const proxy = load("app/api/catalog/[...segments]/route.ts");
  assert.match(proxy, /identityUiEnabled\(\)/);
  assert.match(proxy, /verifyRoute\(path, method\)/);
  assert.match(proxy, /allowed\.some\(/);
  assert.match(proxy, /origin_not_allowed/);
  assert.match(proxy, /request\.nextUrl\.origin/);
  assert.match(proxy, /ACCESS_COOKIE/);
  assert.match(proxy, /redirect: "manual"/);
  assert.match(proxy, /cache: "no-store"/);
  assert.match(proxy, /Cache-Control/);
  assert.doesNotMatch(proxy, /process\.env\.NEXT_PUBLIC_.*SECRET/);
});

test("catalog backend does not register routes in production", () => {
  const program = load("../../src/ArginTaxPayer.Api/Program.cs");
  const endpoints = load("../../src/ArginTaxPayer.Api/Catalog/CatalogDevelopmentEndpoints.cs");
  assert.match(program, /app\.MapCatalogDevelopmentEndpoints\(\)/);
  assert.match(program, /app\.Environment\.IsDevelopment\(\)/);
  assert.match(endpoints, /IPAddress\.IsLoopback/);
  assert.match(endpoints, /if \(!Local\(http\)\) return Results\.NotFound\(\)/);
});

test("catalog customer selection cannot submit arbitrary client-specified ownership", () => {
  const workspace = load("app/catalog/workspace.tsx");
  assert.match(workspace, /accounts\.filter\(/);
  assert.match(workspace, /setCustomer\(e\.target\.value\)/);
  assert.doesNotMatch(workspace, /type="hidden" name="role"/);
  const search = load("../../src/ArginTaxPayer.Infrastructure/Persistence/Catalog/CatalogSearchService.cs");
  assert.match(search, /x\.CustomerUserId == customer/);
  assert.match(search, /CanSeeCustomerAsync/);
});
