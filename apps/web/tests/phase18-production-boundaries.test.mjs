import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { test } from "node:test";

const read = p => readFileSync(new URL("../"+p, import.meta.url), "utf8");
const program = read("../../src/ArginTaxPayer.Api/Program.cs");
const identity = read("lib/identity.ts");
const proxy = read("app/api/identity/[...path]/route.ts");

test("Production routes require explicit configuration and reject unsafe registration/OTP mode", () => {
  assert.match(program, /builder\.Environment\.IsProduction\(\)/);
  assert.match(program, /ArginTaxPayer:EnableOperationalEndpoints/);
  assert.match(program, /operationalEndpointsEnabled/);
  assert.match(program, /Identity:AllowDevelopmentRegistration/);
  assert.match(program, /Identity:OtpDeliveryMode/);
  assert.match(program, /http:\/\/127\.0\.0\.1:15090/);
  assert.match(program, /argintaxpayer_test/);
  assert.match(program, /StartsWithSegments\("\/api\/dev"\)/);
  assert.match(program, /IsLoopbackAddress\(context\.Connection\.RemoteIpAddress\)/);
  assert.match(program, /if \(app\.Environment\.IsDevelopment\(\)\)/);
  assert.match(program, /app\.MapOpenApi\(\)/);
});

test("Next.js production identity remains opt-in, HTTPS and httpOnly", () => {
  assert.match(identity, /ARGIN_PRODUCTION_UI_ENABLED/);
  assert.match(identity, /ARGIN_PUBLIC_BASE_URL === "https:\/\/tax\.argin\.ir"/);
  assert.match(identity, /ARGIN_IDENTITY_UI_ENABLED/);
  assert.match(identity, /127\.0\.0\.1/);
  assert.match(proxy, /secure: process\.env\.NODE_ENV === "production"/);
  assert.match(proxy, /sameSite: "strict"/);
  assert.match(proxy, /httpOnly: true/);
});

test("Production BFF only accepts exact public HTTPS origin, not arbitrary Host or HTTP origin", () => {
  const code = `import {isSameLocalOrigin} from "./lib/identity-origin.mjs";
    console.log(JSON.stringify([
      isSameLocalOrigin("https://tax.argin.ir", "tax.argin.ir", "http:"),
      isSameLocalOrigin("http://tax.argin.ir", "tax.argin.ir", "http:"),
      isSameLocalOrigin("https://evil.example", "evil.example", "https:"),
      isSameLocalOrigin("https://tax.argin.ir", "evil.example", "http:"),
      isSameLocalOrigin(null, "tax.argin.ir", "http:")
    ]));`;
  const r = spawnSync(process.execPath, ["--input-type=module", "-e", code], {
    cwd: new URL("../", import.meta.url).pathname,
    env: {...process.env, NODE_ENV: "production", ARGIN_PUBLIC_BASE_URL: "https://tax.argin.ir"},
    encoding: "utf8"
  });
  assert.equal(r.status, 0, r.stderr);
  assert.deepEqual(JSON.parse(r.stdout), [true, false, false, false, false]);
});

test("Local developer same-origin policy is still supported", () => {
  const code = `import {isSameLocalOrigin} from "./lib/identity-origin.mjs";
    console.log(JSON.stringify([
      isSameLocalOrigin("http://localhost:3000", "localhost:3000", "http:"),
      isSameLocalOrigin("http://127.0.0.1:3000", "127.0.0.1:3000", "http:"),
      isSameLocalOrigin("https://tax.argin.ir", "tax.argin.ir", "https:")
    ]));`;
  const r=spawnSync(process.execPath, ["--input-type=module", "-e", code], {
    cwd: new URL("../", import.meta.url).pathname,
    env: {...process.env, NODE_ENV: "development"},
    encoding:"utf8"
  });
  assert.equal(r.status, 0, r.stderr);
  assert.deepEqual(JSON.parse(r.stdout), [true, true, false]);
});
