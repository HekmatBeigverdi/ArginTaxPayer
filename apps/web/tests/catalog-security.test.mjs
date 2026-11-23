import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { NextRequest } from "next/server.js";
import { loadTsx } from "./helpers/load-tsx.mjs";

const load = path => readFileSync(new URL("../" + path, import.meta.url), "utf8");

test("catalog browser requests use only same-origin BFF with no session token", () => {
  const client = load("app/catalog/workspace.tsx");
  assert.match(client, /fetch\("\/api\/catalog\//);
  assert.doesNotMatch(client, /localStorage|sessionStorage|Bearer |NEXT_PUBLIC_.*TOKEN/);
});

test("catalog BFF is restricted to its route allowlist", () => {
  const proxy = load("app/api/catalog/[...segments]/route.ts");
  assert.match(proxy, /identityUiEnabled\(\)/);
  assert.match(proxy, /verifyRoute\(path, method\)/);
  assert.match(proxy, /allowed\.some\(/);
  assert.match(proxy, /origin_not_allowed/);
  assert.match(proxy, /isSameLocalOrigin\(/);
  assert.match(proxy, /ACCESS_COOKIE/);
  assert.match(proxy, /redirect: "manual"/);
  assert.match(proxy, /cache: "no-store"/);
  assert.match(proxy, /Cache-Control/);
  assert.doesNotMatch(proxy, /process\.env\.NEXT_PUBLIC_.*SECRET/);
});

test("catalog backend is mapped in the operational console and requires loopback", () => {
  const program = load("../../src/ArginTaxPayer.Api/Program.cs");
  const endpoints = load("../../src/ArginTaxPayer.Api/Catalog/CatalogDevelopmentEndpoints.cs");
  assert.match(program, /GetValue<bool>\("ArginTaxPayer:EnableOperationalEndpoints"\)/);
  assert.match(program, /app\.MapCatalogDevelopmentEndpoints\(\)/);
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

test("catalog writes behind the production proxy validate the public origin and forward the session", async (t) => {
  const env = { NODE_ENV: "production", ARGIN_PUBLIC_BASE_URL: "https://tax.argin.ir" };
  for (const [key, value] of Object.entries(env)) {
    const previous = process.env[key];
    process.env[key] = value;
    t.after(() => {
      if (previous === undefined) delete process.env[key];
      else process.env[key] = previous;
    });
  }
  const calls = [];
  let backendStatus = 201;
  const route = loadTsx(new URL("../app/api/catalog/[...segments]/route.ts", import.meta.url), {
    globals: {
      AbortSignal,
      fetch: async (url, options) => {
        calls.push({ url, options });
        return Response.json({}, { status: backendStatus });
      },
    },
    modules: {
      "next/headers": { cookies: async () => ({ get: () => ({ value: "test-session" }) }) },
      "../../../../lib/identity": {
        ACCESS_COOKIE: "session",
        identityApiOrigin: () => "http://127.0.0.1:15090",
        identityUiEnabled: () => true,
      },
    },
  });
  const customer = "12345678-1234-1234-1234-123456789abc";
  const product = "abcdef12-1234-1234-1234-123456789abc";
  const body = JSON.stringify({ internalCode: "P1" });
  function request(method, origin, owner = customer) {
    const segments = ["customers", owner, "products", ...(method === "PUT" ? [product] : [])];
    return route[method](new NextRequest("http://localhost:15091/api/catalog/" + segments.join("/"), {
      method, headers: { host: "tax.argin.ir", origin }, body,
    }), { params: Promise.resolve({ segments }) });
  }

  for (const method of ["POST", "PUT"]) {
    backendStatus = method === "POST" ? 201 : 200;
    const response = await request(method, "https://tax.argin.ir");
    assert.equal(response.status, backendStatus);
    assert.equal(calls.at(-1).url, "http://127.0.0.1:15090/api/dev/catalog/customers/" +
      customer + "/products" + (method === "PUT" ? "/" + product : ""));
    assert.equal(calls.at(-1).options.headers.Authorization, "Bearer test-session");
    assert.equal(calls.at(-1).options.method, method);
    assert.equal(calls.at(-1).options.body, body);
  }
  const crossOrigin = await request("POST", "https://other.example");
  assert.equal(crossOrigin.status, 403);
  assert.equal((await crossOrigin.json()).error, "origin_not_allowed");
  assert.equal(calls.length, 2);

  backendStatus = 403;
  const forbidden = await request("POST", "https://tax.argin.ir", product);
  assert.equal(forbidden.status, 403);
  assert.equal(calls.length, 3);
  assert.match(calls.at(-1).url, new RegExp("/customers/" + product + "/products$"));
});
