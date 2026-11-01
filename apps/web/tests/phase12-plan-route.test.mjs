import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { test } from "node:test";
import { runInNewContext } from "node:vm";
import ts from "typescript";

const require = createRequire(import.meta.url);
const { NextRequest } = require("next/server");
const source = readFileSync(new URL("../app/api/phase12/[...parts]/route.ts", import.meta.url), "utf8");

test("plan proxy compiles and forwards record edits and archives to the authenticated backend", async () => {
  const compiled = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
    reportDiagnostics: true,
  });
  const errors = compiled.diagnostics.filter(diagnostic => diagnostic.category === ts.DiagnosticCategory.Error);
  assert.deepEqual(errors.map(error => ts.flattenDiagnosticMessageText(error.messageText, "\n")), []);

  const exports = {};
  const calls = [];
  runInNewContext(compiled.outputText, {
    exports, URLSearchParams, AbortSignal,
    require(name) {
      if (name === "next/headers") return { cookies: async () => ({ get: () => ({ value: "test-session" }) }) };
      if (name.endsWith("/lib/identity")) return {
        ACCESS_COOKIE: "session", currentIdentity: async () => ({ roles: ["Level1"] }),
        identityApiOrigin: () => "http://127.0.0.1:15089", identityUiEnabled: () => true,
      };
      return require(name);
    },
    fetch: async (url, options) => {
      calls.push({ url, options });
      return Response.json({ status: "ok" });
    },
  });

  const id = "12345678-1234-1234-1234-123456789abc";
  const parts = ["subscriptions", "plans", id];
  for (const method of ["PUT", "DELETE"]) {
    const body = method === "PUT" ? JSON.stringify({ code: "updated" }) : undefined;
    const response = await exports[method](new NextRequest("http://localhost:3000/api/phase12/" + parts.join("/"), {
      method, ...(body ? { body, headers: { "Content-Type": "application/json" } } : {}),
    }), { params: Promise.resolve({ parts }) });
    assert.equal(response.status, 200);
    const forwarded = calls.at(-1);
    assert.equal(forwarded.url, "http://127.0.0.1:15089/api/dev/" + parts.join("/"));
    assert.equal(forwarded.options.method, method);
    assert.equal(forwarded.options.headers.Authorization, "Bearer test-session");
    assert.equal(forwarded.options.body, body);
  }

  for (const invalidId of ["not-an-id", id + "/extra"]) {
    const response = await exports.DELETE(new NextRequest("http://localhost:3000/api/phase12/subscriptions/plans/" + invalidId, {
      method: "DELETE",
    }), { params: Promise.resolve({ parts: ["subscriptions", "plans", invalidId] }) });
    assert.equal(response.status, 404);
  }
  assert.equal(calls.length, 2);
});
