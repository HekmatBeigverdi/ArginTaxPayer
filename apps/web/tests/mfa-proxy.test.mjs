import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { runInNewContext } from "node:vm";
import { test } from "node:test";
import ts from "typescript";
import { isSameLocalOrigin } from "../lib/identity-origin.mjs";

const require = createRequire(import.meta.url);
const { NextRequest } = require("next/server");
const source = readFileSync(new URL("../app/api/identity/[...path]/route.ts", import.meta.url), "utf8");
const compiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;

function proxy(backend) {
  const exports = {};
  runInNewContext(compiled, {
    exports,
    require(name) {
      if (name.endsWith("/lib/identity")) return {
        ACCESS_COOKIE: "argin_identity_access", REFRESH_COOKIE: "argin_identity_refresh",
        identityUiEnabled: () => true, identityBackend: backend,
      };
      if (name.endsWith("/lib/identity-origin.mjs")) return { isSameLocalOrigin };
      return require(name);
    },
  });
  return exports;
}

function request(path, { token = true, origin = "http://localhost:3000", body } = {}) {
  return new NextRequest("http://localhost:3000/api/identity/" + path, {
    method: "POST", headers: {
      host: "localhost:3000", origin,
      ...(token ? { cookie: "argin_identity_access=example-token; argin_identity_refresh=example-refresh" } : {}),
      ...(body ? { "Content-Type": "application/json" } : {}),
    }, ...(body ? { body: JSON.stringify(body) } : {}),
  });
}
const context = path => ({ params: Promise.resolve({ path: path.split("/") }) });

test("MFA enrollment accepts an empty POST body and forwards authenticated JSON", async () => {
  let calls = 0;
  const route = proxy(async (path, options) => {
    calls++;
    assert.equal(path, "mfa/enroll");
    assert.equal(options.headers.Authorization, "Bearer example-token");
    assert.equal(options.body, "{}");
    return Response.json({ secret: "TEST-ONLY", provisioningUri: "otpauth://totp/test" });
  });
  const response = await route.POST(request("mfa/enroll"), context("mfa/enroll"));
  assert.equal(response.status, 200);
  assert.equal(calls, 1);
  assert.equal(response.headers.get("cache-control"), "no-store");
  assert.equal((await response.json()).secret, "TEST-ONLY");
});

test("MFA enrollment rejects unauthenticated and cross-origin requests before forwarding", async () => {
  const route = proxy(() => { throw new Error("Must not forward"); });
  assert.equal((await route.POST(request("mfa/enroll", { token: false }), context("mfa/enroll"))).status, 401);
  assert.equal((await route.POST(request("mfa/enroll", { origin: "https://foreign.example" }), context("mfa/enroll"))).status, 403);
});

test("MFA server configuration errors remain distinguishable without leaking diagnostics", async () => {
  const route = proxy(async () => new Response("private configuration details", { status: 503 }));
  const response = await route.POST(request("mfa/enroll"), context("mfa/enroll"));
  assert.equal(response.status, 503);
  assert.doesNotMatch(await response.text(), /private configuration/);
});

test("Successful MFA confirmation clears both revoked session cookies and never caches recovery codes", async () => {
  const route = proxy(async () => Response.json({ codes: ["test-recovery-code"] }));
  const response = await route.POST(request("mfa/confirm", { body: { code: "123456" } }), context("mfa/confirm"));
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("cache-control"), "no-store");
  assert.equal(response.cookies.get("argin_identity_access").value, "");
  assert.equal(response.cookies.get("argin_identity_refresh").value, "");
  assert.deepEqual(await response.json(), { codes: ["test-recovery-code"] });
});
